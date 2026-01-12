using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace decorativeplant_be.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly HealthCheckService _healthCheckService;

    public HealthController(HealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var health = await _healthCheckService.CheckHealthAsync();
        return Ok(health);
    }

    [HttpGet("ready")]
    public async Task<IActionResult> Ready()
    {
        var health = await _healthCheckService.CheckHealthAsync();
        return health.Status == HealthStatus.Healthy ? Ok(health) : StatusCode(503, health);
    }

    [HttpGet("live")]
    public IActionResult Live()
    {
        return Ok(new { status = "Alive", timestamp = DateTime.UtcNow });
    }
}
