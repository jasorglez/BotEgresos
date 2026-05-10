namespace EgresosBot.Api.Infrastructure.Auth;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "EgresosBot";
    public string Audience { get; set; } = "EgresosBot.Api";
    public string SecretKey { get; set; } = "CAMBIAR_SECRET_KEY_DE_32_CHARS_MINIMO";
    public int AccessTokenMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 30;
}
