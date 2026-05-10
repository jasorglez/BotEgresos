using EgresosBot.Api.Application.Models.Tenants;

namespace EgresosBot.Api.Application.Abstractions;

public interface ITenantService
{
    Task<IReadOnlyCollection<TenantSummaryResponse>> GetUserTenantsAsync(int userId, CancellationToken cancellationToken = default);
    Task<RegisterTenantResponse> RegisterAsync(RegisterTenantRequest request, CancellationToken cancellationToken = default);
}
