using EgresosBot.Api.Application.Abstractions;
using EgresosBot.Api.Application.Models.Webhooks;
using Microsoft.AspNetCore.Mvc;

namespace EgresosBot.Api.Controllers;

[ApiController]
[Route("api/webhooks/telegram")]
public sealed class TelegramWebhookController(IBotWebhookService botWebhookService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Receive([FromBody] TelegramWebhookRequest request, CancellationToken cancellationToken)
    {
        var result = await botWebhookService.HandleTelegramAsync(request, cancellationToken);
        return Ok(result);
    }
}
