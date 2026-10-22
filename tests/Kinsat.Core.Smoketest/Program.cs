using Kinsat.Core.Configuration;
using Kinsat.Core.Custody;
using Kinsat.Core.Events;
using Kinsat.Core.Identity;
using Kinsat.Core.Loans;
using Kinsat.Core.Pricing;
using Kinsat.Core.Scoring;
using Kinsat.Sdk.Configuration;
using Kinsat.Sdk.Events;
using Kinsat.Sdk.Identity;
using Kinsat.Sdk.Loans;

var failures = new List<string>();

void Check(string name, bool condition)
{
    Console.WriteLine($"{(condition ? "PASS" : "FAIL")}  {name}");
    if (!condition) failures.Add(name);
}

// --- Identity: default standalone provider stands in for whatever a merchant would plug in ---
var identity = new StandaloneIdentityProvider();
identity.Register("mfi-lagos", "officer.ada", "s3cret", Role.LoanOfficer, "Ada, Loan Officer");
identity.Register("mfi-lagos", "admin.bola", "s3cret", Role.MerchantAdmin, "Bola, Merchant Admin");
identity.Register("mfi-abuja", "admin.chidi", "s3cret", Role.MerchantAdmin, "Chidi, Merchant Admin");
identity.Register("platform", "root.tobe", "s3cret", Role.SystemAdmin, "Tobe, System Admin");

var rejected = await identity.Authenticate(new AuthenticationRequest("mfi-lagos", "officer.ada", "password", "wrong-secret"));
Check("wrong credential is rejected", !rejected.Succeeded);

var officerAuth = await identity.Authenticate(new AuthenticationRequest("mfi-lagos", "officer.ada", "password", "s3cret"));
Check("correct credential authenticates", officerAuth.Succeeded && officerAuth.Principal is not null);

var officerContext = officerAuth.Context!;
Check("resulting context is scoped to the right tenant", officerContext.TenantId == "mfi-lagos");
Check("a tenant user cannot access another tenant", !officerContext.CanAccessTenant("mfi-abuja"));

var revalidated = await identity.Validate("mfi-lagos", officerAuth.Token!);
Check("an issued token re-validates into a fresh context", revalidated is not null);

// --- Config: tiered resolution, gated entirely by CallContext ---
var config = new ConfigurationEngine();

var adminAuth = await identity.Authenticate(new AuthenticationRequest("mfi-lagos", "admin.bola", "password", "s3cret"));
var adminContext = adminAuth.Context!;

var rootAuth = await identity.Authenticate(new AuthenticationRequest("platform", "root.tobe", "password", "s3cret"));
var rootContext = rootAuth.Context!;

var deniedSystemWrite = false;
try
{
    // A merchant admin, however senior within their own tenant, still cannot set platform-wide defaults.
    await config.Set(adminContext, ConfigScope.System, "ltv.warning-threshold", 0.99m);
}
catch (UnauthorizedAccessException)
{
    deniedSystemWrite = true;
}
Check("a merchant admin cannot set system-scope configuration", deniedSystemWrite);

await config.Set(rootContext, ConfigScope.System, "ltv.warning-threshold", 0.70m);
await config.Set(adminContext, ConfigScope.Tenant, "ltv.warning-threshold", 0.75m);
await config.Set(adminContext, ConfigScope.Product, "ltv.warning-threshold", 0.80m, productId: "starter-loan");

var productLevel = await config.Resolve<decimal>(adminContext, "ltv.warning-threshold", productId: "starter-loan");
Check("product scope overrides tenant and system", productLevel == 0.80m);

var tenantFallback = await config.Resolve<decimal>(adminContext, "ltv.warning-threshold", productId: "unconfigured-product");
Check("tenant scope is used when the product has no override", tenantFallback == 0.75m);

var sameTenantOfficerRead = await config.Resolve<decimal>(officerContext, "ltv.warning-threshold");
Check("a same-tenant caller resolves the tenant override too", sameTenantOfficerRead == 0.75m);

var deniedWrite = false;
try
{
    await config.Set(officerContext, ConfigScope.Tenant, "ltv.warning-threshold", 0.99m);
}
catch (UnauthorizedAccessException)
{
    deniedWrite = true;
}
Check("a loan officer cannot change tenant-level configuration", deniedWrite);

