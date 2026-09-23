namespace Kinsat.Sdk.Events;

public abstract record DomainEvent(string TenantId, DateTimeOffset OccurredAtUtc);
