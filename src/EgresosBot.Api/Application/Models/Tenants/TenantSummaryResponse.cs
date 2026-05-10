namespace EgresosBot.Api.Application.Models.Tenants;

public sealed class TenantSummaryResponse
{
    public int TenantId { get; set; }
    public Guid PublicId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string RoleCode { get; set; } = string.Empty;
    public bool IsOwner { get; set; }
}
