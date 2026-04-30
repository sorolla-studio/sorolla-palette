using System;
using System.Collections.Generic;
using UnityEngine;
using Sorolla.Palette.Adapters;
#if GAMEANALYTICS_INSTALLED
using GameAnalyticsSDK;
#endif

namespace Sorolla.Palette
{
    public static partial class Palette
    {
        /// <summary>
        ///     Typed level progression tracking. Auto-tracks duration between <see cref="Start"/>
        ///     and <see cref="Complete"/>/<see cref="Fail"/>. Wire format:
        ///     <c>level_name = "world_{W}_level_{L}"</c> when world is supplied, else <c>"level_{L}"</c>.
        /// </summary>
        public static class Level
        {
            static readonly Dictionary<(int? world, int level), float> s_startTimes
                = new Dictionary<(int? world, int level), float>();

            /// <summary>Mark the start of a level. Fires level_start (Firebase) and records start time for auto-duration.</summary>
            public static void Start(int level, int? world = null, Dictionary<string, object> extraParams = null)
            {
                WarnIfNegative("Start", level, world);

                // Capture timestamp synchronously so duration reflects player wall-time,
                // not whenever the event flushes after MAX consent resolves.
                s_startTimes[(world, level)] = Time.realtimeSinceStartup;
                QueueOrExecute(() => Emit("start", level, world, score: 0, duration: null, extraParams));
            }

            /// <summary>Mark a level completed. Fires level_end{success=1}, auto-fills duration_sec if Start was called.</summary>
            public static void Complete(int level, int? world = null, int score = 0, Dictionary<string, object> extraParams = null)
                => End(level, world, success: true, score, extraParams);

            /// <summary>Mark a level failed. Fires level_end{success=0}, auto-fills duration_sec if Start was called.</summary>
            public static void Fail(int level, int? world = null, int score = 0, Dictionary<string, object> extraParams = null)
                => End(level, world, success: false, score, extraParams);

            static void End(int level, int? world, bool success, int score, Dictionary<string, object> extraParams)
            {
                string verb = success ? "Complete" : "Fail";
                WarnIfNegative(verb, level, world);

                float? duration = null;
                var key = (world, level);
                if (s_startTimes.TryGetValue(key, out float started))
                {
                    duration = Time.realtimeSinceStartup - started;
                    s_startTimes.Remove(key);
                }

                QueueOrExecute(() => Emit(success ? "complete" : "fail", level, world, score, duration, extraParams));
            }

            // level/world == 0 are valid (0-indexed schemes). Negatives are almost always
            // uninitialized ints or off-by-one bugs — warn loud but ship as-is so the bad
            // data stays visible in dashboards instead of being merged into bucket 0.
            static void WarnIfNegative(string verb, int level, int? world)
            {
                if (level < 0)
                    PaletteLog.Warning($"{Tag} Level.{verb}: level={level} is negative; event passed through. Check for uninitialized int or off-by-one.");
                if (world.HasValue && world.Value < 0)
                    PaletteLog.Warning($"{Tag} Level.{verb}: world={world.Value} is negative; event passed through. Check for uninitialized int or off-by-one.");
            }

            static void Emit(string status, int level, int? world, int score, float? duration,
                Dictionary<string, object> extraParams)
            {
                string p1 = world.HasValue ? $"world_{world.Value}" : $"level_{level}";
                string p2 = world.HasValue ? $"level_{level}" : null;

#if GAMEANALYTICS_INSTALLED
                GAProgressionStatus gaStatus = status switch
                {
                    "start" => GAProgressionStatus.Start,
                    "complete" => GAProgressionStatus.Complete,
                    "fail" => GAProgressionStatus.Fail,
                    _ => GAProgressionStatus.Start,
                };
                GameAnalyticsAdapter.TrackProgressionEvent(gaStatus, p1, p2, null, score);
#else
                GameAnalyticsAdapter.TrackProgressionEvent(status, p1, p2, null, score);
#endif

#if FIREBASE_ANALYTICS_INSTALLED
                Dictionary<string, object> extras = extraParams != null
                    ? new Dictionary<string, object>(extraParams)
                    : null;
                if (duration.HasValue)
                {
                    extras ??= new Dictionary<string, object>();
                    extras["duration_sec"] = (float)Math.Round(duration.Value, 2);
                }
                FirebaseAdapter.TrackProgressionEvent(status, p1, p2, null, score, extras);
#endif
            }
        }
    }
}
