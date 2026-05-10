namespace EgresosBot.Api.Application.Models.BotLinks;

public sealed class CreateBotLinkCodeResponse
{
    public string Channel { get; set; } = string.Empty;
    public string LinkCode { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
}
