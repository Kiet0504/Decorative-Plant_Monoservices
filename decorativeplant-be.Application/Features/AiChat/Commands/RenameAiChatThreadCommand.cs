using decorativeplant_be.Application.Common.DTOs.AiChat;
using MediatR;

namespace decorativeplant_be.Application.Features.AiChat.Commands;

public sealed class RenameAiChatThreadCommand : IRequest<AiChatThreadListItemDto>
{
    public Guid UserId { get; set; }
    public Guid ThreadId { get; set; }
    public string Title { get; set; } = string.Empty;
}

