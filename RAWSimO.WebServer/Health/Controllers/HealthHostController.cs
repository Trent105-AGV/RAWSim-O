using Microsoft.AspNetCore.Mvc;

namespace RAWSimO.WebServer.Health.Controllers;

[ApiController]
[Route("")]
public sealed class HealthController : ControllerBase
{
    [HttpGet("health")]
    public IActionResult Health() => Ok(new { ok = true });
}