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

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, TokenResponse>
{
    private readonly IUserAccountService _userAccountService;
    private readonly IJwtService _jwtService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IApplicationDbContext _context;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        IUserAccountService userAccountService,
        IJwtService jwtService,
        IRefreshTokenService refreshTokenService,
        IApplicationDbContext context,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _userAccountService = userAccountService;
        _jwtService = jwtService;
        _refreshTokenService = refreshTokenService;
        _context = context;
        _logger = logger;
    }

    public async Task<TokenResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        // Get userId from refresh token using Redis lookup
        var userIdString = await _refreshTokenService.GetUserIdFromTokenAsync(request.RefreshToken);
        if (string.IsNullOrEmpty(userIdString))
        {
            throw new UnauthorizedException("Invalid or expired refresh token.");
        }

        if (!Guid.TryParse(userIdString, out var userId))
        {
            throw new UnauthorizedException("Invalid user ID format.");
        }

        // Validate the refresh token exists and belongs to the user
        var isValid = await _refreshTokenService.ValidateRefreshTokenAsync(userIdString, request.RefreshToken);
        if (!isValid)
        {
            throw new UnauthorizedException("Invalid refresh token.");
        }

        var userAccount = await _userAccountService.GetByIdAsync(userId, cancellationToken);
        if (userAccount == null || !userAccount.IsActive)
        {
            throw new UnauthorizedException("User account is inactive.");
        }

        await _refreshTokenService.RevokeRefreshTokenAsync(userIdString, request.RefreshToken);

        var roleNorm = StaffRoleNormalizer.Normalize(userAccount.Role);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userAccount.Id.ToString()),
            new Claim(ClaimTypes.Email, userAccount.Email),
            new Claim(ClaimTypes.Role, roleNorm)
        };

        if (!string.IsNullOrWhiteSpace(userAccount.DisplayName))
        {
            claims.Add(new Claim(ClaimTypes.Name, userAccount.DisplayName));
        }

        // Add company_id claim for admin users
        if (roleNorm == "admin" && userAccount.CompanyId.HasValue)
        {
            claims.Add(new Claim("company_id", userAccount.CompanyId.Value.ToString()));
        }

        // Add branch_id claim for staff roles
        if (roleNorm != "admin" && roleNorm != "customer")
        {
            var primaryAssignment = await _context.StaffAssignments
                .Where(sa => sa.StaffId == userAccount.Id && sa.IsPrimary)
                .FirstOrDefaultAsync(cancellationToken);

            if (primaryAssignment != null)
            {
                claims.Add(new Claim("branch_id", primaryAssignment.BranchId.ToString()));
            }
        }

        var accessToken = _jwtService.GenerateAccessToken(claims);
        var newRefreshToken = _jwtService.GenerateRefreshToken();

        var expiration = _jwtService.GetRefreshTokenExpiration() - DateTime.UtcNow;
        await _refreshTokenService.StoreRefreshTokenAsync(userAccount.Id.ToString(), newRefreshToken, expiration);

        _logger.LogInformation("Refreshed tokens for user {UserId}", userAccount.Id);

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };
    }
}
