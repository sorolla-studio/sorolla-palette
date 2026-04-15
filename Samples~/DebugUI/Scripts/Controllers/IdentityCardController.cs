using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sorolla.Palette.DebugUI
{
    /// <summary>
    ///     Controls an identity card with copy-to-clipboard functionality.
    ///     Self-sufficient - can auto-populate with real device info.
    /// </summary>
    public class IdentityCardController : UIComponentBase
    {
        public enum IdentityType
        {
            Custom,
            DeviceId,
            Platform,
            AppVersion,
            SorollaMode,
            IDFA,
            AdjustId,
        }

        [SerializeField] TextMeshProUGUI labelText;
        [SerializeField] TextMeshProUGUI valueText;
        [SerializeField] Button copyButton;
        [SerializeField] Button refreshButton;
        [SerializeField] bool showRefreshButton;
        [SerializeField] IdentityType identityType = IdentityType.Custom;
        [SerializeField] string customLabel;

        string _value;

        void Awake()
        {
            copyButton.onClick.AddListener(CopyToClipboard);
            refreshButton.onClick.AddListener(Refresh);
        }

        void OnDestroy()
        {
            copyButton.onClick.RemoveListener(CopyToClipboard);
            refreshButton.onClick.RemoveListener(Refresh);
        }

        void Refresh()
        {
            valueText.text = "Fetching...";
            AutoPopulate();
            SorollaDebugEvents.RaiseShowToast("Refreshed", ToastType.Info);
        }

        void Start()
        {
            refreshButton.gameObject.SetActive(showRefreshButton);

            if (identityType == IdentityType.AdjustId && (Palette.Config == null || Palette.Config.isPrototypeMode))
            {
                gameObject.SetActive(false);
                return;
            }

            if (identityType != IdentityType.Custom)
            {
                if (identityType == IdentityType.SorollaMode && !Palette.IsInitialized)
                {
                    // Wait for SDK init to get correct mode
                    valueText.text = "Initializing...";
                    Palette.OnInitialized += OnSorollaInitialized;
                }
                else
                {
                    AutoPopulate();
                }
            }
        }

        void OnSorollaInitialized()
        {
            Palette.OnInitialized -= OnSorollaInitialized;
            AutoPopulate();
        }

        void AutoPopulate()
        {
            string label;
            string value;

            switch (identityType)
            {
                case IdentityType.DeviceId:
                    label = "Device ID";
                    value = SystemInfo.deviceUniqueIdentifier;
                    break;
                case IdentityType.Platform:
                    label = "Platform";
                    value = $"{Application.platform} ({SystemInfo.operatingSystem})";
                    break;
                case IdentityType.AppVersion:
                    label = "App Version";
                    value = $"{Application.version} ({Application.unityVersion})";
                    break;
                case IdentityType.SorollaMode:
                    label = "SDK Mode";
                    bool isPrototype = Palette.Config == null || Palette.Config.isPrototypeMode;
                    value = isPrototype ? "Prototype" : "Full";
                    break;
                case IdentityType.IDFA:
                    label = "IDFA";
                    Setup(label, "Fetching...");
                    Palette.GetAdvertisingId(id => Setup("IDFA", id ?? "Not available"));
                    return;
                case IdentityType.AdjustId:
                    label = "Adjust ID";
                    Setup(label, "Fetching...");
                    StartCoroutine(FetchAdjustIdRoutine());
                    return;
                default:
                    label = customLabel;
                    value = "—";
                    break;
            }

            Setup(label, value);
        }

        System.Collections.IEnumerator FetchAdjustIdRoutine()
        {
            // Allow native SDK a moment to catch up
            yield return new WaitForSeconds(0.5f);

            Setup("Adjust ID", "Fetching...");
            Palette.GetAdjustId(adid =>
            {
                Setup("Adjust ID", string.IsNullOrEmpty(adid) ? "N/A" : adid);
            });
        }

        public void Setup(string label, string value)
        {
            _value = value;
            labelText.text = label;
            valueText.text = value;
        }

        void CopyToClipboard()
        {
            if (string.IsNullOrEmpty(_value)) return;

            GUIUtility.systemCopyBuffer = _value;

            SorollaDebugEvents.RaiseShowToast("Copied to clipboard!", ToastType.Success);
            DebugPanelManager.Instance?.Log($"Copied: {_value}");
        }
    }
}
