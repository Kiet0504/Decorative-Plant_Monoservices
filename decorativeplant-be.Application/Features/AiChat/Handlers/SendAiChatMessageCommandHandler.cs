using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using decorativeplant_be.Application.Common;
using decorativeplant_be.Application.Common.DTOs.AiChat;
using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Common.DTOs.RoomScan;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Common.Settings;
using decorativeplant_be.Application.Features.AiChat;
using decorativeplant_be.Application.Features.AiChat.Commands;
using decorativeplant_be.Application.Features.Garden;
using decorativeplant_be.Application.Features.RoomScan.Services;
using decorativeplant_be.Application.Services;
using decorativeplant_be.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace decorativeplant_be.Application.Features.AiChat.Handlers;

public sealed class SendAiChatMessageCommandHandler : IRequestHandler<SendAiChatMessageCommand, AiChatReplyDto>
{
    private static readonly JsonSerializerOptions TaskInfoJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IUserAccountService _userAccountService;
    private readonly IGardenRepository _gardenRepository;
    private readonly IOllamaClient _ollama;
    private readonly IPlantDiagnosisFromBase64Service _formalDiagnosisFromBase64;
    private readonly IChatDiagnosisPipelineSettings _chatDiagnosisPipeline;
    private readonly IChatImageIntentClassifier _imageIntentClassifier;
    private readonly IRoomScanCatalogRankingService _roomScanCatalogRanking;
    private readonly IRoomScanChatSuggestionIntentDetector _roomScanChatIntent;
    private readonly IAiChatProfileShopIntentDetector _profileShopIntentDetector;
    private readonly IOptions<RoomScanHandlerOptions> _roomScanHandlerOptions;
    private readonly AiRoutingSettings _aiRouting;
    private readonly IUserContentSafetyService _contentSafety;
    private readonly IPlantAssistantScopeService _plantScope;
    private readonly IApplicationDbContext _db;
    private readonly ILogger<SendAiChatMessageCommandHandler> _logger;

    public SendAiChatMessageCommandHandler(
        IUserAccountService userAccountService,
        IGardenRepository gardenRepository,
        IApplicationDbContext db,
        IOllamaClient ollama,
        IPlantDiagnosisFromBase64Service formalDiagnosisFromBase64,
        IChatDiagnosisPipelineSettings chatDiagnosisPipeline,
        IChatImageIntentClassifier imageIntentClassifier,
        IRoomScanCatalogRankingService roomScanCatalogRanking,
        IRoomScanChatSuggestionIntentDetector roomScanChatIntent,
        IAiChatProfileShopIntentDetector profileShopIntentDetector,
        IOptions<RoomScanHandlerOptions> roomScanHandlerOptions,
        IOptions<AiRoutingSettings> aiRouting,
        IUserContentSafetyService contentSafety,
        IPlantAssistantScopeService plantScope,
        ILogger<SendAiChatMessageCommandHandler> logger)
    {
        _userAccountService = userAccountService;
        _gardenRepository = gardenRepository;
        _db = db;
        _ollama = ollama;
        _formalDiagnosisFromBase64 = formalDiagnosisFromBase64;
        _chatDiagnosisPipeline = chatDiagnosisPipeline;
        _imageIntentClassifier = imageIntentClassifier;
        _roomScanCatalogRanking = roomScanCatalogRanking;
        _roomScanChatIntent = roomScanChatIntent;
        _profileShopIntentDetector = profileShopIntentDetector;
        _roomScanHandlerOptions = roomScanHandlerOptions;
        _aiRouting = aiRouting.Value;
        _contentSafety = contentSafety;
        _plantScope = plantScope;
        _logger = logger;
    }

    public async Task<AiChatReplyDto> Handle(SendAiChatMessageCommand request, CancellationToken cancellationToken)
    {
        // Only classify the *latest* user message. Past turns may contain phrases that the user
        // already saw blocked; punishing future on-topic messages because of that history made
        // every follow-up look rejected (e.g. a bomb prompt followed by a plant-care question).
        var latestUserMessage = request.Messages
            .LastOrDefault(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))?
            .Content;
        var userSafety = _contentSafety.Classify(latestUserMessage);
        if (userSafety != ContentSafetyKind.Allowed)
        {
            var replyText = userSafety == ContentSafetyKind.SelfHarmCrisis
                ? _contentSafety.CrisisChatReply
                : _contentSafety.BlockedChatReply;
            return new AiChatReplyDto { Reply = replyText, ContentBlocked = true };
        }

