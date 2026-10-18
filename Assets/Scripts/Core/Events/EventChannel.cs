using System;

namespace Gladiator.Core
{
    public delegate void EventHandler<T>(in T e) where T : struct;

    /// <summary>
    /// Typed, allocation-free event channel. Subscribing allocates (setup time); raising never does.
    /// Handlers may unsubscribe themselves while being raised.
    /// </summary>
    public sealed class EventChannel<T> where T : struct
    {
        EventHandler<T>[] _handlers = new EventHandler<T>[4];
        int _count;

        public int SubscriberCount => _count;

        public void Subscribe(EventHandler<T> handler)
        {
            if (handler == null) return;
            if (_count == _handlers.Length) Array.Resize(ref _handlers, _count * 2);
            _handlers[_count++] = handler;
        }

        public void Unsubscribe(EventHandler<T> handler)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_handlers[i] != handler) continue;
                _count--;
                Array.Copy(_handlers, i + 1, _handlers, i, _count - i);
                _handlers[_count] = null;
                return;
            }
        }

        public void Raise(in T e)
        {
            for (int i = 0; i < _count; i++)
            {
                var h = _handlers[i];
                h(in e);
                if (i < _count && _handlers[i] != h) i--; // handler removed itself
            }
        }

        public void Clear()
        {
            Array.Clear(_handlers, 0, _handlers.Length);
            _count = 0;
        }
    }
}
