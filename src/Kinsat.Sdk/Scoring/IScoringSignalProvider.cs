using Kinsat.Sdk.Identity;

namespace Kinsat.Sdk.Scoring;

public interface IScoringSignalProvider
{
    string Name { get; }
    Task<ScoreContribution> Score(CallContext context, ScoringContext scoringContext, CancellationToken cancellationToken = default);
}
