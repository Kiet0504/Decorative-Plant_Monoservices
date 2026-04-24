using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.AiChat.Commands;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Application.Features.AiChat.Handlers;

public sealed class DeleteAiChatThreadCommandHandler : IRequestHandler<DeleteAiChatThreadCommand, bool>
{
    private readonly IApplicationDbContext _db;

    public DeleteAiChatThreadCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<bool> Handle(DeleteAiChatThreadCommand request, CancellationToken cancellationToken)
    {
        var thread = await _db.AiChatThreads
            .FirstOrDefaultAsync(t => t.Id == request.ThreadId && t.UserId == request.UserId, cancellationToken);
        if (thread == null)
        {
            return false;
        }

        _db.AiChatThreads.Remove(thread);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }
}

