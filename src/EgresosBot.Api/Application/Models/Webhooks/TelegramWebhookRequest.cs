namespace EgresosBot.Api.Application.Models.Webhooks;

public sealed class TelegramWebhookRequest
{
    public TelegramMessageDto? Message { get; set; }
}

public sealed class TelegramMessageDto
{
    public long MessageId { get; set; }
    public TelegramChatDto? Chat { get; set; }
    public TelegramUserDto? From { get; set; }
    public string? Text { get; set; }
}

public sealed class TelegramChatDto
{
    public long Id { get; set; }
    public string? Type { get; set; }
    public string? Username { get; set; }
}

public sealed class TelegramUserDto
{
    public long Id { get; set; }
    public string? Username { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}
