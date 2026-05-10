using System.Security.Claims;
using EgresosBot.Api.Infrastructure.Auth;

namespace EgresosBot.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static int GetRequiredUserId(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? principal.FindFirstValue("sub");

        if (!int.TryParse(raw, out var userId))
        {
            throw new InvalidOperationException("UserId claim no encontrado.");
        }

        return userId;
    }

    public static int GetRequiredTenantId(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(ClaimNames.TenantId);
        if (!int.TryParse(raw, out var tenantId))
        {
            throw new InvalidOperationException("TenantId claim no encontrado.");
        }

        return tenantId;
    }
}
