using decorativeplant_be.Application.Common.DTOs.AiPlacement;

namespace decorativeplant_be.Application.Common.Interfaces;

public interface IAiPlacementSuggestionClient
{
    Task<AiPlacementSuggestResultDto> SuggestAsync(
        AiPlacementSuggestRequestDto request,
        CancellationToken cancellationToken = default);
}

