using EgresosBot.Api.Application.Models.Webhooks;

namespace EgresosBot.Api.Application.Abstractions;

public interface IBotWebhookService
{
    Task<BotWebhookResponse> HandleTelegramAsync(TelegramWebhookRequest request, CancellationToken cancellationToken = default);
    Task<BotWebhookResponse> HandleWhatsAppAsync(WhatsAppWebhookRequest request, CancellationToken cancellationToken = default);
}
