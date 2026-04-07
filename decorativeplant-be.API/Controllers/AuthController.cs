using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Json;
using decorativeplant_be.Application.Common.DTOs.Auth;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Auth.Commands;
using decorativeplant_be.Application.Services;
using decorativeplant_be.Infrastructure.Auth;

namespace decorativeplant_be.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : BaseController
{
    private readonly IApplicationDbContext _context;
    private readonly IUserAccountService _userAccountService;
    private readonly IJwtService _jwtService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly IQuotaService _quotaService;
    private readonly GoogleOAuthService _googleOAuthService;
    private readonly IDistributedCache _cache;
    private readonly GoogleOAuthSettings _google;
    private readonly FrontendSettings _frontend;

    public AuthController(
        IApplicationDbContext context,
        IUserAccountService userAccountService,
        IJwtService jwtService,
        IRefreshTokenService refreshTokenService,
        ISubscriptionService subscriptionService,
        IQuotaService quotaService,
        GoogleOAuthService googleOAuthService,
        IDistributedCache cache,
        IOptions<GoogleOAuthSettings> google,
        IOptions<FrontendSettings> frontend)
    {
        _context = context;
        _userAccountService = userAccountService;
        _jwtService = jwtService;
        _refreshTokenService = refreshTokenService;
        _subscriptionService = subscriptionService;
        _quotaService = quotaService;
        _googleOAuthService = googleOAuthService;
        _cache = cache;
        _google = google.Value;
        _frontend = frontend.Value;
    }

    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<TokenResponse>>> Register([FromBody] RegisterCommand command)
    {
        var result = await Mediator.Send(command);
        if (string.IsNullOrEmpty(result.AccessToken))
        {
            return Ok(ApiResponse<TokenResponse>.SuccessResponse(result, "User registered successfully. Please check your email for verification code."));
        }
        return Ok(ApiResponse<TokenResponse>.SuccessResponse(result, "User registered successfully."));
    }

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<TokenResponse>>> Login([FromBody] LoginCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<TokenResponse>.SuccessResponse(result, "Login successful."));
    }

    [HttpPost("refresh-token")]
    public async Task<ActionResult<ApiResponse<TokenResponse>>> RefreshToken([FromBody] RefreshTokenCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<TokenResponse>.SuccessResponse(result, "Token refreshed successfully."));
    }

    [HttpPost("logout")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<ActionResult<ApiResponse<bool>>> Logout([FromBody] LogoutCommand command)
    {
        // Get userId from claims if not provided
        if (string.IsNullOrEmpty(command.UserId))
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(ApiResponse<bool>.ErrorResponse("User ID is required."));
            }
            command.UserId = userId;
        }

        var result = await Mediator.Send(command);
        return Ok(result);
    }

    /// <summary>Request a password reset. Sends an OTP to the user's email if the account exists.</summary>
    [HttpPost("forgot-password")]
    public async Task<ActionResult<ApiResponse<object>>> ForgotPassword([FromBody] RequestPasswordResetCommand command)
    {
        await Mediator.Send(command);
        return Ok(ApiResponse<object>.SuccessResponse(new { }, "If an account exists for this email, a verification code has been sent."));
    }

    /// <summary>Reset password using the OTP received by email.</summary>
    [HttpPost("reset-password")]
    public async Task<ActionResult<ApiResponse<object>>> ResetPassword([FromBody] ResetPasswordCommand command)
    {
        await Mediator.Send(command);
        return Ok(ApiResponse<object>.SuccessResponse(new { }, "Password has been reset. You can now log in with your new password."));
    }

    /// <summary>Send a registration OTP to the given email. Use the same email and OTP when calling register.</summary>
    [HttpPost("send-registration-otp")]
    public async Task<ActionResult<ApiResponse<object>>> SendRegistrationOtp([FromBody] SendRegistrationOtpCommand command)
    {
        await Mediator.Send(command);
        return Ok(ApiResponse<object>.SuccessResponse(new { }, "Verification code sent to your email."));
    }

    /// <summary>Verify email using OTP and activate account.</summary>
    [HttpPost("verify-email")]
    public async Task<ActionResult<ApiResponse<TokenResponse>>> VerifyEmail([FromBody] VerifyEmailCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<TokenResponse>.SuccessResponse(result, "Email verified successfully. You are now logged in."));
    }

    /// <summary>
    /// Step 2 of registration: save onboarding profile for AI plant consultation.
    /// Requires Authorization header with JWT from verify-email step.
    /// Frontend should call this immediately after email verification.
    /// </summary>
    [HttpPost("complete-profile")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<ActionResult<ApiResponse<bool>>> CompleteProfile(
        [FromBody] CompleteProfileCommand command)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        command.UserId = userId;  // override — never trust body for this

        var result = await Mediator.Send(command);
        return Ok(ApiResponse<bool>.SuccessResponse(result, "Profile completed successfully."));
    }

    /// <summary>
    /// Get the current authenticated user's profile information.
    /// Returns all user data including onboarding profile fields.
    /// Requires Authorization header with valid JWT token.
    /// </summary>
    [HttpGet("me")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public async Task<ActionResult<ApiResponse<object>>> GetCurrentUser()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(ApiResponse<object>.ErrorResponse("Invalid or missing user ID in token.", statusCode: 401));

        var u = await _context.UserAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId);
        if (u == null)
            return NotFound(ApiResponse<object>.ErrorResponse("User not found.", statusCode: 404));

        var userData = new
        {
            // ===== BASIC USER INFO =====
            id = u.Id,
            fullName = u.DisplayName ?? "Anonymous",
            email = u.Email,
            role = u.Role,
            phone = u.Phone ?? "",
            biography = u.Bio ?? "",
            avatar = u.AvatarUrl ?? "https://ui-avatars.com/api/?name=" + (u.DisplayName ?? u.Email),
            joinDate = u.CreatedAt.ToString("MMM dd, yyyy"),
            lastLogin = u.LastLoginAt.HasValue ? u.LastLoginAt.Value.ToString("g") : "Never",
            isActive = u.IsActive,
            emailVerified = u.EmailVerified,

            // ===== ONBOARDING PROFILE FIELDS =====
            isProfileCompleted = u.IsProfileCompleted,
            experienceLevel = u.ExperienceLevel,
            sunlightExposure = u.SunlightExposure,
            roomTemperatureRange = u.RoomTemperatureRange,
            humidityLevel = u.HumidityLevel,
            wateringFrequency = u.WateringFrequency,
            plantPlacement = u.PlacementLocation,
            spaceSize = u.SpaceSize,
            hasChildren = u.HasChildrenOrPets,
            hasPets = u.HasChildrenOrPets,
            plantGoals = u.PlantGoals != null ? u.PlantGoals.RootElement : (object?)null,
            stylePreference = u.PreferredStyle,
            budget = u.BudgetRange,
            location = u.LocationCity,
            hardinessZone = u.HardinessZone
        };

        return Ok(ApiResponse<object>.SuccessResponse(userData, "User profile retrieved successfully."));
    }

    // ==================== Google OAuth (Authorization Code) ====================

    [HttpGet("google/start")]
    public async Task<IActionResult> GoogleStart([FromQuery] string? returnTo, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_google.ClientId) ||
            string.IsNullOrWhiteSpace(_google.ClientSecret) ||
            string.IsNullOrWhiteSpace(_google.BaseUrl) ||
            string.IsNullOrWhiteSpace(_frontend.BaseUrl))
        {
            return BadRequest(ApiResponse<object>.ErrorResponse(
                "Google OAuth is not configured. Set GOOGLE__ClientId, GOOGLE__ClientSecret, GOOGLE__BaseUrl, and FRONTEND__BaseUrl.",
                statusCode: 400));
        }

        var state = Guid.NewGuid().ToString("N");
        var redirectUri = $"{_google.BaseUrl.TrimEnd('/')}/auth/google/callback";

        var payload = JsonSerializer.Serialize(new GoogleStatePayload(
            ReturnTo: string.IsNullOrWhiteSpace(returnTo) ? "/auth/oauth-callback" : returnTo.Trim()));

        await _cache.SetStringAsync(
            CacheKeys.GoogleState(state),
            payload,
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) },
            cancellationToken);

        var url = _googleOAuthService.BuildAuthorizeUrl(redirectUri, state);
        return Redirect(url);
    }

    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallback([FromQuery] string? code, [FromQuery] string? state, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("Missing code or state.", statusCode: 400));
        }

        var stateKey = CacheKeys.GoogleState(state);
        var stateJson = await _cache.GetStringAsync(stateKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(stateJson))
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("Invalid or expired state.", statusCode: 400));
        }

        await _cache.RemoveAsync(stateKey, cancellationToken);

        GoogleStatePayload? statePayload;
        try
        {
            statePayload = JsonSerializer.Deserialize<GoogleStatePayload>(stateJson);
        }
        catch
        {
            statePayload = null;
        }

        var redirectUri = $"{_google.BaseUrl.TrimEnd('/')}/auth/google/callback";
        var googleUser = await _googleOAuthService.ExchangeCodeAndVerifyAsync(code, redirectUri, cancellationToken);

        var email = googleUser.Email.Trim();
        var displayName = string.IsNullOrWhiteSpace(googleUser.Name) ? null : googleUser.Name.Trim();

        // Auto-link by email (include inactive). Google email is treated as verified.
        var existing = await _userAccountService.FindByEmailAsync(email, includeInactive: true, cancellationToken: cancellationToken);

        Guid userId;
        string role;
        string? finalDisplayName;

        if (existing == null)
        {
            var created = await _userAccountService.CreateUserAccountAsync(
                email: email,
                passwordHash: null,
                phone: null,
                role: "customer",
                displayName: displayName,
                emailVerified: true,
                cancellationToken: cancellationToken);

            // Mirror normal registration parity for new customers
            await _subscriptionService.CreateFreeSubscriptionAsync(created.Id, cancellationToken);
            await _quotaService.SeedDefaultQuotaForUserAsync(created.Id, "Free", cancellationToken);

            userId = created.Id;
            role = created.Role;
            finalDisplayName = created.DisplayName;
        }
        else
        {
            // Ensure existing account is active/verified (no OTP for Google)
            if (!existing.EmailVerified || !existing.IsActive)
            {
                var verified = await _userAccountService.VerifyEmailAsync(existing.Email, cancellationToken);
                userId = verified.Id;
                role = verified.Role;
                finalDisplayName = verified.DisplayName;
            }
            else
            {
                userId = existing.Id;
                role = existing.Role;
                finalDisplayName = existing.DisplayName;
            }
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Role, role)
        };
        if (!string.IsNullOrWhiteSpace(finalDisplayName))
        {
            claims.Add(new Claim(ClaimTypes.Name, finalDisplayName));
        }

        var accessToken = _jwtService.GenerateAccessToken(claims);
        var refreshToken = _jwtService.GenerateRefreshToken();
        var expiration = _jwtService.GetRefreshTokenExpiration() - DateTime.UtcNow;
        await _refreshTokenService.StoreRefreshTokenAsync(userId.ToString(), refreshToken, expiration);

        var tokenResponse = new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };

        var exchangeCode = Guid.NewGuid().ToString("N");
        await _cache.SetStringAsync(
            CacheKeys.GoogleExchange(exchangeCode),
            JsonSerializer.Serialize(tokenResponse),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2) },
            cancellationToken);

        var returnPath = statePayload?.ReturnTo;
        if (string.IsNullOrWhiteSpace(returnPath) || !returnPath.StartsWith("/", StringComparison.Ordinal))
        {
            returnPath = "/auth/oauth-callback";
        }

        var redirectTo = $"{_frontend.BaseUrl.TrimEnd('/')}{returnPath}?exchangeCode={Uri.EscapeDataString(exchangeCode)}";
        return Redirect(redirectTo);
    }

    public sealed record GoogleExchangeRequest(string ExchangeCode);

    [HttpPost("google/exchange")]
    public async Task<ActionResult<ApiResponse<TokenResponse>>> GoogleExchange([FromBody] GoogleExchangeRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ExchangeCode))
        {
            return BadRequest(ApiResponse<TokenResponse>.ErrorResponse("ExchangeCode is required.", statusCode: 400));
        }

        var key = CacheKeys.GoogleExchange(request.ExchangeCode.Trim());
        var json = await _cache.GetStringAsync(key, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return Unauthorized(ApiResponse<TokenResponse>.ErrorResponse("Invalid or expired exchange code.", statusCode: 401));
        }

        await _cache.RemoveAsync(key, cancellationToken);

        TokenResponse? tokens;
        try
        {
            tokens = JsonSerializer.Deserialize<TokenResponse>(json);
        }
        catch
        {
            tokens = null;
        }

        if (tokens == null || string.IsNullOrWhiteSpace(tokens.AccessToken) || string.IsNullOrWhiteSpace(tokens.RefreshToken))
        {
            return Unauthorized(ApiResponse<TokenResponse>.ErrorResponse("Invalid exchange payload.", statusCode: 401));
        }

        return Ok(ApiResponse<TokenResponse>.SuccessResponse(tokens, "Google sign-in completed."));
    }

    private static class CacheKeys
    {
        public static string GoogleState(string state) => $"google_oauth_state:{state}";
        public static string GoogleExchange(string code) => $"google_oauth_exchange:{code}";
    }

    private sealed record GoogleStatePayload(string ReturnTo);
}
