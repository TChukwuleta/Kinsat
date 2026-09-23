namespace Kinsat.Sdk.Custody;

public sealed record CollateralBalance(string LoanId, decimal ConfirmedSats, decimal UnconfirmedSats, int Confirmations);