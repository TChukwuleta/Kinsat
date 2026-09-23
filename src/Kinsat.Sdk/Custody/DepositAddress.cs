namespace Kinsat.Sdk.Custody;

public sealed record DepositAddress(string Address, string LoanApplicationId, DateTimeOffset GeneratedAtUtc);