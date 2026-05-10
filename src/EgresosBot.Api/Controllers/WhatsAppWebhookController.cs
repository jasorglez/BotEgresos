using EgresosBot.Api.Application.Abstractions;
using EgresosBot.Api.Application.Models.Webhooks;
using Microsoft.AspNetCore.Mvc;

namespace EgresosBot.Api.Controllers;

[ApiController]
[Route("api/webhooks/whatsapp")]
public sealed class WhatsAppWebhookController(IBotWebhookService botWebhookService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Receive([FromForm] WhatsAppWebhookRequest request, CancellationToken cancellationToken)
    {
        var result = await botWebhookService.HandleWhatsAppAsync(request, cancellationToken);
        return Ok(result);
    }
}
