namespace EgresosBot.Api.Infrastructure.Seed;

public sealed class SeedOptions
{
    public bool Enabled { get; set; } = true;
    public string TenantName { get; set; } = "Empresa Demo";
    public string TenantSlug { get; set; } = "empresa-demo";
    public string UserFirstName { get; set; } = "Admin";
    public string UserLastName { get; set; } = "Demo";
    public string UserEmail { get; set; } = "admin@egresosbot.local";
    public string UserPassword { get; set; } = "Admin123$";
    public string RoleCode { get; set; } = "OWNER";
}
