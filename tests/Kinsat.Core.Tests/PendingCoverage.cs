using System;
using System.Collections.Generic;
using System.Text;

namespace Kinsat.Core.Tests;

/// <summary>
/// Deliberately failing/skipped placeholders, not real tests. Their job is to make
/// missing coverage visible in test output instead of silently absent: when a feature
/// below gets a real implementation, replace its Skip test here with a real test class,
/// and remove the corresponding line from this file.
///
/// The loan lifecycle itself, and the custody/scoring/pricing contracts it depends on,
/// are now implemented and tested (see LoanLifecycleTests.cs) against simulated defaults
/// (SimulatedCustodyProvider, SimulatedScoringProvider, SimulatedPriceOracle). What
/// remains pending is specifically the real, non-simulated implementations.
/// </summary>
public class PendingCoverage
{
    [Fact(Skip = "ICustodyProvider has no real implementation yet, only SimulatedCustodyProvider. BTCPay Server's Greenfield API is next up per README/ROADMAP.")]
    public void Custody_provider_real_btcpay_implementation() { }

    [Fact(Skip = "IScoringSignalProvider has no real on-chain/repayment scoring model yet, only SimulatedScoringProvider (a stable hash of the borrower id).")]
    public void Scoring_provider_real_on_chain_model() { }

    [Fact(Skip = "TestIdentity.Context() is public and KinsatPrincipal is freely constructible, so any code referencing Kinsat.Sdk can currently mint a CallContext claiming any role, " +
                 "including SystemAdmin, without going through an IIdentityProvider. See CallContext's own doc comment. Needs a capability-token redesign before real deployment.")]
    public void CallContext_construction_is_not_actually_gated() { }

    [Fact(Skip = "End-to-end test against real infrastructure (real BTCPay instance, real price feed) doesn't exist yet; the simulated-default version of this now exists in " +
                 "LoanLifecycleTests.cs and tests/Kinsat.Core.Smoketest/Program.cs.")]
    public void End_to_end_authentication_to_finalization_against_real_infrastructure() { }
}
