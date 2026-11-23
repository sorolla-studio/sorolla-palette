using Sorolla.Palette.Adapters;
using Sorolla.Palette.ATT;

namespace Sorolla.Palette
{
    /// <summary>
    ///     The one place consent rules live. Resolves GDPR/UMP + ATT into per-vendor consent
    ///     signals and fans the resolved decision out to every adapter. Extracted from Palette so
    ///     this compliance-critical logic is reachable and testable independent of the facade.
    /// </summary>
    internal static class ConsentCoordinator
    {
        /// <summary>
        ///     Resolved consent decision fanned out to every vendor. Four real signals exist in the
        ///     code: analytics (broad), ad_storage (GDPR/UMP ad consent), ad_personalization
        ///     (== ad_user_data; ad consent AND iOS ATT, gated on ads being present), and
        ///     advertiserTracking (Facebook attribution; ad consent AND iOS ATT but NOT gated on ads
        ///     being present, so Prototype still attributes installs). One <see cref="Resolve"/>
        ///     produces this, one <see cref="ApplyConsent"/> consumes it, so the boot path and every
        ///     later re-resolution share a single source of truth.
        /// </summary>
        internal readonly struct ConsentSignals
        {
            public readonly bool Analytics;
            public readonly bool AdStorage;
            public readonly bool AdPersonalization;
            public readonly bool AdvertiserTracking;

            public ConsentSignals(bool analytics, bool adStorage, bool adPersonalization, bool advertiserTracking)
            {
                Analytics = analytics;
                AdStorage = adStorage;
                AdPersonalization = adPersonalization;
                AdvertiserTracking = advertiserTracking;
            }
        }

        /// <summary>
        ///     The one place consent rules live. <paramref name="adsPresent"/> is whether the
        ///     MAX/Full ad module is compiled in; in Prototype it is false so ad signals can never
        ///     be granted (no ad-consent basis, no ads: the compliant default).
        /// </summary>
        internal static ConsentSignals Resolve(Adapters.ConsentStatus gdpr, bool adPurposes, ATTBridge.AuthorizationStatus att, bool adsPresent)
        {
            // Analytics is broader than ad consent: granted for everyone EXCEPT a confirmed GDPR
            // decline, so installs/first_open stay countable for non-GDPR (NotApplicable),
            // undetermined geography (Required/Unknown), and consenting (Obtained) users.
            bool analytics = gdpr != Adapters.ConsentStatus.Denied;
            // ad_storage follows the GDPR/UMP decision (and requires ads to exist).
            bool adStorage = adsPresent && (gdpr == Adapters.ConsentStatus.Obtained || gdpr == Adapters.ConsentStatus.NotApplicable);
            // ad_personalization / ad_user_data additionally require the TCF ad purposes (see
            // AdPurposesGranted) and ATT authorization on iOS (personalized ads need BOTH consent AND
            // ATT). AttStatus returns Authorized off-iOS, so ATT drops out on Android.
            bool adPersonalization = adStorage && adPurposes && att == ATTBridge.AuthorizationStatus.Authorized;
            // Facebook advertiser tracking is ATTRIBUTION, not in-app ad serving: it follows the
            // GDPR ad-consent decision and iOS ATT, but is NOT gated on ads being present, so a
            // Prototype build (FB used solely for attribution, no in-app ads) still attributes
            // installs when ATT-authorized. Reproduces pre-unification FB behavior on both paths:
            // Full -> (GDPR ad consent AND ATT); Prototype -> ATT only (GDPR NotApplicable, no UMP flow).
            bool advertiserTracking =
                (gdpr == Adapters.ConsentStatus.Obtained || gdpr == Adapters.ConsentStatus.NotApplicable)
                && adPurposes && att == ATTBridge.AuthorizationStatus.Authorized;
            return new ConsentSignals(analytics, adStorage, adPersonalization, advertiserTracking);
        }

        // Whether the MAX/Full ad module is compiled in (see Resolve).
#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
        const bool AdsPresent = true;
#else
        const bool AdsPresent = false;
#endif

