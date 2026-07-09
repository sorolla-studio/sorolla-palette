namespace Sorolla.Palette
{
    /// <summary>
    ///     Plain data snapshot of SDK state for the QA bridge, captured on the Unity main thread by
    ///     <see cref="SorollaDiagnostics.CaptureQaState"/> and serialized by <see cref="QaSnapshot"/>.
    ///     Holds only values the SDK already tracks (no ad-adapter changes) so the snapshot stays out
    ///     of Adapter Endpoint Review scope. Strings are the display-formatted values diagnostics
    ///     already produces (sensitive keys masked), so serialization is trivial and PII-free.
    /// </summary>
    internal struct SorollaQaState
    {
        // Header
        public string SdkVersion;
        public string Mode;              // "full" | "prototype" | "unknown"
        public bool DevelopmentBuild;
        public bool BridgeArmed;
        public bool Ready;
        internal string DeviceWallClock; // ISO-8601 local time with UTC offset, captured with the snapshot

        // Consent
        public string ConsentStatus;     // Unknown | NotApplicable | Required | Obtained | Denied
        public string ConsentGeography;  // gdpr | non_gdpr | unknown (derived from consent status)
        public string Att;               // authorized | denied | restricted | not_determined
        public bool CanRequestAds;
        public bool ConsentFormShownThisSession;
        public bool ConsentSignalsKnown;
        public bool AdStorageConsent;
        public bool AdPersonalizationConsent;
        public bool AdUserDataConsent;
        public bool AnalyticsStorageConsent;
        public bool TcStringPresent;
        public string PurposeConsents;   // raw IAB bit string, or "" when absent

        // Remote Config (verbose-independent; sourced from Palette.RemoteConfigStatus)
        public string RemoteConfigStatus;       // "defaults" | "cached" | "live"
        public bool RemoteConfigFetchSeen;       // secondary: a Firebase fetch-complete was observed this session
        public bool RemoteConfigFetchSuccess;    // secondary: that observed fetch succeeded
        // Per-key served values: union of keys Firebase knows and registered in-app defaults.
        // Lets a release-build QA pass verify a console change reached the device without Debug.Log.
        public SorollaQaRcValue[] RemoteConfigValues;

        // Adapters (status strings; enums can grow a "failed" state later without re-keying gates)
        public string MaxAdapter;
        public string AdjustAdapter;
        public string FirebaseAdapter;
        public string GameAnalyticsAdapter;
        public string FacebookAdapter;
        internal bool CrashlyticsReady;
        internal string CrashlyticsOutcome;

        // Identity / attribution
        public bool AdvertisingIdPresent;
        public bool AdvertisingIdZeroed;
        public bool AdjustAdidPresent;
        public string AttributionNetwork;
        public string AdjustEnvironment;
        public bool FacebookAttEnabled;
        public bool FacebookAttApplied;

        // Ads (at-least-once facts; verbose-independent, already tracked)
        public bool InterstitialLoaded;
        public bool InterstitialCompleted;
        public bool RewardedLoaded;
        public bool RewardedCompleted;
        public bool AdRevenueSeen;

        // Per-name event aggregation (count + last params). Makes one end-of-run snapshot sufficient.
        public SorollaQaEvent[] Events;

        // IAP. Purchase facts the SDK already tracks; per-purchase product visibility comes through the
        // event aggregation ("purchase" event). Store-init / product-count is deferred: Unity IAP v5
        // games own the StoreController, so that needs a game-side hook (Phase 3), not an SDK surface.
        public bool IapTrackingAttached;
        public int IapPurchaseCount;
        public int IapDuplicateCount;
        public string IapVerification;
        public string IapLastIssue;

        // Red flags
        public int SdkWarningCount;
        public int SdkErrorCount;
        public string LastSdkError;
        public int RuntimeProblemUniqueCount;
        public int RuntimeProblemTotalCount;
        public string RuntimeProblemSummary;
    }

    /// <summary>
    ///     One Remote Config key in the QA snapshot: the value the getters would serve and where it
    ///     came from ("firebase_remote" fetched/cached, "firebase_default" in-app default via Firebase,
    ///     "gameanalytics", "in_app_default", or "missing").
    /// </summary>
    internal struct SorollaQaRcValue
    {
        public string Key;
        public string Value;
        public string Source;
    }

    /// <summary>One aggregated event in the QA snapshot: a dispatched name, how many times it fired, and the last params seen.</summary>
    internal struct SorollaQaEvent
    {
        public string Name;
        public int Count;
        public SorollaDiagnosticPayloadLine[] LastParams;
    }
}
