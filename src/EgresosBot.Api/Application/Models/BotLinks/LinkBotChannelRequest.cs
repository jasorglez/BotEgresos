namespace EgresosBot.Api.Application.Models.BotLinks;

public sealed class LinkBotChannelRequest
{
    public string Channel { get; set; } = string.Empty;
    public string LinkCode { get; set; } = string.Empty;
    public string? ExternalUserId { get; set; }
    public string ExternalChatId { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Username { get; set; }
}