var deniedCrossTenantWrite = false;
try
{
    var abujaAuth = await identity.Authenticate(new AuthenticationRequest("mfi-abuja", "admin.chidi", "password", "s3cret"));
    var abujaContext = abujaAuth.Context!;
    // Abuja's own admin acting on their own tenant is fine...
    await config.Set(abujaContext, ConfigScope.Tenant, "ltv.warning-threshold", 0.65m);
    var abujaLevel = await config.Resolve<decimal>(abujaContext, "ltv.warning-threshold");
    Check("a different tenant's admin can set and read only their own tenant's config", abujaLevel == 0.65m);

    // ...and Lagos's tenant value is untouched by it
    var lagosStillIntact = await config.Resolve<decimal>(adminContext, "ltv.warning-threshold", productId: "unconfigured-product");
    Check("one tenant's config write never leaks into another tenant's resolution", lagosStillIntact == 0.75m);
}
catch
{
    deniedCrossTenantWrite = true;
}
Check("a tenant admin acting only within their own tenant is never blocked", !deniedCrossTenantWrite);

// --- Events: the SDK's event bus, the surface extensions subscribe to ---
var bus = new InProcessEventBus();
var received = new List<string>();
bus.Subscribe<LtvBreached>((e, _) =>
{
    received.Add($"LTV breach on {e.LoanId}: {e.Ltv:P0} ({e.Tier})");
    return Task.CompletedTask;
});

await bus.Publish(new LtvBreached("mfi-lagos", DateTimeOffset.UtcNow, "loan-001", 0.82m, "margin-call-tier-2"));
Check("event bus delivers a published event to its subscriber", received.Count == 1);

// --- Loan lifecycle: application through underwriting, disbursement, margin call, liquidation, repayment, closure ---
var custody = new SimulatedCustodyProvider();
var priceOracle = new SimulatedPriceOracle();
priceOracle.SetPrice("mfi-lagos", 100000m);
var aggregator = new ScoringAggregator([new SimulatedScoringProvider()]);
var repository = new LoanRepository();
var loanEvents = new InProcessEventBus();
var publishedEvents = new List<string>();
loanEvents.Subscribe<CollateralDeposited>((e, _) => { publishedEvents.Add($"CollateralDeposited:{e.LoanId}"); return Task.CompletedTask; });
loanEvents.Subscribe<LoanOriginated>((e, _) => { publishedEvents.Add($"LoanOriginated:{e.LoanId}"); return Task.CompletedTask; });
loanEvents.Subscribe<LtvBreached>((e, _) => { publishedEvents.Add($"LtvBreached:{e.LoanId}"); return Task.CompletedTask; });
loanEvents.Subscribe<MarginCallTriggered>((e, _) => { publishedEvents.Add($"MarginCallTriggered:{e.LoanId}"); return Task.CompletedTask; });
loanEvents.Subscribe<LiquidationExecuted>((e, _) => { publishedEvents.Add($"LiquidationExecuted:{e.LoanId}"); return Task.CompletedTask; });
loanEvents.Subscribe<RepaymentReceived>((e, _) => { publishedEvents.Add($"RepaymentReceived:{e.LoanId}"); return Task.CompletedTask; });
loanEvents.Subscribe<CreditScoreUpdated>((e, _) => { publishedEvents.Add($"CreditScoreUpdated:{e.BorrowerId}"); return Task.CompletedTask; });

var lifecycle = new LoanLifecycleService(repository, custody, aggregator, priceOracle, loanEvents);

var product = new LoanProduct(
    ProductId: "starter-loan", TenantId: "mfi-lagos", Name: "Starter Bitcoin Loan", Currency: "NGN",
    MaxLtv: 0.5m, MarginCallLtv: 0.65m, LiquidationLtv: 0.80m,
    MarginCallCureWindow: TimeSpan.Zero, AutoApproveScoreThreshold: 50, AutoRejectScoreThreshold: 25);

// Auto-reject path: borrower-bola scores 24 with the simulated provider, below the reject threshold.
var rejectedApp = await lifecycle.SubmitApplicationAsync(rootContext, product, "borrower-bola", 500m);
Check("a low-scoring borrower is auto-rejected", rejectedApp.Status == LoanStatus.Rejected && rejectedApp.Decision == UnderwritingDecision.AutoReject);

// Manual review path: borrower-ada scores 29, between the two thresholds.
var reviewApp = await lifecycle.SubmitApplicationAsync(rootContext, product, "borrower-ada", 500m);
Check("a mid-scoring borrower is routed to manual review", reviewApp.Status == LoanStatus.UnderReview && reviewApp.Decision == UnderwritingDecision.ManualReview);

