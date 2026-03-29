using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using decorativeplant_be.Application.Common.DTOs.Auth;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Auth.Commands;

namespace decorativeplant_be.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : BaseController
{
    private readonly IApplicationDbContext _context;

    public AuthController(IApplicationDbContext context)
    {
        _context = context;
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
}
