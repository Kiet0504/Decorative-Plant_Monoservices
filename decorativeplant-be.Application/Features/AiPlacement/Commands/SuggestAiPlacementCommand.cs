using decorativeplant_be.Application.Common.DTOs.AiPlacement;
using MediatR;

namespace decorativeplant_be.Application.Features.AiPlacement.Commands;

public sealed class SuggestAiPlacementCommand : IRequest<AiPlacementSuggestResultDto>
{
    public Guid UserId { get; set; }
    public AiPlacementSuggestRequestDto Request { get; set; } = new();
}

