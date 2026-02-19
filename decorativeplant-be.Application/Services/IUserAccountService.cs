using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Services;

public interface IUserAccountService
{
    Task<UserAccount> CreateUserAccountAsync(
        string email,
        string passwordHash,
        string? phone,
        string role,
        string? displayName = null,
        bool emailVerified = false,
        CancellationToken cancellationToken = default);

    Task<UserAccount?> FindByEmailAsync(string email, bool includeInactive = false, CancellationToken cancellationToken = default);

    Task<UserAccount?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<bool> ValidatePasswordAsync(UserAccount userAccount, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the password for the given user (e.g. after password reset).
    /// </summary>
    Task UpdatePasswordAsync(Guid userId, string newPasswordHash, CancellationToken cancellationToken = default);
    
    Task<UserAccount> VerifyEmailAsync(string email, CancellationToken cancellationToken = default);
}
