namespace Kinsat.Sdk.Identity;

public sealed record AuthenticationResult(bool Succeeded, CallContext? Context, string? Token, string? FailureReason)
{
    public KinsatPrincipal? Principal => Context?.Principal;

    public static AuthenticationResult Success(CallContext context, string token) => new(true, context, token, null);
    public static AuthenticationResult Failure(string reason) => new(false, null, null, reason);
}
