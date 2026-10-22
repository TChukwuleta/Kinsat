using System;
using System.Collections.Generic;
using System.Text;

namespace Kinsat.Sdk.Loans;

/// <summary>
/// A submitted application prior to approval (FR-2.3). Mutable by design: this is a
/// live aggregate the lifecycle service updates as underwriting resolves, not a
/// value type.
/// </summary>
public sealed class LoanApplication
{
    public required string Id { get; init; }
    public required string TenantId { get; init; }
    public required string BorrowerId { get; init; }
    public required string ProductId { get; init; }
    public required decimal RequestedAmount { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }

    public LoanStatus Status { get; set; } = LoanStatus.Applied;
    public UnderwritingDecision? Decision { get; set; }
    public double? CombinedScore { get; set; }
}
