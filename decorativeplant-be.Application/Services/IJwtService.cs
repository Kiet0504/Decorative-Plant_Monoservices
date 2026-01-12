using System.Security.Claims;

namespace decorativeplant_be.Application.Services;

public interface IJwtService
{
    string GenerateAccessToken(IEnumerable<Claim> claims);
    string GenerateRefreshToken();
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
    DateTime GetRefreshTokenExpiration();
}
