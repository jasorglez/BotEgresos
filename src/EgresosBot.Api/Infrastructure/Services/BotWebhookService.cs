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
    private const string ExpenseDateState = "EXPENSE_DATE";
    private const string ExpenseAmountState = "EXPENSE_AMOUNT";
    private const string ExpenseDescriptionState = "EXPENSE_DESCRIPTION";
    private const string ExpenseConfirmState = "EXPENSE_CONFIRM";
    private const int RecentExpensesLimit = 5;
    private const int DailyChartDays = 7;

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

        if (lowerText is "0" or "4" or "e" or "f" or "cancelar" or "salir" or "menu" or "menÃº" or "inicio")
        {
            session.CurrentState = IdleState;
            draft = new TelegramExpenseDraft();
            await SaveSessionAsync(session, draft, normalizedText, cancellationToken);

            return new BotWebhookResponse
            {
                Success = true,
                Channel = "TELEGRAM",
                ChatId = chatId,
                Message = "âŒ Operacion cancelada.\n\n" + BuildMainMenu(botLink.User)
            };
        }

        switch (session.CurrentState)
        {
            case IdleState:
                if (lowerText is "1" or "a" or "registrar" or "egreso" or "gasto")
                {
                    session.CurrentState = ExpenseDateState;
                    draft = new TelegramExpenseDraft();
                    await SaveSessionAsync(session, draft, normalizedText, cancellationToken);

                    return new BotWebhookResponse
                    {
                        Success = true,
                        Channel = "TELEGRAM",
                        ChatId = chatId,
                        Message = "ðŸ§¾ Vamos a registrar un egreso.\n\nðŸ“… Â¿Deseas la fecha de hoy?\nPresiona S para usar hoy.\nPara otro dÃ­a, escrÃ­belo en formato AAAA-MM-DD o DD/MM/AAAA.\n\nâ†©ï¸ Escribe 0 para volver al menÃº."
                    };
                }

                if (lowerText is "2" or "b" or "ultimos" or "Ãºltimos" or "ver")
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
                            ? "ðŸ“­ No hay egresos registrados todavia.\n\n" + BuildMainMenu(botLink.User)
                            : "ðŸ“š Ultimos egresos:\n" + string.Join("\n", recentItems) + "\n\n" + BuildMainMenu(botLink.User)
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

                if (lowerText is "5" or "g" or "grafica" or "grÃ¡fica" or "grafica dia" or "grafica por dia" or "grÃ¡fica por dÃ­a")
                {
                    var chartMessage = await BuildDailyExpenseChartAsync(botLink.TenantId, cancellationToken);
                    await SaveSessionAsync(session, draft, normalizedText, cancellationToken);
                    return new BotWebhookResponse
                    {
                        Success = true,
                        Channel = "TELEGRAM",
                        ChatId = chatId,
                        Message = chartMessage + "\n\n" + BuildMainMenu(botLink.User)
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

            case ExpenseDateState:
                if (!TryParseExpenseDate(normalizedText, out var expenseDate))
                {
                    await SaveSessionAsync(session, draft, normalizedText, cancellationToken);
                    return new BotWebhookResponse
                    {
                        Success = false,
                        Channel = "TELEGRAM",
                        ChatId = chatId,
                        Message = "âš ï¸ No entendÃ­ la fecha.\nPresiona S para usar hoy o escribe una fecha como 2026-05-11 o 11/05/2026.\n\nâ†©ï¸ Escribe 0 para volver al menÃº."
                    };
                }

                draft.ExpenseDate = expenseDate;
                session.CurrentState = ExpenseAmountState;
                await SaveSessionAsync(session, draft, normalizedText, cancellationToken);

                return new BotWebhookResponse
                {
                    Success = true,
                    Channel = "TELEGRAM",
                    ChatId = chatId,
                    Message = $"ðŸ“… Fecha seleccionada: {expenseDate:yyyy-MM-dd}\n\nðŸ’µ Ahora envÃ­a el monto.\nEjemplo: 250.50\n\nâ†©ï¸ Escribe 0 para volver al menÃº."
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
                        Message = "âš ï¸ No entendi el monto.\nEscribe un numero como 250 o 250.50\n\nâ†©ï¸ Escribe 0 para volver al menu."
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
                    Message = "âœï¸ Ahora escribe la descripcion del egreso.\nEjemplo: gasolina, papeleria, comida.\n\nâ†©ï¸ Escribe 0 para volver al menu."
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
                        Message = "âš ï¸ La descripcion no puede ir vacia.\nEscribe una descripcion corta.\n\nâ†©ï¸ Escribe 0 para volver al menu."
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
                    Message = $"âœ… Confirma el egreso:\n\nðŸ“… Fecha: {draft.ExpenseDate:yyyy-MM-dd}\nðŸ’µ Monto: ${draft.AmountSubtotal:0.00} MXN\nðŸ“ Descripcion: {draft.Description}\n\n1 Confirmar\n4 Cancelar\nâ†©ï¸ 0 Volver al menÃº"
                };

            case ExpenseConfirmState:
                if (lowerText is not "1" and not "a" and not "si" and not "sÃ­" and not "confirmar" and not "ok")
                {
                    await SaveSessionAsync(session, draft, normalizedText, cancellationToken);
                    return new BotWebhookResponse
                    {
                        Success = false,
                        Channel = "TELEGRAM",
                        ChatId = chatId,
                        Message = "âš ï¸ Para guardar responde 1, A o si.\nSi no quieres continuar, responde 4, E o cancelar."
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
                        Message = "âš ï¸ La sesion del egreso quedo incompleta.\n\n" + BuildMainMenu(botLink.User)
                    };
                }

                var createdExpense = await expenseService.CreateExpenseAsync(
                    botLink.TenantId,
                    botLink.UserId,
                    new CreateExpenseRequest
                    {
                        ExpenseDate = draft.ExpenseDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
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
                    Message = $"ðŸŽ‰ Egreso registrado.\n\nðŸ§¾ Folio: {createdExpense.Id}\nðŸ’µ Monto: ${createdExpense.AmountTotal:0.00} MXN\nðŸ“ Descripcion: {createdExpense.Description}\nðŸ“… Fecha: {createdExpense.ExpenseDate:yyyy-MM-dd}\n\n{BuildMainMenu(botLink.User)}"
                };

            default:
                session.CurrentState = IdleState;
                await SaveSessionAsync(session, new TelegramExpenseDraft(), normalizedText, cancellationToken);
                return new BotWebhookResponse
                {
                    Success = false,
                    Channel = "TELEGRAM",
                    ChatId = chatId,
                    Message = "ðŸ”„ La sesion se reinicio.\n\n" + BuildMainMenu(botLink.User)
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

    private static bool TryParseExpenseDate(string value, out DateOnly expenseDate)
    {
        var normalized = value.Trim();

        if (normalized.Equals("s", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("si", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("sÃ­", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("hoy", StringComparison.OrdinalIgnoreCase))
        {
            expenseDate = DateOnly.FromDateTime(DateTime.UtcNow);
            return true;
        }

        if (DateOnly.TryParseExact(normalized, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out expenseDate))
        {
            return true;
        }

        if (DateOnly.TryParseExact(normalized, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out expenseDate))
        {
            return true;
        }

        return DateOnly.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.None, out expenseDate);
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
        return $"â•”â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•—\nâ•‘ âœ¨ MenÃº principal â•‘\nâ•šâ•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•\nðŸ‘¤ Usuario: {BuildUserDisplayName(user)}\n\n1ï¸âƒ£ [A] Registrar egreso\n2ï¸âƒ£ [B] Ver Ãºltimos egresos\n3ï¸âƒ£ [D] Ayuda\n4ï¸âƒ£ [E] Cancelar operaciÃ³n\n5ï¸âƒ£ [G] GrÃ¡fica por dÃ­a\n0ï¸âƒ£ [F] Volver al menÃº";
    }

    private static string BuildHelpMessage(AppUser user)
    {
        return $"ðŸ†˜ Ayuda\nðŸ‘¤ Usuario: {BuildUserDisplayName(user)}\n\n1ï¸âƒ£ [A] Registrar egreso\n2ï¸âƒ£ [B] Ver Ãºltimos egresos\n3ï¸âƒ£ [D] Ayuda\n4ï¸âƒ£ [E] Cancelar operaciÃ³n\n5ï¸âƒ£ [G] GrÃ¡fica por dÃ­a\n0ï¸âƒ£ [F] Volver al menÃº\n\nSi eliges registrar, el bot te pedirÃ¡:\nâ€¢ monto\nâ€¢ descripciÃ³n\nâ€¢ confirmaciÃ³n\n\nSi te equivocas en cualquier paso, escribe 0, F, 4, E, cancelar o menu.";
    }

    private static string BuildUserDisplayName(AppUser user)
    {
        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? user.Email : fullName;
    }

    private async Task<string> BuildDailyExpenseChartAsync(int tenantId, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = today.AddDays(-(DailyChartDays - 1));

        var rawData = await dbContext.Expenses
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.ExpenseDate >= startDate)
            .GroupBy(x => x.ExpenseDate)
            .Select(g => new
            {
                Date = g.Key,
                Total = g.Sum(x => x.AmountTotal)
            })
            .ToListAsync(cancellationToken);

        var totalsByDay = rawData.ToDictionary(x => x.Date, x => x.Total);
        var series = Enumerable.Range(0, DailyChartDays)
            .Select(offset => startDate.AddDays(offset))
            .Select(date => new
            {
                Date = date,
                Total = totalsByDay.TryGetValue(date, out var total) ? total : 0m
            })
            .ToList();

        if (series.All(x => x.Total <= 0))
        {
            return "📊 Gráfica por día\n\nNo hay egresos registrados en los últimos 7 días.";
        }

        var maxTotal = series.Max(x => x.Total);
        var lines = series.Select(item =>
        {
            var blocks = BuildTrafficBar(item.Total, maxTotal);
            return $"{item.Date:MM-dd} {blocks} ${item.Total:0.00}";
        });

        return "📊 Gráfica por día\n\n" + string.Join("\n", lines);
    }

    private static string BuildTrafficBar(decimal total, decimal maxTotal)
    {
        if (total <= 0 || maxTotal <= 0)
        {
            return "⬜";
        }

        var ratio = total / maxTotal;

        return ratio switch
        {
            <= 0.25m => "🟩",
            <= 0.50m => "🟨🟨",
            <= 0.75m => "🟧🟧🟧",
            _ => "🟥🟥🟥🟥"
        };
    }

    private sealed class TelegramExpenseDraft
    {
        public DateOnly? ExpenseDate { get; set; }
        public decimal? AmountSubtotal { get; set; }
        public string? Description { get; set; }
        public string? LastMessage { get; set; }
        public DateTime? LastMessageAtUtc { get; set; }
    }
}

