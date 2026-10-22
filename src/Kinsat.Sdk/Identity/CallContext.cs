namespace Kinsat.Sdk.Identity;

/// <summary>
/// Proof that a call has been authenticated. Every core operation that reads or changes
/// tenant data takes a CallContext, and the intent is that the only way to obtain one is
/// an IIdentityProvider succeeding (AuthenticateAsync, or ValidateAsync for an existing
/// session).
///
/// KNOWN GAP, not yet fixed: From() below is public, and KinsatPrincipal's constructor is
/// public too, so any code that references Kinsat.Sdk can currently mint a CallContext
/// claiming any Role or TenantId, including SystemAdmin, without ever calling an
/// IIdentityProvider. The compile-time guarantee described above does not actually hold
/// today. A real fix needs a capability token that only a registered IIdentityProvider
/// receives at startup, not a public static factory, and is worth doing before any real
/// deployment. Until then, Security.Guard's tenant/role checks are the actual enforcement
/// point in this codebase, not CallContext's construction. See
/// Kinsat.Core.Identity.SystemActor for the one legitimate internal use of From() today
/// (automated background processes), kept in one named, documented place rather than
/// constructed ad hoc wherever an automated actor is needed.
/// </summary>
public sealed class CallContext
{
    public KinsatPrincipal Principal { get; }

    private CallContext(KinsatPrincipal principal) => Principal = principal;

    public static CallContext From(KinsatPrincipal principal) => new(principal);

    public string TenantId => Principal.TenantId;
    public Role Role => Principal.Role;

    /// <summary>
    /// True if this context may act on the given tenant's data: its own tenant, or any
    /// tenant if the caller is a SystemAdmin operating the whole deployment.
    /// </summary>
    public bool CanAccessTenant(string tenantId) =>
        Role == Role.SystemAdmin || string.Equals(TenantId, tenantId, StringComparison.Ordinal);
}
