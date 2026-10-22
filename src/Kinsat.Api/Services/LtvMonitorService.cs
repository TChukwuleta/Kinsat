using Kinsat.Core.Identity;
using Kinsat.Core.Loans;

namespace Kinsat.Api.Services;

/// <summary>
/// Continuous LTV monitoring (FR-6.1): on each tick, recomputes LTV for every active or
/// margin-called loan across every tenant, via LoanLifecycleService, which publishes
/// LtvBreached / MarginCallTriggered through IEventBus exactly as it does when called
/// directly. Uses SystemActor for its CallContext since this is genuinely an automated
/// process with no human behind it, see SystemActor's and CallContext's doc comments for
/// the honest caveat about what that construction path does and doesn't guarantee today.
/// </summary>
public sealed class LtvMonitorService(
    LoanRepository repository,
    LoanLifecycleService lifecycle,
    LoanProductCatalog products,
    ILogger<LtvMonitorService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("LTV monitor started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var activeLoans = repository.GetActiveLoans();
            foreach (var loan in activeLoans)
            {
                try
                {
                    var product = products.Get(loan.ProductId);
                    var context = SystemActor.ForTenant(loan.TenantId);
                    var updated = await lifecycle.RecomputeLtv(context, product, loan.Id, stoppingToken);

                    if (updated.Status == Kinsat.Sdk.Loans.LoanStatus.MarginCalled)
                        logger.LogWarning("Loan {LoanId} is under a margin call, LTV {Ltv:P0}.", updated.Id, updated.CurrentLtv);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "LTV recompute failed for loan {LoanId}.", loan.Id);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
