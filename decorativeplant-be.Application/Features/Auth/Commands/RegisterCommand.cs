using decorativeplant_be.Application.Common.DTOs.Auth;
using MediatR;

namespace decorativeplant_be.Application.Features.Auth.Commands;

public class RegisterCommand : IRequest<TokenResponse>
{
    public string Email { get; set; } = string.Empty;
    /// <summary>Optional. When provided, registration requires this OTP (from send-registration-otp) and marks email as verified.</summary>
    public string? Otp { get; set; }
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    /// <summary>Required. Primary phone number for the user.</summary>
    public string PhoneNumber { get; set; } = string.Empty;
}
