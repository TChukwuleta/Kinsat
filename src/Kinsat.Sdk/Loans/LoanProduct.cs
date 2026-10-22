using System;
using System.Collections.Generic;
using System.Text;

namespace Kinsat.Sdk.Loans;


/// <summary>
/// A tenant-configured loan product (FR-2.1). In a persisted deployment this comes from
/// the config engine; for now it's passed directly into the lifecycle service so the
/// state machine can be built and tested independently of a persistence decision.
/// </summary>
public sealed record LoanProduct(
    string ProductId,
    string TenantId,
    string Name,
    string Currency,
    decimal MaxLtv,
    decimal MarginCallLtv,
    decimal LiquidationLtv,
    TimeSpan MarginCallCureWindow,
    double AutoApproveScoreThreshold,
    double AutoRejectScoreThreshold);
