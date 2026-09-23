namespace Kinsat.Sdk.Identity;

public sealed record KinsatPrincipal(
    string Subject,
    string TenantId,
    Role Role,
    string DisplayName);
