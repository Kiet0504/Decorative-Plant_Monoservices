using decorativeplant_be.Application.Common.DTOs.AiChat;
using MediatR;

namespace decorativeplant_be.Application.Features.AiChat.Commands;

public sealed class SendAiChatMessageV2Command : IRequest<AiChatSendMessageResultDto>
{
    public Guid UserId { get; set; }
    public AiChatSendMessageRequestDto Request { get; set; } = new();
}

