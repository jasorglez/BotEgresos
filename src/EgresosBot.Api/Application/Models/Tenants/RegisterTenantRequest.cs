namespace EgresosBot.Api.Application.Models.Tenants;

public sealed class RegisterTenantRequest
{
    public string CompanyName { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public string Password { get; set; } = string.Empty;
}
