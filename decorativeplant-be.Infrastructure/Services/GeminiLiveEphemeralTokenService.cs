using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using decorativeplant_be.Application.Common.DTOs.AiLive;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Common.Settings;
using decorativeplant_be.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace decorativeplant_be.Infrastructure.Services;

public sealed class GeminiLiveEphemeralTokenService : IGeminiLiveEphemeralTokenService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _http;
    private readonly IApplicationDbContext _db;
    private readonly AiDiagnosisSettings _gemini;
    private readonly AiLiveSettings _live;
    private readonly ILogger<GeminiLiveEphemeralTokenService> _logger;

    public GeminiLiveEphemeralTokenService(
        HttpClient http,
        IApplicationDbContext db,
        IOptions<AiDiagnosisSettings> gemini,
        IOptions<AiLiveSettings> live,
        ILogger<GeminiLiveEphemeralTokenService> logger)
    {
        _http = http;
        _db = db;
        _gemini = gemini.Value;
        _live = live.Value;
        _logger = logger;
    }

    public async Task<GeminiLiveTokenResponseDto> CreateTokenAsync(
        Guid userId,
        Guid arSessionId,
        Guid? productListingId,
        CancellationToken cancellationToken)
    {
        if (!_live.Enabled)
        {
            throw new ValidationException("Gemini Live is disabled (AiLive:Enabled=false).");
        }

        var apiKey = (_gemini.GeminiApiKey ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new ValidationException(
                "Gemini API key is not configured. Set AiDiagnosis:GeminiApiKey for ephemeral Live tokens.");
        }

        var session = await _db.ArPreviewSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == arSessionId, cancellationToken);

        if (session == null || session.UserId != userId)
        {
            throw new NotFoundException("AR preview session", arSessionId);
        }

        if (session.ExpiresAt < DateTime.UtcNow)
        {
            throw new ValidationException(
                "This AR preview session has expired. Open AR preview again and start a new session.");
        }

        var systemInstruction = await BuildSystemInstructionAsync(session, productListingId, cancellationToken)
            .ConfigureAwait(false);

        var baseUrl = string.IsNullOrWhiteSpace(_gemini.GeminiBaseUrl)
            ? "https://generativelanguage.googleapis.com"
            : _gemini.GeminiBaseUrl.TrimEnd('/');

        var expire = DateTime.UtcNow.AddMinutes(30);
        var newSessionExpire = DateTime.UtcNow.AddMinutes(1);

        var apiVer = string.IsNullOrWhiteSpace(_live.AuthTokensApiVersion)
            ? "v1alpha"
            : _live.AuthTokensApiVersion.Trim().TrimStart('/');

        var url = $"{baseUrl}/{apiVer}/authTokens:create?key={Uri.EscapeDataString(apiKey)}";

        var bodyCamel = new
        {
            authToken = new
            {
                expireTime = expire.ToString("o"),
                newSessionExpireTime = newSessionExpire.ToString("o"),
                uses = 1
            }
        };

        // Google REST/proto-json may accept camelCase (SDK-style) or snake_case. Try camelCase, then snake_case on 400.
        using var response1 = await _http
            .PostAsJsonAsync(url, bodyCamel, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        var raw = await response1.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var ok = response1.IsSuccessStatusCode;

        if (!ok && response1.StatusCode == HttpStatusCode.BadRequest)
        {
            var snake = new Dictionary<string, object?>
            {
                ["auth_token"] = new Dictionary<string, object?>
                {
                    ["expire_time"] = expire.ToString("o"),
                    ["new_session_expire_time"] = newSessionExpire.ToString("o"),
                    ["uses"] = 1
                }
            };
            var snakeJson = JsonSerializer.Serialize(snake);
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(snakeJson, Encoding.UTF8, "application/json")
            };
            using var response2 = await _http.SendAsync(req, cancellationToken).ConfigureAwait(false);
            var raw2 = await response2.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (response2.IsSuccessStatusCode)
            {
                raw = raw2;
                ok = true;
            }
            else
            {
                _logger.LogWarning(
                    "Gemini authTokens:create (snake_case retry) failed: {Status} {Body}",
                    (int)response2.StatusCode,
                    raw2.Length > 2000 ? raw2[..2000] + "…" : raw2);
                ThrowTokenError(response2, raw2, apiVer);
            }
        }

        if (!ok)
        {
            _logger.LogWarning(
                "Gemini authTokens:create failed ({Version}): {Status} {Body}",
                apiVer,
                (int)response1.StatusCode,
                raw.Length > 2000 ? raw[..2000] + "…" : raw);
            ThrowTokenError(response1, raw, apiVer);
        }

        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;
        var tokenName = root.TryGetProperty("name", out var n) ? n.GetString() : null;
        if (string.IsNullOrWhiteSpace(tokenName))
        {
            throw new ValidationException("Invalid token response from Gemini API (missing name).");
        }

        DateTime? exp = null;
        DateTime? nsExp = null;
        if (TryGetStringProp(root, "expireTime", "expire_time", out var ets))
        {
            _ = DateTime.TryParse(ets, out var parsed);
            exp = parsed;
        }

        if (TryGetStringProp(root, "newSessionExpireTime", "new_session_expire_time", out var nsts))
        {
            _ = DateTime.TryParse(nsts, out var parsed);
            nsExp = parsed;
        }

        var voice = string.IsNullOrWhiteSpace(_live.VoiceName)
            ? null
            : _live.VoiceName.Trim();

        return new GeminiLiveTokenResponseDto
        {
            EphemeralAccessToken = tokenName,
            ExpireTimeUtc = exp,
            NewSessionExpireTimeUtc = nsExp,
            LiveModel = string.IsNullOrWhiteSpace(_live.LiveModel)
                ? "gemini-2.0-flash-live-001"
                : _live.LiveModel.Trim(),
            VoiceName = voice,
            SystemInstruction = systemInstruction
        };
    }

    private async Task<string> BuildSystemInstructionAsync(
        ArPreviewSession session,
        Guid? productListingId,
        CancellationToken cancellationToken)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("You are the in-app plant and decor assistant for Decorative Plant.");
        sb.AppendLine(
            "The user is in an AR preview: they see a virtual plant placed on a real surface. " +
            "Give concise spoken answers about placement, size, lighting, and style. " +
            "Do not default to disease diagnosis unless they ask about pests or damage.");
        sb.AppendLine();
        sb.AppendLine("--- Shop listing & AR context ---");

        var raw = session.ScanJson?.RootElement.GetRawText() ?? "{}";
        if (raw.Length > 12000)
        {
            raw = raw[..12000] + "…";
        }

        sb.AppendLine("Stored scan + placement JSON from the mobile AR flow:");
        sb.AppendLine(raw);

        if (productListingId.HasValue)
        {
            var pl = await _db.ProductListings.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == productListingId.Value, cancellationToken)
                .ConfigureAwait(false);
            if (pl != null)
            {
                var title = pl.ProductInfo?.RootElement.TryGetProperty("title", out var t) == true
                    ? t.GetString()
                    : null;
                sb.AppendLine(
                    $"Product listing in focus: listingId={pl.Id}, title=\"{title ?? "unknown"}\".");
            }
        }

        return sb.ToString();
    }

    private static bool TryGetStringProp(JsonElement root, string camel, string snake, out string? value)
    {
        if (root.TryGetProperty(camel, out var c))
        {
            value = c.GetString();
            return true;
        }

        if (root.TryGetProperty(snake, out var s))
        {
            value = s.GetString();
            return true;
        }

        value = null;
        return false;
    }

    private static void ThrowTokenError(HttpResponseMessage response, string raw, string apiVer)
    {
        var detail = TryExtractGoogleErrorMessage(raw);
        var status = (int)response.StatusCode;
        var reason = response.ReasonPhrase ?? string.Empty;
        string hint;
        if (detail != null)
        {
            hint = $"HTTP {status} ({apiVer}). Google: {TruncateForClient(detail, 900)}";
        }
        else if (!string.IsNullOrWhiteSpace(raw))
        {
            hint = $"HTTP {status} ({apiVer}). Response: {TruncateForClient(raw.Trim(), 700)}";
        }
        else
        {
            hint =
                $"HTTP {status} {reason} ({apiVer}). Empty body — confirm AiDiagnosis:GeminiApiKey, server outbound HTTPS to generativelanguage.googleapis.com, and API restrictions on the key.";
        }

        throw new ValidationException($"Could not create a Live session token. {hint}");
    }

    private static string? TryExtractGoogleErrorMessage(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.TryGetProperty("error", out var err) && err.ValueKind == JsonValueKind.Object)
            {
                if (err.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                {
                    return m.GetString();
                }

                if (err.TryGetProperty("status", out var st) && st.ValueKind == JsonValueKind.String)
                {
                    return st.GetString();
                }

                if (err.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in details.EnumerateArray())
                    {
                        if (item.TryGetProperty("message", out var dm))
                        {
                            return dm.GetString();
                        }
                    }
                }

                if (err.TryGetProperty("code", out var code))
                {
                    return code.ValueKind == JsonValueKind.Number
                        ? code.GetRawText()
                        : code.GetString();
                }
            }
        }
        catch (JsonException)
        {
            return TruncateForClient(raw.Trim(), 500);
        }

        return null;
    }

    private static string TruncateForClient(string s, int maxLen)
    {
        if (s.Length <= maxLen)
        {
            return s;
        }

        return new StringBuilder(maxLen + 1).Append(s, 0, maxLen).Append('…').ToString();
    }
}
