using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Sorolla.Palette.Adapters;
using UnityEngine;

namespace Sorolla.Palette
{
    /// <summary>
    ///     Freshness of the values served by the Remote Config getters.
    /// </summary>
    public enum RemoteConfigStatus
    {
        /// <summary>No fetch has ever succeeded on this device; only in-code defaults are served.</summary>
        Defaults = 0,
        /// <summary>Values fetched in a previous session are served from disk.</summary>
        Cached = 1,
        /// <summary>Values are current for this session (fetch succeeded or a real-time update was activated).</summary>
        Live = 2,
    }

    public static partial class Palette
    {
        const string RcTag = "[Palette:RemoteConfig]";
        // Dev-build diagnostics: each missing/unparseable key warns once per session.
        static readonly HashSet<string> s_rcWarnedKeys = new HashSet<string>();

        /// <summary>
        ///     Freshness of the values currently served by the getters. Monotonic within a session:
        ///     Defaults -> Cached (previous session's values loaded from disk) -> Live (fetched this
        ///     session). Gate anything that must not run on stale balance (A/B bucketing, gameplay
        ///     start behind a network wall) on Cached or Live.
        /// </summary>
        public static RemoteConfigStatus RemoteConfigStatus => (RemoteConfigStatus)RemoteConfigState.Level;

        /// <summary>
        ///     Fired whenever the served values may have changed: first cached load, fetch
        ///     activation, real-time update, or GameAnalytics configs becoming ready.
        ///     The collection holds the updated keys when known, and is empty when the change
        ///     is unspecified (re-read everything you care about).
        ///     If values are already readable when you subscribe, the handler fires immediately -
        ///     late subscribers never miss the initial load.
        /// </summary>
        public static event Action<IReadOnlyCollection<string>> OnRemoteConfigChanged
        {
            add => RemoteConfigState.SubscribeChanged(value);
            remove => RemoteConfigState.UnsubscribeChanged(value);
        }

        /// <summary>
        ///     Fired when a real-time update arrived but was NOT activated because
        ///     <see cref="AutoActivateRemoteConfigUpdates" /> is false. Call
        ///     <see cref="ActivateRemoteConfigAsync" /> at a safe moment (between rounds) to apply it;
        ///     <see cref="OnRemoteConfigChanged" /> then fires as usual.
        /// </summary>
        public static event Action<IReadOnlyCollection<string>> OnRemoteConfigUpdateAvailable
        {
            add => RemoteConfigState.SubscribeUpdateAvailable(value);
            remove => RemoteConfigState.UnsubscribeUpdateAvailable(value);
        }

        /// <summary>
        ///     Completes true as soon as <see cref="RemoteConfigStatus" /> reaches
        ///     <paramref name="minStatus" />, or false after <paramref name="timeoutSeconds" />
        ///     (a timeout of 0 or less waits indefinitely).
        ///     Typical gate before gameplay start: <c>await Palette.WaitForRemoteConfig(5f)</c>.
        ///     Devices that have fetched before pass instantly via the disk cache.
        /// </summary>
        public static Task<bool> WaitForRemoteConfig(float timeoutSeconds = 5f, RemoteConfigStatus minStatus = RemoteConfigStatus.Cached)
            => RemoteConfigState.WaitFor((int)minStatus, timeoutSeconds);

        /// <summary>
        ///     Get Remote Config string value. Resolution order, identical for every type:
        ///     Firebase (remote, cached, or registered in-app default) -> GameAnalytics ->
        ///     defaults registered via <see cref="SetRemoteConfigDefaults" /> -> <paramref name="defaultValue" />.
        /// </summary>
        public static string GetRemoteConfig(string key, string defaultValue = "")
            => TryResolve(key, out string raw) ? raw : defaultValue;

        /// <summary>
        ///     Get Remote Config int value. Decimal values truncate toward zero.
        ///     See <see cref="GetRemoteConfig" /> for resolution order.
        /// </summary>
        public static int GetRemoteConfigInt(string key, int defaultValue = 0)
        {
            if (!TryResolve(key, out string raw)) return defaultValue;
            string trimmed = raw.Trim();
            if (long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l)) return (int)l;
            if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return (int)d;
            WarnOnce(key, $"value '{raw}' is not an int; serving call-site default");
            return defaultValue;
        }

