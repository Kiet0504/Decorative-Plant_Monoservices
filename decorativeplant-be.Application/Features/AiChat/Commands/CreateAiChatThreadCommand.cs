using decorativeplant_be.Application.Common.DTOs.AiChat;
using MediatR;

namespace decorativeplant_be.Application.Features.AiChat.Commands;

public sealed class CreateAiChatThreadCommand : IRequest<AiChatCreateThreadResultDto>
{
    public Guid UserId { get; set; }
    public string? Title { get; set; }
}

