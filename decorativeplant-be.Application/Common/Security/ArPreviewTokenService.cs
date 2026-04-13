using System.Security.Cryptography;
using System.Text;

namespace decorativeplant_be.Application.Common.Security;

public interface IArPreviewTokenService
{
    string CreateSalt();
    string CreateViewerToken(Guid sessionId, long expUnixSeconds, string salt);
    bool ValidateViewerToken(Guid sessionId, string token, string salt, DateTime utcNow, DateTime expiresAtUtc);
}

public class ArPreviewTokenService : IArPreviewTokenService
{
    private readonly string _secret;

    public ArPreviewTokenService(Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _secret =
            configuration["ArPreview:ViewerTokenSecret"]
            ?? configuration["JwtSettings:SecretKey"]
            ?? string.Empty;
    }

    public string CreateSalt()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    public string CreateViewerToken(Guid sessionId, long expUnixSeconds, string salt)
    {
        if (string.IsNullOrWhiteSpace(_secret))
            throw new InvalidOperationException("Viewer token secret is not configured.");

        var data = $"{sessionId:D}.{expUnixSeconds}.{salt}";
        var sig = Sign(data);
        return $"{expUnixSeconds}.{sig}";
    }

    public bool ValidateViewerToken(Guid sessionId, string token, string salt, DateTime utcNow, DateTime expiresAtUtc)
    {
        if (string.IsNullOrWhiteSpace(_secret)) return false;
        if (string.IsNullOrWhiteSpace(token)) return false;

        var parts = token.Split('.', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2) return false;

        if (!long.TryParse(parts[0], out var expUnix)) return false;

        var expUtc = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
        if (utcNow > expUtc) return false;
        if (expUtc > expiresAtUtc) return false;

        var expected = Sign($"{sessionId:D}.{expUnix}.{salt}");
        return FixedTimeEquals(expected, parts[1]);
    }

    private string Sign(string data)
    {
        var key = Encoding.UTF8.GetBytes(_secret);
        var msg = Encoding.UTF8.GetBytes(data);
        using var h = new HMACSHA256(key);
        var hash = h.ComputeHash(msg);
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> data)
    {
        return Convert.ToBase64String(data)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        return ba.Length == bb.Length && CryptographicOperations.FixedTimeEquals(ba, bb);
    }
}

