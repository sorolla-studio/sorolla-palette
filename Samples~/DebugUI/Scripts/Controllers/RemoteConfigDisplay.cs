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
    ///     If keys array is empty, auto-discovers all keys from Remote Config.
    /// </summary>
    public class RemoteConfigDisplay : UIComponentBase
    {
        [SerializeField] GameObject configRowPrefab;
        [SerializeField] Transform container;
        [SerializeField] Button fetchButton;

        [Header("Config Keys (leave empty to show all)")]
        [SerializeField] string[] _keysToDisplay;

        readonly List<GameObject> _rows = new List<GameObject>();

        void Awake() => fetchButton.onClick.AddListener(HandleFetchClicked);

        void OnDestroy() => fetchButton.onClick.RemoveListener(HandleFetchClicked);

        void Start()
        {
            container.gameObject.SetActive(false);
            if (Palette.IsRemoteConfigReady())
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
                string value = FirebaseRemoteConfigAdapter.GetString(key, "—");
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
