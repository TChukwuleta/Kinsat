using System.Collections.Concurrent;
using Kinsat.Sdk.Identity;

namespace Kinsat.Core.Identity;

public sealed class StandaloneIdentityProvider : IIdentityProvider
{
    private sealed record Credential(string Secret, KinsatPrincipal Principal);

    private readonly ConcurrentDictionary<string, Credential> _users = new();
    private readonly ConcurrentDictionary<string, KinsatPrincipal> _sessions = new();

    public void Register(string tenantId, string subject, string secret, Role role, string displayName)
    {
        var principal = new KinsatPrincipal(subject, tenantId, role, displayName);
        _users[UserKey(tenantId, subject)] = new Credential(secret, principal);
    }

    public Task<AuthenticationResult> Authenticate(AuthenticationRequest request, CancellationToken cancellationToken = default)
    {
        if (!_users.TryGetValue(UserKey(request.TenantId, request.Subject), out var credential) || credential.Secret != request.Credential)
            return Task.FromResult(AuthenticationResult.Failure("Invalid credentials."));

        var token = Guid.NewGuid().ToString("N");
        _sessions[token] = credential.Principal;
        return Task.FromResult(AuthenticationResult.Success(credential.Principal, token));
    }

    public Task<CallContext?> Validate(string tenantId, string token, CancellationToken cancellationToken = default)
    {
        if (_sessions.TryGetValue(token, out var principal) && principal.TenantId == tenantId)
            return Task.FromResult<CallContext?>(CallContext.From(principal));

        return Task.FromResult<CallContext?>(null);
    }

    private static string UserKey(string tenantId, string subject) => $"{tenantId}:{subject}";
}
