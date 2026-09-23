namespace Kinsat.Sdk.Scoring;

public sealed record ScoringContext(string TenantId, string BorrowerId, IReadOnlyDictionary<string, string> Attributes);
