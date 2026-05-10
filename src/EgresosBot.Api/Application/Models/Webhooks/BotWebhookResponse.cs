namespace EgresosBot.Api.Application.Models.Webhooks;

public sealed class BotWebhookResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Channel { get; set; }
    public string? ChatId { get; set; }
}
