using System.Text.Json;
using decorativeplant_be.Application.Common.DTOs.AiChat;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.AiChat.Commands;
using decorativeplant_be.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Application.Features.AiChat.Handlers;

public sealed class SendAiChatMessageV2CommandHandler : IRequestHandler<SendAiChatMessageV2Command, AiChatSendMessageResultDto>
{
    private const int MaxMessages = 200;
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(30);

    private readonly IApplicationDbContext _db;
    private readonly IMediaStorageService _media;
    private readonly IMediator _mediator;

    public SendAiChatMessageV2CommandHandler(IApplicationDbContext db, IMediaStorageService media, IMediator mediator)
    {
        _db = db;
        _media = media;
        _mediator = mediator;
    }

    public async Task<AiChatSendMessageResultDto> Handle(SendAiChatMessageV2Command request, CancellationToken cancellationToken)
    {
        var text = (request.Request.Content ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(text))
        {
            text = "…";
        }

        var now = DateTime.UtcNow;

        AiChatThread? thread = null;
        if (request.Request.ThreadId.HasValue)
        {
            thread = await _db.AiChatThreads
                .FirstOrDefaultAsync(
                    t => t.Id == request.Request.ThreadId.Value && t.UserId == request.UserId,
                    cancellationToken);
        }

        if (thread == null)
        {
            thread = new AiChatThread
            {
                UserId = request.UserId,
                Title = "New chat",
                CreatedAt = now,
                UpdatedAt = now
            };
            _db.AiChatThreads.Add(thread);
            await _db.SaveChangesAsync(cancellationToken);
        }

        string? attachmentUrl = null;
        var normalizedImage = NormalizeAttachedImageBase64(request.Request.AttachedImageBase64);
        if (!string.IsNullOrEmpty(normalizedImage))
        {
            var bytes = Convert.FromBase64String(normalizedImage);
            await using var stream = new MemoryStream(bytes);
            var mime = NormalizeImageMimeType(request.Request.AttachedImageMimeType);
            var ext = mime == "image/png" ? ".png" : mime == "image/webp" ? ".webp" : ".jpg";
            attachmentUrl = await _media.UploadImageAsync(stream, mime, ext, "ai-chat", cancellationToken);
        }

        var savedUser = new AiChatMessage
        {
            ThreadId = thread.Id,
            Role = "user",
            Content = text,
            CreatedAt = now,
            AttachmentUrl = attachmentUrl,
            AttachmentMimeType = NormalizeImageMimeType(request.Request.AttachedImageMimeType)
        };
        _db.AiChatMessages.Add(savedUser);
        thread.UpdatedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        // Build LLM context from persisted turns (cap to recent slice for prompt size).
        var history = await _db.AiChatMessages
            .AsNoTracking()
            .Where(m => m.ThreadId == thread.Id)
            .OrderByDescending(m => m.CreatedAt)
            .Take(80)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new AiChatMessageDto { Role = m.Role, Content = m.Content })
            .ToListAsync(cancellationToken);

        var reply = await _mediator.Send(new SendAiChatMessageCommand
        {
            UserId = request.UserId,
            Messages = history,
            IncludeUserProfileContext = request.Request.IncludeUserProfileContext,
            IncludeGardenListContext = request.Request.IncludeGardenListContext,
            GardenPlantId = request.Request.GardenPlantId,
            AttachedImageBase64 = request.Request.AttachedImageBase64,
            AttachedImageMimeType = request.Request.AttachedImageMimeType,
            RoomScanFollowUp = request.Request.RoomScanFollowUp,
            ArSessionId = request.Request.ArSessionId,
            ProductListingId = request.Request.ProductListingId,
            PlacementContextJson = request.Request.PlacementContextJson,
            UtcOffsetMinutes = request.Request.UtcOffsetMinutes
        }, cancellationToken);

        var assistantNow = DateTime.UtcNow;
        var savedAssistant = new AiChatMessage
        {
            ThreadId = thread.Id,
            Role = "assistant",
            Content = reply.Reply ?? string.Empty,
            CreatedAt = assistantNow,
            DiagnosisJson = SerializeToDocument(reply.Diagnosis),
            RecommendationsJson = SerializeToDocument(reply.NewRecommendations),
            MetadataJson = SerializeToDocument(new
            {
                reply.ResolvedIntent,
                reply.SuggestedIntent,
                reply.ContentBlocked,
                reply.OutOfScope,
                reply.UiSuggestions,
                DiagnosisGardenPlantId = reply.Diagnosis != null ? request.Request.GardenPlantId : null
            })
        };
        _db.AiChatMessages.Add(savedAssistant);
        thread.UpdatedAt = assistantNow;
        if (string.IsNullOrWhiteSpace(thread.Title) || string.Equals(thread.Title, "New chat", StringComparison.OrdinalIgnoreCase))
        {
            var title = text.Trim();
            if (title.Length > 60) title = title[..60];
            thread.Title = title.Length == 0 ? "New chat" : title;
        }
        await _db.SaveChangesAsync(cancellationToken);

        await ApplyRetentionAsync(thread.Id, cancellationToken);

        return new AiChatSendMessageResultDto
        {
            Reply = reply,
            SavedUserMessage = MapSaved(savedUser),
            SavedAssistantMessage = MapSaved(savedAssistant, reply, request.Request.GardenPlantId)
        };
    }

    private async Task ApplyRetentionAsync(Guid threadId, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.Subtract(MaxAge);

        var old = await _db.AiChatMessages
            .Where(m => m.ThreadId == threadId && m.CreatedAt < cutoff)
            .ToListAsync(cancellationToken);

        if (old.Count > 0)
        {
            _db.AiChatMessages.RemoveRange(old);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var count = await _db.AiChatMessages
            .Where(m => m.ThreadId == threadId)
            .CountAsync(cancellationToken);

        if (count <= MaxMessages) return;

        var toDelete = await _db.AiChatMessages
            .Where(m => m.ThreadId == threadId)
            .OrderBy(m => m.CreatedAt)
            .Take(count - MaxMessages)
            .ToListAsync(cancellationToken);

        if (toDelete.Count > 0)
        {
            _db.AiChatMessages.RemoveRange(toDelete);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    private static AiChatHistoryMessageDto MapSaved(AiChatMessage m, AiChatReplyDto? reply = null, Guid? gardenPlantIdForTurn = null)
    {
        Guid? diagnosisGardenPlantId = null;
        if (reply?.Diagnosis != null && gardenPlantIdForTurn.HasValue)
        {
            diagnosisGardenPlantId = gardenPlantIdForTurn;
        }

        return new AiChatHistoryMessageDto
        {
            Id = m.Id,
            Role = m.Role,
            Content = m.Content,
            CreatedAt = m.CreatedAt,
            AttachmentUrl = m.AttachmentUrl,
            AttachmentMimeType = m.AttachmentMimeType,
            Diagnosis = reply?.Diagnosis,
            DiagnosisGardenPlantId = diagnosisGardenPlantId,
            NewRecommendations = reply?.NewRecommendations
        };
    }

    private static JsonDocument? SerializeToDocument<T>(T? value)
    {
        if (value == null) return null;
        try
        {
            return JsonDocument.Parse(JsonSerializer.Serialize(value));
        }
        catch
        {
            return null;
        }
    }

    private static string? NormalizeAttachedImageBase64(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
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
        if (string.IsNullOrWhiteSpace(mime)) return "image/jpeg";
        var m = mime.Trim().ToLowerInvariant();
        return m is "image/jpeg" or "image/png" or "image/webp" ? m : "image/jpeg";
    }
}

