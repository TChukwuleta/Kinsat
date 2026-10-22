using System;
using System.Collections.Generic;
using System.Text;

namespace Kinsat.Sdk.Identity;

/// <summary>
/// Base class for every identity provider, including a merchant's own SSO or IAM bridge.
/// It is the only route to a CallContext: Issue is protected, so only a derived provider
/// can mint one, and only for a principal it has actually authenticated.
/// </summary>
public abstract class IdentityProviderBase : IIdentityProvider
{
    public abstract Task<AuthenticationResult> Authenticate(AuthenticationRequest request, CancellationToken cancellationToken = default);

    public abstract Task<CallContext?> Validate(string tenantId, string token, CancellationToken cancellationToken = default);

    /// <summary>Mint a CallContext for a principal this provider has verified.</summary>
    protected static CallContext Issue(KinsatPrincipal principal) => new(principal);
}
