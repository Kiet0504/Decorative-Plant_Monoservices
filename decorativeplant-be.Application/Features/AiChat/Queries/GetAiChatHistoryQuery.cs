using decorativeplant_be.Application.Common.DTOs.AiChat;
using MediatR;

namespace decorativeplant_be.Application.Features.AiChat.Queries;

public sealed class GetAiChatHistoryQuery : IRequest<AiChatHistoryDto>
{
    public Guid UserId { get; set; }
    public Guid? ThreadId { get; set; }
    public int Limit { get; set; } = 200;
}

