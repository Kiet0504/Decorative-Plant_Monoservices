using decorativeplant_be.Application.Common.DTOs.AiPlacement;

namespace decorativeplant_be.Application.Common.Interfaces;

public interface IAiPlacementPreviewGenerator
{
    Task<AiPlacementPreviewResultDto> GenerateAsync(
        AiPlacementPreviewRequestDto request,
        CancellationToken cancellationToken = default);
}

