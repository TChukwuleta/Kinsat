using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Kinsat.Core.Security;
using Kinsat.Sdk.Identity;
using Kinsat.Sdk.Pricing;

namespace Kinsat.Core.Pricing;

public sealed class SimulatedPriceOracle : IPriceOracle
{
    private readonly ConcurrentDictionary<string, decimal> _prices = new();
    private const decimal DefaultPrice = 65000m;

    public void SetPrice(string tenantId, decimal btcPriceInSettlementCurrency) =>
        _prices[tenantId] = btcPriceInSettlementCurrency;

    public Task<decimal> GetBtcPriceAsync(CallContext context, string tenantId, CancellationToken cancellationToken = default)
    {
        Guard.RequireTenantAccess(context, tenantId);
        return Task.FromResult(_prices.GetValueOrDefault(tenantId, DefaultPrice));
    }
}
