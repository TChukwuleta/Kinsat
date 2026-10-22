using System.Collections.Concurrent;
using Kinsat.Sdk.Identity;

namespace Kinsat.Core.Identity;

/// <summary>
/// Default IIdentityProvider for a standalone core deployment: shared-secret credentials
/// held in memory. This is intentionally minimal, its only job is to prove the contract
/// and give a self-hoster something that works out of the box. A merchant extension
/// replaces this wholesale with their own SSO/IAM (or a KYC vendor's session system) by
/// implementing IIdentityProvider; nothing else in core changes when that swap happens,
/// since core only ever depends on the interface.
/// </summary>
public sealed class StandaloneIdentityProvider : IdentityProviderBase
{
    private sealed record Credential(string Secret, KinsatPrincipal Principal);

    private readonly ConcurrentDictionary<string, Credential> _users = new();
    private readonly ConcurrentDictionary<string, CallContext> _sessions = new();

    public void Register(string tenantId, string subject, string secret, Role role, string displayName)
    {
        var principal = new KinsatPrincipal(subject, tenantId, role, displayName);
        _users[UserKey(tenantId, subject)] = new Credential(secret, principal);
    }

    public override Task<AuthenticationResult> Authenticate(AuthenticationRequest request, CancellationToken cancellationToken = default)
    {
        if (!_users.TryGetValue(UserKey(request.TenantId, request.Subject), out var credential) || credential.Secret != request.Credential)
            return Task.FromResult(AuthenticationResult.Failure("Invalid credentials."));

        var token = Guid.NewGuid().ToString("N");
        var context = Issue(credential.Principal);
        _sessions[token] = context;
        return Task.FromResult(AuthenticationResult.Success(context, token));
    }

    public override Task<CallContext?> Validate(string tenantId, string token, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(token, out var context) && context.TenantId == tenantId)
            return Task.FromResult<CallContext?>(context);

        return Task.FromResult<CallContext?>(null);
    }

    private static string UserKey(string tenantId, string subject) => $"{tenantId}:{subject}";
}
