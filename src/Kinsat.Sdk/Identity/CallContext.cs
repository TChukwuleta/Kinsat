namespace Kinsat.Sdk.Identity;

public sealed class CallContext
{
    public KinsatPrincipal Principal { get; }

    private CallContext(KinsatPrincipal principal) => Principal = principal;

    public static CallContext From(KinsatPrincipal principal) => new(principal);

    public string TenantId => Principal.TenantId;
    public Role Role => Principal.Role;
    public bool CanAccessTenant(string tenantId) =>
        Role == Role.SystemAdmin || string.Equals(TenantId, tenantId, StringComparison.Ordinal);
}
