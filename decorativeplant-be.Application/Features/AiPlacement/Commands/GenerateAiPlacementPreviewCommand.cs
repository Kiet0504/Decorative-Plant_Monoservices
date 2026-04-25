using decorativeplant_be.Application.Common.DTOs.AiPlacement;
using MediatR;

namespace decorativeplant_be.Application.Features.AiPlacement.Commands;

public sealed class GenerateAiPlacementPreviewCommand : IRequest<AiPlacementPreviewResultDto>
{
    public Guid UserId { get; set; }
    public AiPlacementPreviewRequestDto Request { get; set; } = new();
}