        /// <summary>
        ///     Get Remote Config float value. See <see cref="GetRemoteConfig" /> for resolution order.
        /// </summary>
        public static float GetRemoteConfigFloat(string key, float defaultValue = 0f)
        {
            if (!TryResolve(key, out string raw)) return defaultValue;
            if (double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return (float)d;
            WarnOnce(key, $"value '{raw}' is not a float; serving call-site default");
            return defaultValue;
        }

        /// <summary>
        ///     Get Remote Config bool value. Accepts true/false, 1/0, yes/no, on/off (case-insensitive)
        ///     on every tier. See <see cref="GetRemoteConfig" /> for resolution order.
        /// </summary>
        public static bool GetRemoteConfigBool(string key, bool defaultValue = false)
        {
            if (!TryResolve(key, out string raw)) return defaultValue;
            switch (raw.Trim().ToLowerInvariant())
            {
                case "1":
                case "true":
                case "t":
                case "yes":
                case "y":
                case "on":
                    return true;
                case "0":
                case "false":
                case "f":
                case "no":
                case "n":
                case "off":
                    return false;
                default:
                    WarnOnce(key, $"value '{raw}' is not a bool; serving call-site default");
                    return defaultValue;
            }
        }

        /// <summary>
        ///     Register in-app defaults. Works before or after initialization; values are served
        ///     when no fetched or cached value exists for a key, on every provider tier.
        ///     Also registered with Firebase so dashboard `useInAppDefault` parameters resolve to them.
        /// </summary>
        public static void SetRemoteConfigDefaults(Dictionary<string, object> defaults)
        {
            RemoteConfigState.RegisterDefaults(defaults);
            FirebaseRemoteConfigAdapter.SetDefaults(defaults);
        }

        /// <summary>
        ///     When true (default), real-time Remote Config updates are activated immediately and
        ///     <see cref="OnRemoteConfigChanged" /> fires. Set false for games where mid-session value
        ///     flips would be jarring; <see cref="OnRemoteConfigUpdateAvailable" /> then fires instead
        ///     and the game activates via <see cref="ActivateRemoteConfigAsync" /> when safe.
        /// </summary>
        public static bool AutoActivateRemoteConfigUpdates
        {
            get => FirebaseRemoteConfigAdapter.AutoActivateUpdates;
            set => FirebaseRemoteConfigAdapter.AutoActivateUpdates = value;
        }

        /// <summary>
        ///     Manually activate fetched Remote Config values.
        ///     Use when <see cref="AutoActivateRemoteConfigUpdates" /> is false.
        ///     Returns true when new values were activated (<see cref="OnRemoteConfigChanged" />
        ///     fires); false when there was nothing new to apply or activation failed.
        /// </summary>
        public static Task<bool> ActivateRemoteConfigAsync() => FirebaseRemoteConfigAdapter.ActivateAsync();

        // Single resolution path shared by every getter (string and typed), so one logical key
        // can never be served by different providers depending on the type it is read as.
        static bool TryResolve(string key, out string raw)
        {
            if (FirebaseRemoteConfigAdapter.TryGetRaw(key, out raw)) return true;
            if (GameAnalyticsAdapter.TryGetRemoteConfigValue(key, out raw)) return true;
            if (RemoteConfigState.TryGetDefault(key, out raw)) return true;
            // Only meaningful once values are readable; before that, missing keys are expected.
            if (RemoteConfigState.Level > RemoteConfigState.LevelDefaults)
                WarnOnce(key, "not found in remote config, GameAnalytics, or registered defaults; serving call-site default. Check for a typo or an unpublished template parameter");
            return false;
        }

        static void WarnOnce(string key, string message)
        {
            if (!Debug.isDebugBuild || !s_rcWarnedKeys.Add(key)) return;
            PaletteLog.Warning($"{RcTag} Remote Config key '{key}': {message}.");
        }
    }
}
