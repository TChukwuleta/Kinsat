using System;
using System.Collections.Generic;
using System.Text;

namespace Kinsat.Sdk.Loans;

public enum LoanStatus
{
    Applied,
    UnderReview,
    Rejected,
    CollateralPending,
    Active,
    MarginCalled,
    Liquidated,
    Closed
}
