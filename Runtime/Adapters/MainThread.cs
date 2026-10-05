using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Scripting;

namespace Sorolla.Palette.Adapters
{
    /// <summary>
    ///     The Unity main thread, for Palette state that vendor callbacks reach from other threads. A
    ///     fullscreen ad pauses Unity and MAX reports its revenue on a background thread: the vendor calls
    ///     run there at once, and the Palette state they reach (diagnostics, Firebase's pre-init queue) hands
    ///     itself to the main thread.
    /// </summary>
    [Preserve]
    internal static class MainThread
    {
        static SynchronizationContext s_context;
        static int s_threadId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        [Preserve]
        static void Capture()
        {
            s_context = SynchronizationContext.Current;
            s_threadId = Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>True on the main thread, and before capture (Editor tests), where nothing runs off it.</summary>
        internal static bool IsCurrent => s_context == null || Thread.CurrentThread.ManagedThreadId == s_threadId;

        /// <summary>From another thread (<see cref="IsCurrent"/> false): runs the action on the main thread's next frame.</summary>
        internal static void Post(Action action) => s_context.Post(_ => action(), null);
    }
}
