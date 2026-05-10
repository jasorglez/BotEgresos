namespace EgresosBot.Api.Application.Models.BotLinks;

public sealed class BotLinkResponse
{
    public int Id { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string? ExternalUserId { get; set; }
    public string? ExternalChatId { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Username { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LinkedAtUtc { get; set; }
}
