using EgresosBot.Api.Application.Abstractions;
using EgresosBot.Api.Application.Models.BotLinks;
using EgresosBot.Api.Application.Models.Expenses;
using EgresosBot.Api.Application.Models.Webhooks;
using EgresosBot.Api.Domain.Entities;
using EgresosBot.Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;

namespace EgresosBot.Api.Infrastructure.Services;

public sealed class BotWebhookService(
    EgresosBotDbContext dbContext,
    IBotLinkService botLinkService,
    IExpenseService expenseService) : IBotWebhookService
{
    private const string IdleState = "IDLE";
    private const string ExpenseAmountState = "EXPENSE_AMOUNT";
    private const string ExpenseDescriptionState = "EXPENSE_DESCRIPTION";
    private const string ExpenseConfirmState = "EXPENSE_CONFIRM";

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

        var response = await HandleTelegramLinkedMessageAsync(botLink, chatId, text, cancellationToken);

        return response;
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

        await UpsertPassiveSessionAsync(botLink, chatId, body, cancellationToken);

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

    private async Task<BotWebhookResponse> HandleTelegramLinkedMessageAsync(BotLink botLink, string chatId, string? text, CancellationToken cancellationToken)
    {
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
                CurrentState = IdleState,
                SessionJson = BuildSessionPayload(new TelegramExpenseDraft(), text),
                LastInteractionAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            dbContext.BotSessions.Add(session);
        }

        var normalizedText = text?.Trim();
        var draft = ParseDraft(session.SessionJson);

        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            await SaveSessionAsync(session, draft, text, cancellationToken);
            return new BotWebhookResponse
            {
                Success = true,
                Channel = "TELEGRAM",
                ChatId = chatId,
                Message = "Escribe registrar para capturar un egreso o cancelar para salir."
            };
        }

        var lowerText = normalizedText.ToLowerInvariant();

        if (lowerText is "cancelar" or "salir")
        {
            session.CurrentState = IdleState;
            draft = new TelegramExpenseDraft();
            await SaveSessionAsync(session, draft, normalizedText, cancellationToken);

            return new BotWebhookResponse
            {
                Success = true,
                Channel = "TELEGRAM",
                ChatId = chatId,
                Message = "Operacion cancelada. Escribe registrar para iniciar otro egreso."
            };
        }

        switch (session.CurrentState)
        {
            case IdleState:
                if (lowerText is "registrar" or "egreso" or "gasto")
                {
                    session.CurrentState = ExpenseAmountState;
                    draft = new TelegramExpenseDraft();
                    await SaveSessionAsync(session, draft, normalizedText, cancellationToken);

                    return new BotWebhookResponse
                    {
                        Success = true,
                        Channel = "TELEGRAM",
                        ChatId = chatId,
                        Message = "Vamos a registrar un egreso. Envia el monto. Ejemplo: 250.50"
                    };
                }

                await SaveSessionAsync(session, draft, normalizedText, cancellationToken);
                return new BotWebhookResponse
                {
                    Success = true,
                    Channel = "TELEGRAM",
                    ChatId = chatId,
                    Message = "Comandos disponibles: registrar, cancelar."
                };

            case ExpenseAmountState:
                if (!TryParseAmount(normalizedText, out var amount))
                {
                    await SaveSessionAsync(session, draft, normalizedText, cancellationToken);
                    return new BotWebhookResponse
                    {
                        Success = false,
                        Channel = "TELEGRAM",
                        ChatId = chatId,
                        Message = "No entendi el monto. Escribe un numero como 250 o 250.50"
                    };
                }

                draft.AmountSubtotal = amount;
                session.CurrentState = ExpenseDescriptionState;
                await SaveSessionAsync(session, draft, normalizedText, cancellationToken);

                return new BotWebhookResponse
                {
                    Success = true,
                    Channel = "TELEGRAM",
                    ChatId = chatId,
                    Message = "Ahora escribe la descripcion del egreso. Ejemplo: gasolina, papeleria, comida."
                };

            case ExpenseDescriptionState:
                if (string.IsNullOrWhiteSpace(normalizedText))
                {
                    await SaveSessionAsync(session, draft, normalizedText, cancellationToken);
                    return new BotWebhookResponse
                    {
                        Success = false,
                        Channel = "TELEGRAM",
                        ChatId = chatId,
                        Message = "La descripcion no puede ir vacia. Escribe una descripcion corta."
                    };
                }

                draft.Description = normalizedText;
                session.CurrentState = ExpenseConfirmState;
                await SaveSessionAsync(session, draft, normalizedText, cancellationToken);

                return new BotWebhookResponse
                {
                    Success = true,
                    Channel = "TELEGRAM",
                    ChatId = chatId,
                    Message = $"Confirma el egreso:\nMonto: ${draft.AmountSubtotal:0.00} MXN\nDescripcion: {draft.Description}\n\nResponde si para guardar o cancelar."
                };

            case ExpenseConfirmState:
                if (lowerText is not "si" and not "sí" and not "confirmar" and not "ok")
                {
                    await SaveSessionAsync(session, draft, normalizedText, cancellationToken);
                    return new BotWebhookResponse
                    {
                        Success = false,
                        Channel = "TELEGRAM",
                        ChatId = chatId,
                        Message = "Para guardar responde si. Si no quieres continuar, escribe cancelar."
                    };
                }

                if (draft.AmountSubtotal is null || string.IsNullOrWhiteSpace(draft.Description))
                {
                    session.CurrentState = IdleState;
                    await SaveSessionAsync(session, new TelegramExpenseDraft(), normalizedText, cancellationToken);
                    return new BotWebhookResponse
                    {
                        Success = false,
                        Channel = "TELEGRAM",
                        ChatId = chatId,
                        Message = "La sesion del egreso quedo incompleta. Escribe registrar para empezar de nuevo."
                    };
                }

                var createdExpense = await expenseService.CreateExpenseAsync(
                    botLink.TenantId,
                    botLink.UserId,
                    new CreateExpenseRequest
                    {
                        ExpenseDate = DateOnly.FromDateTime(DateTime.UtcNow),
                        Description = draft.Description,
                        AmountSubtotal = draft.AmountSubtotal.Value,
                        IvaAmount = 0,
                        Notes = "Capturado desde Telegram",
                        PaymentMethod = "TELEGRAM"
                    },
                    cancellationToken);

                session.CurrentState = IdleState;
                await SaveSessionAsync(session, new TelegramExpenseDraft(), normalizedText, cancellationToken);

                return new BotWebhookResponse
                {
                    Success = true,
                    Channel = "TELEGRAM",
                    ChatId = chatId,
                    Message = $"Egreso registrado.\nFolio: {createdExpense.Id}\nMonto: ${createdExpense.AmountTotal:0.00} MXN\nDescripcion: {createdExpense.Description}\nFecha: {createdExpense.ExpenseDate:yyyy-MM-dd}"
                };

            default:
                session.CurrentState = IdleState;
                await SaveSessionAsync(session, new TelegramExpenseDraft(), normalizedText, cancellationToken);
                return new BotWebhookResponse
                {
                    Success = false,
                    Channel = "TELEGRAM",
                    ChatId = chatId,
                    Message = "La sesion se reinicio. Escribe registrar para capturar un egreso."
                };
        }
    }

    private async Task UpsertPassiveSessionAsync(BotLink botLink, string chatId, string? text, CancellationToken cancellationToken)
    {
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
                CurrentState = IdleState,
                SessionJson = BuildSessionPayload(new TelegramExpenseDraft(), text),
                LastInteractionAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            dbContext.BotSessions.Add(session);
        }

        await SaveSessionAsync(session, ParseDraft(session.SessionJson), text, cancellationToken);
    }

    private async Task SaveSessionAsync(BotSession session, TelegramExpenseDraft draft, string? lastText, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        session.SessionJson = BuildSessionPayload(draft, lastText);
        session.LastInteractionAt = now;
        session.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static TelegramExpenseDraft ParseDraft(string? sessionJson)
    {
        if (string.IsNullOrWhiteSpace(sessionJson))
        {
            return new TelegramExpenseDraft();
        }

        try
        {
            return JsonSerializer.Deserialize<TelegramExpenseDraft>(sessionJson) ?? new TelegramExpenseDraft();
        }
        catch
        {
            return new TelegramExpenseDraft();
        }
    }

    private static string BuildSessionPayload(TelegramExpenseDraft draft, string? text)
    {
        draft.LastMessage = string.IsNullOrWhiteSpace(text) ? null : text;
        draft.LastMessageAtUtc = DateTime.UtcNow;
        return JsonSerializer.Serialize(draft);
    }

    private static bool TryParseAmount(string value, out decimal amount)
    {
        var normalized = value.Trim()
            .Replace("$", string.Empty, StringComparison.Ordinal)
            .Replace(",", string.Empty, StringComparison.Ordinal);

        return decimal.TryParse(
            normalized,
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out amount) && amount > 0;
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

    private sealed class TelegramExpenseDraft
    {
        public decimal? AmountSubtotal { get; set; }
        public string? Description { get; set; }
        public string? LastMessage { get; set; }
        public DateTime? LastMessageAtUtc { get; set; }
    }
}
