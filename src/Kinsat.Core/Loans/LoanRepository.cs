using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Kinsat.Core.Security;
using Kinsat.Sdk.Identity;
using Kinsat.Sdk.Loans;

namespace Kinsat.Core.Loans;

public sealed class LoanRepository
{
    private readonly ConcurrentDictionary<string, LoanApplication> _applications = new();
    private readonly ConcurrentDictionary<string, Loan> _loans = new();

    public void SaveApplication(LoanApplication application) => _applications[application.Id] = application;

    public LoanApplication GetApplication(CallContext context, string applicationId)
    {
        if (!_applications.TryGetValue(applicationId, out var application))
            throw new KeyNotFoundException($"No application '{applicationId}'.");

        Guard.RequireTenantAccess(context, application.TenantId);
        return application;
    }

    public void SaveLoan(Loan loan) => _loans[loan.Id] = loan;

    public Loan GetLoan(CallContext context, string loanId)
    {
        if (!_loans.TryGetValue(loanId, out var loan))
            throw new KeyNotFoundException($"No loan '{loanId}'.");

        Guard.RequireTenantAccess(context, loan.TenantId);
        return loan;
    }

    /// <summary>Used by the LTV monitor to find everything it needs to watch, across every tenant.</summary>
    public IReadOnlyList<Loan> GetActiveLoans() =>
        _loans.Values.Where(l => l.Status is LoanStatus.Active or LoanStatus.MarginCalled).ToList();
}
