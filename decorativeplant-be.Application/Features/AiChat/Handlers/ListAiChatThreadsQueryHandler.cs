using decorativeplant_be.Application.Common.DTOs.AiChat;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.AiChat.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Application.Features.AiChat.Handlers;

public sealed class ListAiChatThreadsQueryHandler : IRequestHandler<ListAiChatThreadsQuery, AiChatThreadListDto>
{
    private readonly IApplicationDbContext _db;

    public ListAiChatThreadsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AiChatThreadListDto> Handle(ListAiChatThreadsQuery request, CancellationToken cancellationToken)
    {
        var limit = request.Limit is <= 0 ? 50 : Math.Min(request.Limit, 50);

        var threads = await _db.AiChatThreads
            .AsNoTracking()
            .Where(t => t.UserId == request.UserId)
            .OrderByDescending(t => t.UpdatedAt)
            .Take(limit)
            .Select(t => new AiChatThreadListItemDto
            {
                Id = t.Id,
                Title = t.Title ?? "New chat",
                UpdatedAt = t.UpdatedAt
            })
            .ToListAsync(cancellationToken);

        return new AiChatThreadListDto { Threads = threads };
    }
}

