using System.Net.Http.Json;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;

namespace decorativeplant_be.Infrastructure.Auth;

public record GoogleTokenResult(string Email, string? Name, string? Picture);
public record GoogleOAuthTokenResult(
    string AccessToken,
    string? RefreshToken,
    int ExpiresInSeconds,
    string? Scope,
    string? IdToken);

public sealed class GoogleOAuthService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GoogleOAuthSettings _google;

    public GoogleOAuthService(IHttpClientFactory httpClientFactory, IOptions<GoogleOAuthSettings> google)
    {
        _httpClientFactory = httpClientFactory;
        _google = google.Value;
    }

    public string BuildAuthorizeUrl(string redirectUri, string state, string? scope = null)
    {
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _google.ClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = string.IsNullOrWhiteSpace(scope) ? "openid email profile" : scope,
            ["state"] = state,
            ["access_type"] = "offline",
            ["prompt"] = "consent",
            ["include_granted_scopes"] = "true"
        };

        var q = string.Join("&",
            query.Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));

        return $"https://accounts.google.com/o/oauth2/v2/auth?{q}";
    }

    public async Task<GoogleTokenResult> ExchangeCodeAndVerifyAsync(string code, string redirectUri, CancellationToken ct)
    {
        var token = await ExchangeCodeForTokensAsync(code, redirectUri, ct);
        if (token == null || string.IsNullOrWhiteSpace(token.id_token))
            throw new InvalidOperationException("Google token response is missing id_token.");

        var payload = await GoogleJsonWebSignature.ValidateAsync(
            token.id_token,
            new GoogleJsonWebSignature.ValidationSettings { Audience = new[] { _google.ClientId } });

        if (string.IsNullOrWhiteSpace(payload.Email))
            throw new InvalidOperationException("Google id_token does not contain an email.");

        return new GoogleTokenResult(payload.Email, payload.Name, payload.Picture);
    }

    public async Task<GoogleOAuthTokenResult> ExchangeCodeForOAuthTokenAsync(string code, string redirectUri, CancellationToken ct)
    {
        var token = await ExchangeCodeForTokensAsync(code, redirectUri, ct);
        if (token == null || string.IsNullOrWhiteSpace(token.access_token))
            throw new InvalidOperationException("Google token response is missing access_token.");

        return new GoogleOAuthTokenResult(
            AccessToken: token.access_token!,
            RefreshToken: token.refresh_token,
            ExpiresInSeconds: token.expires_in ?? 3600,
            Scope: token.scope,
            IdToken: token.id_token);
    }

    public async Task<GoogleOAuthTokenResult> RefreshAccessTokenAsync(string refreshToken, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string?>
            {
                ["client_id"] = _google.ClientId,
                ["client_secret"] = _google.ClientSecret,
                ["refresh_token"] = refreshToken,
                ["grant_type"] = "refresh_token"
            }!)
        };

        using var resp = await client.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        var token = await resp.Content.ReadFromJsonAsync<TokenResponseDto>(cancellationToken: ct);
        if (token == null || string.IsNullOrWhiteSpace(token.access_token))
            throw new InvalidOperationException("Google refresh response is missing access_token.");

        return new GoogleOAuthTokenResult(
            AccessToken: token.access_token!,
            RefreshToken: string.IsNullOrWhiteSpace(token.refresh_token) ? refreshToken : token.refresh_token,
            ExpiresInSeconds: token.expires_in ?? 3600,
            Scope: token.scope,
            IdToken: token.id_token);
    }

    public async Task<GoogleTokenResult> VerifyIdTokenAsync(string idToken, CancellationToken ct = default)
    {
        var payload = await GoogleJsonWebSignature.ValidateAsync(
            idToken,
            new GoogleJsonWebSignature.ValidationSettings { Audience = new[] { _google.ClientId } });

        if (string.IsNullOrWhiteSpace(payload.Email))
            throw new InvalidOperationException("Google id_token does not contain an email.");

        return new GoogleTokenResult(payload.Email, payload.Name, payload.Picture);
    }

    private async Task<TokenResponseDto?> ExchangeCodeForTokensAsync(string code, string redirectUri, CancellationToken ct)
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
        return await resp.Content.ReadFromJsonAsync<TokenResponseDto>(cancellationToken: ct);
    }

    private sealed class TokenResponseDto
    {
        public string? id_token { get; set; }
        public string? access_token { get; set; }
        public string? refresh_token { get; set; }
        public int? expires_in { get; set; }
        public string? scope { get; set; }
    }
}

