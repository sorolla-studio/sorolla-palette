using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Firebase.Extensions;
using Firebase.RemoteConfig;
using UnityEngine;
using UnityEngine.Scripting;

namespace Sorolla.Palette.Adapters
{
    /// <summary>
    ///     Firebase Remote Config adapter implementation. Registered at runtime.
    ///     Owns the fetch lifecycle: auto-fetch at init, retry with backoff and on
    ///     app-foreground after failure, real-time updates. Reports freshness
    ///     transitions to <see cref="RemoteConfigState" />.
    /// </summary>
    [Preserve]
    internal class FirebaseRemoteConfigAdapterImpl : IFirebaseRemoteConfigAdapter
    {
        const string Tag = "[Palette:RemoteConfig]";
        static readonly int[] RetryDelaysSeconds = { 5, 30, 120 };

        bool _initRequested;
        bool _init;
        bool _fetching;
        bool _fetchedOnce;
        int _retriesScheduled;
        bool _focusHooked;
        Dictionary<string, object> _pendingDefaults;
        bool _pendingAutoFetch;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        [Preserve]
        static void Register()
        {
            FirebaseRemoteConfigAdapter.RegisterImpl(new FirebaseRemoteConfigAdapterImpl());
        }

        public bool AutoActivateUpdates { get; set; } = true;

        public void Initialize(Dictionary<string, object> defaults, bool autoFetch)
        {
            if (_initRequested) return;
            _initRequested = true;
            // Merge, don't overwrite. Palette.Initialize calls this with defaults=null, which would
            // otherwise wipe defaults a studio set earlier via SetRemoteConfigDefaults from Awake/
            // OnEnable (DR-93). Only touch _pendingDefaults when the caller actually supplied some.
            if (defaults != null)
            {
                if (_pendingDefaults == null)
                    _pendingDefaults = defaults;
                else
                    foreach (KeyValuePair<string, object> kvp in defaults)
                        _pendingDefaults[kvp.Key] = kvp.Value;
            }
            _pendingAutoFetch = autoFetch;

            FirebaseCoreManager.Initialize(available =>
            {
                if (available)
                {
                    InitializeInternal();
                }
                else
                {
                    PaletteLog.Error($"{Tag} Firebase not available");
                    AdapterDiagnostics.Record(AdapterDiagnosticVendor.FirebaseRemoteConfig, AdapterDiagnosticStatus.Failed,
                        "firebase_unavailable", "Firebase not available");
                    RemoteConfigState.NotifyFirebaseUnavailable();
                }
            });
        }

