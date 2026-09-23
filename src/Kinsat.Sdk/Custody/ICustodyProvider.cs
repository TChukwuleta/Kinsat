using Kinsat.Sdk.Identity;

namespace Kinsat.Sdk.Custody;

public interface ICustodyProvider
{
    Task<DepositAddress> GenerateDepositAddress(CallContext context, string loanApplicationId, CancellationToken cancellationToken = default);
    Task<CollateralBalance> GetBalance(CallContext context, string loanId, CancellationToken cancellationToken = default);
    Task<CustodyTransferResult> Release(CallContext context, string loanId, decimal sats, string destination, CancellationToken cancellationToken = default);
    Task<CustodyTransferResult> Liquidate(CallContext context, string loanId, decimal sats, CancellationToken cancellationToken = default);
}
