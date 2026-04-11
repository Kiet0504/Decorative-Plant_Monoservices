using MediatR;
using Microsoft.Extensions.Logging;
using decorativeplant_be.Application.Common;
using decorativeplant_be.Application.Common.DTOs.Auth;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Features.Auth.Commands;
using decorativeplant_be.Application.Services;
using decorativeplant_be.Application.Common.Interfaces;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Application.Features.Auth.Handlers;

public class LoginCommandHandler : IRequestHandler<LoginCommand, TokenResponse>
{
    private readonly IUserAccountService _userAccountService;
    private readonly IJwtService _jwtService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IApplicationDbContext _context;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        IUserAccountService userAccountService,
        IJwtService jwtService,
        IRefreshTokenService refreshTokenService,
        IApplicationDbContext context,
        ILogger<LoginCommandHandler> logger)
    {
        _userAccountService = userAccountService;
        _jwtService = jwtService;
        _refreshTokenService = refreshTokenService;
        _context = context;
        _logger = logger;
    }

    public async Task<TokenResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var userAccount = await _userAccountService.FindByEmailAsync(request.Email, cancellationToken: cancellationToken);
        if (userAccount == null || !userAccount.IsActive)
        {
            throw new UnauthorizedException("Invalid email or password.");
        }

        var isValidPassword = await _userAccountService.ValidatePasswordAsync(userAccount, request.Password, cancellationToken);
        if (!isValidPassword)
        {
            throw new UnauthorizedException("Invalid email or password.");
        }

        var user = await _userAccountService.GetByIdAsync(userAccount.Id, cancellationToken)
            ?? userAccount;

        var roleNorm = StaffRoleNormalizer.Normalize(user.Role);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, roleNorm)
        };

        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            claims.Add(new Claim(ClaimTypes.Name, user.DisplayName));
        }

        // Add company_id claim for admin users
        if (roleNorm == "admin" && user.CompanyId.HasValue)
        {
            claims.Add(new Claim("company_id", user.CompanyId.Value.ToString()));
        }

        // Add branch_id claim for staff roles
        if (roleNorm != "admin" && roleNorm != "customer")
        {
            var primaryAssignment = await _context.StaffAssignments
                .Where(sa => sa.StaffId == user.Id && sa.IsPrimary)
                .FirstOrDefaultAsync(cancellationToken);

            if (primaryAssignment != null)
            {
                claims.Add(new Claim("branch_id", primaryAssignment.BranchId.ToString()));
            }
        }

        var accessToken = _jwtService.GenerateAccessToken(claims);
        var refreshToken = _jwtService.GenerateRefreshToken();

        var expiration = _jwtService.GetRefreshTokenExpiration() - DateTime.UtcNow;
        await _refreshTokenService.StoreRefreshTokenAsync(user.Id.ToString(), refreshToken, expiration);

        _logger.LogInformation("User {UserId} logged in successfully", user.Id);

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30) // Access token expiration
        };
    }
}
