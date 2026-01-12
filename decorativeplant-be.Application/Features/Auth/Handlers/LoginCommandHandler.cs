using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using decorativeplant_be.Application.Common.DTOs.Auth;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Features.Auth.Commands;
using decorativeplant_be.Application.Services;
using decorativeplant_be.Domain.Entities;
using System.Security.Claims;

namespace decorativeplant_be.Application.Features.Auth.Handlers;

public class LoginCommandHandler : IRequestHandler<LoginCommand, TokenResponse>
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly IJwtService _jwtService;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        IJwtService jwtService,
        ILogger<LoginCommandHandler> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtService = jwtService;
        _logger = logger;
    }

    public async Task<TokenResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !user.IsActive)
        {
            throw new UnauthorizedException("Invalid email or password.");
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            throw new UnauthorizedException("Invalid email or password.");
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
            new Claim(ClaimTypes.Name, user.UserName ?? string.Empty)
        };

        var roles = await _userManager.GetRolesAsync(user);
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var accessToken = _jwtService.GenerateAccessToken(claims);
        var refreshToken = _jwtService.GenerateRefreshToken();

        // Store refresh token (in a real app, you'd store this in a database)
        // For now, we'll just return it

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30) // Access token expiration
        };
    }
}
