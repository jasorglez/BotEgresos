namespace EgresosBot.Api.Application.Models.Tenants;

public sealed class RegisterTenantResponse
{
    public int TenantId { get; set; }
    public Guid TenantPublicId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public int UserId { get; set; }
    public Guid UserPublicId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string RoleCode { get; set; } = "OWNER";
}
