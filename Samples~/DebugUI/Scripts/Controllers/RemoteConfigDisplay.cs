using System.Collections.Generic;
using System.Linq;
using Sorolla.Palette.Adapters;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sorolla.Palette.DebugUI
{
    /// <summary>
    ///     Displays remote config key-value pairs from Firebase.
    ///     Supports real-time updates, in-app defaults, and manual activation.
    /// </summary>
    public class RemoteConfigDisplay : UIComponentBase
    {
        [SerializeField] GameObject configRowPrefab;
        [SerializeField] Transform container;
        [SerializeField] Button fetchButton;
        [SerializeField] Button setDefaultsButton;
        [SerializeField] Button activateButton;

        [Header("Config Keys (leave empty to show all)")]
        [SerializeField] string[] _keysToDisplay;

        readonly List<GameObject> _rows = new List<GameObject>();

        void Awake()
        {
            fetchButton.onClick.AddListener(HandleFetchClicked);
            if (setDefaultsButton != null)
                setDefaultsButton.onClick.AddListener(HandleSetDefaultsClicked);
            if (activateButton != null)
                activateButton.onClick.AddListener(HandleActivateClicked);
        }

        void OnDestroy()
        {
            fetchButton.onClick.RemoveListener(HandleFetchClicked);
            if (setDefaultsButton != null)
                setDefaultsButton.onClick.RemoveListener(HandleSetDefaultsClicked);
            if (activateButton != null)
                activateButton.onClick.RemoveListener(HandleActivateClicked);
        }

        protected override void SubscribeToEvents()
        {
            Palette.OnRemoteConfigUpdated += HandleRemoteConfigUpdated;
            SorollaDebugEvents.OnToggleChanged += HandleToggleChanged;
        }

        protected override void UnsubscribeFromEvents()
        {
            Palette.OnRemoteConfigUpdated -= HandleRemoteConfigUpdated;
            SorollaDebugEvents.OnToggleChanged -= HandleToggleChanged;
        }

        void Start()
        {
            container.gameObject.SetActive(false);
            UpdateActivateButtonVisibility();
            if (Palette.IsRemoteConfigReady())
                RefreshConfigDisplay();
        }

        void HandleToggleChanged(ToggleType type, bool value)
        {
            if (type != ToggleType.AutoActivateConfig) return;

            Palette.AutoActivateRemoteConfigUpdates = value;
            UpdateActivateButtonVisibility();
            DebugPanelManager.Instance?.Log(
                $"Auto-activate config: {(value ? "ON" : "OFF")}", LogSource.Firebase);
        }

        void UpdateActivateButtonVisibility()
        {
            if (activateButton != null)
                activateButton.gameObject.SetActive(!Palette.AutoActivateRemoteConfigUpdates);
        }

        void HandleRemoteConfigUpdated(IReadOnlyCollection<string> updatedKeys)
        {
            int count = updatedKeys?.Count ?? 0;
            DebugPanelManager.Instance?.Log(
                $"Real-time config update ({count} keys)", LogSource.Firebase);
            SorollaDebugEvents.RaiseShowToast($"Config updated ({count} keys)", ToastType.Info);
            RefreshConfigDisplay();
        }

        void HandleFetchClicked()
        {
            fetchButton.interactable = false;
            DebugPanelManager.Instance?.Log("Fetching Remote Config...", LogSource.Firebase);
            Palette.FetchRemoteConfig(OnFetchComplete);
        }

        void OnFetchComplete(bool success)
        {
            fetchButton.interactable = true;

            if (success)
            {
                SorollaDebugEvents.RaiseShowToast("Remote Config fetched!", ToastType.Success);
                DebugPanelManager.Instance?.Log("Remote Config fetch successful", LogSource.Firebase);
                RefreshConfigDisplay();
            }
            else
            {
                SorollaDebugEvents.RaiseShowToast("Failed to fetch config", ToastType.Error);
                DebugPanelManager.Instance?.Log("Remote Config fetch failed", LogSource.Firebase, LogLevel.Error);
            }
        }

        void HandleSetDefaultsClicked()
        {
            Palette.SetRemoteConfigDefaults(new Dictionary<string, object>
            {
                { "welcome_msg", "Hello!" },
                { "max_retries", 3 },
                { "feature_flag", true }
            });

            DebugPanelManager.Instance?.Log("Set RC defaults (3 values)", LogSource.Firebase);
            SorollaDebugEvents.RaiseShowToast("Defaults set", ToastType.Success);
            RefreshConfigDisplay();
        }

        async void HandleActivateClicked()
        {
            DebugPanelManager.Instance?.Log("Activating config...", LogSource.Firebase);
            bool result = await Palette.ActivateRemoteConfigAsync();

            if (result)
            {
                SorollaDebugEvents.RaiseShowToast("Config activated", ToastType.Success);
                DebugPanelManager.Instance?.Log("Config activated successfully", LogSource.Firebase);
                RefreshConfigDisplay();
            }
            else
            {
                SorollaDebugEvents.RaiseShowToast("Activation failed", ToastType.Error);
                DebugPanelManager.Instance?.Log("Config activation failed", LogSource.Firebase, LogLevel.Error);
            }
        }

        void RefreshConfigDisplay()
        {
            ClearRows();

            // Use specified keys or auto-discover all from Firebase
            string[] keys = _keysToDisplay != null && _keysToDisplay.Length > 0
                ? _keysToDisplay
                : FirebaseRemoteConfigAdapter.GetKeys().ToArray();

            if (keys.Length == 0)
                return;

            container.gameObject.SetActive(true);

            foreach (string key in keys)
            {
                string value = FirebaseRemoteConfigAdapter.GetString(key, "\u2014");
                AddConfigRow(key, value);
            }

            // Defer layout rebuild to next frame (required for nested layouts)
            RebuildLayoutNextFrame();
        }

        void RebuildLayoutNextFrame()
        {
            var current = container as RectTransform;
            while (current != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(current);
                current = current.parent as RectTransform;
            }
        }

        void AddConfigRow(string key, object value)
        {
            GameObject row = Instantiate(configRowPrefab, container);
            _rows.Add(row);

            var texts = row.GetComponentsInChildren<TextMeshProUGUI>();
            if (texts.Length >= 2)
            {
                texts[0].text = key;
                texts[1].text = FormatValue(value);
                texts[1].color = GetValueColor(value);
            }
        }

        void ClearRows()
        {
            foreach (GameObject row in _rows)
                Destroy(row);
            _rows.Clear();
        }

        static string FormatValue(object value) => value switch
        {
            string s => $"\"{s}\"",
            float f => f.ToString("F2"),
            bool b => b ? "true" : "false",
            _ => value?.ToString() ?? "null",
        };

        Color GetValueColor(object value) => value switch
        {
            string => Theme.accentOrange,
            float or double => Theme.accentYellow,
            int or long => Theme.accentCyan,
            bool => Theme.accentPurple,
            _ => Theme.textPrimary,
        };
    }
}
