using Kinsat.Sdk.Identity;

namespace Kinsat.Core.Security;

public static class Guard
{
    public static void RequireTenantAccess(CallContext context, string tenantId)
    {
        if (!context.CanAccessTenant(tenantId))
            throw new UnauthorizedAccessException(
                $"Principal '{context.Principal.Subject}' (tenant '{context.TenantId}') may not access tenant '{tenantId}'.");
    }

    public static void RequireRole(CallContext context, params Role[] allowed)
    {
        if (Array.IndexOf(allowed, context.Role) < 0)
            throw new UnauthorizedAccessException(
                $"Principal '{context.Principal.Subject}' has role '{context.Role}', which is not permitted for this operation.");
    }
}
