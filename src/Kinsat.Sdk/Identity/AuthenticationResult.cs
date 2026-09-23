namespace Kinsat.Sdk.Identity;

public sealed record AuthenticationResult(bool Succeeded, KinsatPrincipal? Principal, string? Token, string? FailureReason)
{
    public static AuthenticationResult Success(KinsatPrincipal principal, string token) => new(true, principal, token, null);
    public static AuthenticationResult Failure(string reason) => new(false, null, null, reason);
}