        void InitializeInternal()
        {
            if (_init) return;

#if UNITY_EDITOR
            ConfigSettings settings = FirebaseRemoteConfig.DefaultInstance.ConfigSettings;
            settings.MinimumFetchIntervalInMilliseconds = 0;
            FirebaseRemoteConfig.DefaultInstance.SetConfigSettingsAsync(settings);
#endif

            if (_pendingDefaults != null && _pendingDefaults.Count > 0)
            {
                Dictionary<string, object> pending = _pendingDefaults;
                FirebaseRemoteConfig.DefaultInstance.SetDefaultsAsync(pending).ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted)
                    {
                        AdapterDiagnostics.Record(AdapterDiagnosticVendor.FirebaseRemoteConfig, AdapterDiagnosticStatus.Warning,
                            "defaults_failed", "Failed to set Remote Config defaults");
                        PaletteLog.Error($"{Tag} Failed to set Remote Config defaults.");
                        PaletteLog.Verbose($"{Tag} Failed to set defaults: {task.Exception}");
                    }
                    else
                    {
                        PaletteLog.Verbose($"{Tag} Defaults set ({pending.Count} values)");
                        AdapterDiagnostics.Record(AdapterDiagnosticVendor.FirebaseRemoteConfig,
                            AdapterDiagnosticStatus.DispatchAccepted, "defaults_set",
                            "Remote Config defaults set");
                    }

                    _pendingDefaults = null;
                    OnReady();
                });
            }
            else
            {
                OnReady();
            }
        }

        void OnReady()
        {
            _init = true;
            AdapterDiagnostics.Record(AdapterDiagnosticVendor.FirebaseRemoteConfig, AdapterDiagnosticStatus.Ready,
                "initialized", "Initialized");

            FirebaseRemoteConfig.DefaultInstance.OnConfigUpdateListener += OnConfigUpdateReceived;
            Application.quitting += OnApplicationQuitting;

            // Values activated in a previous session are already readable from disk.
            ConfigInfo info = FirebaseRemoteConfig.DefaultInstance.Info;
            if (info.FetchTime.Year > 2000)
            {
                PaletteLog.Vital($"{Tag} Cached config available (last fetch: {info.FetchTime:u})");
                RemoteConfigState.NotifyFirebaseCached();
            }

            if (_pendingAutoFetch)
                Fetch();
        }

        void Fetch()
        {
            // A backoff retry can fire after exiting play mode in the Editor.
            if (!Application.isPlaying) return;
            if (_fetching || _fetchedOnce || !_init) return;
            _fetching = true;

            FirebaseRemoteConfig.DefaultInstance.FetchAndActivateAsync().ContinueWithOnMainThread(task =>
            {
                _fetching = false;

                if (task.IsFaulted)
                {
                    PaletteLog.Error($"{Tag} Fetch failed. Rebuild with verbose logging to inspect Firebase details.");
                    PaletteLog.Verbose($"{Tag} Fetch failed: {task.Exception?.Message}");
                    AdapterDiagnostics.Record(AdapterDiagnosticVendor.FirebaseRemoteConfig, AdapterDiagnosticStatus.Warning,
                        "fetch_failed", "Fetch failed");
                    ScheduleRetry();
                    return;
                }

                ConfigInfo info = FirebaseRemoteConfig.DefaultInstance.Info;
                PaletteLog.Vital($"{Tag} Fetch complete (newValuesActivated: {task.Result}, lastFetchStatus: {info.LastFetchStatus})");
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.FirebaseRemoteConfig, AdapterDiagnosticStatus.DispatchAccepted,
                    "fetch_complete", $"Fetch complete: {info.LastFetchStatus}");
                _fetchedOnce = true;
                UnhookFocus();
                RemoteConfigState.NotifyFirebaseLive(null);
            });
        }

        // Transient boot-time network failures (cellular waking up, captive wifi) previously
        // lost the whole session to defaults: the single auto-fetch was the only attempt.
        // Retry on a short backoff and on every app-foreground until one fetch lands.
        void ScheduleRetry()
        {
            if (!_focusHooked)
            {
                Application.focusChanged += OnFocusChanged;
                _focusHooked = true;
            }

            if (_retriesScheduled >= RetryDelaysSeconds.Length) return;
            int delay = RetryDelaysSeconds[_retriesScheduled];
            _retriesScheduled++;
            PaletteLog.Vital($"{Tag} Retrying fetch in {delay}s (attempt {_retriesScheduled}/{RetryDelaysSeconds.Length})");
            Task.Delay(TimeSpan.FromSeconds(delay)).ContinueWithOnMainThread(_ => Fetch());
        }

        void OnFocusChanged(bool focused)
        {
            if (focused && !_fetchedOnce) Fetch();
        }

        void UnhookFocus()
        {
            if (!_focusHooked) return;
            Application.focusChanged -= OnFocusChanged;
            _focusHooked = false;
        }

        void OnConfigUpdateReceived(object sender, ConfigUpdateEventArgs args)
        {
            if (args.Error != RemoteConfigError.None)
            {
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.FirebaseRemoteConfig, AdapterDiagnosticStatus.Warning,
                    "realtime_update_error", $"Real-time update error: {args.Error}");
                PaletteLog.Warning($"{Tag} Real-time update error: {args.Error}");
                return;
            }

            var updatedKeys = args.UpdatedKeys?.ToList() ?? new List<string>();
            PaletteLog.Vital($"{Tag} Real-time update received ({updatedKeys.Count} keys)");

            if (AutoActivateUpdates)
            {
                FirebaseRemoteConfig.DefaultInstance.ActivateAsync().ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted)
                    {
                        AdapterDiagnostics.Record(AdapterDiagnosticVendor.FirebaseRemoteConfig, AdapterDiagnosticStatus.Warning,
                            "auto_activate_failed", "Auto-activate failed");
                        PaletteLog.Error($"{Tag} Auto-activate failed.");
                        PaletteLog.Verbose($"{Tag} Auto-activate failed: {task.Exception?.Message}");
                    }
                    else
                    {
                        AdapterDiagnostics.Record(AdapterDiagnosticVendor.FirebaseRemoteConfig,
                            AdapterDiagnosticStatus.DispatchAccepted, "auto_activate_complete",
                            "Real-time update activated");
                        RemoteConfigState.NotifyFirebaseLive(updatedKeys.AsReadOnly());
                    }
                });
            }
            else
            {
                // Notify with keys but don't activate - game decides when via ActivateAsync.
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.FirebaseRemoteConfig, AdapterDiagnosticStatus.Ready,
                    "realtime_update_received", "Real-time update received");
                RemoteConfigState.NotifyUpdateAvailable(updatedKeys.AsReadOnly());
            }
        }

        void OnApplicationQuitting()
        {
            if (_init)
                FirebaseRemoteConfig.DefaultInstance.OnConfigUpdateListener -= OnConfigUpdateReceived;
            UnhookFocus();
        }

        public void SetDefaults(Dictionary<string, object> defaults)
        {
            if (defaults == null || defaults.Count == 0) return;

            if (!_init)
            {
                // Merge into the pending set applied at init (same posture as Initialize).
                if (_pendingDefaults == null)
                    _pendingDefaults = defaults;
                else
                    foreach (KeyValuePair<string, object> kvp in defaults)
                        _pendingDefaults[kvp.Key] = kvp.Value;
                return;
            }

            FirebaseRemoteConfig.DefaultInstance.SetDefaultsAsync(defaults).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    AdapterDiagnostics.Record(AdapterDiagnosticVendor.FirebaseRemoteConfig, AdapterDiagnosticStatus.Warning,
                        "defaults_failed", "Failed to set Remote Config defaults");
                    PaletteLog.Error($"{Tag} Failed to set Remote Config defaults.");
                    PaletteLog.Verbose($"{Tag} Failed to set defaults: {task.Exception}");
                }
                else
                {
                    PaletteLog.Verbose($"{Tag} Defaults set ({defaults.Count} values)");
                    AdapterDiagnostics.Record(AdapterDiagnosticVendor.FirebaseRemoteConfig,
                        AdapterDiagnosticStatus.DispatchAccepted, "defaults_set",
                        "Remote Config defaults set");
                }
            });
        }

        public Task<bool> ActivateAsync()
        {
            if (!_init)
            {
                AdapterDiagnostics.Record(AdapterDiagnosticVendor.FirebaseRemoteConfig,
                    AdapterDiagnosticStatus.DispatchDropped, "activate_before_init",
                    "ActivateAsync called before init");
                return Task.FromResult(false);
            }

            var tcs = new TaskCompletionSource<bool>();
            FirebaseRemoteConfig.DefaultInstance.ActivateAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    AdapterDiagnostics.Record(AdapterDiagnosticVendor.FirebaseRemoteConfig, AdapterDiagnosticStatus.Warning,
                        "activate_failed", "Activate failed");
                    PaletteLog.Error($"{Tag} Activate failed. Rebuild with verbose logging to inspect Firebase details.");
                    PaletteLog.Verbose($"{Tag} Activate failed: {task.Exception?.Message}");
                    tcs.SetResult(false);
                }
                else
                {
                    // Result is false when there was nothing new to activate - don't claim
                    // Live freshness or raise a spurious change event for a no-op.
                    if (task.Result)
                    {
                        PaletteLog.Vital($"{Tag} Config activated");
                        AdapterDiagnostics.Record(AdapterDiagnosticVendor.FirebaseRemoteConfig,
                            AdapterDiagnosticStatus.DispatchAccepted, "activate_complete",
                            "Config activated");
                        RemoteConfigState.NotifyFirebaseLive(null);
                    }
                    tcs.SetResult(task.Result);
                }
            });
            return tcs.Task;
        }

        public bool TryGetSource(string key, out string source)
        {
            source = null;
            if (!_init) return false;
            try
            {
                switch (FirebaseRemoteConfig.DefaultInstance.GetValue(key).Source)
                {
                    case ValueSource.RemoteValue:
                        source = "remote";
                        return true;
                    case ValueSource.DefaultValue:
                        source = "default";
                        return true;
                    default:
                        return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        public IReadOnlyCollection<string> GetKnownKeys()
        {
            if (!_init) return Array.Empty<string>();
            try
            {
                return FirebaseRemoteConfig.DefaultInstance.Keys.ToList();
            }
            catch (Exception)
            {
                return Array.Empty<string>();
            }
        }

        public bool TryGetRaw(string key, out string value)
        {
            value = null;
            if (!_init) return false;
            try
            {
                ConfigValue configValue = FirebaseRemoteConfig.DefaultInstance.GetValue(key);
                // StaticValue = key unknown to Firebase (not remote, not cached, no in-app default).
                if (configValue.Source == ValueSource.StaticValue) return false;
                value = configValue.StringValue;
                return true;
            }
            catch (Exception ex)
            {
                PaletteLog.Error($"{Tag} Error getting Remote Config key '{key}'.");
                PaletteLog.Verbose($"{Tag} Error getting '{key}': {ex.Message}");
                return false;
            }
        }
    }
}
