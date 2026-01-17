using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Services;

public interface IUserAccountService
{
    Task<UserAccount> CreateUserAccountAsync(
        string email,
        string passwordHash,
        string? phone,
        string role,
        UserProfile? userProfile = null,
        CancellationToken cancellationToken = default);

    Task<UserAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<(UserAccount userAccount, UserProfile? userProfile)> GetUserWithProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> ValidatePasswordAsync(UserAccount userAccount, string password, CancellationToken cancellationToken = default);
}
