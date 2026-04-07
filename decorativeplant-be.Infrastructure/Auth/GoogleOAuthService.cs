using System.Net.Http.Json;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;

namespace decorativeplant_be.Infrastructure.Auth;

public record GoogleTokenResult(string Email, string? Name, string? Picture);

public sealed class GoogleOAuthService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GoogleOAuthSettings _google;

    public GoogleOAuthService(IHttpClientFactory httpClientFactory, IOptions<GoogleOAuthSettings> google)
    {
        _httpClientFactory = httpClientFactory;
        _google = google.Value;
    }

    public string BuildAuthorizeUrl(string redirectUri, string state)
    {
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _google.ClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = "openid email profile",
            ["state"] = state,
            ["access_type"] = "offline",
            ["prompt"] = "consent"
        };

        var q = string.Join("&",
            query.Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));

        return $"https://accounts.google.com/o/oauth2/v2/auth?{q}";
    }

    public async Task<GoogleTokenResult> ExchangeCodeAndVerifyAsync(string code, string redirectUri, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string?>
            {
                ["client_id"] = _google.ClientId,
                ["client_secret"] = _google.ClientSecret,
                ["code"] = code,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = redirectUri
            }!)
        };

        using var resp = await client.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        var token = await resp.Content.ReadFromJsonAsync<TokenResponseDto>(cancellationToken: ct);
        if (token == null || string.IsNullOrWhiteSpace(token.id_token))
        {
            throw new InvalidOperationException("Google token response is missing id_token.");
        }

        var payload = await GoogleJsonWebSignature.ValidateAsync(
            token.id_token,
            new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _google.ClientId }
            });

        if (string.IsNullOrWhiteSpace(payload.Email))
        {
            throw new InvalidOperationException("Google id_token does not contain an email.");
        }

        return new GoogleTokenResult(payload.Email, payload.Name, payload.Picture);
    }

    private sealed class TokenResponseDto
    {
        public string? id_token { get; set; }
    }
}

