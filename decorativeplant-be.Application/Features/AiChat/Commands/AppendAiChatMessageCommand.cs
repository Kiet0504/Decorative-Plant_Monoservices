using decorativeplant_be.Application.Common.DTOs.AiChat;
using MediatR;

namespace decorativeplant_be.Application.Features.AiChat.Commands;

public sealed class AppendAiChatMessageCommand : IRequest<AiChatAppendMessageResultDto>
{
    public Guid UserId { get; set; }
    public AiChatAppendMessageRequestDto Request { get; set; } = new();
}

