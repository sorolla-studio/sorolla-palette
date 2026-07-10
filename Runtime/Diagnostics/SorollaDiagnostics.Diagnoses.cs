namespace Sorolla.Palette
{
    // Phase 5 (message content pass, spec sections 3/8/10): structured WHY/SIGNAL/FIX text for every
    // row class where the SDK genuinely knows the diagnosis at the row-producing site. Populated INTO
    // the row via the Add(...) overload that accepts why/signal/fix (SorollaDiagnostics.Rows.cs) -
    // this file only builds the strings, it does not decide severity (that stays where it already
    // lives). Every string here is written against spec section 8: plain sentence first, vendor term
    // in parentheses when it's the searchable term, no overclaiming verbs, ids/counts literal.
    internal static partial class SorollaDiagnostics
    {
        // ---- Config ----

        // The "Palette mode" row's own Fail case (config missing so mode can't be determined at all -
        // distinct from SorollaConfigMissingDiagnosis, which is the "Config" group's own row for the
        // same underlying fact). Init may be wedged rather than merely running the wrong mode: see
        // DR-133, a null-config early-return before subscribing MAX's init callback.
        internal static (string why, string signal, string fix) PaletteModeUnknownDiagnosis() => (
            "No SorollaConfig asset was found in Resources, so Palette cannot determine which mode (Prototype or Full) it is running in.",
            "Init may be wedged rather than merely degraded - a null config makes InitializeMax() early-return before subscribing MAX's init callback (DR-133), so IsInitialized never flips and OnInitialized never fires on a MAX-compiled build.",
            "Palette > Configuration, then create the config asset (it must land at exactly Assets/Resources/SorollaConfig.asset).");

        internal static (string why, string signal, string fix) SorollaConfigMissingDiagnosis() => (
            "Assets/Resources/SorollaConfig.asset does not exist, or is not exactly at that path/name.",
            "Palette silently runs Prototype mode instead of Full mode; ads, Adjust, and the vendor SDKs behave as if none of them are configured.",
            "Palette window -> Run Setup, or create the asset at exactly Assets/Resources/SorollaConfig.asset. The path and filename are read via Resources.Load by string, so a rename or wrong folder is invisible until this check catches it.");

        // Adjust app token is a documented hard build gate in Full mode (BuildValidatorPreprocessor
        // throws BuildFailedException on an empty/short token) - this row is the runtime mirror of
        // that same fact, for a dev-build session where the throw hasn't happened yet.
        internal static (string why, string signal, string fix) AdjustTokenMissingDiagnosis() => (
            "SorollaConfig.adjustAppToken is empty (or too short to be real) while the SDK is in Full mode.",
            "Every Adjust call is a no-op; attribution and the Adjust ADID/Attribution rows never resolve. A device or release build will fail outright - Full mode makes this token a hard build gate.",
            "Adjust dashboard -> the app -> All Settings -> copy the App Token into SorollaConfig.adjustAppToken. Prototype mode has no such requirement if Adjust isn't needed yet.");

        // ---- SDK package missing (Full mode) ----

        internal static (string why, string signal, string fix) PackageMissingDiagnosis(string vendorLabel, string packageId) => (
            $"SorollaConfig is in Full mode, but the {vendorLabel} package ({packageId}) is not in Packages/manifest.json.",
            $"{vendorLabel} calls silently no-op; this row is the only place that shows it - no compile error, no runtime exception.",
            $"Add {packageId} to Packages/manifest.json (git dependency, pinned tag) and let Unity resolve it. See the Full-mode install checklist for the exact manifest lines.");

        // ---- Facebook (fb-failure-triage.md ladder rung 1) ----

        // The boulder-evolution case: DiagnoseProbeFailure already asks Graph whether the current
        // platform is registered and, when it isn't, writes "{platform} not registered on FB app
        // {appId}" into the row's Detail (FacebookAdapter.cs OnPlatformDiagnosisProbe). That string
        // already carries everything WHY needs; this just re-shapes it into the three-part contract
        // instead of re-deriving the fact.
        internal static (string why, string signal, string fix) FacebookPlatformNotRegisteredDiagnosis(string platformNotRegisteredDetail)
        {
            string why = $"{platformNotRegisteredDetail} (Facebook Graph API's supported_platforms list for this app does not include the platform this build is running on).";
            const string signal = "Every Facebook call from this platform is rejected. The native log shows a misleading transport error (\"Unable to complete SSL connection\" / FBSDKLog secure-network-request-failed) that looks like a network problem but is not.";
            const string fix = "Facebook developer console -> the app -> Settings -> Basic -> Add Platform, with this build's bundle/package id. No rebuild needed; restart the app once the platform is added.";
            return (why, signal, fix);
        }

        // Generic FB probe failure where the platform IS registered (or platform status is unknown,
        // e.g. offline) - the ladder's later rungs (device clock/TLS, network path, credentials) are
        // real causes the SDK cannot distinguish from inside the app. Named honestly as the boundary,
        // per the fb-failure-triage.md ladder and the WHY/SIGNAL/FIX honesty rule (spec 3): a fact the
        // SDK cannot verify never gets an invented single cause.
        internal static (string why, string signal, string fix) FacebookGenericFailureDiagnosis(string vendorDetail)
        {
            string why = $"Facebook's validation probe failed ({vendorDetail}). The app's platform registration was checked and did not explain it, so the cause is one the SDK cannot see from inside the app: device clock/certificate, network path (VPN/private DNS blocking Facebook domains), or app credentials.";
            const string signal = "Every Facebook call is rejected; siblings (Firebase, Adjust, MAX) may keep passing at the same time if the cause is Facebook-specific (e.g. a Facebook certificate expiring while others are still valid on a future-dated device clock).";
            const string fix = "Unknown from in-app data alone. Check the device clock is on automatic, then \"Copy SDK state\" (Actions) and send it to Sorolla - or walk the Facebook failure triage ladder (rung 1.5: device clock; rung 2: device network path; rung 3: does another Sorolla game on this device also fail).";
            return (why, signal, fix);
        }

        internal static (string why, string signal, string fix) FacebookDeviceClockSuspectDiagnosis(string vendorDetail)
        {
            string today = System.DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            string why = $"Facebook's validation probe failed with an SSL/TLS/certificate error ({vendorDetail}). Device-clock suspect: the device's current date is {today}.";
            const string signal = "A wrong device clock makes vendors with near-expiry TLS certificates fail while others still work.";
            const string fix = "If this date is wrong, fix Settings -> General -> Date & Time -> Set Automatically, then restart - a wrong device clock makes vendors with near-expiry TLS certificates fail while others still work.";
            return (why, signal, fix);
        }

        internal static (string why, string signal, string fix) GameAnalyticsPlatformKeyMissingDiagnosis(string platformName) => (
            $"GameAnalytics has no {platformName} key pair.",
            $"100% of {platformName} GameAnalytics events are dropped silently.",
            $"Add the {platformName} keys in Assets/Resources/GameAnalytics/Settings.asset from the GameAnalytics dashboard.");

        // ---- Consent ----

        internal static (string why, string signal, string fix) CannotRequestAdsDiagnosis() => (
            "The user has not granted the consent MAX's CMP requires before ads can be requested (GDPR/consent-mode gate), or consent was explicitly denied.",
            "No ad request leaves the device; Show rewarded/Show interstitial both report \"not loaded\" even though every other adapter row can still pass.",
            "This is often correct behavior, not a bug - a real user in a consent-required region who declines ads. To re-test the accepted-consent path: Actions -> Reset consent, then accept in the CMP form that reopens.");

        internal static (string why, string signal, string fix) AttDeniedDiagnosis() => (
            "The user denied the iOS App Tracking Transparency (ATT) prompt, or it is restricted by an MDM/parental-controls profile.",
            "IDFA reads as zeroed/unavailable; ad personalization and Adjust's device-level attribution both degrade to a non-tracking mode - this is Apple's intended behavior for a denial, not an SDK fault.",
            "Nothing to fix in the SDK. To re-test the allowed path, the ATT decision must be reset at the OS level (Settings -> the app -> Tracking) or with a fresh install; the SDK has no re-prompt API.");

        // ---- Ads ----

        // ---- Firebase (native library unavailable) ----

        // The single most common Fail this menu will ever show: FirebaseCoreManagerImpl reports
        // "Firebase native library not available in Editor..." on literally every editor playmode
        // session (no native Firebase binary in the Editor), which is a completely different fact
        // from the same class of message on a real device build (missing config file / bad project
        // setup). Distinguishing them here means editor testers stop reading this as a real failure.
        internal static (string why, string signal, string fix) FirebaseUnavailableInEditorDiagnosis(string vendorLabel) => (
            $"The Unity Editor has no native Firebase library, so {vendorLabel} cannot initialize here - this is expected in every editor playmode session, not a configuration problem.",
            $"{vendorLabel} calls no-op silently for the rest of this editor session; the row stays Fail the whole time you play in-editor.",
            "Nothing to fix for editor testing. This must be re-checked on an actual Android/iOS device build - if it still fails there, it's a real config gap (see the device-build diagnosis for the same row).");

        internal static (string why, string signal, string fix) FirebaseUnavailableOnDeviceDiagnosis(string vendorLabel) => (
            $"{vendorLabel} could not reach the native Firebase library on a real device build - most commonly a missing or misplaced config file (Assets/google-services.json for Android, Assets/GoogleService-Info.plist for iOS).",
            $"All {vendorLabel} calls are dropped; Firebase's own console shows no data from this build.",
            "Confirm the REAL config file is at Assets/google-services.json / Assets/GoogleService-Info.plist (Firebase console -> the app -> download it) - the Build Health window's own \"Found\" check fuzzy-matches the auto-generated google-services-desktop.json and can false-positive, so verify the file exists by eye, not just by the checkmark.");

        internal static (string why, string signal, string fix) AdLoadFailedDiagnosis(string format, string loadIssue) => (
            $"MAX's {format} load request finished with a failure ({(string.IsNullOrEmpty(loadIssue) ? "no fill or network error" : loadIssue)}).",
            $"Show {format.ToLowerInvariant()} reports \"not loaded\" until a retry succeeds; this is normal mediation behavior under low fill, not necessarily a misconfiguration.",
            "If this persists across multiple sessions: AppLovin MAX dashboard -> check the ad unit is active and has a network assigned, and that the device/region has fill for this format. A single failed load is routine and self-heals on retry.");
    }
}
