using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using StackExchange.Redis;
using decorativeplant_be.Application.Services;

namespace decorativeplant_be.Infrastructure.Cache;

public class RedisRefreshTokenService : IRefreshTokenService
{
    private const string InstanceName = "DecorativePlant:";
    private const string TokenKeyPrefix = InstanceName + "refresh_token:";
    private const string TokenLookupPrefix = InstanceName + "token_lookup:";

    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisRefreshTokenService> _logger;
    private readonly IConnectionMultiplexer? _redis;

    public RedisRefreshTokenService(
        IDistributedCache cache,
        ILogger<RedisRefreshTokenService> logger,
        IConnectionMultiplexer? redis = null)
    {
        _cache = cache;
        _logger = logger;
        _redis = redis;
    }

    public async Task StoreRefreshTokenAsync(string userId, string refreshToken, TimeSpan expiration)
    {
        try
        {
            var tokenHash = GetTokenHash(refreshToken);
            var key = $"refresh_token:{userId}:{tokenHash}";
            var lookupKey = $"token_lookup:{tokenHash}";
            
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
            var key = $"refresh_token:{userId}:{tokenHash}";
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
            var lookupKey = $"token_lookup:{tokenHash}";
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
            var key = $"refresh_token:{userId}:{tokenHash}";
            var lookupKey = $"token_lookup:{tokenHash}";
            
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
        if (_redis == null)
        {
            _logger.LogWarning("RevokeAllUserTokensAsync: Redis not configured; tokens will expire naturally.");
            return;
        }

        try
        {
            var db = _redis.GetDatabase();
            var pattern = $"{TokenKeyPrefix}{userId}:*";
            var revoked = 0;

            foreach (var endpoint in _redis.GetEndPoints())
            {
                var server = _redis.GetServer(endpoint);
                foreach (var key in server.Keys(pattern: pattern))
                {
                    var keyStr = key.ToString();
                    if (keyStr.StartsWith(TokenKeyPrefix, StringComparison.Ordinal))
                    {
                        var parts = keyStr.Split(':');
                        if (parts.Length >= 4)
                        {
                            var tokenHash = parts[^1];
                            var lookupKey = $"{TokenLookupPrefix}{tokenHash}";
                            await db.KeyDeleteAsync(key);
                            await db.KeyDeleteAsync(lookupKey);
                            revoked++;
                        }
                    }
                }
            }

            _logger.LogInformation("Revoked {Count} refresh token(s) for user {UserId}", revoked, userId);
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
