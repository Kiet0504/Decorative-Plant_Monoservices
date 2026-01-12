using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using decorativeplant_be.Application.Services;

namespace decorativeplant_be.Infrastructure.Cache;

public class RedisRefreshTokenService : IRefreshTokenService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisRefreshTokenService> _logger;
    private const string TokenKeyPrefix = "refresh_token:";
    private const string TokenLookupPrefix = "token_lookup:";

    public RedisRefreshTokenService(
        IDistributedCache cache,
        ILogger<RedisRefreshTokenService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task StoreRefreshTokenAsync(string userId, string refreshToken, TimeSpan expiration)
    {
        try
        {
            var tokenHash = GetTokenHash(refreshToken);
            var key = $"{TokenKeyPrefix}{userId}:{tokenHash}";
            var lookupKey = $"{TokenLookupPrefix}{tokenHash}";
            
            var tokenData = new RefreshTokenData
            {
                UserId = userId,
                Token = refreshToken,
                CreatedAt = DateTime.UtcNow
            };

            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            };

            var json = JsonSerializer.Serialize(tokenData);
            
            // Store token with userId key
            await _cache.SetStringAsync(key, json, options);
            
            // Store lookup mapping (token hash -> userId) for quick lookup
            await _cache.SetStringAsync(lookupKey, userId, options);
            
            _logger.LogDebug("Stored refresh token for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing refresh token for user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> ValidateRefreshTokenAsync(string userId, string refreshToken)
    {
        try
        {
            var tokenHash = GetTokenHash(refreshToken);
            var key = $"{TokenKeyPrefix}{userId}:{tokenHash}";
            var cachedToken = await _cache.GetStringAsync(key);
            
            if (string.IsNullOrEmpty(cachedToken))
            {
                _logger.LogWarning("Refresh token not found for user {UserId}", userId);
                return false;
            }

            var tokenData = JsonSerializer.Deserialize<RefreshTokenData>(cachedToken);
            if (tokenData == null || tokenData.Token != refreshToken)
            {
                _logger.LogWarning("Invalid refresh token data for user {UserId}", userId);
                return false;
            }

            _logger.LogDebug("Validated refresh token for user {UserId}", userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating refresh token for user {UserId}", userId);
            return false;
        }
    }

    public async Task<string?> GetUserIdFromTokenAsync(string refreshToken)
    {
        try
        {
            var tokenHash = GetTokenHash(refreshToken);
            var lookupKey = $"{TokenLookupPrefix}{tokenHash}";
            var userId = await _cache.GetStringAsync(lookupKey);
            return userId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting userId from refresh token");
            return null;
        }
    }

    public async Task RevokeRefreshTokenAsync(string userId, string refreshToken)
    {
        try
        {
            var tokenHash = GetTokenHash(refreshToken);
            var key = $"{TokenKeyPrefix}{userId}:{tokenHash}";
            var lookupKey = $"{TokenLookupPrefix}{tokenHash}";
            
            await _cache.RemoveAsync(key);
            await _cache.RemoveAsync(lookupKey);
            
            _logger.LogInformation("Revoked refresh token for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking refresh token for user {UserId}", userId);
            throw;
        }
    }

    public async Task RevokeAllUserTokensAsync(string userId)
    {
        try
        {
            // Note: This is a simplified implementation
            // In production, you might want to maintain a set of all tokens per user
            // For now, we'll log that all tokens should be considered revoked
            // The tokens will expire naturally based on their TTL
            _logger.LogInformation("All refresh tokens for user {UserId} will expire naturally", userId);
            
            // If you need immediate revocation of all tokens, consider maintaining
            // a list of active tokens per user in Redis
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking all tokens for user {UserId}", userId);
            throw;
        }
    }

    private static string GetTokenHash(string token)
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(hashBytes);
    }

    private class RefreshTokenData
    {
        public string UserId { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
