using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;

namespace Sorolla.Palette.Adapters
{
    /// <summary>
    ///     Vendor-neutral Remote Config state: freshness level, registered in-app defaults,
    ///     change subscribers, and waiters. Fed by the Firebase impl and the GameAnalytics
    ///     adapter; read by the public Palette Remote Config surface.
    ///     Levels mirror Sorolla.Palette.RemoteConfigStatus (0 Defaults, 1 Cached, 2 Live);
    ///     kept as ints here because this assembly cannot reference Sorolla.Runtime.
    /// </summary>
    internal static class RemoteConfigState
    {
        public const int LevelDefaults = 0;
        public const int LevelCached = 1;
        public const int LevelLive = 2;

        const string Tag = "[Palette:RemoteConfig]";
        static readonly IReadOnlyCollection<string> NoKeys = Array.Empty<string>();
        static readonly Dictionary<string, string> s_defaults = new Dictionary<string, string>();
        static readonly List<(int minLevel, TaskCompletionSource<bool> tcs)> s_waiters = new List<(int, TaskCompletionSource<bool>)>();
        static Action<IReadOnlyCollection<string>> s_changed;
        static Action<IReadOnlyCollection<string>> s_updateAvailable;
        static bool s_gaReady;
        static bool s_firebaseUnavailable;

        public static int Level { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Level = LevelDefaults;
            s_gaReady = false;
            s_firebaseUnavailable = false;
            s_changed = null;
            s_updateAvailable = null;
            s_defaults.Clear();
            lock (s_waiters) s_waiters.Clear();
        }

        public static void RegisterDefaults(Dictionary<string, object> defaults)
        {
            if (defaults == null) return;
            foreach (KeyValuePair<string, object> kvp in defaults)
            {
                if (string.IsNullOrEmpty(kvp.Key)) continue;
                s_defaults[kvp.Key] = Stringify(kvp.Value);
            }
        }

        public static bool TryGetDefault(string key, out string value) => s_defaults.TryGetValue(key, out value);

        /// <summary>
        ///     Subscribe to value changes. If values are already readable (level above Defaults),
        ///     the callback fires immediately so late subscribers never miss the initial load.
        /// </summary>
        public static void SubscribeChanged(Action<IReadOnlyCollection<string>> callback)
        {
            if (callback == null) return;
            s_changed += callback;
            // "Readable" covers the GA-before-Firebase window too: GA values can be served
            // while Level is still Defaults (status stays Firebase-governed by design).
            if (Level > LevelDefaults || s_gaReady) SafeInvoke(callback, NoKeys);
        }

        public static void UnsubscribeChanged(Action<IReadOnlyCollection<string>> callback) => s_changed -= callback;

        public static void SubscribeUpdateAvailable(Action<IReadOnlyCollection<string>> callback) => s_updateAvailable += callback;

        public static void UnsubscribeUpdateAvailable(Action<IReadOnlyCollection<string>> callback) => s_updateAvailable -= callback;

        // --- Provider notifications (main thread) ---

        /// <summary>Values fetched in a previous session are readable from Firebase's disk cache.</summary>
        public static void NotifyFirebaseCached() => Advance(LevelCached, NoKeys);

        /// <summary>A fetch or real-time update was activated this session. Null keys = unspecified, re-read everything.</summary>
        public static void NotifyFirebaseLive(IReadOnlyCollection<string> changedKeys) => Advance(LevelLive, changedKeys ?? NoKeys);

        /// <summary>Firebase init failed; GameAnalytics (if ready) governs freshness from now on.</summary>
        public static void NotifyFirebaseUnavailable()
        {
            s_firebaseUnavailable = true;
            if (s_gaReady) Advance(LevelLive, NoKeys);
        }

        /// <summary>GameAnalytics remote configs are ready (fetched fresh during GA init this session).</summary>
        public static void NotifyGaReady()
        {
            s_gaReady = true;
            if (!FirebaseRemoteConfigAdapter.HasImpl || s_firebaseUnavailable)
                Advance(LevelLive, NoKeys);
            else
                // GA-tier values changed, but overall freshness stays governed by Firebase.
                SafeRaise(s_changed, NoKeys);
        }

        /// <summary>Real-time update received but not activated (AutoActivateUpdates is false).</summary>
        public static void NotifyUpdateAvailable(IReadOnlyCollection<string> keys) => SafeRaise(s_updateAvailable, keys ?? NoKeys);

        public static Task<bool> WaitFor(int minLevel, float timeoutSeconds)
        {
            if (Level >= minLevel) return Task.FromResult(true);
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (s_waiters) s_waiters.Add((minLevel, tcs));
            if (timeoutSeconds > 0)
                Task.Delay(TimeSpan.FromSeconds(timeoutSeconds)).ContinueWith(_ =>
                {
                    tcs.TrySetResult(Level >= minLevel);
                    lock (s_waiters) s_waiters.RemoveAll(w => w.tcs.Task.IsCompleted);
                });
            return tcs.Task;
        }

        static void Advance(int level, IReadOnlyCollection<string> changedKeys)
        {
            if (level > Level)
            {
                Level = level;
                CompleteWaiters();
            }
            SafeRaise(s_changed, changedKeys);
        }

        static void CompleteWaiters()
        {
            lock (s_waiters)
            {
                for (int i = s_waiters.Count - 1; i >= 0; i--)
                {
                    (int minLevel, TaskCompletionSource<bool> tcs) = s_waiters[i];
                    if (tcs.Task.IsCompleted) { s_waiters.RemoveAt(i); continue; }
                    if (Level >= minLevel)
                    {
                        tcs.TrySetResult(true);
                        s_waiters.RemoveAt(i);
                    }
                }
            }
        }

        // One bad subscriber must not strand the others (same posture as Palette.FlushPending).
        static void SafeRaise(Action<IReadOnlyCollection<string>> handlers, IReadOnlyCollection<string> keys)
        {
            if (handlers == null) return;
            foreach (Delegate d in handlers.GetInvocationList())
                SafeInvoke((Action<IReadOnlyCollection<string>>)d, keys);
        }

        static void SafeInvoke(Action<IReadOnlyCollection<string>> callback, IReadOnlyCollection<string> keys)
        {
            try
            {
                callback(keys);
            }
            catch (Exception ex)
            {
                PaletteLog.Warning($"{Tag} Change subscriber threw: {ex.Message}");
            }
        }

        static string Stringify(object value) => value switch
        {
            null => "",
            bool b => b ? "true" : "false",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString(),
        };
    }
}
