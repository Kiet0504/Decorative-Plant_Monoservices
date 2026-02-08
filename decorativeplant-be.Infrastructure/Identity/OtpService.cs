using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using decorativeplant_be.Application.Services;

namespace decorativeplant_be.Infrastructure.Identity;

public class OtpService : IOtpService
{
    public const string PurposeRegistration = "Registration";
    public const string PurposePasswordReset = "PasswordReset";

    private const string KeyPrefix = "DecorativePlant:otp:";
    private const int OtpLength = 6;

    private readonly IDistributedCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OtpService> _logger;
    private static readonly Random NumberGenerator = Random.Shared;

    public OtpService(
        IDistributedCache cache,
        IConfiguration configuration,
        ILogger<OtpService> logger)
    {
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> CreateOtpAsync(string email, string purpose, int expiresInMinutes, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var code = GenerateNumericCode(OtpLength);
        var codeHash = HashCode(normalizedEmail, code, purpose);

        var key = GetCacheKey(normalizedEmail, purpose);
        await _cache.RemoveAsync(key, cancellationToken);

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(expiresInMinutes)
        };
        await _cache.SetStringAsync(key, codeHash, options, cancellationToken);

        _logger.LogInformation("OTP created for {Email}, purpose {Purpose}", normalizedEmail, purpose);
        return code;
    }

    public async Task<bool> ValidateAndConsumeOtpAsync(string email, string code, string purpose, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var codeHash = HashCode(normalizedEmail, code.Trim(), purpose);
        var key = GetCacheKey(normalizedEmail, purpose);

        var stored = await _cache.GetStringAsync(key, cancellationToken);
        if (string.IsNullOrEmpty(stored) || stored != codeHash)
            return false;

        await _cache.RemoveAsync(key, cancellationToken);
        return true;
    }

    private static string GetCacheKey(string normalizedEmail, string purpose)
        => $"{KeyPrefix}{purpose}:{normalizedEmail}";

    private static string GenerateNumericCode(int length)
    {
        var sb = new StringBuilder(length);
        for (var i = 0; i < length; i++)
            sb.Append(NumberGenerator.Next(0, 10));
        return sb.ToString();
    }

    private string HashCode(string email, string code, string purpose)
    {
        var secret = _configuration["OtpSettings:Secret"] ?? _configuration["JwtSettings:SecretKey"] ?? "default-otp-secret-change-in-production";
        var payload = $"{email}|{purpose}|{code}|{secret}";
        var bytes = Encoding.UTF8.GetBytes(payload);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}
