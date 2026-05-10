using EgresosBot.Api.Application.Abstractions;
using EgresosBot.Api.Application.Models.BotLinks;
using EgresosBot.Api.Application.Models.Webhooks;
using EgresosBot.Api.Domain.Entities;
using EgresosBot.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EgresosBot.Api.Infrastructure.Services;

public sealed class BotWebhookService(
    EgresosBotDbContext dbContext,
    IBotLinkService botLinkService) : IBotWebhookService
{
    public async Task<BotWebhookResponse> HandleTelegramAsync(TelegramWebhookRequest request, CancellationToken cancellationToken = default)
    {
        var message = request.Message;
        if (message?.Chat is null || message.From is null)
        {
            return new BotWebhookResponse { Success = false, Channel = "TELEGRAM", Message = "Update sin mensaje util." };
        }

        var chatId = message.Chat.Id.ToString();
        var text = message.Text?.Trim();

        if (TryExtractStartCode(text, out var linkCode))
        {
            var linked = await botLinkService.LinkByCodeAsync(new LinkBotChannelRequest
            {
                Channel = "TELEGRAM",
                LinkCode = linkCode,
                ExternalChatId = chatId,
                ExternalUserId = message.From.Id.ToString(),
                Username = message.From.Username ?? message.Chat.Username
            }, cancellationToken);

            return linked is null
                ? new BotWebhookResponse { Success = false, Channel = "TELEGRAM", ChatId = chatId, Message = "Codigo invalido o expirado." }
                : new BotWebhookResponse { Success = true, Channel = "TELEGRAM", ChatId = chatId, Message = "Telegram vinculado correctamente." };
        }

        var botLink = await dbContext.BotLinks
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Channel == "TELEGRAM" && x.ExternalChatId == chatId && x.IsActive, cancellationToken);

        if (botLink is null)
        {
            return new BotWebhookResponse
            {
                Success = false,
                Channel = "TELEGRAM",
                ChatId = chatId,
                Message = "Chat no vinculado. Usa /start CODIGO para conectar tu cuenta."
            };
        }

        await UpsertSessionAsync(botLink, chatId, text, cancellationToken);

        return new BotWebhookResponse
        {
            Success = true,
            Channel = "TELEGRAM",
            ChatId = chatId,
            Message = string.IsNullOrWhiteSpace(text)
                ? "Evento recibido de Telegram."
                : $"Mensaje recibido: {text}"
        };
    }

    public async Task<BotWebhookResponse> HandleWhatsAppAsync(WhatsAppWebhookRequest request, CancellationToken cancellationToken = default)
    {
        var chatId = request.From?.Trim();
        var body = request.Body?.Trim();

        if (string.IsNullOrWhiteSpace(chatId))
        {
            return new BotWebhookResponse { Success = false, Channel = "WHATSAPP", Message = "Webhook sin remitente." };
        }

        if (TryExtractWhatsAppLinkCode(body, out var linkCode))
        {
            var linked = await botLinkService.LinkByCodeAsync(new LinkBotChannelRequest
            {
                Channel = "WHATSAPP",
                LinkCode = linkCode,
                ExternalChatId = chatId,
                ExternalUserId = request.WaId,
                PhoneNumber = request.From,
                Username = request.ProfileName
            }, cancellationToken);

            return linked is null
                ? new BotWebhookResponse { Success = false, Channel = "WHATSAPP", ChatId = chatId, Message = "Codigo invalido o expirado." }
                : new BotWebhookResponse { Success = true, Channel = "WHATSAPP", ChatId = chatId, Message = "WhatsApp vinculado correctamente." };
        }

        var botLink = await dbContext.BotLinks
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Channel == "WHATSAPP" && x.ExternalChatId == chatId && x.IsActive, cancellationToken);

        if (botLink is null)
        {
            return new BotWebhookResponse
            {
                Success = false,
                Channel = "WHATSAPP",
                ChatId = chatId,
                Message = "Numero no vinculado. Envia: vincular CODIGO"
            };
        }

        await UpsertSessionAsync(botLink, chatId, body, cancellationToken);

        return new BotWebhookResponse
        {
            Success = true,
            Channel = "WHATSAPP",
            ChatId = chatId,
            Message = string.IsNullOrWhiteSpace(body)
                ? "Evento recibido de WhatsApp."
                : $"Mensaje recibido: {body}"
        };
    }

    private async Task UpsertSessionAsync(BotLink botLink, string chatId, string? text, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var session = await dbContext.BotSessions
            .FirstOrDefaultAsync(x => x.Channel == botLink.Channel && x.ChatId == chatId, cancellationToken);

        if (session is null)
        {
            session = new BotSession
            {
                TenantId = botLink.TenantId,
                UserId = botLink.UserId,
                Channel = botLink.Channel,
                ChatId = chatId,
                CurrentState = "IDLE",
                SessionJson = BuildSessionPayload(text),
                LastInteractionAt = now,
                CreatedAt = now,
                UpdatedAt = now
            };

            dbContext.BotSessions.Add(session);
        }
        else
        {
            session.SessionJson = BuildSessionPayload(text);
            session.LastInteractionAt = now;
            session.UpdatedAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string BuildSessionPayload(string? text)
    {
        var safeText = string.IsNullOrWhiteSpace(text) ? string.Empty : text.Replace("\"", "\\\"", StringComparison.Ordinal);
        return $$"""{"lastMessage":"{{safeText}}","lastMessageAtUtc":"{{DateTime.UtcNow:O}}"}""";
    }

    private static bool TryExtractStartCode(string? text, out string linkCode)
    {
        linkCode = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        const string prefix = "/start ";
        if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        linkCode = text[prefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(linkCode);
    }

    private static bool TryExtractWhatsAppLinkCode(string? text, out string linkCode)
    {
        linkCode = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        const string prefix = "vincular ";
        if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        linkCode = text[prefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(linkCode);
    }
}
