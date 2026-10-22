using System;
using System.Collections.Generic;
using System.Text;
using Kinsat.Sdk.Identity;

namespace Kinsat.Core.Identity;

/// <summary>
/// The sanctioned source of contexts for automated background processes (the LTV monitor,
/// future scheduled jobs). They are real actors but cannot log in interactively, so this
/// is a small identity provider of its own that issues only the AutomatedSystem role,
/// scoped to one tenant. It cannot be asked for any other role.
/// </summary>
public static class SystemActor
{
    private sealed class SystemIdentityProvider : IdentityProviderBase
    {
        public static CallContext For(string tenantId, string displayName) =>
            Issue(new KinsatPrincipal("system", tenantId, Role.AutomatedSystem, displayName));

        public override Task<AuthenticationResult> Authenticate(AuthenticationRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(AuthenticationResult.Failure("The system actor does not authenticate interactively."));

        public override Task<CallContext?> Validate(string tenantId, string token, CancellationToken cancellationToken = default) =>
            Task.FromResult<CallContext?>(null);
    }

    public static CallContext ForTenant(string tenantId, string displayName = "Automated System") =>
        SystemIdentityProvider.For(tenantId, displayName);
}
