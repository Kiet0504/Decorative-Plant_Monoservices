using Microsoft.EntityFrameworkCore;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Services;
using decorativeplant_be.Domain.Entities;
using decorativeplant_be.Infrastructure.Data;

namespace decorativeplant_be.Infrastructure.Identity;

public class UserAccountService : IUserAccountService
{
    private readonly IRepositoryFactory _repositoryFactory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordService _passwordService;
    private readonly ApplicationDbContext _context;

    public UserAccountService(
        IRepositoryFactory repositoryFactory,
        IUnitOfWork unitOfWork,
        IPasswordService passwordService,
        ApplicationDbContext context)
    {
        _repositoryFactory = repositoryFactory;
        _unitOfWork = unitOfWork;
        _passwordService = passwordService;
        _context = context;
    }

    public async Task<UserAccount> CreateUserAccountAsync(
        string email,
        string passwordHash,
        string? phone,
        string role,
        UserProfile? userProfile = null,
        CancellationToken cancellationToken = default)
    {
        var userAccountRepository = _repositoryFactory.CreateRepository<UserAccount>();

        var userAccount = new UserAccount
        {
            Email = email,
            PasswordHash = passwordHash,
            Phone = phone,
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await userAccountRepository.AddAsync(userAccount, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        if (userProfile != null)
        {
            userProfile.UserId = userAccount.Id;
            userProfile.UserAccount = userAccount;
            userAccount.UserProfile = userProfile;

            await _context.UserProfiles.AddAsync(userProfile, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return userAccount;
    }

    public async Task<UserAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var userAccountRepository = _repositoryFactory.CreateRepository<UserAccount>();
        return await userAccountRepository.FirstOrDefaultAsync(
            u => u.Email == email && u.IsActive,
            cancellationToken);
    }

    public async Task<(UserAccount userAccount, UserProfile? userProfile)> GetUserWithProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var userAccountRepository = _repositoryFactory.CreateRepository<UserAccount>();
        var userAccount = await userAccountRepository.FirstOrDefaultAsync(
            u => u.Id == userId,
            cancellationToken);

        if (userAccount == null)
        {
            throw new InvalidOperationException($"User account with ID {userId} not found.");
        }

        var userProfile = await _context.UserProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

        return (userAccount, userProfile);
    }

    public Task<bool> ValidatePasswordAsync(UserAccount userAccount, string password, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_passwordService.VerifyPassword(password, userAccount.PasswordHash));
    }
}
