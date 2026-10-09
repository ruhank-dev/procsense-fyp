using System;
using System.Collections.Generic;

namespace ProcSense.Core.Pipeline
{
    /// <summary>Synchronous publish/subscribe. Handlers run in subscription order; publishing allocates nothing.</summary>
    public sealed class EventBus
    {
        private readonly Dictionary<Type, object> _handlers = new Dictionary<Type, object>();

        public void Subscribe<T>(Action<T> handler)
        {
            if (!_handlers.TryGetValue(typeof(T), out var list))
                _handlers[typeof(T)] = list = new List<Action<T>>();
            ((List<Action<T>>)list).Add(handler);
        }

        public void Publish<T>(T evt)
        {
            if (!_handlers.TryGetValue(typeof(T), out var list)) return;
            var handlers = (List<Action<T>>)list;
            for (int i = 0; i < handlers.Count; i++) handlers[i](evt); // index loop: no enumerator allocation
        }
    }
}
