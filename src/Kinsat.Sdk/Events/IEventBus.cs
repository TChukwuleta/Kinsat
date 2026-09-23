namespace Kinsat.Sdk.Events;

public interface IEventBus
{
    Task Publish<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default) where TEvent : DomainEvent;
    void Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler) where TEvent : DomainEvent;
}