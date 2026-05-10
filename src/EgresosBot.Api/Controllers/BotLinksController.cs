using EgresosBot.Api.Application.Abstractions;
using EgresosBot.Api.Application.Models.BotLinks;
using EgresosBot.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EgresosBot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/bot-links")]
public sealed class BotLinksController(IBotLinkService botLinkService) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var tenantId = User.GetRequiredTenantId();
        var userId = User.GetRequiredUserId();
        var links = await botLinkService.GetUserLinksAsync(tenantId, userId, cancellationToken);
        return Ok(links);
    }

    [HttpPost("link-code")]
    public async Task<IActionResult> CreateLinkCode([FromBody] CreateBotLinkCodeRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = User.GetRequiredTenantId();
            var userId = User.GetRequiredUserId();
            var result = await botLinkService.CreateLinkCodeAsync(tenantId, userId, request, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
