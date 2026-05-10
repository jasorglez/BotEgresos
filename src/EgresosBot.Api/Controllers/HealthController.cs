using EgresosBot.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EgresosBot.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController(EgresosBotDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        status = "ok",
        service = "EgresosBot.Api",
        utc = DateTime.UtcNow
    });

    [HttpGet("db")]
    public async Task<IActionResult> GetDatabase(CancellationToken cancellationToken)
    {
        var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
        return Ok(new
        {
            database = "EgresosBotDb",
            canConnect
        });
    }
}
