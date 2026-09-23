namespace Kinsat.Sdk.Identity;

public interface IIdentityProvider
{
    Task<AuthenticationResult> Authenticate(AuthenticationRequest request, CancellationToken cancellationToken = default);

    Task<CallContext?> Validate(string tenantId, string token, CancellationToken cancellationToken = default);
}