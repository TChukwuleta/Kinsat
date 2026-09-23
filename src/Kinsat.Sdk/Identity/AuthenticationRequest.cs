namespace Kinsat.Sdk.Identity;

public sealed record AuthenticationRequest(string TenantId, string Subject, string Scheme, string Credential);