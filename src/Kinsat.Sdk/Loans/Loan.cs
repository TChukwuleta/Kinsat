using System;
using System.Collections.Generic;
using System.Text;

namespace Kinsat.Sdk.Loans;

/// <summary>
/// An approved, disbursed (or disbursing) loan and its current lifecycle state. Mutable
/// for the same reason as LoanApplication: the lifecycle service is the only thing that
/// should mutate it, everything else reads it through the loan repository.
/// </summary>
public sealed class Loan
{
    public required string Id { get; init; }
    public required string TenantId { get; init; }
    public required string BorrowerId { get; init; }
    public required string ProductId { get; init; }
    public required decimal PrincipalAmount { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }

    public LoanStatus Status { get; set; } = LoanStatus.CollateralPending;
    public decimal OutstandingBalance { get; set; }
    public decimal CollateralSats { get; set; }
    public decimal CurrentLtv { get; set; }
    public DateTimeOffset? MarginCallCureBy { get; set; }
}
