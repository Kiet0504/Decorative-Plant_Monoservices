using MediatR;
using Microsoft.AspNetCore.Mvc;
using decorativeplant_be.Application.Common.DTOs.Auth;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Features.Auth.Commands;

namespace decorativeplant_be.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : BaseController
{
    public AuthController()
    {
    }

    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<TokenResponse>>> Register([FromBody] RegisterCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<TokenResponse>.SuccessResponse(result, "User registered successfully."));
    }

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<TokenResponse>>> Login([FromBody] LoginCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<TokenResponse>.SuccessResponse(result, "Login successful."));
    }

    [HttpPost("refresh-token")]
    public async Task<ActionResult<ApiResponse<TokenResponse>>> RefreshToken([FromBody] RefreshTokenCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<TokenResponse>.SuccessResponse(result, "Token refreshed successfully."));
    }
}
