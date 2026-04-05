using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Services;
using decorativeplant_be.Domain.Entities;
using decorativeplant_be.Infrastructure.Data;
using decorativeplant_be.Application.Common.Exceptions;

namespace decorativeplant_be.Infrastructure.Identity;

public class UserAccountService : IUserAccountService
{
    private readonly IRepositoryFactory _repositoryFactory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordService _passwordService;

    public UserAccountService(
        IRepositoryFactory repositoryFactory,
        IUnitOfWork unitOfWork,
        IPasswordService passwordService)
    {
        _repositoryFactory = repositoryFactory;
        _unitOfWork = unitOfWork;
        _passwordService = passwordService;
    }

    public async Task<UserAccount> CreateUserAccountAsync(
        string email,
        string passwordHash,
        string? phone,
        string role,
        string? displayName = null,
        bool emailVerified = false,
        CancellationToken cancellationToken = default)
    {
        var userAccountRepository = _repositoryFactory.CreateRepository<UserAccount>();

        var userAccount = new UserAccount
        {
            Email = email,
            PasswordHash = passwordHash,
            Phone = phone,
            Role = role,
            IsActive = emailVerified, // Only active if verified
            DisplayName = displayName,
            EmailVerified = emailVerified,
            CreatedAt = DateTime.UtcNow
        };

        await userAccountRepository.AddAsync(userAccount, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return userAccount;
    }

    public async Task<UserAccount?> FindByEmailAsync(string email, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var userAccountRepository = _repositoryFactory.CreateRepository<UserAccount>();
        return await userAccountRepository.FirstOrDefaultAsync(
            u => u.Email == email && (includeInactive || u.IsActive),
            cancellationToken);
    }

    public async Task<UserAccount?> FindByPhoneAsync(string phone, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var userAccountRepository = _repositoryFactory.CreateRepository<UserAccount>();
        return await userAccountRepository.FirstOrDefaultAsync(
            u => u.Phone == phone && (includeInactive || u.IsActive),
            cancellationToken);
    }

    public async Task<UserAccount?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var userAccountRepository = _repositoryFactory.CreateRepository<UserAccount>();
        return await userAccountRepository.FirstOrDefaultAsync(
            u => u.Id == userId,
            cancellationToken);
    }

    public Task<bool> ValidatePasswordAsync(UserAccount userAccount, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userAccount.PasswordHash))
            return Task.FromResult(false);
        return Task.FromResult(_passwordService.VerifyPassword(password, userAccount.PasswordHash));
    }

    public async Task UpdatePasswordAsync(Guid userId, string newPasswordHash, CancellationToken cancellationToken = default)
    {
        var userAccountRepository = _repositoryFactory.CreateRepository<UserAccount>();
        var user = await userAccountRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null)
            throw new InvalidOperationException("User not found.");
        user.PasswordHash = newPasswordHash;
        user.UpdatedAt = DateTime.UtcNow;
        await userAccountRepository.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserAccount> VerifyEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var userAccountRepository = _repositoryFactory.CreateRepository<UserAccount>();
        var user = await userAccountRepository.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user == null)
            throw new ValidationException("User not found.");

        if (user.EmailVerified && user.IsActive)
            return user;

        user.EmailVerified = true;
        user.IsActive = true;
        user.UpdatedAt = DateTime.UtcNow;

        await userAccountRepository.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return user;
    }
}
