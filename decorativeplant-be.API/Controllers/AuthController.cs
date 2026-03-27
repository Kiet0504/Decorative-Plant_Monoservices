using MediatR;
using Microsoft.AspNetCore.Mvc;
using decorativeplant_be.Application.Common.DTOs.Auth;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Features.Auth.Commands;

namespace decorativeplant_be.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : BaseController
{
    public AuthController()
    {
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
}
