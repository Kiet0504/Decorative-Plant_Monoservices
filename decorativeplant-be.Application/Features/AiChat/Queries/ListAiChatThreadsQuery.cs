using decorativeplant_be.Application.Common.DTOs.AiChat;
using MediatR;

namespace decorativeplant_be.Application.Features.AiChat.Queries;

public sealed class ListAiChatThreadsQuery : IRequest<AiChatThreadListDto>
{
    public Guid UserId { get; set; }
    public int Limit { get; set; } = 50;
}

