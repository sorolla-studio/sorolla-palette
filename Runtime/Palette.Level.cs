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

            // Cap on concurrently-tracked level start times. A real game has ~1 level in flight;
            // this only bounds the leak from levels Started but never Completed/Failed (B-18).
            const int MaxTrackedStartTimes = 64;

            /// <summary>Mark the start of a level. Fires level_start (Firebase) and records start time for auto-duration.</summary>
            /// <param name="extraParams">Optional structured params. Sent to Firebase only - GameAnalytics receives the curated progression fields, not these.</param>
            public static void Start(int level, int? world = null, Dictionary<string, object> extraParams = null)
            {
                WarnIfNegative("Start", level, world);

                // Bound the in-flight start-time map: a level Started but never Completed/Failed
                // (player quit to menu, app backgrounded) would otherwise leak its entry forever.
                // Past a generous cap, drop the oldest tracked start (smallest timestamp) (B-18).
                if (s_startTimes.Count >= MaxTrackedStartTimes && !s_startTimes.ContainsKey((world, level)))
                    EvictOldestStartTime();

                // Capture timestamp synchronously so duration reflects player wall-time,
                // not whenever the event flushes after MAX consent resolves.
                s_startTimes[(world, level)] = Time.realtimeSinceStartup;

                // Snapshot the caller's dict on the pre-consent queued path (DR-145 residual): the
                // closure runs 1-3s later after consent resolves, so a caller that mutates or reuses
                // the same dict before the flush would otherwise rewrite the dispatched values.
                // Matches TrackEvent/Economy, which already snapshot at enqueue (B-13). Only the queued
                // path needs it - when initialized, QueueOrExecute runs synchronously (no mutate window).
                Dictionary<string, object> snapshot = extraParams;
                if (extraParams != null && !IsInitialized)
                    snapshot = new Dictionary<string, object>(extraParams);
                QueueOrExecute(() => Emit("start", level, world, score: 0, duration: null, snapshot));
            }

            /// <summary>Mark a level completed. Fires level_end{success=1}, auto-fills duration_sec if Start was called.</summary>
            /// <param name="extraParams">Optional structured params. Sent to Firebase only - GameAnalytics receives the curated progression fields, not these.</param>
            public static void Complete(int level, int? world = null, int score = 0, Dictionary<string, object> extraParams = null)
                => End(level, world, success: true, score, extraParams);

            /// <summary>Mark a level failed. Fires level_end{success=0}, auto-fills duration_sec if Start was called.</summary>
            /// <param name="extraParams">Optional structured params. Sent to Firebase only - GameAnalytics receives the curated progression fields, not these.</param>
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

                // Snapshot on the queued path (DR-145 residual) - see Start for rationale.
                Dictionary<string, object> snapshot = extraParams;
                if (extraParams != null && !IsInitialized)
                    snapshot = new Dictionary<string, object>(extraParams);
                QueueOrExecute(() => Emit(success ? "complete" : "fail", level, world, score, duration, snapshot));
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

            // Remove the oldest in-flight start time (smallest realtime timestamp). Only called when
            // the tracked-start cap is exceeded, so the per-call enumeration cost is not a hot path.
            static void EvictOldestStartTime()
            {
                (int? world, int level) oldestKey = default;
                float oldest = float.MaxValue;
                bool found = false;
                foreach (var kvp in s_startTimes)
                {
                    if (kvp.Value >= oldest) continue;
                    oldest = kvp.Value;
                    oldestKey = kvp.Key;
                    found = true;
                }
                if (found) s_startTimes.Remove(oldestKey);
            }

            static void Emit(string status, int level, int? world, int score, float? duration,
                Dictionary<string, object> extraParams)
            {
                SorollaDiagnostics.RecordProgression(status);

                string p1 = world.HasValue ? $"world_{world.Value}" : $"level_{level}";
                string p2 = world.HasValue ? $"level_{level}" : null;
                string levelName = world.HasValue ? $"{p1}_{p2}" : p1;
                string eventName = status == "start" ? "level_start" : "level_end";

                var diagnosticParams = extraParams != null
                    ? new Dictionary<string, object>(extraParams)
                    : new Dictionary<string, object>();
                diagnosticParams["level_name"] = levelName;
                if (status != "start")
                    diagnosticParams["success"] = status == "complete";
                if (score > 0)
                    diagnosticParams["score"] = score;
                if (duration.HasValue)
                    diagnosticParams["duration_sec"] = (float)Math.Round(duration.Value, 2);

                SorollaDiagnostics.RecordEventDispatch("level", eventName, diagnosticParams);
                if (status != "start" && score > 0)
                    SorollaDiagnostics.RecordEventDispatch("level", "post_score", new Dictionary<string, object>
                    {
                        { "score", score },
                    });

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
