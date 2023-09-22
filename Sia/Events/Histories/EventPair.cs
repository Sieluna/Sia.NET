namespace Sia;

public record struct EventPair<TTarget, TEvent>(TTarget Target, TEvent Event)
    where TEvent : IEvent;