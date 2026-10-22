using System;
using System.Collections.Generic;
using System.Text;
using Kinsat.Core.Custody;
using Kinsat.Core.Events;
using Kinsat.Core.Loans;
using Kinsat.Core.Pricing;
using Kinsat.Core.Scoring;
using Kinsat.Sdk.Events;
using Kinsat.Sdk.Identity;
using Kinsat.Sdk.Loans;

namespace Kinsat.Core.Tests;

/// <summary>
/// Exercises the full lifecycle against the simulated custody/scoring/pricing defaults.
/// Borrower ids below are pinned to scores already confirmed against the real
/// SimulatedScoringProvider implementation (see tests/Kinsat.Core.Smoketest/Program.cs,
/// which runs these same scenarios network-free and is the source of truth these
/// borrower-id/threshold pairings were taken from): borrower-bola=24, borrower-ada=29,
/// borrower-safe=93, borrower-margin-1=99.
/// </summary>
public class LoanLifecycleTests
{
    private static (LoanLifecycleService Lifecycle, LoanRepository Repository, SimulatedCustodyProvider Custody, SimulatedPriceOracle Price, InProcessEventBus Events, List<string> Published) NewHarness()
    {
        var custody = new SimulatedCustodyProvider();
        var price = new SimulatedPriceOracle();
        price.SetPrice("mfi-lagos", 100000m);
        var aggregator = new ScoringAggregator([new SimulatedScoringProvider()]);
        var repository = new LoanRepository();
        var events = new InProcessEventBus();
        var published = new List<string>();

        events.Subscribe<CollateralDeposited>((e, _) => { published.Add($"CollateralDeposited:{e.LoanId}"); return Task.CompletedTask; });
        events.Subscribe<LoanOriginated>((e, _) => { published.Add($"LoanOriginated:{e.LoanId}"); return Task.CompletedTask; });
        events.Subscribe<LtvBreached>((e, _) => { published.Add($"LtvBreached:{e.LoanId}"); return Task.CompletedTask; });
        events.Subscribe<MarginCallTriggered>((e, _) => { published.Add($"MarginCallTriggered:{e.LoanId}"); return Task.CompletedTask; });
        events.Subscribe<LiquidationExecuted>((e, _) => { published.Add($"LiquidationExecuted:{e.LoanId}"); return Task.CompletedTask; });
        events.Subscribe<RepaymentReceived>((e, _) => { published.Add($"RepaymentReceived:{e.LoanId}"); return Task.CompletedTask; });
        events.Subscribe<CreditScoreUpdated>((e, _) => { published.Add($"CreditScoreUpdated:{e.BorrowerId}"); return Task.CompletedTask; });

        var lifecycle = new LoanLifecycleService(repository, custody, aggregator, price, events);
        return (lifecycle, repository, custody, price, events, published);
    }

    private static LoanProduct StarterProduct(TimeSpan? cureWindow = null) => new(
        ProductId: "starter-loan", TenantId: "mfi-lagos", Name: "Starter Bitcoin Loan", Currency: "NGN",
        MaxLtv: 0.5m, MarginCallLtv: 0.65m, LiquidationLtv: 0.80m,
        MarginCallCureWindow: cureWindow ?? TimeSpan.Zero, AutoApproveScoreThreshold: 50, AutoRejectScoreThreshold: 25);

    private static CallContext RootContext() =>
        TestIdentity.Context(new KinsatPrincipal("root", "platform", Role.SystemAdmin, "Root"));

    [Fact]
    public async Task Low_scoring_borrower_is_auto_rejected()
    {
        var (lifecycle, _, _, _, _, _) = NewHarness();
        var app = await lifecycle.SubmitApplicationAsync(RootContext(), StarterProduct(), "borrower-bola", 500m);

        Assert.Equal(LoanStatus.Rejected, app.Status);
        Assert.Equal(UnderwritingDecision.AutoReject, app.Decision);
    }

    [Fact]
    public async Task Mid_scoring_borrower_is_routed_to_manual_review()
    {
        var (lifecycle, _, _, _, _, _) = NewHarness();
        var app = await lifecycle.SubmitApplicationAsync(RootContext(), StarterProduct(), "borrower-ada", 500m);

        Assert.Equal(LoanStatus.UnderReview, app.Status);
        Assert.Equal(UnderwritingDecision.ManualReview, app.Decision);
    }

    [Fact]
    public async Task Loan_officer_can_clear_manual_review()
    {
        var (lifecycle, _, _, _, _, _) = NewHarness();
        var app = await lifecycle.SubmitApplicationAsync(RootContext(), StarterProduct(), "borrower-ada", 500m);

        var officerContext = TestIdentity.Context(new KinsatPrincipal("officer.ada", "mfi-lagos", Role.LoanOfficer, "Ada"));
        var approved = await lifecycle.ApproveApplicationAsync(officerContext, app.Id);

        Assert.Equal(LoanStatus.CollateralPending, approved.Status);
    }

