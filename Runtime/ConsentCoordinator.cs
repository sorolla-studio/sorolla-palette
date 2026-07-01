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
        ///     produces this, one <see cref="ApplyConsent"/> consumes it, so the boot path and the
        ///     CMP-resolution path share a single source of truth.
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
        internal static ConsentSignals Resolve(Adapters.ConsentStatus gdpr, ATTBridge.AuthorizationStatus att, bool adsPresent)
        {
            // Analytics is broader than ad consent: granted for everyone EXCEPT a confirmed GDPR
            // decline, so installs/first_open stay countable for non-GDPR (NotApplicable),
            // undetermined geography (Required/Unknown), and consenting (Obtained) users.
            bool analytics = gdpr != Adapters.ConsentStatus.Denied;
            // ad_storage follows the GDPR/UMP decision (and requires ads to exist).
            bool adStorage = adsPresent && (gdpr == Adapters.ConsentStatus.Obtained || gdpr == Adapters.ConsentStatus.NotApplicable);
            // ad_personalization / ad_user_data additionally require ATT authorization on iOS
            // (personalized ads need BOTH consent AND ATT). AttStatus returns Authorized off-iOS,
            // so this collapses to ad_storage on Android.
            bool adPersonalization = adStorage && att == ATTBridge.AuthorizationStatus.Authorized;
            // Facebook advertiser tracking is ATTRIBUTION, not in-app ad serving: it follows the
            // GDPR ad-consent decision and iOS ATT, but is NOT gated on ads being present, so a
            // Prototype build (FB used solely for attribution, no in-app ads) still attributes
            // installs when ATT-authorized. Reproduces pre-unification FB behavior on both paths:
            // Full -> (GDPR ad consent AND ATT); Prototype -> ATT only (GDPR NotApplicable, no CMP).
            bool advertiserTracking =
                (gdpr == Adapters.ConsentStatus.Obtained || gdpr == Adapters.ConsentStatus.NotApplicable)
                && att == ATTBridge.AuthorizationStatus.Authorized;
            return new ConsentSignals(analytics, adStorage, adPersonalization, advertiserTracking);
        }

        /// <summary>
        ///     Boot-time consent before any CMP/ATT resolution. Always {Analytics:true,
        ///     AdStorage:false, AdPersonalization:false}: analytics ON so first_open counts, ads
        ///     gated until OnMaxConsentChanged resolves them (Full) or absent entirely (Prototype).
        /// </summary>
        internal static ConsentSignals ResolveBootSignals()
        {
#if SOROLLA_MAX_ENABLED && APPLOVIN_MAX_INSTALLED
            // MAX/UMP owns the ad-consent decision and hasn't resolved yet; treat GDPR as Unknown so
            // ad signals stay denied until OnMaxConsentChanged refines them.
            return Resolve(Adapters.ConsentStatus.Unknown, ATTBridge.GetStatus(), adsPresent: true);
#else
            // No MAX module: not a UMP/GDPR context and no ads to gate.
            return Resolve(Adapters.ConsentStatus.NotApplicable, ATTBridge.GetStatus(), adsPresent: false);
#endif
        }

        /// <summary>
        ///     Idempotent fan-out of a resolved decision to every vendor. <paramref name="initial"/>
        ///     true on the boot path (adapters get Initialize), false on a CMP / mid-session
        ///     resolution (UpdateConsent). Consent analytics EVENTS are deliberately NOT here: they
        ///     stay change-gated at the call site (DR-41: markers must lead FlushPending).
        /// </summary>
        internal static void ApplyConsent(ConsentSignals s, bool initial)
        {
            if (initial)
                GameAnalyticsAdapter.Initialize(s.Analytics, Palette.VerboseLogging);
            else
                GameAnalyticsAdapter.UpdateConsent(s.Analytics);

#if SOROLLA_FACEBOOK_ENABLED
            // Facebook = attribution: use advertiserTracking (ATT + ad consent, NOT ads-present),
            // so Prototype keeps attributing installs while Firebase ad signals stay ads-gated.
            if (initial)
                FacebookAdapter.Initialize(s.AdvertiserTracking);
            else
                FacebookAdapter.UpdateConsent(s.AdvertiserTracking);
#endif

#if FIREBASE_ANALYTICS_INSTALLED
            // Boot analytics consent is GRANTED by default (collection on) so first_open is countable
            // even before the CMP resolves; ad consent follows the resolved signals. See
            // SorollaIOSPostProcessor / GradlePropertiesFixer for the matching platform Consent Mode
            // defaults that govern the very first native ping.
            if (initial)
                FirebaseAdapter.Initialize(adStorageConsent: s.AdStorage, adPersonalizationConsent: s.AdPersonalization, analyticsConsent: s.Analytics, verboseLogging: Palette.VerboseLogging);
            else
                FirebaseAdapter.UpdateConsent(adStorageConsent: s.AdStorage, adPersonalizationConsent: s.AdPersonalization, analyticsConsent: s.Analytics);
#endif

            // Adjust is initialized later, inside OnMaxSdkInitialized (MAX docs: init other SDKs in
            // the MAX callback). On the boot pass its impl is null so this no-ops; on a CMP
            // resolution it takes the resolved ad-storage decision. Gated on ad-consent, NOT ATT
            // (disabling on ATT-deny would break SKAdNetwork / organic install attribution). Full-only.
#if SOROLLA_ADJUST_ENABLED && ADJUST_SDK_INSTALLED
            if (!initial)
                AdjustAdapter.UpdateConsent(s.AdStorage);
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
