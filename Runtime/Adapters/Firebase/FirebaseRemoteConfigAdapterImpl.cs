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
    /// </summary>
    [Preserve]
    internal class FirebaseRemoteConfigAdapterImpl : IFirebaseRemoteConfigAdapter
    {
        const string Tag = "[Palette:RemoteConfig]";
        private bool _initRequested;
        private bool _init;
        private bool _initFailed;
        private bool _fetching;
        private bool _fetchedOnce;
        private List<Action<bool>> _fetchCallbacks;
        private Dictionary<string, object> _pendingDefaults;
        private bool _pendingAutoFetch;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        [Preserve]
        private static void Register()
        {
            FirebaseRemoteConfigAdapter.RegisterImpl(new FirebaseRemoteConfigAdapterImpl());
        }

        public bool IsReady { get; private set; }
        public bool AutoActivateUpdates { get; set; } = true;
        public event Action<IReadOnlyCollection<string>> OnConfigUpdated;

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
                    _initFailed = true;
                    InvokeFetchCallbacks(false);
                }
            });
        }

        private void InitializeInternal()
        {
            if (_init) return;
            _init = true;

#if UNITY_EDITOR
            ConfigSettings settings = FirebaseRemoteConfig.DefaultInstance.ConfigSettings;
            settings.MinimumFetchIntervalInMilliseconds = 0;
            FirebaseRemoteConfig.DefaultInstance.SetConfigSettingsAsync(settings);
#endif

            if (_pendingDefaults != null && _pendingDefaults.Count > 0)
            {
                FirebaseRemoteConfig.DefaultInstance.SetDefaultsAsync(_pendingDefaults).ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted)
                    {
                        PaletteLog.Error($"{Tag} Failed to set Remote Config defaults.");
                        PaletteLog.Verbose($"{Tag} Failed to set defaults: {task.Exception}");
                    }
                    else
                    {
                        PaletteLog.Verbose($"{Tag} Defaults set ({_pendingDefaults.Count} values)");
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

        private void OnReady()
        {
            IsReady = true;

            FirebaseRemoteConfig.DefaultInstance.OnConfigUpdateListener += OnConfigUpdateReceived;
            Application.quitting += OnApplicationQuitting;

            if (_fetchCallbacks?.Count > 0 || _pendingAutoFetch)
                FetchAndActivate(null);
        }

        private void OnConfigUpdateReceived(object sender, ConfigUpdateEventArgs args)
        {
            if (args.Error != RemoteConfigError.None)
            {
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
                        PaletteLog.Error($"{Tag} Auto-activate failed.");
                        PaletteLog.Verbose($"{Tag} Auto-activate failed: {task.Exception?.Message}");
                    }
                    else
                    {
                        OnConfigUpdated?.Invoke(updatedKeys.AsReadOnly());
                    }
                });
            }
            else
            {
                // Notify with keys but don't activate - game decides when
                OnConfigUpdated?.Invoke(updatedKeys.AsReadOnly());
            }
        }

        private void OnApplicationQuitting()
        {
            if (_init)
            {
                FirebaseRemoteConfig.DefaultInstance.OnConfigUpdateListener -= OnConfigUpdateReceived;
            }
        }

        public void SetDefaults(Dictionary<string, object> defaults)
        {
            if (defaults == null || defaults.Count == 0) return;

            if (!_init)
            {
                // Store for when init completes
                _pendingDefaults = defaults;
                return;
            }

            FirebaseRemoteConfig.DefaultInstance.SetDefaultsAsync(defaults).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    PaletteLog.Error($"{Tag} Failed to set Remote Config defaults.");
                    PaletteLog.Verbose($"{Tag} Failed to set defaults: {task.Exception}");
                }
                else
                {
                    PaletteLog.Verbose($"{Tag} Defaults set ({defaults.Count} values)");
                }
            });
        }

        public void FetchAndActivate(Action<bool> onComplete)
        {
            if (onComplete != null)
                (_fetchCallbacks ??= new()).Add(onComplete);

            if (_initFailed)
            {
                InvokeFetchCallbacks(false);
                return;
            }

            if (!_init)
            {
                if (_initRequested)
                    return; // callbacks parked, will fire after init -> OnReady -> fetch

                PaletteLog.Warning($"{Tag} Not initialized");
                InvokeFetchCallbacks(false);
                return;
            }

            if (_fetching) return; // callback parked, will fire when in-flight fetch completes

            if (_fetchedOnce)
            {
                // Auto-fetch already succeeded this session - skip redundant network call.
                // Real-time updates handle subsequent changes via OnConfigUpdateListener.
                InvokeFetchCallbacks(true);
                return;
            }

            _fetching = true;

            FirebaseRemoteConfig.DefaultInstance.FetchAndActivateAsync().ContinueWithOnMainThread(task =>
            {
                _fetching = false;

                if (task.IsFaulted)
                {
                    PaletteLog.Error($"{Tag} Fetch failed. Rebuild with verbose logging to inspect Firebase details.");
                    PaletteLog.Verbose($"{Tag} Fetch failed: {task.Exception?.Message}");
                    InvokeFetchCallbacks(false);
                    return;
                }

                var info = FirebaseRemoteConfig.DefaultInstance.Info;
                PaletteLog.Vital($"{Tag} Fetch complete (newValuesActivated: {task.Result}, lastFetchStatus: {info.LastFetchStatus})");
                _fetchedOnce = true;
                InvokeFetchCallbacks(true);
            });
        }

        private void InvokeFetchCallbacks(bool success)
        {
            if (_fetchCallbacks == null || _fetchCallbacks.Count == 0) return;
            var callbacks = _fetchCallbacks;
            _fetchCallbacks = null;
            foreach (var cb in callbacks)
                cb.Invoke(success);
        }

        public Task<bool> ActivateAsync()
        {
            if (!_init) return Task.FromResult(false);

            var tcs = new TaskCompletionSource<bool>();
            FirebaseRemoteConfig.DefaultInstance.ActivateAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    PaletteLog.Error($"{Tag} Activate failed. Rebuild with verbose logging to inspect Firebase details.");
                    PaletteLog.Verbose($"{Tag} Activate failed: {task.Exception?.Message}");
                    tcs.SetResult(false);
                }
                else
                {
                    PaletteLog.Vital($"{Tag} Config activated");
                    tcs.SetResult(true);
                }
            });
            return tcs.Task;
        }

        public string GetString(string key, string defaultValue)
        {
            if (!_init) return defaultValue;
            try
            {
                string value = FirebaseRemoteConfig.DefaultInstance.GetValue(key).StringValue;
                return string.IsNullOrEmpty(value) ? defaultValue : value;
            }
            catch (Exception ex)
            {
                PaletteLog.Error($"{Tag} Error getting Remote Config key '{key}'.");
                PaletteLog.Verbose($"{Tag} Error getting '{key}': {ex.Message}");
                return defaultValue;
            }
        }

        public bool GetBool(string key, bool defaultValue) =>
            GetTypedValue(key, defaultValue, cv => cv.BooleanValue);

        public long GetLong(string key, long defaultValue) =>
            GetTypedValue(key, defaultValue, cv => cv.LongValue);

        public int GetInt(string key, int defaultValue) => (int)GetLong(key, defaultValue);

        public double GetDouble(string key, double defaultValue) =>
            GetTypedValue(key, defaultValue, cv => cv.DoubleValue);

        public float GetFloat(string key, float defaultValue) => (float)GetDouble(key, defaultValue);

        // Shared read path for non-string typed getters: init guard, static-default detection,
        // and uniform error handling. The reader lambdas are non-capturing, so the C# compiler
        // caches them as static delegates — no per-call allocation on this read path.
        T GetTypedValue<T>(string key, T defaultValue, Func<ConfigValue, T> read)
        {
            if (!_init) return defaultValue;
            try
            {
                ConfigValue configValue = FirebaseRemoteConfig.DefaultInstance.GetValue(key);
                if (configValue.Source == ValueSource.StaticValue && string.IsNullOrEmpty(configValue.StringValue))
                    return defaultValue;
                return read(configValue);
            }
            catch (Exception ex)
            {
                PaletteLog.Error($"{Tag} Error getting Remote Config key '{key}'.");
                PaletteLog.Verbose($"{Tag} Error getting '{key}': {ex.Message}");
                return defaultValue;
            }
        }

        public IEnumerable<string> GetKeys()
        {
            if (!_init) return Array.Empty<string>();
            return FirebaseRemoteConfig.DefaultInstance.Keys;
        }
    }
}
