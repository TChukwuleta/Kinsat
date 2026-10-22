using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Kinsat.Sdk.Custody;
using Kinsat.Sdk.Identity;

namespace Kinsat.Core.Custody;

public sealed class SimulatedCustodyProvider : ICustodyProvider
{
    private readonly ConcurrentDictionary<string, decimal> _confirmedSats = new();

    public Task<DepositAddress> GenerateDepositAddress(CallContext context, string loanApplicationId, CancellationToken cancellationToken = default)
    {
        var address = $"sim-addr-{loanApplicationId}";
        return Task.FromResult(new DepositAddress(address, loanApplicationId, DateTimeOffset.UtcNow));
    }

    public void SimulateDeposit(string loanId, decimal sats) => _confirmedSats.AddOrUpdate(loanId, sats, (_, existing) => existing + sats);

    public Task<CollateralBalance> GetBalance(CallContext context, string loanId, CancellationToken cancellationToken = default)
    {
        var confirmed = _confirmedSats.GetValueOrDefault(loanId);
        return Task.FromResult(new CollateralBalance(loanId, confirmed, UnconfirmedSats: 0, Confirmations: confirmed > 0 ? 6 : 0));
    }

    public Task<CustodyTransferResult> Release(CallContext context, string loanId, decimal sats, string destination, CancellationToken cancellationToken = default)
    {
        if (!TryDebit(loanId, sats))
            return Task.FromResult(new CustodyTransferResult(false, null, "Insufficient simulated collateral balance."));

        return Task.FromResult(new CustodyTransferResult(true, $"sim-tx-release-{Guid.NewGuid():N}", null));
    }

    public Task<CustodyTransferResult> Liquidate(CallContext context, string loanId, decimal sats, CancellationToken cancellationToken = default)
    {
        if (!TryDebit(loanId, sats))
            return Task.FromResult(new CustodyTransferResult(false, null, "Insufficient simulated collateral balance."));

        return Task.FromResult(new CustodyTransferResult(true, $"sim-tx-liquidate-{Guid.NewGuid():N}", null));
    }

    private bool TryDebit(string loanId, decimal sats)
    {
        var current = _confirmedSats.GetValueOrDefault(loanId);
        if (current < sats) return false;
        _confirmedSats[loanId] = current - sats;
        return true;
    }
}
