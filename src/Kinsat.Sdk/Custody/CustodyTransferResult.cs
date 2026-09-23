namespace Kinsat.Sdk.Custody;

public sealed record CustodyTransferResult(bool Succeeded, string? TransactionReference, string? FailureReason);