        /// <summary>
        ///     The GDPR decision, read from the IAB TCF record Google UMP keeps on the device. Unknown
        ///     until the consent flow first reads it, so ad signals stay denied at boot (Full). Without the
        ///     MAX module there is no UMP flow and no ads to gate: NotApplicable, never re-read.
        /// </summary>
        internal static Adapters.ConsentStatus Status { get; private set; } =
#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
            Adapters.ConsentStatus.Unknown;
#else
            Adapters.ConsentStatus.NotApplicable;
#endif

        /// <summary>
        ///     Whether the TCF record grants the ad purposes behind the personalized-ad signals. Google's
        ///     Consent Mode mapping grounds ad_personalization on Purposes 3 and 4 and ad_user_data on 1
        ///     and 7; one signal feeds both, so all four. True when the record holds no answer (outside
        ///     GDPR, or not asked yet): <see cref="Status"/> alone decides then.
        /// </summary>
        internal static bool AdPurposesGranted { get; private set; } = true;

        internal static bool GrantsAdPurposes(bool tcStringPresent, string purposeConsents) =>
            !tcStringPresent || (Granted(purposeConsents, 1) && Granted(purposeConsents, 3)
                && Granted(purposeConsents, 4) && Granted(purposeConsents, 7));

        static bool Granted(string purposeConsents, int purpose) =>
            purposeConsents != null && purposeConsents.Length >= purpose && purposeConsents[purpose - 1] == '1';

        /// <summary>The current decision resolved against <paramref name="att"/>.</summary>
        internal static ConsentSignals ResolveCurrent(ATTBridge.AuthorizationStatus att) => Resolve(Status, AdPurposesGranted, att, AdsPresent);

        /// <summary>
        ///     Re-reads the TCF record into <see cref="Status"/> (Full only). Main thread only: the
        ///     record is PlayerPrefs on iOS.
        /// </summary>
        internal static Adapters.ConsentStatus Refresh()
        {
#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
            bool readable = IabTcf.Read(out bool tcStringPresent, out int gdprApplies, out string purposeConsents);
            Status = FromTcfRecord(readable, tcStringPresent, gdprApplies, purposeConsents);
            AdPurposesGranted = GrantsAdPurposes(tcStringPresent, purposeConsents);
            string record = readable
                ? $"tcString={(tcStringPresent ? "present" : "absent")}, gdprApplies={(gdprApplies < 0 ? "unset" : gdprApplies.ToString())}"
                : "record unreadable";
            PaletteLog.Vital($"[Palette] ConsentStatus: {Status} ({record})");
#endif
            return Status;
        }

        /// <summary>
        ///     GDPR status from the TCF record UMP writes. UMP is the one authority on applicability
        ///     (IABTCF_gdprApplies). Only a recorded answer counts as a refusal, and it outranks a later
        ///     "does not apply" (DR-34). The answer is Purpose 1 (store and access information on a
        ///     device), the purpose Consent Mode grounds ad_storage on. An unreadable record, or one UMP
        ///     has not written (no successful update yet, or no GDPR message published), is Unknown.
        /// </summary>
        internal static Adapters.ConsentStatus FromTcfRecord(bool readable, bool tcStringPresent, int gdprApplies, string purposeConsents)
        {
            if (!readable) return Adapters.ConsentStatus.Unknown;
            if (tcStringPresent)
                return !string.IsNullOrEmpty(purposeConsents) && purposeConsents[0] == '1'
                    ? Adapters.ConsentStatus.Obtained
                    : Adapters.ConsentStatus.Denied;
            if (gdprApplies == 1) return Adapters.ConsentStatus.Required;      // consent region, no answer yet
            if (gdprApplies == 0) return Adapters.ConsentStatus.NotApplicable; // UMP: GDPR does not apply
            return Adapters.ConsentStatus.Unknown;
        }

