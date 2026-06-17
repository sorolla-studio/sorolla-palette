using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sorolla.Palette.Adapters
{
    /// <summary>
    ///     Interface for Firebase Remote Config adapter implementation.
    ///     Lifecycle (fetch, retry, activation) is owned by the impl; it reports
    ///     state transitions to <see cref="RemoteConfigState" /> directly.
    /// </summary>
    internal interface IFirebaseRemoteConfigAdapter
    {
        bool AutoActivateUpdates { get; set; }
        void Initialize(Dictionary<string, object> defaults, bool autoFetch);
        void SetDefaults(Dictionary<string, object> defaults);
        Task<bool> ActivateAsync();
        bool TryGetRaw(string key, out string value);
    }

    /// <summary>
    ///     Firebase Remote Config adapter. Delegates to implementation when available.
    /// </summary>
    internal static class FirebaseRemoteConfigAdapter
    {
        const string Tag = "[Palette:RemoteConfig]";

        static IFirebaseRemoteConfigAdapter s_impl;

        internal static void RegisterImpl(IFirebaseRemoteConfigAdapter impl)
        {
            s_impl = impl;
            PaletteLog.Vital($"{Tag} Implementation registered");
            AdapterDiagnostics.Record(AdapterDiagnosticVendor.FirebaseRemoteConfig, AdapterDiagnosticStatus.Registered,
                "registered", "Implementation registered");
        }

        public static bool HasImpl => s_impl != null;

        /// <summary>
        ///     When true, real-time config updates are activated immediately.
        ///     Set false for games where mid-session activation would be jarring.
        /// </summary>
        public static bool AutoActivateUpdates
        {
            get => s_impl?.AutoActivateUpdates ?? true;
            set { if (s_impl != null) s_impl.AutoActivateUpdates = value; }
        }

        public static void Initialize(Dictionary<string, object> defaults = null, bool autoFetch = true)
        {
            if (s_impl != null)
                s_impl.Initialize(defaults, autoFetch);
            else
            {
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.FirebaseRemoteConfig, AdapterDiagnosticStatus.Unavailable,
                    "not_installed", "Remote Config implementation not installed");
                PaletteLog.Warning($"{Tag} Not installed");
            }
        }

        /// <summary>
        ///     Set in-app defaults. Works before or after initialization.
        /// </summary>
        public static void SetDefaults(Dictionary<string, object> defaults) => s_impl?.SetDefaults(defaults);

        /// <summary>
        ///     Manually activate fetched config. Use when AutoActivateUpdates is false.
        /// </summary>
        public static Task<bool> ActivateAsync() => s_impl?.ActivateAsync() ?? Task.FromResult(false);

        /// <summary>
        ///     Raw string value for a key known to Firebase (remote, cached, or in-app default).
        ///     False when the impl is missing, not initialized, or the key is unknown.
        /// </summary>
        public static bool TryGetRaw(string key, out string value)
        {
            value = null;
            return s_impl?.TryGetRaw(key, out value) ?? false;
        }
    }
}
