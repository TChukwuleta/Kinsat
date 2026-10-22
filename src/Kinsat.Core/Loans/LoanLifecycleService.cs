using System;
using System.Collections.Generic;
using System.Text;
using Kinsat.Core.Scoring;
using Kinsat.Core.Security;
using Kinsat.Sdk.Custody;
using Kinsat.Sdk.Events;
using Kinsat.Sdk.Identity;
using Kinsat.Sdk.Loans;
using Kinsat.Sdk.Pricing;
using Kinsat.Sdk.Scoring;

namespace Kinsat.Core.Loans;

/// <summary>
/// The loan lifecycle state machine (BRD Section 7): application through underwriting,
/// collateral, disbursement, LTV monitoring, margin calls, liquidation or repayment, and
/// closure, publishing the Kinsat.Sdk.Events domain events at each transition. It depends
/// on ICustodyProvider, IScoringSignalProvider (via ScoringAggregator), and IPriceOracle
/// only through their interfaces, never on a concrete implementation, so it works
/// identically whether it's wired to the simulated defaults or a real BTCPay/pricing/
/// scoring stack later.
///
/// One honest simplification: repayment and margin-call cure actions don't yet verify the
/// caller is the specific borrower who owns the loan, only that they're in the right
/// tenant. Borrower-to-loan ownership enforcement is a real gap, not an oversight, and
/// belongs in Security.Guard alongside the checks that already exist there.
/// </summary>
public sealed class LoanLifecycleService(
    LoanRepository repository,
    ICustodyProvider custody,
    ScoringAggregator scoring,
    IPriceOracle priceOracle,
    IEventBus events)
{
    private const decimal SatsPerBtc = 100_000_000m;

    public async Task<LoanApplication> SubmitApplicationAsync(
        CallContext context, LoanProduct product, string borrowerId, decimal requestedAmount, CancellationToken cancellationToken = default)
    {
        Guard.RequireTenantAccess(context, product.TenantId);

        var application = new LoanApplication
        {
            Id = Guid.NewGuid().ToString("N"),
            TenantId = product.TenantId,
            BorrowerId = borrowerId,
            ProductId = product.ProductId,
            RequestedAmount = requestedAmount,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        var (combinedScore, _) = await scoring.ScoreAsync(
            context, new ScoringContext(product.TenantId, borrowerId, new Dictionary<string, string>()), cancellationToken);

        application.CombinedScore = combinedScore;

        application.Decision = combinedScore <= product.AutoRejectScoreThreshold ? UnderwritingDecision.AutoReject
            : combinedScore >= product.AutoApproveScoreThreshold ? UnderwritingDecision.AutoApprove
            : UnderwritingDecision.ManualReview;

        application.Status = application.Decision switch
        {
            UnderwritingDecision.AutoReject => LoanStatus.Rejected,
            UnderwritingDecision.AutoApprove => LoanStatus.CollateralPending,
            _ => LoanStatus.UnderReview
        };

        repository.SaveApplication(application);
        return application;
    }

    /// <summary>A loan officer clears an application that landed in manual review (FR-3.3).</summary>
    public Task<LoanApplication> ApproveApplicationAsync(CallContext context, string applicationId, CancellationToken cancellationToken = default)
    {
        Guard.RequireRole(context, Role.LoanOfficer, Role.MerchantAdmin, Role.SystemAdmin);

        var application = repository.GetApplication(context, applicationId);
        if (application.Status != LoanStatus.UnderReview)
            throw new InvalidOperationException($"Application '{applicationId}' is not awaiting manual review (status: {application.Status}).");

        application.Status = LoanStatus.CollateralPending;
        repository.SaveApplication(application);
        return Task.FromResult(application);
    }

    public async Task<DepositAddress> RequestCollateralDepositAsync(CallContext context, string applicationId, CancellationToken cancellationToken = default)
    {
        var application = repository.GetApplication(context, applicationId);
        if (application.Status != LoanStatus.CollateralPending)
            throw new InvalidOperationException($"Application '{applicationId}' is not ready for a collateral deposit (status: {application.Status}).");

        return await custody.GenerateDepositAddress(context, applicationId, cancellationToken);
    }

    /// <summary>
    /// Checks confirmed collateral against the requested amount; if the resulting LTV is
    /// within the product's max, disburses (FR-4.3, FR-5.1) and publishes
    /// CollateralDeposited and LoanOriginated.
    /// </summary>
    public async Task<Loan> ConfirmCollateralAndDisburse(
        CallContext context, LoanProduct product, string applicationId, CancellationToken cancellationToken = default)
    {
        Guard.RequireRole(context, Role.AutomatedSystem, Role.LoanOfficer, Role.MerchantAdmin, Role.SystemAdmin);

        var application = repository.GetApplication(context, applicationId);
        if (application.Status != LoanStatus.CollateralPending)
            throw new InvalidOperationException($"Application '{applicationId}' is not awaiting collateral (status: {application.Status}).");

        var balance = await custody.GetBalance(context, applicationId, cancellationToken);
        var price = await priceOracle.GetBtcPriceAsync(context, product.TenantId, cancellationToken);
        var collateralValue = balance.ConfirmedSats / SatsPerBtc * price;
        var initialLtv = collateralValue == 0 ? decimal.MaxValue : application.RequestedAmount / collateralValue;

        if (initialLtv > product.MaxLtv)
            throw new InvalidOperationException(
                $"Confirmed collateral gives an initial LTV of {initialLtv:P0}, above the product maximum of {product.MaxLtv:P0}.");

        var loan = new Loan
        {
            Id = applicationId,
            TenantId = application.TenantId,
            BorrowerId = application.BorrowerId,
            ProductId = application.ProductId,
            PrincipalAmount = application.RequestedAmount,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Status = LoanStatus.Active,
            OutstandingBalance = application.RequestedAmount,
            CollateralSats = balance.ConfirmedSats,
            CurrentLtv = initialLtv
        };

        repository.SaveLoan(loan);

        await events.Publish(new CollateralDeposited(loan.TenantId, DateTimeOffset.UtcNow, loan.Id, balance.ConfirmedSats), cancellationToken);
        await events.Publish(new LoanOriginated(loan.TenantId, DateTimeOffset.UtcNow, loan.Id, loan.BorrowerId), cancellationToken);

        return loan;
    }

    /// <summary>
    /// Recomputes LTV against the current price (FR-6.1) and, on a breach, issues a
    /// margin call with a cure deadline (FR-6.2). Intended to be called on a schedule,
    /// e.g. by Kinsat.Api's LtvMonitorService, for every active loan.
    /// </summary>
    public async Task<Loan> RecomputeLtv(CallContext context, LoanProduct product, string loanId, CancellationToken cancellationToken = default)
    {
        Guard.RequireRole(context, Role.AutomatedSystem, Role.LoanOfficer, Role.MerchantAdmin, Role.SystemAdmin);

        var loan = repository.GetLoan(context, loanId);
        if (loan.Status is not (LoanStatus.Active or LoanStatus.MarginCalled))
            return loan;

        var price = await priceOracle.GetBtcPriceAsync(context, product.TenantId, cancellationToken);
        var collateralValue = loan.CollateralSats / SatsPerBtc * price;
        loan.CurrentLtv = collateralValue == 0 ? decimal.MaxValue : loan.OutstandingBalance / collateralValue;

        if (loan.CurrentLtv >= product.MarginCallLtv && loan.Status == LoanStatus.Active)
        {
            loan.Status = LoanStatus.MarginCalled;
            loan.MarginCallCureBy = DateTimeOffset.UtcNow.Add(product.MarginCallCureWindow);

            await events.Publish(new LtvBreached(loan.TenantId, DateTimeOffset.UtcNow, loan.Id, loan.CurrentLtv, "margin-call"), cancellationToken);
            await events.Publish(new MarginCallTriggered(loan.TenantId, DateTimeOffset.UtcNow, loan.Id, loan.MarginCallCureBy.Value), cancellationToken);
        }
        else if (loan.CurrentLtv < product.MarginCallLtv && loan.Status == LoanStatus.MarginCalled)
        {
            // Price recovered or the borrower topped up enough that the next recompute
            // clears the call without a separate cure action being needed.
            loan.Status = LoanStatus.Active;
            loan.MarginCallCureBy = null;
        }

        repository.SaveLoan(loan);
        return loan;
    }

    /// <summary>
    /// Re-checks confirmed collateral (a borrower top-up looks like any other deposit to
    /// custody) and clears the margin call if LTV is back within range (FR-6.3).
    /// </summary>
    public async Task<Loan> CureMarginCall(CallContext context, LoanProduct product, string loanId, CancellationToken cancellationToken = default)
    {
        var loan = repository.GetLoan(context, loanId);
        if (loan.Status != LoanStatus.MarginCalled)
            throw new InvalidOperationException($"Loan '{loanId}' has no active margin call to cure.");

        var balance = await custody.GetBalance(context, loanId, cancellationToken);
        loan.CollateralSats = balance.ConfirmedSats;

        var price = await priceOracle.GetBtcPriceAsync(context, product.TenantId, cancellationToken);
        var collateralValue = loan.CollateralSats / SatsPerBtc * price;
        loan.CurrentLtv = collateralValue == 0 ? decimal.MaxValue : loan.OutstandingBalance / collateralValue;

        if (loan.CurrentLtv < product.MarginCallLtv)
        {
            loan.Status = LoanStatus.Active;
            loan.MarginCallCureBy = null;
        }

        repository.SaveLoan(loan);
        return loan;
    }

    /// <summary>
    /// Liquidates enough collateral to bring LTV back to the product's max, not the
    /// borrower's entire position (FR-6.4). Only acts on a margin call whose cure window
    /// has actually expired.
    /// </summary>
    public async Task<Loan> Liquidate(CallContext context, LoanProduct product, string loanId, CancellationToken cancellationToken = default)
    {
        Guard.RequireRole(context, Role.AutomatedSystem, Role.MerchantAdmin, Role.SystemAdmin);

        var loan = repository.GetLoan(context, loanId);
        if (loan.Status != LoanStatus.MarginCalled)
            throw new InvalidOperationException($"Loan '{loanId}' is not under a margin call.");
        if (loan.MarginCallCureBy is null || DateTimeOffset.UtcNow < loan.MarginCallCureBy)
            throw new InvalidOperationException($"Loan '{loanId}' is still within its cure window.");

        var price = await priceOracle.GetBtcPriceAsync(context, product.TenantId, cancellationToken);
        var valuePerSat = price / SatsPerBtc;

        var satsToLiquidate = SatsNeededToRestoreTargetLtv(loan.OutstandingBalance, loan.CollateralSats, valuePerSat, product.MaxLtv);
        satsToLiquidate = Math.Clamp(satsToLiquidate, 0, loan.CollateralSats);

        var result = await custody.Liquidate(context, loanId, satsToLiquidate, cancellationToken);
        if (!result.Succeeded)
            throw new InvalidOperationException($"Liquidation failed: {result.FailureReason}");

        loan.CollateralSats -= satsToLiquidate;
        loan.OutstandingBalance -= satsToLiquidate * valuePerSat;

        await events.Publish(new LiquidationExecuted(loan.TenantId, DateTimeOffset.UtcNow, loan.Id, satsToLiquidate), cancellationToken);

        if (loan.OutstandingBalance <= 0)
        {
            if (loan.CollateralSats > 0)
                await custody.Release(context, loanId, loan.CollateralSats, $"borrower:{loan.BorrowerId}", cancellationToken);

            loan.Status = LoanStatus.Closed;
            loan.CollateralSats = 0;
            loan.MarginCallCureBy = null;
            await events.Publish(new CreditScoreUpdated(loan.TenantId, DateTimeOffset.UtcNow, loan.BorrowerId, NewScore: 20), cancellationToken);
        }
        else
        {
            loan.Status = LoanStatus.Active;
            loan.MarginCallCureBy = null;
            var collateralValue = loan.CollateralSats / SatsPerBtc * price;
            loan.CurrentLtv = collateralValue == 0 ? decimal.MaxValue : loan.OutstandingBalance / collateralValue;
        }

        repository.SaveLoan(loan);
        return loan;
    }

    /// <summary>Solves for how many sats to sell so post-sale LTV lands at targetLtv (FR-6.4).</summary>
    private static decimal SatsNeededToRestoreTargetLtv(decimal outstandingBalance, decimal collateralSats, decimal valuePerSat, decimal targetLtv)
    {
        if (valuePerSat <= 0 || targetLtv >= 1) return collateralSats;

        var numerator = outstandingBalance - targetLtv * collateralSats * valuePerSat;
        var denominator = valuePerSat * (1 - targetLtv);
        if (denominator <= 0) return collateralSats;

        var sats = numerator / denominator;
        return sats < 0 ? 0 : sats;
    }

    /// <summary>
    /// Applies a repayment (FR-7.1), curing a margin call it happens to resolve, and
    /// closes the loan with full collateral release on full repayment (FR-7.3, FR-7.4).
    /// </summary>
    public async Task<Loan> Repay(CallContext context, LoanProduct product, string loanId, decimal amount, CancellationToken cancellationToken = default)
    {
        var loan = repository.GetLoan(context, loanId);
        if (loan.Status is not (LoanStatus.Active or LoanStatus.MarginCalled))
            throw new InvalidOperationException($"Loan '{loanId}' is not open for repayment (status: {loan.Status}).");

        loan.OutstandingBalance -= amount;
        await events.Publish(new RepaymentReceived(loan.TenantId, DateTimeOffset.UtcNow, loan.Id, amount), cancellationToken);

        if (loan.OutstandingBalance <= 0)
        {
            if (loan.CollateralSats > 0)
                await custody.Release(context, loanId, loan.CollateralSats, $"borrower:{loan.BorrowerId}", cancellationToken);

            loan.Status = LoanStatus.Closed;
            loan.CollateralSats = 0;
            loan.MarginCallCureBy = null;
            await events.Publish(new CreditScoreUpdated(loan.TenantId, DateTimeOffset.UtcNow, loan.BorrowerId, NewScore: 90), cancellationToken);
        }
        else if (loan.Status == LoanStatus.MarginCalled)
        {
            var price = await priceOracle.GetBtcPriceAsync(context, product.TenantId, cancellationToken);
            var collateralValue = loan.CollateralSats / SatsPerBtc * price;
            loan.CurrentLtv = collateralValue == 0 ? decimal.MaxValue : loan.OutstandingBalance / collateralValue;

            if (loan.CurrentLtv < product.MarginCallLtv)
            {
                loan.Status = LoanStatus.Active;
                loan.MarginCallCureBy = null;
            }
        }

        repository.SaveLoan(loan);
        return loan;
    }
}
