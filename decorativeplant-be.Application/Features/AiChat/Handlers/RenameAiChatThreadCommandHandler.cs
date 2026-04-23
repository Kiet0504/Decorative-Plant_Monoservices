using decorativeplant_be.Application.Common.DTOs.AiChat;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.AiChat.Commands;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Application.Features.AiChat.Handlers;

public sealed class RenameAiChatThreadCommandHandler : IRequestHandler<RenameAiChatThreadCommand, AiChatThreadListItemDto>
{
    private readonly IApplicationDbContext _db;

    public RenameAiChatThreadCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AiChatThreadListItemDto> Handle(RenameAiChatThreadCommand request, CancellationToken cancellationToken)
    {
        var title = (request.Title ?? string.Empty).Trim();
        if (title.Length == 0) title = "New chat";
        if (title.Length > 120) title = title[..120];

        var thread = await _db.AiChatThreads
            .FirstOrDefaultAsync(t => t.Id == request.ThreadId && t.UserId == request.UserId, cancellationToken);
        if (thread == null)
        {
            // Return a minimal item (controller can decide status code; keep handler simple).
            return new AiChatThreadListItemDto { Id = request.ThreadId, Title = title, UpdatedAt = DateTime.UtcNow };
        }

        thread.Title = title;
        thread.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return new AiChatThreadListItemDto
        {
            Id = thread.Id,
            Title = thread.Title ?? "New chat",
            UpdatedAt = thread.UpdatedAt
        };
    }
}

