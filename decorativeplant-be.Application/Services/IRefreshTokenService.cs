namespace decorativeplant_be.Application.Services;

public interface IRefreshTokenService
{
    Task StoreRefreshTokenAsync(string userId, string refreshToken, TimeSpan expiration);
    Task<bool> ValidateRefreshTokenAsync(string userId, string refreshToken);
    Task<string?> GetUserIdFromTokenAsync(string refreshToken);
    Task RevokeRefreshTokenAsync(string userId, string refreshToken);
    Task RevokeAllUserTokensAsync(string userId);
}
