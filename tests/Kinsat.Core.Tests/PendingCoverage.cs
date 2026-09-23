using System;
using System.Collections.Generic;
using System.Text;

namespace Kinsat.Core.Tests;

public class PendingCoverage
{
    [Fact(Skip = "ICustodyProvider has no implementation yet (BTCPay Server default is next up per README).")]
    public void Custody_provider_default_implementation() { }

    [Fact(Skip = "IScoringSignalProvider aggregator has no implementation yet.")]
    public void Scoring_aggregator() { }

    [Fact(Skip = "Loan lifecycle state machine (application through closure/liquidation) does not exist yet.")]
    public void Loan_lifecycle_state_machine() { }

    [Fact(Skip = "No end-to-end integration test exists yet: authentication through a full loan " +
                 "(application, disbursement, LTV breach, margin call, liquidation or repayment, closure) " +
                 "requires the loan lifecycle and custody provider to exist first.")]
    public void End_to_end_authentication_to_finalization() { }
}
