using System;
using System.Collections.Generic;
using System.Text;
using Kinsat.Sdk.Identity;

namespace Kinsat.Core.Tests;

/// <summary>
/// Test-only identity provider. It exists to show the rule in action: even tests must
/// derive from IdentityProviderBase to mint a CallContext.
/// </summary>
internal sealed class TestIdentity : IdentityProviderBase
{
    public static CallContext Context(KinsatPrincipal principal) => Issue(principal);

    public override Task<AuthenticationResult> Authenticate(AuthenticationRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(AuthenticationResult.Failure("test identity does not authenticate"));

    public override Task<CallContext?> Validate(string tenantId, string token, CancellationToken cancellationToken = default) =>
        Task.FromResult<CallContext?>(null);
}
