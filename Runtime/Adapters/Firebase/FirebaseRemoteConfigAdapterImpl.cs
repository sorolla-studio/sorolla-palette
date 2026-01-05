using System;
using System.Collections.Generic;
using Firebase.Extensions;
using Firebase.RemoteConfig;
using UnityEngine;

namespace Sorolla.Palette.Adapters
{
    /// <summary>
    ///     Firebase Remote Config adapter implementation. Registered at runtime.
    /// </summary>
    internal class FirebaseRemoteConfigAdapterImpl : IFirebaseRemoteConfigAdapter
    {
        private const string Tag = "[Sorolla:RemoteConfig]";
        private bool _initRequested;
        private bool _init;
        private bool _fetching;
        private Dictionary<string, object> _pendingDefaults;
        private bool _pendingAutoFetch;
        private Action<bool> _pendingFetchCallback;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            FirebaseRemoteConfigAdapter.RegisterImpl(new FirebaseRemoteConfigAdapterImpl());
        }

        public bool IsReady { get; private set; }

        public void Initialize(Dictionary<string, object> defaults, bool autoFetch)
        {
            if (_initRequested) return;
            _initRequested = true;
            _pendingDefaults = defaults;
            _pendingAutoFetch = autoFetch;

            FirebaseCoreManager.Initialize(available =>
            {
                if (available)
                {
                    InitializeInternal();
                }
                else
                {
                    Debug.LogError($"{Tag} Firebase not available");
                    if (_pendingFetchCallback != null)
                    {
                        var callback = _pendingFetchCallback;
                        _pendingFetchCallback = null;
                        callback.Invoke(false);
                    }
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
                        Debug.LogError($"{Tag} Failed to set defaults: {task.Exception}");
                    else
                        Debug.Log($"{Tag} Defaults set ({_pendingDefaults.Count} values)");

                    _pendingDefaults = null;
                });
            }

            if (_pendingFetchCallback != null)
            {
                var callback = _pendingFetchCallback;
                _pendingFetchCallback = null;
                FetchAndActivate(callback);
            }
            else if (_pendingAutoFetch)
            {
                FetchAndActivate(null);
            }
            else
            {
                IsReady = true;
            }
        }

        public void FetchAndActivate(Action<bool> onComplete)
        {
            if (!_init)
            {
                if (_initRequested)
                {
                    _pendingFetchCallback = onComplete;
                    return;
                }

                Debug.LogWarning($"{Tag} Not initialized");
                onComplete?.Invoke(false);
                return;
            }

            if (_fetching) return;
            _fetching = true;

            FirebaseRemoteConfig.DefaultInstance.FetchAndActivateAsync().ContinueWithOnMainThread(task =>
            {
                _fetching = false;
                IsReady = true;

                if (task.IsFaulted)
                {
                    Debug.LogError($"{Tag} Fetch failed: {task.Exception?.Message}");
                    onComplete?.Invoke(false);
                    return;
                }

                Debug.Log($"{Tag} Fetch complete (activated: {task.Result})");
                onComplete?.Invoke(true);
            });
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
                Debug.LogError($"{Tag} Error getting '{key}': {ex.Message}");
                return defaultValue;
            }
        }

        public bool GetBool(string key, bool defaultValue)
        {
            if (!_init) return defaultValue;
            try
            {
                ConfigValue configValue = FirebaseRemoteConfig.DefaultInstance.GetValue(key);
                if (configValue.Source == ValueSource.StaticValue && string.IsNullOrEmpty(configValue.StringValue))
                    return defaultValue;
                return configValue.BooleanValue;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Tag} Error getting '{key}': {ex.Message}");
                return defaultValue;
            }
        }

        public long GetLong(string key, long defaultValue)
        {
            if (!_init) return defaultValue;
            try
            {
                ConfigValue configValue = FirebaseRemoteConfig.DefaultInstance.GetValue(key);
                if (configValue.Source == ValueSource.StaticValue && string.IsNullOrEmpty(configValue.StringValue))
                    return defaultValue;
                return configValue.LongValue;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Tag} Error getting '{key}': {ex.Message}");
                return defaultValue;
            }
        }

        public int GetInt(string key, int defaultValue) => (int)GetLong(key, defaultValue);

        public double GetDouble(string key, double defaultValue)
        {
            if (!_init) return defaultValue;
            try
            {
                ConfigValue configValue = FirebaseRemoteConfig.DefaultInstance.GetValue(key);
                if (configValue.Source == ValueSource.StaticValue && string.IsNullOrEmpty(configValue.StringValue))
                    return defaultValue;
                return configValue.DoubleValue;
            }
            catch (Exception ex)
            {
                Debug.LogError($"{Tag} Error getting '{key}': {ex.Message}");
                return defaultValue;
            }
        }

        public float GetFloat(string key, float defaultValue) => (float)GetDouble(key, defaultValue);

        public IEnumerable<string> GetKeys()
        {
            if (!_init) return Array.Empty<string>();
            return FirebaseRemoteConfig.DefaultInstance.Keys;
        }
    }
}
