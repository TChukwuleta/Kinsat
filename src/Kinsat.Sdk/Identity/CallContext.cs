namespace Kinsat.Sdk.Identity;

/// <summary>
/// Proof that a call has been authenticated. Every core operation that reads or changes
/// tenant data takes a CallContext. There is no public constructor and no public factory:
/// the only code that can mint one is a class deriving from IdentityProviderBase, which
/// exposes a protected Issue method. So holding a CallContext means an identity provider
/// vouched for it, and a stray piece of code cannot simply declare itself a SystemAdmin.
///
/// What this does and does not stop: it removes accidental and casual forgery at compile
/// time, since you must write a class that derives from IdentityProviderBase to issue
/// anything. It does not stop a hostile extension that deliberately subclasses the base,
/// or reflection. The host decides which identity providers are registered, so the trust
/// boundary is "which providers did the operator install". Signing principals so the
/// host can verify them is the later hardening step for closed third-party extensions.
/// Security.Guard still enforces tenant and role checks on every operation regardless.
/// </summary>
public sealed class CallContext
{
    public KinsatPrincipal Principal { get; }

    internal CallContext(KinsatPrincipal principal) => Principal = principal;

    public string TenantId => Principal.TenantId;
    public Role Role => Principal.Role;

    /// <summary>
    /// True if this context may act on the given tenant's data: its own tenant, or any
    /// tenant if the caller is a SystemAdmin operating the whole deployment.
    /// </summary>
    public bool CanAccessTenant(string tenantId) =>
        Role == Role.SystemAdmin || string.Equals(TenantId, tenantId, StringComparison.Ordinal);
}
