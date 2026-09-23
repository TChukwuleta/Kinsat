namespace Kinsat.Sdk.Scoring;

public sealed record ScoreContribution(string SourceName, double Score, double Weight, string? Rationale);