        /// <summary>
        ///     Idempotent fan-out of a resolved decision to every vendor. <paramref name="initial"/>
        ///     true on the boot path (adapters get Initialize), false on every re-resolution
        ///     (consent flow, privacy options, app focus: UpdateConsent). Consent analytics EVENTS are deliberately NOT here: they
        ///     stay change-gated at the call site (DR-41: markers must lead FlushPending).
        /// </summary>
        internal static void ApplyConsent(ConsentSignals s, bool initial)
        {
            // R2: guard each vendor's boot Initialize behind catch-continue so one vendor throwing
            // can't skip the others or the trailing diagnostics snapshot. UpdateConsent (the
            // re-resolution path) is left unguarded; its callers guard at their own call site.
            if (initial)
                Palette.SafeInit("GameAnalytics", () => GameAnalyticsAdapter.Initialize(s.Analytics, Palette.VerboseLogging));
            else
                GameAnalyticsAdapter.UpdateConsent(s.Analytics);

#if SOROLLA_FACEBOOK_ENABLED
            // Facebook = attribution: use advertiserTracking (ATT + ad consent, NOT ads-present),
            // so Prototype keeps attributing installs while Firebase ad signals stay ads-gated.
            if (initial)
                Palette.SafeInit("Facebook", () => FacebookAdapter.Initialize(s.AdvertiserTracking));
            else
                FacebookAdapter.UpdateConsent(s.AdvertiserTracking);
#endif

#if FIREBASE_ANALYTICS_INSTALLED
            // Boot analytics consent is GRANTED by default (collection on) so first_open is countable
            // even before UMP resolves; ad consent follows the resolved signals. See
            // SorollaIOSPostProcessor / GradlePropertiesFixer for the matching platform Consent Mode
            // defaults that govern the very first native ping.
            if (initial)
                Palette.SafeInit("Firebase Analytics", () => FirebaseAdapter.Initialize(adStorageConsent: s.AdStorage, adPersonalizationConsent: s.AdPersonalization, analyticsConsent: s.Analytics, verboseLogging: Palette.VerboseLogging));
            else
                FirebaseAdapter.UpdateConsent(adStorageConsent: s.AdStorage, adPersonalizationConsent: s.AdPersonalization, analyticsConsent: s.Analytics);
#endif

            // Adjust is initialized later, inside OnMaxSdkInitialized (MAX docs: init other SDKs in
            // the MAX callback). Until then its impl is null so this no-ops; on a later
            // re-resolution it takes the resolved ad-storage decision. Gated on ad-consent, NOT ATT
            // (disabling on ATT-deny would break SKAdNetwork / organic install attribution). Full-only.
#if SOROLLA_ADJUST_ENABLED && ADJUST_SDK_INSTALLED
            if (!initial)
                AdjustAdapter.UpdateConsent(s.AdStorage);
#endif

            // MAX takes the ad-storage decision as its GDPR consent flag. AppLovin records it at SDK
            // initialization, and the consent flow resolves before MAX starts (Palette.OnConsentGathered).
#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
            if (!initial)
                MaxAdapter.UpdateConsent(s.AdStorage);
#endif

            // QA snapshot: ad_user_data tracks ad_personalization (both gated on ATT on iOS).
            SorollaDiagnostics.RecordConsentSignals(adStorage: s.AdStorage, adPersonalization: s.AdPersonalization, adUserData: s.AdPersonalization, analyticsStorage: s.Analytics);
        }

        // On iOS, ad personalization and ad_user_data require BOTH GDPR/UMP consent AND ATT
        // authorization (Apple: personalized ads need ATT; ad_storage may still follow GDPR alone).
        // ATTBridge.GetStatus() returns Authorized off-iOS / in Editor, so this collapses to
        // adConsent on Android.
        internal static bool AdPersonalizationAllowed(bool adConsent) =>
            adConsent && ATTBridge.GetStatus() == ATTBridge.AuthorizationStatus.Authorized;

        // Lowercase snake_case GDPR/UMP decision for analytics params.
        internal static string GdprString(Adapters.ConsentStatus status) => status switch
        {
            Adapters.ConsentStatus.Obtained => "obtained",
            Adapters.ConsentStatus.Denied => "denied",
            Adapters.ConsentStatus.NotApplicable => "not_applicable",
            Adapters.ConsentStatus.Required => "required",
            _ => "unknown",
        };
    }
}
