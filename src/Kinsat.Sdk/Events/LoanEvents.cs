namespace Kinsat.Sdk.Events;

public sealed record LoanOriginated(string TenantId, DateTimeOffset OccurredAtUtc, string LoanId, string BorrowerId)
    : DomainEvent(TenantId, OccurredAtUtc);

public sealed record CollateralDeposited(string TenantId, DateTimeOffset OccurredAtUtc, string LoanId, decimal Sats)
    : DomainEvent(TenantId, OccurredAtUtc);

public sealed record LtvBreached(string TenantId, DateTimeOffset OccurredAtUtc, string LoanId, decimal Ltv, string Tier)
    : DomainEvent(TenantId, OccurredAtUtc);

public sealed record MarginCallTriggered(string TenantId, DateTimeOffset OccurredAtUtc, string LoanId, DateTimeOffset CureBy)
    : DomainEvent(TenantId, OccurredAtUtc);

public sealed record LiquidationExecuted(string TenantId, DateTimeOffset OccurredAtUtc, string LoanId, decimal SatsLiquidated)
    : DomainEvent(TenantId, OccurredAtUtc);

public sealed record RepaymentReceived(string TenantId, DateTimeOffset OccurredAtUtc, string LoanId, decimal Amount)
    : DomainEvent(TenantId, OccurredAtUtc);

public sealed record CreditScoreUpdated(string TenantId, DateTimeOffset OccurredAtUtc, string BorrowerId, double NewScore)
    : DomainEvent(TenantId, OccurredAtUtc);