    [Fact]
    public async Task Different_tenants_staff_cannot_approve_another_tenants_application()
    {
        var (lifecycle, _, _, _, _, _) = NewHarness();
        var app = await lifecycle.SubmitApplicationAsync(RootContext(), StarterProduct(), "borrower-ada", 500m);

        var abujaContext = TestIdentity.Context(new KinsatPrincipal("admin.chidi", "mfi-abuja", Role.MerchantAdmin, "Chidi"));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => lifecycle.ApproveApplicationAsync(abujaContext, app.Id));
    }

    [Fact]
    public async Task Confirmed_collateral_within_max_ltv_disburses_the_loan()
    {
        var (lifecycle, _, custody, _, _, published) = NewHarness();
        var product = StarterProduct();
        var context = RootContext();

        var app = await lifecycle.SubmitApplicationAsync(context, product, "borrower-safe", 500m);
        await lifecycle.RequestCollateralDepositAsync(context, app.Id);
        custody.SimulateDeposit(app.Id, 2_000_000m); // 0.02 BTC at 100,000 -> collateral value 2,000 vs 500 requested

        var loan = await lifecycle.ConfirmCollateralAndDisburse(context, product, app.Id);

        Assert.Equal(LoanStatus.Active, loan.Status);
        Assert.True(Math.Abs(loan.CurrentLtv - 0.25m) < 0.001m);
        Assert.Contains($"CollateralDeposited:{app.Id}", published);
        Assert.Contains($"LoanOriginated:{app.Id}", published);
    }

    [Fact]
    public async Task Collateral_above_max_ltv_is_refused()
    {
        var (lifecycle, _, custody, _, _, _) = NewHarness();
        var product = StarterProduct();
        var context = RootContext();

        var app = await lifecycle.SubmitApplicationAsync(context, product, "borrower-safe", 500m);
        await lifecycle.RequestCollateralDepositAsync(context, app.Id);
        custody.SimulateDeposit(app.Id, 200_000m); // far too little collateral for a 0.5 max LTV at this price

        await Assert.ThrowsAsync<InvalidOperationException>(() => lifecycle.ConfirmCollateralAndDisburse(context, product, app.Id));
    }

    [Fact]
    public async Task Full_repayment_closes_the_loan_and_releases_collateral()
    {
        var (lifecycle, _, custody, _, _, published) = NewHarness();
        var product = StarterProduct();
        var context = RootContext();

        var app = await lifecycle.SubmitApplicationAsync(context, product, "borrower-safe", 500m);
        await lifecycle.RequestCollateralDepositAsync(context, app.Id);
        custody.SimulateDeposit(app.Id, 2_000_000m);
        await lifecycle.ConfirmCollateralAndDisburse(context, product, app.Id);

        var closed = await lifecycle.Repay(context, product, app.Id, 500m);

        Assert.Equal(LoanStatus.Closed, closed.Status);
        Assert.Equal(0, closed.CollateralSats);

        var balance = await custody.GetBalance(context, app.Id);
        Assert.Equal(0, balance.ConfirmedSats);
        Assert.Contains($"RepaymentReceived:{app.Id}", published);
        Assert.Contains("CreditScoreUpdated:borrower-safe", published);
    }

    [Fact]
    public async Task Price_drop_past_margin_tier_triggers_a_margin_call()
    {
        var (lifecycle, _, custody, price, _, published) = NewHarness();
        var product = StarterProduct();
        var context = RootContext();

        var app = await lifecycle.SubmitApplicationAsync(context, product, "borrower-margin-1", 500m);
        await lifecycle.RequestCollateralDepositAsync(context, app.Id);
        custody.SimulateDeposit(app.Id, 2_000_000m);
        await lifecycle.ConfirmCollateralAndDisburse(context, product, app.Id);

        price.SetPrice("mfi-lagos", 35000m); // LTV moves to ~0.71, above the 0.65 margin tier
        var loan = await lifecycle.RecomputeLtv(context, product, app.Id);

        Assert.Equal(LoanStatus.MarginCalled, loan.Status);
        Assert.NotNull(loan.MarginCallCureBy);
        Assert.Contains($"LtvBreached:{app.Id}", published);
        Assert.Contains($"MarginCallTriggered:{app.Id}", published);
    }

    [Fact]
    public async Task Liquidation_sells_only_enough_collateral_to_restore_target_ltv()
    {
        var (lifecycle, _, custody, price, _, published) = NewHarness();
        var product = StarterProduct(TimeSpan.Zero); // expires immediately so liquidation can proceed in-test
        var context = RootContext();

        var app = await lifecycle.SubmitApplicationAsync(context, product, "borrower-margin-1", 500m);
        await lifecycle.RequestCollateralDepositAsync(context, app.Id);
        custody.SimulateDeposit(app.Id, 2_000_000m);
        await lifecycle.ConfirmCollateralAndDisburse(context, product, app.Id);

        price.SetPrice("mfi-lagos", 35000m);
        await lifecycle.RecomputeLtv(context, product, app.Id);

        var loan = await lifecycle.Liquidate(context, product, app.Id);

        Assert.Equal(LoanStatus.Active, loan.Status);
        Assert.True(loan.CollateralSats > 0, "liquidation should not have sold every sat of collateral");
        Assert.True(Math.Abs(loan.CurrentLtv - product.MaxLtv) < 0.01m, "post-liquidation LTV should land back near the product's max");
        Assert.Contains($"LiquidationExecuted:{app.Id}", published);
    }

    [Fact]
    public async Task Liquidation_before_cure_window_expires_is_refused()
    {
        var (lifecycle, _, custody, price, _, _) = NewHarness();
        var product = StarterProduct(TimeSpan.FromHours(24)); // real cure window this time
        var context = RootContext();

        var app = await lifecycle.SubmitApplicationAsync(context, product, "borrower-margin-1", 500m);
        await lifecycle.RequestCollateralDepositAsync(context, app.Id);
        custody.SimulateDeposit(app.Id, 2_000_000m);
        await lifecycle.ConfirmCollateralAndDisburse(context, product, app.Id);

        price.SetPrice("mfi-lagos", 35000m);
        await lifecycle.RecomputeLtv(context, product, app.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => lifecycle.Liquidate(context, product, app.Id));
    }
}
