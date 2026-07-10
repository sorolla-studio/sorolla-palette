using System;
using UnityEngine;

namespace Sorolla.Palette
{
    /// <summary>Platform a QA expectation applies to.</summary>
    [Flags]
    public enum SorollaQaPlatform
    {
        None = 0,
        Android = 1 << 0,
        iOS = 1 << 1,
        Both = Android | iOS
    }

    /// <summary>Store product type, independent of any specific IAP package's own enum.</summary>
    public enum SorollaQaSkuType
    {
        Consumable,
        NonConsumable,
        Subscription
    }

    /// <summary>Area a known-expected QA failure belongs to.</summary>
    public enum SorollaQaArea
    {
        Iap,
        Ads,
        Analytics,
        Economy,
        Addressables,
        Consent,
        Other
    }

    /// <summary>
    ///     The mode this game is meant to ship in, per the studio's own declaration. Unspecified is
    ///     the default and means "no mismatch check" - Prototype is a first-class RELEASE path
    ///     (Facebook UA tests), never a failure by itself; the only mode failure a machine can detect
    ///     is this asset's declared intent disagreeing with the build's actual mode (see DR-133: this
    ///     asset is optional and never load-bearing at init time).
    /// </summary>
    public enum SorollaQaIntendedMode
    {
        Unspecified,
        Prototype,
        Full
    }

    /// <summary>One expected store SKU (id + type). Price/vendor metadata stays in the publisher roster.</summary>
    [Serializable]
    public class SorollaQaSku
    {
        public string id;
        public SorollaQaSkuType type;
    }

    /// <summary>
    ///     A failure the verdict should NOT treat as a regression (e.g. IAP store-init failing on
    ///     Android when the game is iOS-only). Keeps the mechanical verdict from crying wolf on
    ///     known, accepted platform gaps.
    /// </summary>
    [Serializable]
    public class SorollaQaExpectedFailure
    {
        public SorollaQaArea area;
        public SorollaQaPlatform platform;
        [TextArea] public string note;
    }

    /// <summary>
    ///     Studio-owned QA expectations for this game: the subset of the publisher's per-game QA
    ///     roster (games.yaml) that a studio actually owns and that the greenlight verdict needs to
    ///     interpret results correctly. Deliberately excludes publisher-only data (SDK pin, build
    ///     methods, keystore paths, QA history, vendor secrets) - those stay in the private roster.
    ///     Optional: a missing asset means "no expectations configured", never a hard failure. Do not
    ///     make anything at init time depend on this asset (see DR-133: a null-config early-return
    ///     before subscribing init callbacks wedges the SDK forever - the same trap must not be
    ///     reintroduced via this asset).
    ///     Create via: Assets > Create > Palette > QA Expectations
    ///     Save to: Assets/Resources/SorollaQaExpectations.asset
    /// </summary>
    [CreateAssetMenu(fileName = "SorollaQaExpectations", menuName = "Palette/QA Expectations", order = 2)]
    public class SorollaQaExpectations : ScriptableObject
    {
        [Header("Mode")]
        [Tooltip("The mode this game is meant to ship in. Unspecified = no mismatch check. Prototype is a first-class release path (FB UA tests), never a failure by itself.")]
        public SorollaQaIntendedMode intendedMode = SorollaQaIntendedMode.Unspecified;

        [Header("Feature Flags")]
        public bool usesRewarded;
        public bool usesInterstitial;
        public bool hasEconomy;
        public bool tracksEconomy;
        public bool usesIap;
        public bool usesAddressables;

        [Header("In-App Purchases")]
        [Tooltip("Platforms this game sells IAP on. None/empty = no IAP store expected on any platform.")]
        public SorollaQaPlatform iapPlatforms = SorollaQaPlatform.None;

        [Tooltip("Every SKU the store is expected to return. Count is derived from this list's length.")]
        public SorollaQaSku[] expectedSkus = Array.Empty<SorollaQaSku>();

        /// <summary>Derived from <see cref="expectedSkus"/>; not itself serialized.</summary>
        public int ExpectedSkuCount => expectedSkus?.Length ?? 0;

        [Header("Known Expected Failures")]
        [Tooltip("Failures the verdict should treat as expected, not a regression (e.g. IAP init failing on a platform this game doesn't sell on).")]
        public SorollaQaExpectedFailure[] knownExpectedFailures = Array.Empty<SorollaQaExpectedFailure>();

        [Header("Ad Cadence")]
        [Tooltip("Player level the first interstitial is expected to fire at. 0 = not applicable.")]
        public int firstInterstitialAtLevel;

        [Header("Notes")]
        [TextArea] public string notes;

        static SorollaQaExpectations s_current;
        static bool s_loaded;

        /// <summary>
        ///     Lazy, cached, null-safe accessor - mirrors <see cref="Palette.Config"/>'s load pattern.
        ///     Null means "no expectations configured", a normal state (asset is optional; nothing at
        ///     SDK init time reads this). Save the asset to Assets/Resources/SorollaQaExpectations.asset.
        /// </summary>
        public static SorollaQaExpectations Current
        {
            get
            {
                if (!s_loaded)
                {
                    s_current = Resources.Load<SorollaQaExpectations>("SorollaQaExpectations");
                    s_loaded = true;
                }
                return s_current;
            }
        }
    }
}
