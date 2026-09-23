using System;
using System.Collections.Generic;
using System.Text;
using Kinsat.Sdk.Events;

namespace Kinsat.Core.Events;

public sealed class InProcessEventBus : IEventBus
{
    private readonly Dictionary<Type, List<Func<DomainEvent, CancellationToken, Task>>> _handlers = new();

    public void Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler) where TEvent : DomainEvent
    {
        if (!_handlers.TryGetValue(typeof(TEvent), out var list))
            _handlers[typeof(TEvent)] = list = new List<Func<DomainEvent, CancellationToken, Task>>();

        list.Add((domainEvent, cancellationToken) => handler((TEvent)domainEvent, cancellationToken));
    }

    public async Task Publish<TEvent>(TEvent domainEvent, CancellationToken cancellationToken = default) where TEvent : DomainEvent
    {
        if (!_handlers.TryGetValue(typeof(TEvent), out var handlers))
            return;

        foreach (var handler in handlers)
            await handler(domainEvent, cancellationToken);
    }
}
