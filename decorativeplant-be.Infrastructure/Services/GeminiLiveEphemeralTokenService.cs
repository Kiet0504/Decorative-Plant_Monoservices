using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
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

        var preferredVer = string.IsNullOrWhiteSpace(_live.AuthTokensApiVersion)
            ? "v1alpha"
            : _live.AuthTokensApiVersion.Trim().TrimStart('/');
        var alternateVer = string.Equals(preferredVer, "v1alpha", StringComparison.OrdinalIgnoreCase)
            ? "v1beta"
            : "v1alpha";

        // Live ephemeral tokens must pin the Live model (see google-genai CreateAuthTokenConfig + LiveEphemeralParameters).
        // Without this, auth_tokens often returns 400 INVALID_ARGUMENT for Live-only keys.
        var liveModelRaw = string.IsNullOrWhiteSpace(_live.LiveModel)
            ? "gemini-live-2.5-flash-preview"
            : _live.LiveModel.Trim();
        var modelResource = liveModelRaw.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? liveModelRaw
            : $"models/{liveModelRaw}";

        // Final wire shape on the AuthToken service (matches python-genai test_create_global_lock):
        //   { "uses": 2, "bidiGenerateContentSetup": { "model": "models/..." } }
        //
        // NOTE on omitted timestamps: python-genai's _CreateAuthTokenConfig_to_mldev only
        // sets expireTime / newSessionExpireTime when the caller passes them. Google server
        // defaults are 30 min (expireTime) and 60 s (newSessionExpireTime). Sending them
        // explicitly as RFC-3339 strings has been observed to trigger INVALID_ARGUMENT,
        // so we omit them and let the server default. tokens.py then
        // `_convert_bidi_setup_to_token_setup` UNWRAPS `setup` before posting, so the C#
        // body is already in the un-wrapped shape.
        var bodyCamel = new Dictionary<string, object?>
        {
            ["uses"] = 2,
            ["bidiGenerateContentSetup"] = new Dictionary<string, object?>
            {
                ["model"] = modelResource,
            },
        };

        // Some keys only expose auth_tokens on one API version; retry the other on HTTP 404 only.
        string raw;
        var apiVerUsed = preferredVer;
        var got = await TryPostAuthTokenAsync(
                baseUrl,
                apiKey,
                preferredVer,
                bodyCamel,
                cancellationToken)
            .ConfigureAwait(false);
        if (got.HasValue)
        {
            (raw, apiVerUsed) = got.Value;
        }
        else
        {
            _logger.LogInformation(
                "Gemini auth_tokens returned 404 for {Ver}; retrying alternate API version {Alt}",
                preferredVer,
                alternateVer);
            got = await TryPostAuthTokenAsync(
                    baseUrl,
                    apiKey,
                    alternateVer,
                    bodyCamel,
                    cancellationToken)
                .ConfigureAwait(false);
            if (got.HasValue)
            {
                (raw, apiVerUsed) = got.Value;
            }
            else
            {
                throw new ValidationException(
                    "Could not create a Live session token. Google returned HTTP 404 for POST …/auth_tokens on both " +
                    preferredVer + " and " + alternateVer +
                    ". Confirm Generative Language API is enabled and the API key is valid.");
            }
        }

        _logger.LogInformation("Gemini auth_tokens succeeded using API version {Ver}", apiVerUsed);

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
                ? "gemini-live-2.5-flash-preview"
                : _live.LiveModel.Trim(),
            VoiceName = voice,
            SystemInstruction = systemInstruction
        };
    }

    /// <summary>Returns null only when Google responds 404 (wrong API path for this key — caller may retry another API version).</summary>
    private async Task<(string Raw, string Version)?> TryPostAuthTokenAsync(
        string baseUrl,
        string apiKey,
        string apiVer,
        object bodyCamel,
        CancellationToken cancellationToken)
    {
        var url = $"{baseUrl}/{apiVer}/auth_tokens?key={Uri.EscapeDataString(apiKey)}";

        // Serialize the body up-front so we can log it verbatim on failure.
        // Google's INVALID_ARGUMENT responses don't tell us which field is bad,
        // so we need the exact wire bytes to diagnose.
        var bodyJson = JsonSerializer.Serialize(bodyCamel, JsonOptions);

        _logger.LogInformation(
            "Gemini auth_tokens request ({Version}) body: {Body}",
            apiVer,
            bodyJson);

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(bodyJson, Encoding.UTF8, "application/json"),
        };

        using var response1 = await _http
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);
        var raw = await response1.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (response1.IsSuccessStatusCode)
        {
            return (raw, apiVer);
        }

        if (response1.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning(
                "Gemini auth_tokens 404 ({Version}): {Body}",
                apiVer,
                raw.Length > 2000 ? raw[..2000] + "…" : raw);
            return null;
        }

        _logger.LogWarning(
            "Gemini auth_tokens failed ({Version}): {Status} {Body}. Request was: {RequestBody}",
            apiVer,
            (int)response1.StatusCode,
            raw.Length > 2000 ? raw[..2000] + "…" : raw,
            bodyJson);
        ThrowTokenError(response1, raw, apiVer);
        throw new UnreachableException();
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

    [DoesNotReturn]
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
