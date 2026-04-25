using decorativeplant_be.Application.Common.DTOs.AiPlacement;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.AiPlacement.Commands;
using MediatR;

namespace decorativeplant_be.Application.Features.AiPlacement.Handlers;

public sealed class SuggestAiPlacementCommandHandler : IRequestHandler<SuggestAiPlacementCommand, AiPlacementSuggestResultDto>
{
    private readonly IAiPlacementSuggestionClient _client;

    public SuggestAiPlacementCommandHandler(IAiPlacementSuggestionClient client)
    {
        _client = client;
    }

    public async Task<AiPlacementSuggestResultDto> Handle(SuggestAiPlacementCommand request, CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
        {
            throw new BadRequestException("User ID is required.");
        }

        if (request.Request == null)
        {
            throw new BadRequestException("Request is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Request.RoomImageBase64))
        {
            throw new BadRequestException("RoomImageBase64 is required.");
        }

        return await _client.SuggestAsync(request.Request, cancellationToken);
    }
}

