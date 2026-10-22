using System;
using System.Collections.Generic;
using System.Text;
using Kinsat.Sdk.Identity;

namespace Kinsat.Sdk.Pricing;


/// <summary>
/// The price feed LTV monitoring depends on (A-3 in the BRD flags this as a real
/// dependency worth stress-testing). A tenant's settlement currency decides the pair;
/// the default deployment can use one implementation for every tenant, or a tenant can
/// configure its own if it needs a different venue or fallback behaviour.
///
/// tenantId is explicit rather than inferred from context.TenantId on purpose: a
/// SystemAdmin's own tenant is the platform itself, not any one merchant, so inferring
/// the target tenant from the caller's principal silently resolves the wrong tenant's
/// price whenever a cross-tenant caller acts on a specific merchant's behalf. Callers
/// pass the tenant the operation actually concerns (e.g. the loan's product.TenantId).
/// </summary>
public interface IPriceOracle
{
    /// <summary>Current price of 1 BTC in the given tenant's settlement currency.</summary>
    Task<decimal> GetBtcPriceAsync(CallContext context, string tenantId, CancellationToken cancellationToken = default);
}
