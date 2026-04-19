using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Composition;

namespace Contracts
{
    [PartCreationPolicy(CreationPolicy.Shared)]
    [Export(typeof(IEventAggregator))]
    public class SimpleEventAggregator : IEventAggregator
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new();

        public void Publish<TEvent>(TEvent @event)
        {
            var type = typeof(TEvent);
            if (_handlers.TryGetValue(type, out var list))
            {
                foreach (var handler in list)
                {
                    if (handler is Action<TEvent> action)
                        action(@event);
                }
            }
        }

        public void Subscribe<TEvent>(Action<TEvent> handler)
        {
            var type = typeof(TEvent);

            if (!_handlers.TryGetValue(type, out var list))
            {
                list = new List<Delegate>();
                _handlers[type] = list;
            }

            list.Add(handler);
        }
    }
}
