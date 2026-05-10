namespace EgresosBot.Api.Application.Models.Auth;

public sealed class RevokeTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
