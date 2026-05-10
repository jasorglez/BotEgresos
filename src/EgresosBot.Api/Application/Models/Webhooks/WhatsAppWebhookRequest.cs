namespace EgresosBot.Api.Application.Models.Webhooks;

public sealed class WhatsAppWebhookRequest
{
    public string? Body { get; set; }
    public string? From { get; set; }
    public string? WaId { get; set; }
    public string? ProfileName { get; set; }
}
