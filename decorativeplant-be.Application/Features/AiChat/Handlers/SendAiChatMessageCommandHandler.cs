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
using decorativeplant_be.Application.Features.Garden.Queries;
using decorativeplant_be.Application.Features.Garden;
using decorativeplant_be.Application.Features.RoomScan.Services;
using decorativeplant_be.Application.Features.Commerce.ProductListings.Queries;
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
    private readonly IAiContextInferenceService _contextInference;
    private readonly IOptions<RoomScanHandlerOptions> _roomScanHandlerOptions;
    private readonly AiRoutingSettings _aiRouting;
    private readonly IUserContentSafetyService _contentSafety;
    private readonly IPlantAssistantScopeService _plantScope;
    private readonly IApplicationDbContext _db;
    private readonly IMediator _mediator;
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
        IAiContextInferenceService contextInference,
        IOptions<RoomScanHandlerOptions> roomScanHandlerOptions,
        IOptions<AiRoutingSettings> aiRouting,
        IUserContentSafetyService contentSafety,
        IPlantAssistantScopeService plantScope,
        IMediator mediator,
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
        _contextInference = contextInference;
        _roomScanHandlerOptions = roomScanHandlerOptions;
        _aiRouting = aiRouting.Value;
        _contentSafety = contentSafety;
        _plantScope = plantScope;
        _mediator = mediator;
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
                List<CareScheduleTaskInfoDto>? suggestedSchedules = null;
                if (focusPlant != null)
                {
                    try
                    {
                        var recoveryLines = new List<string>
                        {
                            $"Active issue (photo diagnosis): {diagnosis.Disease}"
                        };
                        if (diagnosis.Recommendations is { Count: > 0 })
                        {
                            recoveryLines.Add("Suggested actions: " + string.Join("; ", diagnosis.Recommendations.Take(6)));
                        }

                        if (!string.IsNullOrWhiteSpace(diagnosis.Explanation))
                        {
                            recoveryLines.Add("Notes: " + diagnosis.Explanation.Trim());
                        }

                        var plan = await _mediator.Send(new GenerateGardenPlantAiSchedulePlanQuery
                        {
                            UserId = request.UserId,
                            PlantId = focusPlant.Id,
                            HorizonDays = 30,
                            UtcOffsetMinutes = request.UtcOffsetMinutes,
                            RecoveryDiagnosisContext = string.Join(Environment.NewLine, recoveryLines)
                        }, cancellationToken);
                        suggestedSchedules = plan.Tasks;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "AI chat: could not generate suggested schedules after diagnosis.");
                    }
                }

                return new AiChatReplyDto
                {
                    Reply = FormatDiagnosisAsChatReply(diagnosis),
                    SuggestedIntent = PlantChatIntentDetector.DiseaseDiagnosisIntent,
                    Diagnosis = ToDiagnosisSummary(diagnosis),
                    SuggestedSchedules = suggestedSchedules,
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

        // If the user asked about a specific shop item by name/title, prefer an explicit product-listing search.
        // This keeps the assistant grounded in actual inventory rather than only profile-ranked picks.
        // Example: "I see that you have Lan Y in your shop...".
        List<RoomScanRecommendationDto>? directTitleMatches = null;
        var requestedTitle = TryExtractShopListingName(lastUserText);
        if (!string.IsNullOrWhiteSpace(requestedTitle))
        {
            directTitleMatches = await TryLoadListingMatchesBySearchAsync(
                requestedTitle!,
                cancellationToken);
            if (directTitleMatches is { Count: > 0 })
            {
                // Treat this turn as shop-focused so clients can render the picks.
                resolvedIntentApi = AiChatIntentResolver.ResolvedProfileShop;
            }
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
            (directTitleMatches is { Count: > 0 }) || (profileCatalogRecs is { Count: > 0 }),
            request.IncludeUserProfileContext,
            request.IncludeGardenListContext);

        if (directTitleMatches is { Count: > 0 })
        {
            systemPrompt += BuildShopSearchPromptAppendix(requestedTitle!, directTitleMatches);
        }
        else if (profileCatalogRecs is { Count: > 0 })
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
                // Prefer explicit title matches when present; otherwise use profile-ranked picks.
                NewRecommendations = directTitleMatches is { Count: > 0 }
                    ? directTitleMatches
                    : (profileCatalogRecs is { Count: > 0 } ? profileCatalogRecs : null),
                ResolvedIntent = resolvedIntentApi
            };
            await TryAppendRoomScanNewRecommendationsAsync(
                request,
                lastUserText,
                replyDto,
                cancellationToken);

            if (ShouldShowSetupIdeaCards(request.Messages, lastUserText, request.PlacementContextJson))
            {
                replyDto.UiSuggestions = new AiChatUiSuggestionsDto
                {
                    SetupIdeaCards = BuildSetupIdeaCardsDynamic(lastUserText, request.PlacementContextJson, request.Messages, reply),
                    QuickActions = new List<AiChatQuickActionDto>(),
                    AvailableStyles = BuildDefaultAvailableStyles()
                };
            }

            // Gemini-powered context inference (Phase B): return suggestions + contextPatch proposals.
            if (!string.IsNullOrWhiteSpace(lastUserText))
            {
                replyDto.UiSuggestions ??= new AiChatUiSuggestionsDto { AvailableStyles = BuildDefaultAvailableStyles() };
                if (replyDto.UiSuggestions.AvailableStyles.Count == 0)
                {
                    replyDto.UiSuggestions.AvailableStyles = BuildDefaultAvailableStyles();
                }

                try
                {
                    var inferences = await _contextInference.InferAsync(
                        lastUserText!,
                        replyDto.UiSuggestions.AvailableStyles,
                        cancellationToken);
                    if (inferences.Count > 0)
                    {
                        // Keep it small to avoid UI spam.
                        replyDto.UiSuggestions.ContextInferences = inferences.Take(4).ToList();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Context inference failed.");
                }
            }

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

    private static bool ShouldShowSetupIdeaCards(
        IReadOnlyList<AiChatMessageDto> messages,
        string? lastUserText,
        string? placementContextJson)
    {
        if (string.IsNullOrWhiteSpace(lastUserText))
        {
            return false;
        }

        var t = lastUserText.Trim().ToLowerInvariant();
        if (LooksLikeGreetingOrAckOnly(t))
        {
            return false;
        }

        // If the user explicitly asks for setup/decor ideas, we can show cards even after a style is chosen.
        // Otherwise: stop showing setup cards once a style is chosen (prevents spam).
        var explicitSetupAsk =
            t.Contains("setup idea", StringComparison.Ordinal) ||
            t.Contains("setup ideas", StringComparison.Ordinal) ||
            t.Contains("setup", StringComparison.Ordinal) ||
            t.Contains("decorate", StringComparison.Ordinal) ||
            t.Contains("decoration", StringComparison.Ordinal) ||
            (t.Contains("another", StringComparison.Ordinal) && t.Contains("style", StringComparison.Ordinal)) ||
            t.Contains("different style", StringComparison.Ordinal) ||
            t.Contains("other style", StringComparison.Ordinal);

        if (!explicitSetupAsk && TryGetDesignStyleKeyFromPlacementContextJson(placementContextJson) is { Length: > 0 })
        {
            return false;
        }

        string[] keywords =
        [
            "decorate", "decoration", "style", "room", "corner", "setup", "vibe", "green room",
            "living room", "bedroom", "office", "desk", "workspace"
        ];

        return keywords.Any(k => t.Contains(k, StringComparison.Ordinal));
    }

    private static bool LooksLikeGreetingOrAckOnly(string lower)
    {
        if (string.IsNullOrWhiteSpace(lower)) return true;
        var t = lower.Trim();
        if (t.Length > 80) return false;
        string[] greetings = ["hi", "hello", "hey", "good morning", "good afternoon", "good evening", "thanks", "thank you"];
        return greetings.Any(g => t == g || t.StartsWith(g + " ", StringComparison.Ordinal));
    }

    private static string? TryGetDesignStyleKeyFromPlacementContextJson(string? placementContextJson)
    {
        if (string.IsNullOrWhiteSpace(placementContextJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(placementContextJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (!root.TryGetProperty("designContext", out var design) || design.ValueKind != JsonValueKind.Object) return null;
            if (!design.TryGetProperty("styleKey", out var style) || style.ValueKind != JsonValueKind.String) return null;
            var s = style.GetString();
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static List<AiChatSetupIdeaCardDto> BuildSetupIdeaCardsDynamic(
        string? lastUserText,
        string? placementContextJson,
        IReadOnlyList<AiChatMessageDto>? messages,
        string? currentAssistantReply)
    {
        // Hard UX rule: exactly 3 setup cards, but choose the best 3 based on message + context.
        var lower = (lastUserText ?? string.Empty).Trim().ToLowerInvariant();
        // User text + prior assistant + this reply (cards are chosen after the LLM runs, so we can align with what it just said).
        var hintLower = BuildStyleHintLowerForSetupCards(lastUserText, messages, currentAssistantReply);
        var roomLightKey = TryGetRoomLightKeyFromPlacementContextJson(placementContextJson);

        var wantsDesk = lower.Contains("desk", StringComparison.Ordinal) || lower.Contains("workspace", StringComparison.Ordinal) ||
                        lower.Contains("office", StringComparison.Ordinal);
        var wantsLiving = lower.Contains("living room", StringComparison.Ordinal) || lower.Contains("livingroom", StringComparison.Ordinal);
        var wantsBedroom = lower.Contains("bedroom", StringComparison.Ordinal) || lower.Contains("sleep", StringComparison.Ordinal);
        var wantsPetSafe = lower.Contains("pet", StringComparison.Ordinal) || lower.Contains("cat", StringComparison.Ordinal) ||
                           lower.Contains("dog", StringComparison.Ordinal) || lower.Contains("kid", StringComparison.Ordinal) ||
                           lower.Contains("child", StringComparison.Ordinal);
        var wantsLively = lower.Contains("lively", StringComparison.Ordinal) || lower.Contains("tropical", StringComparison.Ordinal) ||
                          lower.Contains("vibrant", StringComparison.Ordinal);
        var wantsMinimal = lower.Contains("minimal", StringComparison.Ordinal) || lower.Contains("clean", StringComparison.Ordinal) ||
                           lower.Contains("simple", StringComparison.Ordinal);

        // User wants a different aesthetic than the default trio (often all tie at the same base score).
        var wantsAlternativeStyle =
            (lower.Contains("another", StringComparison.Ordinal) && lower.Contains("style", StringComparison.Ordinal)) ||
            lower.Contains("different style", StringComparison.Ordinal) ||
            lower.Contains("other style", StringComparison.Ordinal) ||
            lower.Contains("something else", StringComparison.Ordinal) ||
            lower.Contains("something different", StringComparison.Ordinal) ||
            lower.Contains("more styles", StringComparison.Ordinal) ||
            lower.Contains("other aesthetic", StringComparison.Ordinal) ||
            lower.Contains("different aesthetic", StringComparison.Ordinal) ||
            lower.Contains("new style", StringComparison.Ordinal) ||
            lower.Contains("try a different", StringComparison.Ordinal);

        var wantsScandi = hintLower.Contains("scandinavian", StringComparison.Ordinal) || hintLower.Contains("scandi", StringComparison.Ordinal) ||
                          hintLower.Contains("nordic", StringComparison.Ordinal) || hintLower.Contains("hygge", StringComparison.Ordinal);
        var wantsBohemian = hintLower.Contains("bohemian", StringComparison.Ordinal) || hintLower.Contains("boho", StringComparison.Ordinal) ||
                             hintLower.Contains("jungalow", StringComparison.Ordinal) || hintLower.Contains("macrame", StringComparison.Ordinal);
        var wantsBiophilic = hintLower.Contains("biophilic", StringComparison.Ordinal) || hintLower.Contains("wellness", StringComparison.Ordinal) ||
                             hintLower.Contains("nature indoors", StringComparison.Ordinal);
        var wantsJapandi = hintLower.Contains("japandi", StringComparison.Ordinal) || hintLower.Contains("wabi", StringComparison.Ordinal) ||
                           hintLower.Contains("zen aesthetic", StringComparison.Ordinal);
        var wantsMidCentury =
            hintLower.Contains("mid-century", StringComparison.Ordinal) ||
            hintLower.Contains("mid century", StringComparison.Ordinal) ||
            hintLower.Contains("midcentury", StringComparison.Ordinal) ||
            hintLower.Contains("mid-century modern", StringComparison.Ordinal) ||
            hintLower.Contains("mid century modern", StringComparison.Ordinal) ||
            hintLower.Contains("atomic age", StringComparison.Ordinal) ||
            (hintLower.Contains("mcm", StringComparison.Ordinal) &&
             (hintLower.Contains(" mcm ", StringComparison.Ordinal) || hintLower.Contains("mcm style", StringComparison.Ordinal) ||
              hintLower.StartsWith("mcm ", StringComparison.Ordinal) || hintLower.EndsWith(" mcm", StringComparison.Ordinal)));

        // Do not bury the starter cards if the user is doubling down on one of them.
        var softenDefaultTrioPenalty = wantsMinimal || wantsDesk || wantsLively;
        var defaultTrioPenalty = wantsAlternativeStyle && !softenDefaultTrioPenalty ? -34 : 0;
        var altStyleBoost = wantsAlternativeStyle ? 22 : 0;
        var namedAltBoost = (wantsScandi || wantsBohemian || wantsBiophilic || wantsJapandi) ? 18 : 0;
        var wantsAnyNamedStyleHint = wantsScandi || wantsBohemian || wantsBiophilic || wantsJapandi || wantsMidCentury;
        // When the user asks for "another style" but no specific style appears in hints, rotate which 3 non-starter cards we show (uses message depth so repeats still change).
        var useAltDeckWindowRotation =
            wantsAlternativeStyle && !wantsAnyNamedStyleHint && !softenDefaultTrioPenalty;

        var library = new List<(AiChatSetupIdeaCardDto Card, Func<int> Score)>
        {
            (
                BuildSetupIdeaCard(
                    id: "Minimal_Green_Corner",
                    title: "Minimal Green Corner",
                    styleKey: "minimal",
                    plantCount: 2,
                    difficulty: "beginner",
                    subtitle: roomLightKey == "low"
                        ? "2 plants • low light friendly"
                        : "2 plants • clean + simple"),
                () =>
                {
                    var s = 10 + defaultTrioPenalty;
                    if (wantsMinimal) s += 12;
                    if (roomLightKey == "low") s += 10;
                    if (wantsLiving) s += 3;
                    return s;
                }
            ),
            (
                BuildSetupIdeaCard(
                    id: "Tropical_Living",
                    title: "Tropical Living",
                    styleKey: "tropical",
                    plantCount: 3,
                    difficulty: "beginner",
                    subtitle: roomLightKey == "low"
                        ? "3 plants • bright window recommended"
                        : "3 plants • lively + bold"),
                () =>
                {
                    var s = 10 + defaultTrioPenalty;
                    if (wantsLively) s += 12;
                    if (wantsLiving) s += 6;
                    if (roomLightKey == "low") s -= 8;
                    return s;
                }
            ),
            (
                BuildSetupIdeaCard(
                    id: "Desk_Plant_Setup",
                    title: "Desk Plant Setup",
                    styleKey: "desk",
                    plantCount: 2,
                    difficulty: "beginner",
                    subtitle: roomLightKey == "low"
                        ? "1–2 small plants • low light options"
                        : "1–2 small plants • workspace"),
                () =>
                {
                    var s = 10 + defaultTrioPenalty;
                    if (wantsDesk) s += 14;
                    if (roomLightKey == "low") s += 6;
                    return s;
                }
            ),
            (
                BuildSetupIdeaCard(
                    id: "Pet_Safe_Greens",
                    title: "Pet-safe Greens",
                    styleKey: "pet_safe",
                    plantCount: 2,
                    difficulty: "beginner",
                    subtitle: "2 plants • kid/pet friendly"),
                () =>
                {
                    var s = 8;
                    if (wantsPetSafe) s += 14;
                    if (wantsAlternativeStyle) s += 6;
                    return s;
                }
            ),
            (
                BuildSetupIdeaCard(
                    id: "Calm_Bedside",
                    title: "Calm Bedside",
                    styleKey: "minimal",
                    plantCount: 1,
                    difficulty: "beginner",
                    subtitle: "1 plant • calm + easy care"),
                () =>
                {
                    var s = 6;
                    if (wantsBedroom) s += 14;
                    if (wantsMinimal) s += 4;
                    if (roomLightKey == "low") s += 4;
                    if (wantsAlternativeStyle) s += 10;
                    return s;
                }
            ),
            (
                BuildSetupIdeaCard(
                    id: "Scandinavian_Serenity",
                    title: "Scandinavian Serenity",
                    styleKey: "scandinavian",
                    plantCount: 2,
                    difficulty: "beginner",
                    subtitle: roomLightKey == "low"
                        ? "2 plants • light woods + airy palette"
                        : "2 plants • pale palette + natural wood"),
                () =>
                {
                    var s = 9 + altStyleBoost + namedAltBoost;
                    if (wantsScandi) s += 20;
                    if (wantsMinimal) s += 4;
                    if (roomLightKey == "bright") s += 3;
                    return s;
                }
            ),
            (
                BuildSetupIdeaCard(
                    id: "Bohemian_Jungalow",
                    title: "Bohemian Jungalow",
                    styleKey: "bohemian",
                    plantCount: 3,
                    difficulty: "beginner",
                    subtitle: "3 plants • layered textures + warmth"),
                () =>
                {
                    var s = 9 + altStyleBoost + namedAltBoost;
                    if (wantsBohemian) s += 20;
                    if (wantsLively) s += 6;
                    if (wantsLiving) s += 4;
                    return s;
                }
            ),
            (
                BuildSetupIdeaCard(
                    id: "Biophilic_Corner",
                    title: "Biophilic Corner",
                    styleKey: "biophilic",
                    plantCount: 3,
                    difficulty: "beginner",
                    subtitle: "3 plants • nature-forward + calm"),
                () =>
                {
                    var s = 9 + altStyleBoost + namedAltBoost;
                    if (wantsBiophilic) s += 20;
                    if (wantsLiving) s += 3;
                    return s;
                }
            ),
            (
                BuildSetupIdeaCard(
                    id: "Japandi_Quiet",
                    title: "Japandi Quiet",
                    styleKey: "japandi",
                    plantCount: 2,
                    difficulty: "beginner",
                    subtitle: "2 plants • calm lines + earthy tones"),
                () =>
                {
                    var s = 9 + altStyleBoost + namedAltBoost;
                    if (wantsJapandi) s += 20;
                    if (wantsMinimal) s += 5;
                    return s;
                }
            ),
            (
                BuildSetupIdeaCard(
                    id: "Mid_Century_Grove",
                    title: "Mid-Century Grove",
                    styleKey: "mid_century",
                    plantCount: 2,
                    difficulty: "beginner",
                    subtitle: "2 plants • teak tones + sculptural leaves"),
                () =>
                {
                    var s = 9 + altStyleBoost + namedAltBoost;
                    if (wantsMidCentury) s += 24;
                    if (wantsLiving) s += 4;
                    if (wantsLively) s += 3;
                    if (wantsMinimal) s += 3;
                    return s;
                }
            )
        };

        var tieSalt = $"{lower}|msg:{messages?.Count ?? 0}";
        var scoredOrdered = library
            .Select(x => (Card: x.Card, Score: x.Score()))
            .OrderByDescending(t => t.Score)
            .ThenBy(t => StableSetupCardTieBreaker(t.Card.Id, tieSalt))
            .ToList();

        List<AiChatSetupIdeaCardDto> picked;
        if (useAltDeckWindowRotation)
        {
            var alts = scoredOrdered.Where(t => !IsStarterSetupIdeaCard(t.Card.Id)).ToList();
            if (alts.Count >= 3)
            {
                var n = alts.Count;
                var maxStart = n - 3;
                var start = (int)(StableSetupCardTieBreaker("altwin", tieSalt) % (maxStart + 1));
                picked = alts.Skip(start).Take(3).Select(t => t.Card).DistinctBy(x => x.Id).ToList();
            }
            else
            {
                picked = scoredOrdered.Select(t => t.Card).DistinctBy(x => x.Id).Take(3).ToList();
            }
        }
        else
        {
            picked = scoredOrdered.Select(t => t.Card).DistinctBy(x => x.Id).Take(3).ToList();
        }

        // Always return exactly 3 (fallback).
        if (picked.Count < 3)
        {
            var fallback = new[]
            {
                BuildSetupIdeaCard("Mid_Century_Grove", "Mid-Century Grove", "mid_century", 2, "beginner", "2 plants • teak tones + sculptural leaves"),
                BuildSetupIdeaCard("Scandinavian_Serenity", "Scandinavian Serenity", "scandinavian", 2, "beginner", "2 plants • pale palette + natural wood"),
                BuildSetupIdeaCard("Bohemian_Jungalow", "Bohemian Jungalow", "bohemian", 3, "beginner", "3 plants • layered textures + warmth"),
            };
            foreach (var c in fallback)
            {
                if (picked.Count >= 3) break;
                if (picked.All(x => x.Id != c.Id)) picked.Add(c);
            }
        }

        return picked;
    }

    private static string? TryGetRoomLightKeyFromPlacementContextJson(string? placementContextJson)
    {
        if (string.IsNullOrWhiteSpace(placementContextJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(placementContextJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (!root.TryGetProperty("roomContext", out var room) || room.ValueKind != JsonValueKind.Object) return null;
            if (!room.TryGetProperty("lightKey", out var light) || light.ValueKind != JsonValueKind.String) return null;
            var s = light.GetString();
            s = string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToLowerInvariant();
            return s is "low" or "medium" or "bright" ? s : null;
        }
        catch
        {
            return null;
        }
    }

    private static List<AiChatDesignStyleOptionDto> BuildDefaultAvailableStyles()
    {
        return
        [
            new AiChatDesignStyleOptionDto { StyleKey = "minimal", Label = "Minimal" },
            new AiChatDesignStyleOptionDto { StyleKey = "tropical", Label = "Tropical" },
            new AiChatDesignStyleOptionDto { StyleKey = "desk", Label = "Desk" },
            new AiChatDesignStyleOptionDto { StyleKey = "pet_safe", Label = "Pet-safe" },
            new AiChatDesignStyleOptionDto { StyleKey = "scandinavian", Label = "Scandinavian" },
            new AiChatDesignStyleOptionDto { StyleKey = "bohemian", Label = "Bohemian" },
            new AiChatDesignStyleOptionDto { StyleKey = "biophilic", Label = "Biophilic" },
            new AiChatDesignStyleOptionDto { StyleKey = "japandi", Label = "Japandi" },
            new AiChatDesignStyleOptionDto { StyleKey = "mid_century", Label = "Mid-Century Modern" }
        ];
    }

    private static string BuildStyleHintLowerForSetupCards(
        string? lastUserText,
        IReadOnlyList<AiChatMessageDto>? messages,
        string? currentAssistantReply)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(lastUserText))
        {
            sb.Append(lastUserText.Trim());
            sb.Append(' ');
        }

        var prevAssistant = TryGetAssistantMessageBeforeLastUser(messages);
        if (!string.IsNullOrWhiteSpace(prevAssistant))
        {
            var tail = prevAssistant.Trim();
            if (tail.Length > 1600)
            {
                tail = tail[^1600..];
            }

            sb.Append(tail);
            sb.Append(' ');
        }

        if (!string.IsNullOrWhiteSpace(currentAssistantReply))
        {
            var cur = currentAssistantReply.Trim();
            if (cur.Length > 4000)
            {
                cur = cur[..4000];
            }

            sb.Append(cur);
        }

        return sb.ToString().ToLowerInvariant();
    }

    private static string? TryGetAssistantMessageBeforeLastUser(IReadOnlyList<AiChatMessageDto>? messages)
    {
        if (messages is not { Count: >= 2 }) return null;
        var last = messages[^1];
        if (!string.Equals(last.Role, "user", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        for (var i = messages.Count - 2; i >= 0; i--)
        {
            if (string.Equals(messages[i].Role, "assistant", StringComparison.OrdinalIgnoreCase))
            {
                return messages[i].Content;
            }
        }

        return null;
    }

    private static bool IsStarterSetupIdeaCard(string cardId) =>
        string.Equals(cardId, "Minimal_Green_Corner", StringComparison.Ordinal) ||
        string.Equals(cardId, "Tropical_Living", StringComparison.Ordinal) ||
        string.Equals(cardId, "Desk_Plant_Setup", StringComparison.Ordinal);

    private static uint StableSetupCardTieBreaker(string cardId, string tieSalt)
    {
        unchecked
        {
            uint h = 2166136261u;
            foreach (var c in cardId)
            {
                h ^= c;
                h *= 16777619u;
            }

            h ^= 0x9e3779b9u;
            foreach (var c in tieSalt)
            {
                h ^= c;
                h *= 16777619u;
            }

            return h;
        }
    }

    private static AiChatSetupIdeaCardDto BuildSetupIdeaCard(
        string id,
        string title,
        string styleKey,
        int plantCount,
        string difficulty,
        string subtitle)
    {
        return new AiChatSetupIdeaCardDto
        {
            Id = id,
            Title = title,
            StyleKey = styleKey,
            PlantCount = plantCount,
            Difficulty = difficulty,
            Subtitle = subtitle,
            ActionMode = "PATCH_AND_PREFILL",
            ContextPatch = new AiChatContextPatchEnvelopeDto
            {
                Version = 1,
                Patch = new AiChatContextPatchDto
                {
                    DesignContext = new AiChatDesignContextPatchDto
                    {
                        StyleKey = styleKey
                    }
                }
            },
            PreviewImagePrompt = BuildSetupPreviewPrompt(styleKey)
        };
    }

    private static string BuildSetupPreviewPrompt(string styleKey)
    {
        // Standardized prompt template (phase 1).
        return
            $"Indoor plant decoration setup, style: {styleKey}, room: bright modern living room, " +
            "plants arranged naturally, minimal interior design photography, realistic lighting";
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

    private async Task<List<RoomScanRecommendationDto>?> TryLoadListingMatchesBySearchAsync(
        string requestedTitle,
        CancellationToken cancellationToken)
    {
        var search = requestedTitle.Trim();
        if (search.Length < 2)
        {
            return null;
        }
        if (search.Length > 80)
        {
            search = search[..80];
        }

        try
        {
            var paged = await _mediator.Send(
                new GetProductListingsQuery
                {
                    Search = search,
                    // IMPORTANT: Do NOT group by species here.
                    // The shop may have multiple listings for the same plant (different branches/batches),
                    // and grouping would collapse them into a single item, making the chat look like it
                    // "can't find" products that are actually being sold.
                    GroupBySpecies = false,
                    Page = 1,
                    PageSize = 20,
                    SortBy = "inventory",
                    SortOrder = "desc"
                },
                cancellationToken);

            // Map product listings to the same lightweight recommendation DTO used by room-scan/profile shop.
            // GetProductListingsHandler already filters to in-stock, active/published, and price>0 for non-staff.
            var outList = paged.Items
                .Where(p => p.StockQuantity > 0)
                .Take(10)
                .Select(p => new RoomScanRecommendationDto
                {
                    ListingId = p.Id,
                    Title = !string.IsNullOrWhiteSpace(p.CommonNameEn) ? p.CommonNameEn.Trim() : p.Title,
                    Price = p.Price,
                    ImageUrl = p.Images?.FirstOrDefault(i => i.IsPrimary)?.Url ?? p.Images?.FirstOrDefault()?.Url,
                    Reason = "Matched by name from current shop listings."
                })
                .ToList();

            return outList.Count > 0 ? outList : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI chat: product listing search failed for title '{Title}'.", requestedTitle);
            return null;
        }
    }

    private static string? TryExtractShopListingName(string? lastUserText)
    {
        if (string.IsNullOrWhiteSpace(lastUserText))
        {
            return null;
        }

        var t = lastUserText.Trim();

        // Prefer quoted phrases: "Lan Y"
        var firstQuote = t.IndexOf('"');
        if (firstQuote >= 0)
        {
            var secondQuote = t.IndexOf('"', firstQuote + 1);
            if (secondQuote > firstQuote + 1)
            {
                var quoted = t.Substring(firstQuote + 1, secondQuote - firstQuote - 1).Trim();
                return quoted.Length > 0 ? quoted : null;
            }
        }

        // Heuristic for common phrasing: "do you have X" / "I see you have X" / "in your shop X"
        // Keep it conservative; if we can't confidently extract, return null.
        ReadOnlySpan<string> needles =
        [
            "i see that you have ",
            "i see you have ",
            "do you have ",
            "in your shop ",
            "from your shop ",
            "in the shop ",
        ];

        var lower = t.ToLowerInvariant();
        foreach (var n in needles)
        {
            var idx = lower.IndexOf(n, StringComparison.Ordinal);
            if (idx < 0) continue;
            var start = idx + n.Length;
            if (start >= t.Length) continue;
            var chunk = t[start..].Trim();

            // Cut at common punctuation.
            var cut = chunk.IndexOfAny(['.', '?', '!', '\n', '\r', ',']);
            if (cut > 0)
            {
                chunk = chunk[..cut].Trim();
            }

            // Avoid extracting full sentences.
            if (chunk.Length is >= 2 and <= 60)
            {
                return chunk;
            }
        }

        return null;
    }

    private static string BuildShopSearchPromptAppendix(
        string requestedTitle,
        IReadOnlyList<RoomScanRecommendationDto> recs)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("--- Live catalog for this reply (name match) ---");
        sb.AppendLine(
            $"[User asked about \"{requestedTitle}\". These are the closest matching in-stock Decorative Plant listings right now. Discuss only these listings by title.]");
        sb.AppendLine("Matching listings from our catalog:");
        foreach (var r in recs)
        {
            sb.AppendLine($"- {r.Title} (price {r.Price}): {r.Reason}");
        }

        return sb.ToString();
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
                $"- {r.Title} (price {r.Price}): {r.Reason}");
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
        bool injectedProfileBasedCatalogThisTurn,
        bool includeUserProfileContext,
        bool includeGardenListContext)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a helpful, friendly plant care assistant for an app called Decorative Plant.");
        sb.AppendLine(
            "Always write your reply in English unless the user explicitly asks you to answer in another language (e.g. Vietnamese).");
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
                "Recommend shop purchases using ONLY the exact product titles (and reasons) from that block — copy titles VERBATIM (do not translate, do not add Vietnamese aliases). " +
                "Do not substitute a plant from My Garden or a generic species name as a store product unless it matches a catalog title. " +
                "Lead with those picks. When the catalog block includes multiple listings, provide 2–4 options and briefly compare them (best for low light, easiest care, best for pets, etc.). " +
                "Do not reply with only \"open the Shop tab\" when catalog lines are present. " +
                "If the user asks about a plant name that is NOT in the provided catalog list, do not claim our store \"doesn't have it\" — say it is not in the CURRENT in-stock picks (it may be out of stock, unpublished, or spelled differently) and then offer the closest alternatives from the provided list.");

            sb.AppendLine(
                "If the catalog block contains 4 or more listings and the user did not ask for fewer, present EXACTLY 4 product options.");

            sb.AppendLine(
                "Do NOT include listingIds (UUIDs) in your natural-language reply. " +
                "The app UI already knows the listingId from the attached catalog picks. " +
                "Never write text like \"(listingId ...)\" or include any UUIDs in the reply.");
        }
        else
        {
            sb.AppendLine(
                "There is NO embedded catalog list in this thread. If the user asks what to buy without listing data here, " +
                "do NOT invent species as store inventory. Summarize criteria from their profile and point them to Shop or Room corner for more picks.");
        }

        sb.AppendLine(
            "In greetings, address the user naturally (e.g. \"you\" or a short first name). Avoid awkwardly repeating a long legal-style display name in full.");
        sb.AppendLine(
            "CRITICAL: Do NOT invent personal facts (name, pets, kids, location, budgets, preferences). " +
            "Only mention a specific personal detail if it is explicitly present in the \"User profile\" block below or the user said it in the conversation. " +
            "If a field is \"not set\", treat it as unknown and ask a short clarifying question instead of guessing.");
        sb.AppendLine();

        if (includeUserProfileContext)
        {
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
        }

        if (includeGardenListContext)
        {
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
