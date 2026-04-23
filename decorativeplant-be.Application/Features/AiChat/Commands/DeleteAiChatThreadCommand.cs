using MediatR;

namespace decorativeplant_be.Application.Features.AiChat.Commands;

public sealed class DeleteAiChatThreadCommand : IRequest<bool>
{
    public Guid UserId { get; set; }
    public Guid ThreadId { get; set; }
}

