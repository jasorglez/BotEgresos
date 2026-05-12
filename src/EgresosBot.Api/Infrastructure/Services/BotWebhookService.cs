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
    private const int RecentExpensesLimit = 5;

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
            .Include(x => x.User)
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
                Message = BuildMainMenu(botLink.User)
            };
        }

        var lowerText = normalizedText.ToLowerInvariant();

        if (lowerText is "0" or "4" or "e" or "f" or "cancelar" or "salir" or "menu" or "menú" or "inicio")
        {
            session.CurrentState = IdleState;
            draft = new TelegramExpenseDraft();
            await SaveSessionAsync(session, draft, normalizedText, cancellationToken);

            return new BotWebhookResponse
            {
                Success = true,
                Channel = "TELEGRAM",
                ChatId = chatId,
                Message = "❌ Operacion cancelada.\n\n" + BuildMainMenu(botLink.User)
            };
        }

        switch (session.CurrentState)
        {
            case IdleState:
                if (lowerText is "1" or "a" or "registrar" or "egreso" or "gasto")
                {
                    session.CurrentState = ExpenseAmountState;
                    draft = new TelegramExpenseDraft();
                    await SaveSessionAsync(session, draft, normalizedText, cancellationToken);

                    return new BotWebhookResponse
                    {
                        Success = true,
                        Channel = "TELEGRAM",
                        ChatId = chatId,
                        Message = "🧾 Vamos a registrar un egreso.\n\n💵 Envia el monto.\nEjemplo: 250.50\n\n↩️ Escribe 0 para volver al menu."
                    };
                }

                if (lowerText is "2" or "b" or "ultimos" or "últimos" or "ver")
                {
                    var recentExpenses = await expenseService.GetExpensesAsync(botLink.TenantId, cancellationToken);
                    var recentItems = recentExpenses
                        .Take(RecentExpensesLimit)
                        .Select(x => $"#{x.Id} {x.ExpenseDate:yyyy-MM-dd} ${x.AmountTotal:0.00} - {x.Description}")
                        .ToList();

                    await SaveSessionAsync(session, draft, normalizedText, cancellationToken);

                    return new BotWebhookResponse
                    {
                        Success = true,
                        Channel = "TELEGRAM",
                        ChatId = chatId,
                        Message = recentItems.Count == 0
                            ? "📭 No hay egresos registrados todavia.\n\n" + BuildMainMenu(botLink.User)
                            : "📚 Ultimos egresos:\n" + string.Join("\n", recentItems) + "\n\n" + BuildMainMenu(botLink.User)
                    };
                }

                if (lowerText is "3" or "d" or "ayuda" or "help")
                {
                    await SaveSessionAsync(session, draft, normalizedText, cancellationToken);
                    return new BotWebhookResponse
                    {
                        Success = true,
                        Channel = "TELEGRAM",
                        ChatId = chatId,
                        Message = BuildHelpMessage(botLink.User)
                    };
                }

                await SaveSessionAsync(session, draft, normalizedText, cancellationToken);
                return new BotWebhookResponse
                {
                    Success = true,
                    Channel = "TELEGRAM",
                    ChatId = chatId,
                    Message = BuildMainMenu(botLink.User)
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
                        Message = "⚠️ No entendi el monto.\nEscribe un numero como 250 o 250.50\n\n↩️ Escribe 0 para volver al menu."
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
                    Message = "✍️ Ahora escribe la descripcion del egreso.\nEjemplo: gasolina, papeleria, comida.\n\n↩️ Escribe 0 para volver al menu."
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
                        Message = "⚠️ La descripcion no puede ir vacia.\nEscribe una descripcion corta.\n\n↩️ Escribe 0 para volver al menu."
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
                    Message = $"✅ Confirma el egreso:\n\n💵 Monto: ${draft.AmountSubtotal:0.00} MXN\n📝 Descripcion: {draft.Description}\n\n1 Confirmar\n4 Cancelar\n↩️ 0 Volver al menu"
                };

            case ExpenseConfirmState:
                if (lowerText is not "1" and not "a" and not "si" and not "sí" and not "confirmar" and not "ok")
                {
                    await SaveSessionAsync(session, draft, normalizedText, cancellationToken);
                    return new BotWebhookResponse
                    {
                        Success = false,
                        Channel = "TELEGRAM",
                        ChatId = chatId,
                        Message = "⚠️ Para guardar responde 1, A o si.\nSi no quieres continuar, responde 4, E o cancelar."
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
                        Message = "⚠️ La sesion del egreso quedo incompleta.\n\n" + BuildMainMenu(botLink.User)
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
                    Message = $"🎉 Egreso registrado.\n\n🧾 Folio: {createdExpense.Id}\n💵 Monto: ${createdExpense.AmountTotal:0.00} MXN\n📝 Descripcion: {createdExpense.Description}\n📅 Fecha: {createdExpense.ExpenseDate:yyyy-MM-dd}\n\n{BuildMainMenu(botLink.User)}"
                };

            default:
                session.CurrentState = IdleState;
                await SaveSessionAsync(session, new TelegramExpenseDraft(), normalizedText, cancellationToken);
                return new BotWebhookResponse
                {
                    Success = false,
                    Channel = "TELEGRAM",
                    ChatId = chatId,
                    Message = "🔄 La sesion se reinicio.\n\n" + BuildMainMenu(botLink.User)
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

    private static string BuildMainMenu(AppUser user)
    {
        return $"╔══════════════════╗\n║ ✨ Menú principal ║\n╚══════════════════╝\n👤 Usuario: {BuildUserDisplayName(user)}\n\n1️⃣ [A] Registrar egreso\n2️⃣ [B] Ver últimos egresos\n3️⃣ [D] Ayuda\n4️⃣ [E] Cancelar operación\n0️⃣ [F] Volver al menú";
    }

    private static string BuildHelpMessage(AppUser user)
    {
        return $"🆘 Ayuda\n👤 Usuario: {BuildUserDisplayName(user)}\n\n1️⃣ [A] Registrar egreso\n2️⃣ [B] Ver últimos egresos\n3️⃣ [D] Ayuda\n4️⃣ [E] Cancelar operación\n0️⃣ [F] Volver al menú\n\nSi eliges registrar, el bot te pedirá:\n• monto\n• descripción\n• confirmación\n\nSi te equivocas en cualquier paso, escribe 0, F, 4, E, cancelar o menu.";
    }

    private static string BuildUserDisplayName(AppUser user)
    {
        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? user.Email : fullName;
    }

    private sealed class TelegramExpenseDraft
    {
        public decimal? AmountSubtotal { get; set; }
        public string? Description { get; set; }
        public string? LastMessage { get; set; }
        public DateTime? LastMessageAtUtc { get; set; }
    }
}
