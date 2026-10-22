using System;
using System.Collections.Generic;
using System.Text;
using Kinsat.Sdk.Identity;
using Kinsat.Sdk.Scoring;

namespace Kinsat.Core.Scoring;

/// <summary>
/// Combines every registered IScoringSignalProvider into one weighted score (FR-3.2).
/// The core's own provider is registered like any other, there is no special case for
/// it here; an extension adds providers by registering more implementations, not by
/// changing this class.
/// </summary>
public sealed class ScoringAggregator(IEnumerable<IScoringSignalProvider> providers)
{
    public async Task<(double CombinedScore, IReadOnlyList<ScoreContribution> Contributions)> ScoreAsync(
        CallContext context, ScoringContext scoringContext, CancellationToken cancellationToken = default)
    {
        var contributions = new List<ScoreContribution>();
        foreach (var provider in providers)
            contributions.Add(await provider.Score(context, scoringContext, cancellationToken));

        if (contributions.Count == 0)
            return (0, contributions);

        var totalWeight = contributions.Sum(c => c.Weight);
        var combined = totalWeight == 0
            ? 0
            : contributions.Sum(c => c.Score * c.Weight) / totalWeight;

        return (combined, contributions);
    }
}