        var normalizedImage = NormalizeAttachedImageBase64(request.AttachedImageBase64);
        var hasImage = !string.IsNullOrEmpty(normalizedImage);
        var combinedUserMessages = string.Join(
            "\n",
            request.Messages
                .Where(m => string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))
                .Select(m => m.Content));
        if (!_plantScope.IsInScopeForChat(
                combinedUserMessages,
                hasImage,
                request.RoomScanFollowUp != null,
                request.GardenPlantId.HasValue,
                request.ArSessionId.HasValue || !string.IsNullOrWhiteSpace(request.PlacementContextJson),
                request.ProductListingId.HasValue))
        {
            return new AiChatReplyDto { Reply = _plantScope.OutOfScopeReply, OutOfScope = true };
        }

        var user = await _userAccountService.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("User", request.UserId);
        }

        GardenPlant? focusPlant = null;
        if (request.GardenPlantId.HasValue)
        {
            focusPlant = await _gardenRepository.GetPlantByIdAsync(
                request.GardenPlantId.Value,
                includeTaxonomy: true,
                cancellationToken);
            if (focusPlant == null || focusPlant.UserId != request.UserId)
            {
                throw new NotFoundException("Garden plant", request.GardenPlantId.Value);
            }
        }

        IReadOnlyList<CareSchedule> focusSchedules = Array.Empty<CareSchedule>();
        IReadOnlyList<CareLog> focusRecentLogs = Array.Empty<CareLog>();
        IReadOnlyList<PlantDiagnosis> focusDiagnoses = Array.Empty<PlantDiagnosis>();
        if (focusPlant != null)
        {
            focusSchedules = (await _gardenRepository.GetSchedulesByPlantIdAsync(
                    focusPlant.Id,
                    includeInactive: false,
                    cancellationToken))
                .ToList();
            focusRecentLogs = (await _gardenRepository.GetRecentCareLogsByPlantIdAsync(
                    focusPlant.Id,
                    limit: 15,
                    cancellationToken))
                .ToList();
            focusDiagnoses = (await _gardenRepository.GetPlantDiagnosesByPlantIdAsync(focusPlant.Id, cancellationToken))
                .Take(5)
                .ToList();
        }

        var (plants, _) = await _gardenRepository.GetPlantsByUserIdAsync(
            request.UserId,
            includeArchived: false,
            healthFilter: null,
            page: 1,
            pageSize: 30,
            cancellationToken);

        var lastUserText = request.Messages.LastOrDefault(m =>
            string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase))?.Content;
        var useFormalDiagnosisPre = await _imageIntentClassifier.ShouldUseFormalDiagnosisPipelineAsync(
            lastUserText,
            hasImage,
            cancellationToken);
        // AR decor snapshots: user is showing room + virtual plant — not disease diagnosis.
        var useFormalDiagnosis =
            request.ArSessionId.HasValue && hasImage ? false : useFormalDiagnosisPre;

        if (hasImage && useFormalDiagnosis && !_chatDiagnosisPipeline.CanRunFormalGeminiOllamaFromChat)
        {
            _logger.LogInformation(
                "AI chat: formal Gemini+Ollama diagnosis skipped — set AiDiagnosis:Provider=GeminiOllama and AiDiagnosis:GeminiApiKey (chat will use Ollama vision/text instead).");
        }
        else if (hasImage && !useFormalDiagnosis)
        {
            _logger.LogInformation(
                "AI chat: image present but formal diagnosis pipeline not selected (caption looks like general plant care, not disease/damage intent).");
        }

        if (useFormalDiagnosis && _chatDiagnosisPipeline.CanRunFormalGeminiOllamaFromChat && hasImage)
        {
            try
            {
                var mime = NormalizeImageMimeType(request.AttachedImageMimeType);
                var formalGardenContext = focusPlant != null
                    ? BuildFormalDiagnosisGardenContext(
                        user,
                        focusPlant,
                        focusSchedules,
                        focusRecentLogs,
                        focusDiagnoses)
                    : null;
                var diagnosis = await _formalDiagnosisFromBase64.AnalyzeFromBase64Async(
                    normalizedImage!,
                    mime,
                    lastUserText,
                    formalGardenContext,
                    cancellationToken);
                _logger.LogInformation(
                    "AI chat: formal Gemini+Ollama diagnosis completed for user {UserId} (disease label: {Disease}).",
                    request.UserId,
                    diagnosis.Disease);
                return new AiChatReplyDto
                {
                    Reply = FormatDiagnosisAsChatReply(diagnosis),
                    SuggestedIntent = PlantChatIntentDetector.DiseaseDiagnosisIntent,
                    Diagnosis = ToDiagnosisSummary(diagnosis),
                    ResolvedIntent = AiChatIntentResolver.ResolvedFormalDiagnosis
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Formal Gemini+Ollama diagnosis from chat failed; falling back to conversational model.");
            }
        }

        var userMentionsDiseaseOrDamage = PlantChatIntentDetector.IsDiseaseDiagnosisIntent(lastUserText);
        // Broader hint for the conversational system prompt (photos often need pest/damage awareness).
        var diseaseHelpIntent = userMentionsDiseaseOrDamage || hasImage;
        // My Garden CTA in the client: only when the user asked about disease/damage in text — not for every photo attach.
        var suggestedIntentForClient =
            userMentionsDiseaseOrDamage ? PlantChatIntentDetector.DiseaseDiagnosisIntent : null;

        var conversationIncludesRoomScanCatalog = request.Messages.Any(static m =>
            !string.IsNullOrEmpty(m.Content) &&
            m.Content.Contains("[Room scan context", StringComparison.OrdinalIgnoreCase));

        AiChatTurnIntent mainIntent;
        string resolvedIntentApi;
        if (AiChatIntentResolver.IsRoomScanThread(
                conversationIncludesRoomScanCatalog,
                request.RoomScanFollowUp != null))
        {
            mainIntent = AiChatTurnIntent.RoomScanThread;
            resolvedIntentApi = AiChatIntentResolver.ResolvedRoomScanThread;
        }
        else
        {
            var wantsShop = await _profileShopIntentDetector.WantsProfileShopCatalogAsync(
                lastUserText,
                cancellationToken);
            mainIntent = wantsShop
                ? AiChatTurnIntent.ProfileShopRecommendations
                : AiChatTurnIntent.Conversational;
            resolvedIntentApi = wantsShop
                ? AiChatIntentResolver.ResolvedProfileShop
                : AiChatIntentResolver.ResolvedConversational;
        }

        _logger.LogInformation(
            "AI chat resolved intent: {Intent} ({Api}) userId={UserId}",
            mainIntent,
            resolvedIntentApi,
            request.UserId);

        List<RoomScanRecommendationDto>? profileCatalogRecs = null;
        if (mainIntent == AiChatTurnIntent.ProfileShopRecommendations)
        {
            profileCatalogRecs = await LoadProfileShopCatalogRecommendationsAsync(user, lastUserText, cancellationToken);
        }

        var systemPrompt = BuildSystemPrompt(
            user,
            plants,
            focusPlant,
            diseaseHelpIntent,
            focusSchedules,
            focusRecentLogs,
            focusDiagnoses,
            conversationIncludesRoomScanCatalog,
            profileCatalogRecs is { Count: > 0 });

        if (profileCatalogRecs is { Count: > 0 })
        {
            systemPrompt += BuildProfileCatalogPromptAppendix(profileCatalogRecs);
        }

        string? arAppendix;
        try
        {
            arAppendix = await BuildArPreviewPromptAppendixAsync(request, cancellationToken);
        }
        catch (ValidationException ex)
        {
            var msg = ex.Errors.Count > 0 ? ex.Errors[0] : ex.Message;
            return new AiChatReplyDto { Reply = msg };
        }

        if (!string.IsNullOrEmpty(arAppendix))
        {
            systemPrompt += arAppendix;
        }

        var ollamaMessages = new List<OllamaChatTurnDto>
        {
            new() { Role = "system", Content = systemPrompt }
        };

        foreach (var m in request.Messages)
        {
            ollamaMessages.Add(new OllamaChatTurnDto
            {
                Role = m.Role.ToLowerInvariant(),
                Content = m.Content
            });
        }

        if (!string.IsNullOrEmpty(normalizedImage))
        {
            for (var i = ollamaMessages.Count - 1; i >= 0; i--)
            {
                if (!string.Equals(ollamaMessages[i].Role, "user", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var content = string.IsNullOrWhiteSpace(ollamaMessages[i].Content)
                    ? (request.ArSessionId.HasValue
                        ? "The user attached a snapshot from the AR preview (their room with a virtual plant model placed). Describe the scene, comment on whether the placement looks sensible, and suggest how to decorate around this plant. Only discuss disease if the user asked about health or damage."
                        : "The user attached a plant photo. Describe what you see and offer practical care advice. If you notice possible disease or pests, name them cautiously and suggest next steps.")
                    : ollamaMessages[i].Content;
                ollamaMessages[i] = new OllamaChatTurnDto
                {
                    Role = "user",
                    Content = content,
                    ImagesBase64 = new List<string> { normalizedImage }
                };
                break;
            }
        }

        var logSummary = ollamaMessages.Select(m => new
        {
            m.Role,
            ContentLength = m.Content?.Length ?? 0,
            ImageCount = m.ImagesBase64?.Count ?? 0
        });
        _logger.LogInformation(
            "AI chat → {Backend}. UserId={UserId}, GardenPlantId={GardenPlantId}, MessageCount={Count}, Summary={Summary}",
            _aiRouting.UseGeminiOnly ? "Gemini" : "Ollama",
            request.UserId,
            request.GardenPlantId,
            ollamaMessages.Count,
            JsonSerializer.Serialize(logSummary));

        try
        {
            var reply = await _ollama.ChatAsync(ollamaMessages, cancellationToken);
            var replyDto = new AiChatReplyDto
            {
                Reply = reply,
                SuggestedIntent = suggestedIntentForClient,
                NewRecommendations = profileCatalogRecs is { Count: > 0 } ? profileCatalogRecs : null,
                ResolvedIntent = resolvedIntentApi
            };
            await TryAppendRoomScanNewRecommendationsAsync(
                request,
                lastUserText,
                replyDto,
                cancellationToken);
            return replyDto;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            _logger.LogError(ex, "AI chat request failed for user {UserId}", request.UserId);
            return new AiChatReplyDto
            {
                Reply =
                    "I'm having trouble reaching the plant assistant right now. " +
                    (_aiRouting.UseGeminiOnly
                        ? "Please check AiDiagnosis:GeminiApiKey and network access to Google Generative Language API, then try again."
                        : "Please ensure Ollama is running and reachable from the API (see Docker `Ollama__BaseUrl`), then try again."),
                SuggestedIntent = suggestedIntentForClient,
                ResolvedIntent = resolvedIntentApi
            };
        }
    }

    private async Task<List<RoomScanRecommendationDto>?> LoadProfileShopCatalogRecommendationsAsync(
        UserAccount user,
        string? lastUserText,
        CancellationToken cancellationToken)
    {
        try
        {
            var synthetic = UserAccountToRoomProfileMapper.Map(user);
            var maxPrice = UserAccountToRoomProfileMapper.MapBudgetToMaxPrice(user.BudgetRange);
            var skill = UserAccountToRoomProfileMapper.MapSkillLevel(user.ExperienceLevel);
            var pipelineMode = RoomScanPipelineModeParser.FromApiValue(_roomScanHandlerOptions.Value.PipelineMode);
            var notes = lastUserText?.Trim();
            if (notes != null && notes.Length > 1200)
            {
                notes = notes[..1200] + "…";
            }

            var ranking = await _roomScanCatalogRanking.GetRecommendationsAsync(
                new RoomScanCatalogRankingRequest
                {
                    Profile = synthetic,
                    BranchId = null,
                    MaxPrice = maxPrice,
                    PetSafeOnly = user.HasChildrenOrPets == true,
                    SkillLevel = skill,
                    PipelineMode = pipelineMode,
                    RankRefinementNotes = notes,
                    ExcludeListingIds = null
                },
                cancellationToken);

            if (!ranking.NoMatches && ranking.Recommendations.Count > 0)
            {
                _logger.LogInformation(
                    "AI chat: attached {Count} profile-based catalog picks for user {UserId}.",
                    ranking.Recommendations.Count,
                    user.Id);
                return ranking.Recommendations;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI chat: profile-based catalog ranking failed; continuing with text-only prompt.");
        }

        return null;
    }

    private async Task TryAppendRoomScanNewRecommendationsAsync(
        SendAiChatMessageCommand request,
        string? lastUserText,
        AiChatReplyDto replyDto,
        CancellationToken cancellationToken)
    {
        var follow = request.RoomScanFollowUp;
        if (follow == null || string.IsNullOrWhiteSpace(lastUserText))
        {
            return;
        }

        try
        {
            var intent = await _roomScanChatIntent.DetectAsync(lastUserText, cancellationToken);
            if (!intent.WantsDifferentSuggestions)
            {
                return;
            }

            var notes = string.IsNullOrWhiteSpace(intent.RefinementNotes)
                ? lastUserText.Trim()
                : intent.RefinementNotes.Trim();
            if (notes.Length > 1200)
            {
                notes = notes[..1200] + "…";
            }

            if (!_contentSafety.IsAllowed(notes))
            {
                _logger.LogInformation("Room-scan chat refinement skipped: refinement text failed content check.");
                return;
            }

            if (!_plantScope.IsInScopeForPlainUserText(notes))
            {
                _logger.LogInformation("Room-scan chat refinement skipped: refinement text outside plant-assistant scope.");
                return;
            }

            var pipelineMode = RoomScanPipelineModeParser.FromApiValue(
                string.IsNullOrWhiteSpace(follow.PipelineMode)
                    ? _roomScanHandlerOptions.Value.PipelineMode
                    : follow.PipelineMode);

            var ranking = await _roomScanCatalogRanking.GetRecommendationsAsync(
                new RoomScanCatalogRankingRequest
                {
                    Profile = follow.RoomProfile,
                    BranchId = follow.BranchId,
                    MaxPrice = follow.MaxPrice,
                    PetSafeOnly = follow.PetSafeOnly,
                    SkillLevel = follow.SkillLevel,
                    PipelineMode = pipelineMode,
                    RankRefinementNotes = notes,
                    ExcludeListingIds = follow.PreviousListingIds
                },
                cancellationToken);

            if (ranking.NoMatches || ranking.Recommendations.Count == 0)
            {
                _logger.LogInformation("Room-scan chat: intent asked for new picks but catalog returned no rows.");
                return;
            }

            replyDto.NewRecommendations = ranking.Recommendations;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Room-scan chat: could not append new catalog recommendations.");
        }
    }

    private static string BuildProfileCatalogPromptAppendix(IReadOnlyList<RoomScanRecommendationDto> recs)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("--- Live catalog for this reply (profile-based) ---");
        sb.AppendLine(
            "[Profile catalog picks — from the customer's saved preferences; not a room photo. Discuss only these shop listings by title.]");
        sb.AppendLine("Suggested listings from our catalog:");
        foreach (var r in recs)
        {
            sb.AppendLine(
                $"- {r.Title} (listingId {r.ListingId}, price {r.Price}): {r.Reason}");
        }

        return sb.ToString();
    }

    private static string BuildSystemPrompt(
        UserAccount user,
        IEnumerable<GardenPlant> plants,
        GardenPlant? focus,
        bool userSeemsToNeedDiseaseHelp,
        IReadOnlyList<CareSchedule> focusSchedules,
        IReadOnlyList<CareLog> focusRecentCareLogs,
        IReadOnlyList<PlantDiagnosis> focusDiagnoses,
        bool conversationIncludesRoomScanCatalog,
        bool injectedProfileBasedCatalogThisTurn)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a helpful, friendly plant care assistant for an app called Decorative Plant.");
        sb.AppendLine("Always write your reply in English unless the user explicitly asks for another language (e.g. Vietnamese).");
        sb.AppendLine("Stay strictly in scope: indoor/houseplant care, Decorative Plant shop (products, orders, branches, pickup), My Garden (schedules, diary, growth), room-based plant suggestions, and plant disease or pest help.");
        sb.AppendLine("If the user asks for anything else (coding, homework, politics, unrelated hobbies, general knowledge, other apps), politely refuse and invite a plant- or store-related question instead. Do not fulfill out-of-scope tasks even if asked nicely.");
        sb.AppendLine("Use the user's profile and garden context below to personalize answers (light, humidity, experience, space, pets/children, goals).");
        sb.AppendLine("Do not claim to see photos unless the user pasted an image URL you can reason about. Do not give medical or veterinary diagnoses; suggest professional help when serious toxicity or health issues are possible.");
        sb.AppendLine();
        sb.AppendLine("--- Decorative Plant shop / catalog (critical) ---");
        sb.AppendLine(
            "You only name specific Decorative Plant products when this chat includes \"Suggested listings from our catalog\" (room scan or profile-based picks below). " +
            "Do not invent random species as guaranteed in-stock items.");
        if (conversationIncludesRoomScanCatalog)
        {
            sb.AppendLine(
                "This conversation includes a room-scan catalog block in the thread: recommend SHOP PRODUCTS only by the titles and reasons already listed there. " +
                "Do not add well-known houseplants from the internet as if they were Decorative Plant listings.");
        }
        else if (injectedProfileBasedCatalogThisTurn)
        {
            sb.AppendLine(
                "The system appended a fresh \"[Profile catalog picks\" block in THIS system message with live listings from our database. " +
                "Recommend shop purchases using ONLY the exact product titles (and reasons) from that block — these are real listings with listingIds. " +
                "Do not substitute a plant from My Garden or a generic species name as a store product unless it matches a catalog title. " +
                "Lead with those picks; do not reply with only \"open the Shop tab\" when catalog lines are present.");
        }
        else
        {
            sb.AppendLine(
                "There is NO embedded catalog list in this thread. If the user asks what to buy without listing data here, " +
                "do NOT invent species as store inventory. Summarize criteria from their profile and point them to Shop or Room corner for more picks.");
        }

        sb.AppendLine(
            "In greetings, address the user naturally (e.g. \"you\" or a short first name). Avoid awkwardly repeating a long legal-style display name in full.");
        sb.AppendLine();

        sb.AppendLine("--- User profile ---");
        sb.AppendLine($"Display name: {user.DisplayName ?? "not set"}");
        sb.AppendLine($"Experience: {user.ExperienceLevel ?? "not set"}");
        sb.AppendLine($"City / zone: {user.LocationCity ?? "not set"} / {user.HardinessZone ?? "not set"}");
        sb.AppendLine($"Sunlight at home: {user.SunlightExposure ?? "not set"}");
        sb.AppendLine($"Room temperature preference: {user.RoomTemperatureRange ?? "not set"}");
        sb.AppendLine($"Humidity: {user.HumidityLevel ?? "not set"}");
        sb.AppendLine($"Typical watering habit: {user.WateringFrequency ?? "not set"}");
        sb.AppendLine($"Placement: {user.PlacementLocation ?? "not set"}, space: {user.SpaceSize ?? "not set"}");
        sb.AppendLine($"Children or pets at home: {(user.HasChildrenOrPets.HasValue ? user.HasChildrenOrPets.Value.ToString() : "not set")}");
        sb.AppendLine($"Preferred style: {user.PreferredStyle ?? "not set"}, budget: {user.BudgetRange ?? "not set"}");
        sb.AppendLine($"Plant goals: {FormatGoals(user.PlantGoals)}");
        sb.AppendLine();

        sb.AppendLine("--- User's garden (recent plants) ---");
        var list = plants.ToList();
        if (list.Count == 0)
        {
            sb.AppendLine("No plants in the garden yet.");
        }
        else
        {
            foreach (var p in list)
            {
                var d = GardenPlantMapper.DeserializeDetails(p.Details);
                var name = string.IsNullOrWhiteSpace(d.Nickname) ? "Unnamed plant" : d.Nickname;
                var tax = p.Taxonomy?.ScientificName ?? "Unknown species";
                var health = d.Health ?? "?";
                sb.AppendLine($"- {name} ({tax}), health: {health}");
            }
        }

        if (focus != null)
        {
            AppendFocusPlantContextBlock(sb, focus, focusSchedules, focusRecentCareLogs, focusDiagnoses);
            sb.AppendLine();
            sb.AppendLine(
                "Prefer the focus plant's schedules, diary entries, milestones, and past AI diagnoses below when they are relevant to the question. " +
                "If something is missing, say so briefly and give general guidance.");
        }

        sb.AppendLine();
        sb.AppendLine("Keep replies concise unless the user asks for detail. Use bullet points for care steps when helpful.");

        if (userSeemsToNeedDiseaseHelp)
        {
            sb.AppendLine();
            sb.AppendLine("--- Intent ---");
            sb.AppendLine(
                "The user's latest message suggests concern about disease, pests, mold, spots, or visible plant damage. " +
                "Text-only chat cannot replace examining a clear photo. The Decorative Plant app offers AI disease diagnosis " +
                "from My Garden using an uploaded photo of affected leaves. Encourage well-lit, in-focus photos if they use that. " +
                "Do not claim you can see their images unless they pasted a URL here.");
        }

        return sb.ToString();
    }

    /// <summary>Profile + focus plant data for Gemini/Ollama formal disease pipeline (chat photo path).</summary>
    private static string BuildFormalDiagnosisGardenContext(
        UserAccount user,
        GardenPlant focus,
        IReadOnlyList<CareSchedule> focusSchedules,
        IReadOnlyList<CareLog> focusRecentCareLogs,
        IReadOnlyList<PlantDiagnosis> focusDiagnoses)
    {
        var sb = new StringBuilder();
        sb.AppendLine("--- Grower / environment (from user profile) ---");
        sb.AppendLine($"Experience: {user.ExperienceLevel ?? "not set"}");
        sb.AppendLine($"City / zone: {user.LocationCity ?? "not set"} / {user.HardinessZone ?? "not set"}");
        sb.AppendLine($"Sunlight at home: {user.SunlightExposure ?? "not set"}");
        sb.AppendLine($"Humidity: {user.HumidityLevel ?? "not set"}");
        sb.AppendLine($"Typical watering habit: {user.WateringFrequency ?? "not set"}");
        AppendFocusPlantContextBlock(sb, focus, focusSchedules, focusRecentCareLogs, focusDiagnoses);
        sb.AppendLine();
        sb.AppendLine(
            "Relate possible causes of the visible issue to this context when reasonable (e.g. watering vs schedule, humidity, light, recent repot, past checks). " +
            "If context is insufficient, say so.");
        return sb.ToString().Trim();
    }

    private static void AppendFocusPlantContextBlock(
        StringBuilder sb,
        GardenPlant focus,
        IReadOnlyList<CareSchedule> focusSchedules,
        IReadOnlyList<CareLog> focusRecentCareLogs,
        IReadOnlyList<PlantDiagnosis> focusDiagnoses)
    {
        var fd = GardenPlantMapper.DeserializeDetails(focus.Details);
        var fname = string.IsNullOrWhiteSpace(fd.Nickname) ? "This plant" : fd.Nickname;
        var ftax = focus.Taxonomy?.ScientificName ?? "Unknown species";
        var fcommon = focus.Taxonomy != null ? GardenPlantMapper.ToTaxonomySummaryDto(focus.Taxonomy).CommonName : null;
        sb.AppendLine();
        sb.AppendLine("--- Current focus plant (user is likely asking about this one) ---");
        sb.AppendLine(
            $"{fname} — species: {ftax}" +
            (string.IsNullOrWhiteSpace(fcommon) ? "" : $", common name: {fcommon}") +
            $", location in home: {fd.Location ?? "not set"}, source: {fd.Source ?? "not set"}, adopted: {fd.AdoptedDate ?? "not set"}, health: {fd.Health ?? "not set"}, size: {fd.Size ?? "not set"}.");
        AppendFocusMilestones(sb, fd);
        AppendFocusSchedules(sb, focusSchedules);
        AppendFocusCareDiary(sb, focusRecentCareLogs);
        AppendFocusDiagnoses(sb, focusDiagnoses);
    }

    private static void AppendFocusMilestones(StringBuilder sb, GardenPlantDetailsDto fd)
    {
        var milestones = fd.Milestones;
        if (milestones == null || milestones.Count == 0)
        {
            return;
        }

        var sorted = milestones.OrderByDescending(m => m.OccurredAt).Take(8).ToList();
        sb.AppendLine();
        sb.AppendLine("--- Growth milestones (plant diary; this plant) ---");
        foreach (var m in sorted)
        {
            var label = m.Type.ToLowerInvariant() switch
            {
                "first_leaf" => "First leaf",
                "new_growth" => "New growth",
                "flowering" => "Flowering",
                "repotted" => "Repotted",
                _ => string.IsNullOrWhiteSpace(m.Type) ? "Milestone" : m.Type
            };
            var date = m.OccurredAt.ToString("yyyy-MM-dd");
            var line = $"- {date}: {label}";
            if (!string.IsNullOrWhiteSpace(m.Notes))
            {
                line += $" — {m.Notes.Trim()}";
            }

            sb.AppendLine(line);
        }
    }

    private static void AppendFocusSchedules(StringBuilder sb, IReadOnlyList<CareSchedule> schedules)
    {
        if (schedules.Count == 0)
        {
            return;
        }

        sb.AppendLine();
        sb.AppendLine("--- Active care reminders / schedules (this plant) ---");
        foreach (var s in schedules)
        {
            if (s.TaskInfo == null)
            {
                continue;
            }

            try
            {
                var t = JsonSerializer.Deserialize<CareScheduleTaskInfoDto>(
                    s.TaskInfo.RootElement.GetRawText(),
                    TaskInfoJsonOptions);
                if (t == null)
                {
                    continue;
                }

                var parts = new List<string> { $"task: {t.Type}", $"frequency: {t.Frequency}" };
                if (t.IntervalDays.HasValue)
                {
                    parts.Add($"every {t.IntervalDays} days");
                }

                if (!string.IsNullOrWhiteSpace(t.TimeOfDay))
                {
                    parts.Add($"time of day: {t.TimeOfDay}");
                }

                if (t.NextDue.HasValue)
                {
                    parts.Add($"next due (UTC): {t.NextDue:O}");
                }

                sb.AppendLine("- " + string.Join(", ", parts) + ".");
            }
            catch
            {
                sb.AppendLine("- (active schedule; details could not be read)");
            }
        }
    }

    private static void AppendFocusCareDiary(StringBuilder sb, IReadOnlyList<CareLog> logs)
    {
        if (logs.Count == 0)
        {
            return;
        }

        sb.AppendLine();
        sb.AppendLine("--- Recent care diary (newest first; this plant) ---");
        foreach (var log in logs)
        {
            var date = log.PerformedAt?.ToString("yyyy-MM-dd") ?? "?";
            var action = "care";
            string? desc = null;
            string? obs = null;
            string? mood = null;
            try
            {
                if (log.LogInfo != null)
                {
                    var root = log.LogInfo.RootElement;
                    if (root.TryGetProperty("action_type", out var a))
                    {
                        action = a.GetString() ?? action;
                    }

                    if (root.TryGetProperty("description", out var d))
                    {
                        desc = d.GetString();
                    }

                    if (root.TryGetProperty("observations", out var o))
                    {
                        obs = o.GetString();
                    }

                    if (root.TryGetProperty("mood", out var m))
                    {
                        mood = m.GetString();
                    }
                }
            }
            catch
            {
                // keep defaults
            }

            var bits = new List<string> { $"{date}: {action}" };
            if (!string.IsNullOrWhiteSpace(desc))
            {
                bits.Add($"note: {desc.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(obs))
            {
                bits.Add($"observations: {obs.Trim()}");
            }

            if (!string.IsNullOrWhiteSpace(mood))
            {
                bits.Add($"mood: {mood.Trim()}");
            }

            sb.AppendLine("- " + string.Join(" — ", bits));
        }
    }

    private static void AppendFocusDiagnoses(StringBuilder sb, IReadOnlyList<PlantDiagnosis> diagnoses)
    {
        if (diagnoses.Count == 0)
        {
            return;
        }

        sb.AppendLine();
        sb.AppendLine("--- Recent AI disease checks from My Garden (this plant) ---");
        foreach (var d in diagnoses.OrderByDescending(x => x.CreatedAt))
        {
            var date = (d.CreatedAt ?? DateTime.MinValue).ToString("yyyy-MM-dd");
            var (title, summary) = GetDiagnosisTitleAndSummary(d.AiResult);
            var line = $"- {date}: {title}";
            if (!string.IsNullOrWhiteSpace(summary))
            {
                line += $" — {summary}";
            }

            sb.AppendLine(line);
        }
    }

    private static (string Title, string? Summary) GetDiagnosisTitleAndSummary(JsonDocument? aiResult)
    {
        if (aiResult == null)
        {
            return ("Diagnosis", null);
        }

        try
        {
            var disease = aiResult.RootElement.TryGetProperty("disease", out var d) ? d.GetString() : null;
            var recommendations = aiResult.RootElement.TryGetProperty("recommendations", out var r) && r.ValueKind == JsonValueKind.Array
                ? string.Join("; ", r.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrEmpty(x)))
                : null;
            return (disease ?? "Diagnosis", recommendations);
        }
        catch
        {
            return ("Diagnosis", null);
        }
    }

    private static string FormatGoals(JsonDocument? goals)
    {
        if (goals == null) return "not set";
        try
        {
            if (goals.RootElement.ValueKind == JsonValueKind.Array)
            {
                var parts = goals.RootElement.EnumerateArray()
                    .Select(e => e.GetString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
                return parts.Count == 0 ? "not set" : string.Join(", ", parts);
            }
        }
        catch
        {
            // ignore
        }

        return "not set";
    }

    /// <summary>Strips data-URL prefix if present; returns raw base64 or null.</summary>
    private static string? NormalizeAttachedImageBase64(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var s = raw.Trim();
        if (s.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var comma = s.IndexOf(',', StringComparison.Ordinal);
            if (comma >= 0 && comma < s.Length - 1)
            {
                s = s[(comma + 1)..].Trim();
            }
        }

        return string.IsNullOrEmpty(s) ? null : s;
    }

    private static string NormalizeImageMimeType(string? mime)
    {
        if (string.IsNullOrWhiteSpace(mime))
        {
            return "image/jpeg";
        }

        var m = mime.Trim().ToLowerInvariant();
        return m is "image/jpeg" or "image/png" or "image/webp" ? m : "image/jpeg";
    }

    /// <summary>Short chat line only — full detail lives in <see cref="AiChatReplyDto.Diagnosis"/> for the client UI.</summary>
    private static string FormatDiagnosisAsChatReply(AiDiagnosisResultDto d)
    {
        var pct = (int)Math.Round(d.Confidence > 1 ? d.Confidence : d.Confidence * 100);
        return $"We checked your photo — the most likely issue is {d.Disease} (about {pct}% confidence). Details are in the summary below.";
    }

    private static AiChatDiagnosisSummaryDto ToDiagnosisSummary(AiDiagnosisResultDto d)
    {
        return new AiChatDiagnosisSummaryDto
        {
            Disease = d.Disease,
            Confidence = d.Confidence,
            Symptoms = d.Symptoms,
            Recommendations = d.Recommendations,
            Explanation = d.Explanation
        };
    }

    private async Task<string?> BuildArPreviewPromptAppendixAsync(
        SendAiChatMessageCommand request,
        CancellationToken cancellationToken)
    {
        if (!request.ArSessionId.HasValue &&
            string.IsNullOrWhiteSpace(request.PlacementContextJson) &&
            !request.ProductListingId.HasValue)
        {
            return null;
        }

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("--- Shop listing & AR context (Decorative Plant app) ---");
        if (request.ArSessionId.HasValue || !string.IsNullOrWhiteSpace(request.PlacementContextJson))
        {
            sb.AppendLine(
                "AR: the user may attach a photo from the in-app AR view (real room + virtual plant on a surface). " +
                "Comment on placement, lighting/space, and decor. Do not default to disease diagnosis unless they ask about damage or pests.");
        }
        else if (request.ProductListingId.HasValue)
        {
            sb.AppendLine(
                "The user opened this chat from a product detail screen — prioritize advice that fits that listing and indoor decoration.");
        }

        if (request.ArSessionId.HasValue)
        {
            var session = await _db.ArPreviewSessions.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == request.ArSessionId.Value, cancellationToken);
            if (session == null || session.UserId != request.UserId)
            {
                throw new NotFoundException("AR preview session", request.ArSessionId.Value);
            }

            if (session.ExpiresAt < DateTime.UtcNow)
            {
                throw new ValidationException(
                    "This AR preview session has expired. Open AR preview again and start a new session.");
            }

            var raw = session.ScanJson?.RootElement.GetRawText() ?? "{}";
            if (raw.Length > 12000)
            {
                raw = raw[..12000] + "…";
            }

            sb.AppendLine("Stored scan + placement JSON from the mobile AR flow:");
            sb.AppendLine(raw);
        }

        if (!string.IsNullOrWhiteSpace(request.PlacementContextJson))
        {
            var extra = request.PlacementContextJson.Trim();
            if (extra.Length > 4000)
            {
                extra = extra[..4000] + "…";
            }

            sb.AppendLine("Additional placement hints from the client:");
            sb.AppendLine(extra);
        }

        if (request.ProductListingId.HasValue)
        {
            var pl = await _db.ProductListings.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == request.ProductListingId.Value, cancellationToken);
            if (pl != null)
            {
                var title = TryGetListingTitle(pl);
                sb.AppendLine(
                    request.ArSessionId.HasValue
                        ? $"Product listing in focus: listingId={pl.Id}, title=\"{title ?? "unknown"}\"."
                        : $"The user opened assistant from a product detail page. Focus on this shop listing when relevant: listingId={pl.Id}, title=\"{title ?? "unknown"}\".");
            }
        }

        return sb.ToString();
    }

    private static string? TryGetListingTitle(ProductListing pl)
    {
        if (pl.ProductInfo == null)
        {
            return null;
        }

        return pl.ProductInfo.RootElement.TryGetProperty("title", out var t) ? t.GetString() : null;
    }
}
