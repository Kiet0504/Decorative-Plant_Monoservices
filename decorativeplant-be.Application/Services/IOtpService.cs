namespace decorativeplant_be.Application.Services;

/// <summary>
/// Manages OTP creation, validation, and consumption for email verification (e.g. registration, password reset).
/// </summary>
public interface IOtpService
{
    /// <summary>
    /// Generates a new OTP for the given email and purpose, stores its hash, and returns the plain code (to send via email).
    /// Invalidates any previous OTP for the same email + purpose.
    /// </summary>
    /// <param name="email">Target email (case-insensitive).</param>
    /// <param name="purpose">e.g. "Registration" or "PasswordReset".</param>
    /// <param name="expiresInMinutes">Lifetime of the OTP in minutes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The plain OTP code (e.g. 6 digits) to send to the user.</returns>
    Task<string> CreateOtpAsync(string email, string purpose, int expiresInMinutes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the OTP and marks it as used. Returns true only if code matches and is not expired.
    /// </summary>
    Task<bool> ValidateAndConsumeOtpAsync(string email, string code, string purpose, CancellationToken cancellationToken = default);
}
