using decorativeplant_be.Application.Common.DTOs.AiChat;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.AiChat.Commands;
using decorativeplant_be.Domain.Entities;
using MediatR;

namespace decorativeplant_be.Application.Features.AiChat.Handlers;

public sealed class CreateAiChatThreadCommandHandler : IRequestHandler<CreateAiChatThreadCommand, AiChatCreateThreadResultDto>
{
    private readonly IApplicationDbContext _db;

    public CreateAiChatThreadCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AiChatCreateThreadResultDto> Handle(CreateAiChatThreadCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var title = string.IsNullOrWhiteSpace(request.Title) ? "New chat" : request.Title.Trim();
        if (title.Length > 120) title = title[..120];

        var thread = new AiChatThread
        {
            UserId = request.UserId,
            Title = title,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.AiChatThreads.Add(thread);
        await _db.SaveChangesAsync(cancellationToken);

        return new AiChatCreateThreadResultDto
        {
            Thread = new AiChatThreadListItemDto
            {
                Id = thread.Id,
                Title = thread.Title ?? "New chat",
                UpdatedAt = thread.UpdatedAt
            }
        };
    }
}

