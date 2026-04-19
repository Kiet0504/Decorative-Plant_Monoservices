using decorativeplant_be.Application.Common.DTOs.AiLive;

namespace decorativeplant_be.Application.Common.Interfaces;

public interface IGeminiLiveEphemeralTokenService
{
    Task<GeminiLiveTokenResponseDto> CreateTokenAsync(
        Guid userId,
        Guid arSessionId,
        Guid? productListingId,
        CancellationToken cancellationToken);
}
