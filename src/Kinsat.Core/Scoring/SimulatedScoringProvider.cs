using System;
using System.Collections.Generic;
using System.Text;
using Kinsat.Sdk.Identity;
using Kinsat.Sdk.Scoring;

namespace Kinsat.Core.Scoring;

/// <summary>
/// A fake IScoringSignalProvider standing in for the core's real on-chain/repayment
/// Bitcoin credit score (FR-3.1), which needs real chain history to be meaningful. This
/// returns a deterministic score derived from the borrower id, so the same borrower
/// always scores the same way in tests, rather than a random value that would make
/// underwriting-routing tests flaky.
/// </summary>
public sealed class SimulatedScoringProvider : IScoringSignalProvider
{
    public string Name => "Simulated Bitcoin Credit Score";

    public Task<ScoreContribution> Score(CallContext context, ScoringContext scoringContext, CancellationToken cancellationToken = default)
    {
        var score = StableScoreFor(scoringContext.BorrowerId);
        return Task.FromResult(new ScoreContribution(Name, score, Weight: 1.0, Rationale: "Simulated: deterministic placeholder pending a real on-chain scoring model."));
    }

    /// <summary>
    /// A simple, stable 0-100 hash of the borrower id. Deliberately not string.GetHashCode():
    /// .NET randomizes string hash codes per process by default (hash-flooding protection),
    /// so that would silently produce a different score on every run, defeating the point
    /// of a deterministic placeholder.
    /// </summary>
    private static double StableScoreFor(string borrowerId)
    {
        unchecked
        {
            var hash = 17;
            foreach (var ch in borrowerId)
                hash = hash * 31 + ch;

            return Math.Abs(hash % 101);
        }
    }
}