var deniedApproval = false;
try
{
    // A loan officer from a different tenant may not approve this application.
    var abujaOfficerAuth = await identity.Authenticate(new AuthenticationRequest("mfi-abuja", "admin.chidi", "password", "s3cret"));
    await lifecycle.ApproveApplicationAsync(abujaOfficerAuth.Context!, reviewApp.Id);
}
catch (UnauthorizedAccessException)
{
    deniedApproval = true;
}
Check("a different tenant's staff cannot approve this application", deniedApproval);

var approvedApp = await lifecycle.ApproveApplicationAsync(officerContext, reviewApp.Id);
Check("the right tenant's loan officer can clear manual review", approvedApp.Status == LoanStatus.CollateralPending);

// Happy path: borrower-safe scores 93, auto-approved outright.
var happyApp = await lifecycle.SubmitApplicationAsync(rootContext, product, "borrower-safe", 500m);
Check("a high-scoring borrower is auto-approved", happyApp.Status == LoanStatus.CollateralPending && happyApp.Decision == UnderwritingDecision.AutoApprove);

var depositAddress = await lifecycle.RequestCollateralDepositAsync(rootContext, happyApp.Id);
Check("a deposit address is generated for the application", depositAddress.LoanApplicationId == happyApp.Id);

custody.SimulateDeposit(happyApp.Id, 2_000_000m); // 0.02 BTC at 100,000 -> collateral value 2,000 vs 500 requested: LTV 0.25
var activeLoan = await lifecycle.ConfirmCollateralAndDisburse(rootContext, product, happyApp.Id);
Check("confirmed collateral within max LTV disburses the loan", activeLoan.Status == LoanStatus.Active);
Check("initial LTV is computed correctly", Math.Abs(activeLoan.CurrentLtv - 0.25m) < 0.001m);
Check("disbursement publishes CollateralDeposited and LoanOriginated", publishedEvents.Contains($"CollateralDeposited:{happyApp.Id}") && publishedEvents.Contains($"LoanOriginated:{happyApp.Id}"));

// Full repayment: closes the loan and releases all collateral.
var closedLoan = await lifecycle.Repay(rootContext, product, happyApp.Id, 500m);
Check("full repayment closes the loan", closedLoan.Status == LoanStatus.Closed && closedLoan.CollateralSats == 0);
var releasedBalance = await custody.GetBalance(rootContext, happyApp.Id);
Check("collateral is actually released back through custody on closure", releasedBalance.ConfirmedSats == 0);
Check("closure publishes RepaymentReceived and CreditScoreUpdated", publishedEvents.Contains($"RepaymentReceived:{happyApp.Id}") && publishedEvents.Contains($"CreditScoreUpdated:borrower-safe"));

// Margin call and partial liquidation: borrower-margin-1 scores 99, auto-approved.
var marginApp = await lifecycle.SubmitApplicationAsync(rootContext, product, "borrower-margin-1", 500m);
await lifecycle.RequestCollateralDepositAsync(rootContext, marginApp.Id);
custody.SimulateDeposit(marginApp.Id, 2_000_000m);
var marginLoan = await lifecycle.ConfirmCollateralAndDisburse(rootContext, product, marginApp.Id);

priceOracle.SetPrice("mfi-lagos", 35000m); // price drop pushes LTV to ~0.71, above the 0.65 margin call tier
marginLoan = await lifecycle.RecomputeLtv(rootContext, product, marginApp.Id);
Check("a price drop past the margin tier triggers a margin call", marginLoan.Status == LoanStatus.MarginCalled);
Check("margin call publishes LtvBreached and MarginCallTriggered", publishedEvents.Contains($"LtvBreached:{marginApp.Id}") && publishedEvents.Contains($"MarginCallTriggered:{marginApp.Id}"));

// The zero-length cure window means it's already expired by the time we get here, so liquidation proceeds.
marginLoan = await lifecycle.Liquidate(rootContext, product, marginApp.Id);
Check("liquidation sells only enough collateral to restore the target LTV, not everything", marginLoan.Status == LoanStatus.Active && marginLoan.CollateralSats > 0);
Check("post-liquidation LTV lands back near the product's max", Math.Abs(marginLoan.CurrentLtv - product.MaxLtv) < 0.01m);
Check("liquidation publishes LiquidationExecuted", publishedEvents.Contains($"LiquidationExecuted:{marginApp.Id}"));

Console.WriteLine();
if (failures.Count == 0)
{
    Console.WriteLine("All smoke checks passed.");
    return 0;
}

Console.WriteLine($"{failures.Count} check(s) failed: {string.Join(", ", failures)}");
return 1;
