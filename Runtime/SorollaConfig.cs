using UnityEngine;

namespace Sorolla
{
    /// <summary>
    ///     Configuration asset for Sorolla SDK.
    ///     Create via: Assets > Create > Sorolla > Config
    ///     Save to: Assets/Resources/SorollaConfig.asset
    /// </summary>
    [CreateAssetMenu(fileName = "SorollaConfig", menuName = "Sorolla/Config", order = 1)]
    public class SorollaConfig : ScriptableObject
    {
        [Header("Mode")]
        [Tooltip("Prototype = GA + Facebook | Full = GA + MAX + Adjust")]
        public bool isPrototypeMode = true;

        [Header("AppLovin MAX")]
        [Tooltip("SDK Key from AppLovin dashboard")]
        public string maxSdkKey;

        [Tooltip("Rewarded ad unit ID")]
        public string maxRewardedAdUnitId;

        [Tooltip("Interstitial ad unit ID")]
        public string maxInterstitialAdUnitId;

        [Tooltip("Banner ad unit ID (optional)")]
        public string maxBannerAdUnitId;

        [Header("Adjust (Full Mode Only)")]
        [Tooltip("Adjust App Token")]
        public string adjustAppToken;

        /// <summary>
        ///     Validate configuration for current mode
        /// </summary>
        public bool IsValid()
        {
            if (isPrototypeMode)
                return true; // Prototype is lenient

            // Full mode requires MAX and Adjust
            if (string.IsNullOrEmpty(maxSdkKey))
            {
                Debug.LogError("[Sorolla] MAX SDK Key required in Full Mode");
                return false;
            }

            if (string.IsNullOrEmpty(adjustAppToken))
            {
                Debug.LogError("[Sorolla] Adjust App Token required in Full Mode");
                return false;
            }

            return true;
        }
    }
}
