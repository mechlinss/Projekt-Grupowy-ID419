namespace Contracts;

public interface IEventAggregator
{
    void Publish<TEvent>(TEvent @event);
    void Subscribe<TEvent>(Action<TEvent> handler);
}
