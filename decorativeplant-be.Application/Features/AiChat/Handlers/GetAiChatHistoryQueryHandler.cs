using System.Text.Json;
using decorativeplant_be.Application.Common.DTOs.AiChat;
using decorativeplant_be.Application.Common.DTOs.RoomScan;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.AiChat.Queries;
using decorativeplant_be.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Application.Features.AiChat.Handlers;

public sealed class GetAiChatHistoryQueryHandler : IRequestHandler<GetAiChatHistoryQuery, AiChatHistoryDto>
{
    private readonly IApplicationDbContext _db;

    public GetAiChatHistoryQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AiChatHistoryDto> Handle(GetAiChatHistoryQuery request, CancellationToken cancellationToken)
    {
        var limit = request.Limit is <= 0 ? 200 : Math.Min(request.Limit, 200);

        AiChatThread? thread;
        if (request.ThreadId.HasValue)
        {
            thread = await _db.AiChatThreads
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    t => t.Id == request.ThreadId.Value && t.UserId == request.UserId,
                    cancellationToken);
        }
        else
        {
            thread = await _db.AiChatThreads
                .AsNoTracking()
                .Where(t => t.UserId == request.UserId)
                .OrderByDescending(t => t.UpdatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (thread == null)
        {
            return new AiChatHistoryDto { ThreadId = null, Messages = new List<AiChatHistoryMessageDto>() };
        }

        var messages = await _db.AiChatMessages
            .AsNoTracking()
            .Where(m => m.ThreadId == thread.Id)
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        return new AiChatHistoryDto
        {
            ThreadId = thread.Id,
            Messages = messages.Select(Map).ToList()
        };
    }

    private static AiChatHistoryMessageDto Map(AiChatMessage m)
    {
        var meta = Deserialize<AiChatMessageMetadataDto>(m.MetadataJson);
        return new AiChatHistoryMessageDto
        {
            Id = m.Id,
            Role = m.Role,
            Content = m.Content,
            CreatedAt = m.CreatedAt,
            AttachmentUrl = m.AttachmentUrl,
            AttachmentMimeType = m.AttachmentMimeType,
            Diagnosis = Deserialize<AiChatDiagnosisSummaryDto>(m.DiagnosisJson),
            DiagnosisGardenPlantId = meta?.DiagnosisGardenPlantId,
            NewRecommendations = Deserialize<List<RoomScanRecommendationDto>>(m.RecommendationsJson),
            UiSuggestions = meta?.UiSuggestions
        };
    }

    private sealed class AiChatMessageMetadataDto
    {
        public AiChatUiSuggestionsDto? UiSuggestions { get; set; }
        public Guid? DiagnosisGardenPlantId { get; set; }
    }

    private static T? Deserialize<T>(JsonDocument? doc)
    {
        if (doc == null) return default;
        try
        {
            return JsonSerializer.Deserialize<T>(doc.RootElement.GetRawText());
        }
        catch
        {
            return default;
        }
    }
}

