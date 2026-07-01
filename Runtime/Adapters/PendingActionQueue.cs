using System;
using System.Collections.Generic;

namespace Sorolla.Palette.Adapters
{
    /// <summary>
    ///     Shared "queue until ready, then flush" mechanics: buffer actions while a caller isn't
    ///     ready yet, then drain them once it is. Every caller keeps its own ready/failed state
    ///     machine (which differs per adapter) and only delegates queue storage, optional bounded
    ///     eviction, and catch-continue flushing to this class.
    /// </summary>
    internal sealed class PendingActionQueue
    {
        readonly Queue<Action> _pending = new Queue<Action>();
        readonly int _cap;

        /// <param name="cap">Maximum queued actions before the oldest is evicted. 0 = unbounded.</param>
        public PendingActionQueue(int cap = 0) => _cap = cap;

        public int Count => _pending.Count;

        /// <summary>Enqueues an action. At capacity, evicts the oldest (calling <paramref name="onEvicted"/> first).</summary>
        public void Enqueue(Action action, Action onEvicted = null)
        {
            if (_cap > 0 && _pending.Count >= _cap)
            {
                onEvicted?.Invoke();
                _pending.Dequeue();
            }
            _pending.Enqueue(action);
        }

        public void Clear() => _pending.Clear();

        /// <summary>Catch-continue per action so one throw can't strand the rest of the queue (DR-38).</summary>
        public void Flush(Action<Exception> onError = null)
        {
            while (_pending.Count > 0)
            {
                try { _pending.Dequeue().Invoke(); }
                catch (Exception e) { onError?.Invoke(e); }
            }
        }
    }
}
