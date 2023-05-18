namespace Vtb.Common.History
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;

    public delegate bool EventContextGetter<T>(out T eventContext);

    public class EventHistoryWithContext<TEventKey, TEventContext> : EventHistory<TEventKey>
    {
        public EventHistoryWithContext() : base()
        {
            m_Events = new ConcurrentDictionary<TEventKey, TEventContext>();
        }

        public bool Register(TEventKey eventKey, TEventContext eventContext)
        {
            if (m_Events.TryAdd(eventKey, eventContext))
            {
                Register(eventKey);
                return true;
            }

            return false;
        }

        private class IgnoreResultException : Exception { public IgnoreResultException() : base() { } }

        public void Update(TEventKey eventKey, TEventContext eventContext)
        {
            try
            {
                m_Events.AddOrUpdate(eventKey, _=> throw new IgnoreResultException(), (k, v) => eventContext);
            }
            catch (IgnoreResultException)
            {
                // предотвращаем добавление пустой записи
            }
        }

        public bool Register(TEventKey eventKey, Func<TEventContext> eventContextGetter, out TEventContext eventContext)
        {
            if (!m_Events.TryGetValue(eventKey, out eventContext))
            {
                if (m_Events.TryAdd(eventKey, eventContext = eventContextGetter()))
                {
                    Register(eventKey);
                    return true;
                }
            }

            return false;
        }

        public bool Register(TEventKey eventKey, EventContextGetter<TEventContext> eventContextGetter, out TEventContext eventContext)
        {
            if (!m_Events.TryGetValue(eventKey, out eventContext))
            {
                if (eventContextGetter(out eventContext))
                {
                    if (m_Events.TryAdd(eventKey, eventContext))
                    {
                        Register(eventKey);
                        return true;
                    }
                }
                //TODO что то сделать если нет данных
            }

            return false;
        }

        public bool UnRegister(TEventKey eventKey, out TEventContext eventContext)
            => m_Events.TryRemove(eventKey, out eventContext);

        public bool UnRegister(TEventKey eventKey)
            => m_Events.TryRemove(eventKey, out TEventContext c);

        private ConcurrentDictionary<TEventKey, TEventContext> m_Events;
    }

    public class EventHistory<TEvent>
    {
        public EventHistory()
        {
            m_History = new LinkedList<Event>();
        }

        public void Register(TEvent eventKey)
            => RegisterEvent(eventKey);

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void RegisterEvent(Event @event)
            => m_History.AddLast(@event);

        [MethodImpl(MethodImplOptions.Synchronized)]
        public TEvent[] TimeoutEvents(DateTime moment)
            => TimeoutEventsEnumerator(moment).ToArray();

        [MethodImpl(MethodImplOptions.Synchronized)]
        public TEvent[] TimeoutEvents(DateTime moment, int count)
            => TimeoutEventsEnumerator(moment).Take(count).ToArray();

        private IEnumerable<TEvent> TimeoutEventsEnumerator(DateTime moment)
        {
            while (m_History.Count > 0 && m_History.First.Value < moment)
            {
                TEvent eventKey = m_History.First.Value;
                m_History.RemoveFirst();
                yield return eventKey;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public TEvent[] TakeEvents(int count)
        {
            var i = 0;
            var result = new TEvent[Math.Min(m_History.Count, count)];
            foreach (var e in TakeEventsEnumerator().Take(count))
            {
                result[i++] = e;
            }

            return result;
        }

        // использовать с осторожностью, только в эксклюзивном режиме доступа
        public IEnumerable<TEvent> TakeEventsEnumerator()
        {
            while (m_History.Count > 0)
            {
                TEvent eventKey = m_History.First.Value;
                m_History.RemoveFirst();
                yield return eventKey;
            }
        }

        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get => m_History.Count == 0;
        }

        public int Count
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get => m_History.Count;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool TimeoutEvent(DateTime moment, out TEvent eventKey)
        {
            if (m_History.Count > 0 && m_History.First.Value < moment)
            {
                eventKey = m_History.First.Value;
                m_History.RemoveFirst();
                return true;
            }

            eventKey = default(TEvent);
            return false;
        }

        public TEvent[] Events
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                var result = new TEvent[m_History.Count];

                var i = 0;
                foreach (var @event in m_History)
                    result[i++] = @event;

                return result;
            }
        }

        private struct Event
        {
            public readonly DateTime Moment;
            public readonly TEvent Key;

            public Event(TEvent key)
            {
                Moment = DateTime.Now;
                Key = key;
            }

            public static implicit operator DateTime(Event @event)
            {
                return @event.Moment;
            }

            public static implicit operator TEvent(Event @event)
            {
                return @event.Key;
            }

            public static implicit operator Event(TEvent key)
            {
                return new Event(key);
            }
        }

        private LinkedList<Event> m_History;
    }

    public static class EventHistoryUtils
    {
        public static IEnumerable<TEvent> Consume<TEvent>(this EventHistory<TEvent> events, int packetSize)
        {
            for (int i = 0; i < packetSize; i++)
            {
                if (events.TimeoutEvent(DateTime.MaxValue, out var item))
                    yield return item;
                else
                    yield break;
            }
        }
    }
}
