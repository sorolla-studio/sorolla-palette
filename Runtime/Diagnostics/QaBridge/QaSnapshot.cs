using System.Text;

namespace Sorolla.Palette
{
    /// <summary>
    ///     Serializes <see cref="SorollaQaState"/> into the QA bridge <c>/qa/snapshot</c> JSON.
    ///     <see cref="WriteJson"/> is a pure function over the captured state (EditMode-testable);
    ///     <see cref="Build"/> captures live state on the main thread and serializes it.
    /// </summary>
    internal static class QaSnapshot
    {
        /// <summary>Snapshot JSON schema version (review C4-08). An Editor consumer must reject an unknown
        /// schema rather than parse it permissively. Bump when the shape changes incompatibly.</summary>
        internal const string SchemaVersion = "1";

        /// <summary>Captures live SDK state (must run on the Unity main thread) and returns the snapshot JSON.</summary>
        internal static string Build()
        {
            SorollaQaState state = SorollaDiagnostics.CaptureQaState();
            var sb = new StringBuilder(2048);
            WriteJson(state, sb);
            return sb.ToString();
        }

        /// <summary>Pure serializer: writes <paramref name="state"/> as a JSON object into <paramref name="sb"/>.</summary>
        internal static void WriteJson(in SorollaQaState state, StringBuilder sb)
        {
            bool first = true;
            sb.Append('{');

            QaJson.StringMember(sb, ref first, "snapshot_schema", SchemaVersion);
            QaJson.StringMember(sb, ref first, "sdk", state.SdkVersion);
            QaJson.StringMember(sb, ref first, "mode", state.Mode);
            QaJson.BoolMember(sb, ref first, "development_build", state.DevelopmentBuild);
            QaJson.BoolMember(sb, ref first, "armed", state.BridgeArmed);
            QaJson.BoolMember(sb, ref first, "ready", state.Ready);
            QaJson.StringMember(sb, ref first, "device_wall_clock", state.DeviceWallClock);

            QaJson.Comma(sb, ref first);
            QaJson.Key(sb, "build");
            WriteBuild(state, sb);

            QaJson.Comma(sb, ref first);
            QaJson.Key(sb, "consent");
            WriteConsent(state, sb);

            QaJson.Comma(sb, ref first);
            QaJson.Key(sb, "remote_config");
            WriteRemoteConfig(state, sb);

            QaJson.Comma(sb, ref first);
            QaJson.Key(sb, "adapters");
            WriteAdapters(state, sb);

            QaJson.Comma(sb, ref first);
            QaJson.Key(sb, "identity");
            WriteIdentity(state, sb);

            QaJson.Comma(sb, ref first);
            QaJson.Key(sb, "events");
            WriteEvents(state, sb);

            QaJson.Comma(sb, ref first);
            QaJson.Key(sb, "ads");
            WriteAds(state, sb);

            QaJson.Comma(sb, ref first);
            QaJson.Key(sb, "iap");
            WriteIap(state, sb);

            QaJson.Comma(sb, ref first);
            QaJson.Key(sb, "problems");
            WriteProblems(state, sb);

            sb.Append('}');
        }

        // Build identity (review C4-03): lets an Editor consumer confirm the snapshot came from the game and
        // build it expects. `mode` stays at the top level; this block adds the discriminating identity.
        static void WriteBuild(in SorollaQaState state, StringBuilder sb)
        {
            bool first = true;
            sb.Append('{');
            QaJson.StringMember(sb, ref first, "application_id", state.ApplicationId);
            QaJson.StringMember(sb, ref first, "platform", state.Platform);
            QaJson.StringMember(sb, ref first, "app_version", state.AppVersion);
            QaJson.StringMember(sb, ref first, "build_guid", state.BuildGuid);
            sb.Append('}');
        }

        static void WriteEvents(in SorollaQaState state, StringBuilder sb)
        {
            sb.Append('[');
            SorollaQaEvent[] events = state.Events;
            if (events != null)
            {
                for (int i = 0; i < events.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    WriteEvent(events[i], sb);
                }
            }
            sb.Append(']');
        }

        static void WriteEvent(SorollaQaEvent e, StringBuilder sb)
        {
            bool first = true;
            sb.Append('{');
            QaJson.StringMember(sb, ref first, "name", e.Name);
            QaJson.IntMember(sb, ref first, "count", e.Count);
            QaJson.Comma(sb, ref first);
            QaJson.Key(sb, "last_params");
            WriteParams(e.LastParams, sb);
            sb.Append('}');
        }

        static void WriteParams(SorollaDiagnosticPayloadLine[] lines, StringBuilder sb)
        {
            bool first = true;
            sb.Append('{');
            if (lines != null)
            {
                for (int i = 0; i < lines.Length; i++)
                    QaJson.StringMember(sb, ref first, lines[i].Key, lines[i].Value);
            }
            sb.Append('}');
        }

        static void WriteIap(in SorollaQaState state, StringBuilder sb)
        {
            bool first = true;
            sb.Append('{');
            QaJson.BoolMember(sb, ref first, "tracking_attached", state.IapTrackingAttached);
            QaJson.IntMember(sb, ref first, "purchase_count", state.IapPurchaseCount);
            QaJson.IntMember(sb, ref first, "duplicate_count", state.IapDuplicateCount);
            QaJson.StringMember(sb, ref first, "verification", state.IapVerification);
            QaJson.StringMember(sb, ref first, "last_issue", state.IapLastIssue);
            sb.Append('}');
        }

        static void WriteRemoteConfig(in SorollaQaState state, StringBuilder sb)
        {
            bool first = true;
            sb.Append('{');
            QaJson.StringMember(sb, ref first, "status", state.RemoteConfigStatus);
            QaJson.BoolMember(sb, ref first, "fetch_seen", state.RemoteConfigFetchSeen);
            QaJson.BoolMember(sb, ref first, "fetch_success", state.RemoteConfigFetchSuccess);

            QaJson.Comma(sb, ref first);
            QaJson.Key(sb, "values");
            WriteRemoteConfigValues(state.RemoteConfigValues, sb);

            sb.Append('}');
        }

        static void WriteRemoteConfigValues(SorollaQaRcValue[] values, StringBuilder sb)
        {
            bool first = true;
            sb.Append('{');
            if (values != null)
            {
                foreach (SorollaQaRcValue v in values)
                {
                    QaJson.Comma(sb, ref first);
                    QaJson.Key(sb, v.Key);
                    bool innerFirst = true;
                    sb.Append('{');
                    QaJson.StringMember(sb, ref innerFirst, "value", v.Value);
                    QaJson.StringMember(sb, ref innerFirst, "source", v.Source);
                    sb.Append('}');
                }
            }
            sb.Append('}');
        }

        static void WriteConsent(in SorollaQaState state, StringBuilder sb)
        {
            bool first = true;
            sb.Append('{');
            QaJson.StringMember(sb, ref first, "status", state.ConsentStatus);
            QaJson.StringMember(sb, ref first, "geography", state.ConsentGeography);
            QaJson.StringMember(sb, ref first, "att", state.Att);
            QaJson.BoolMember(sb, ref first, "can_request_ads", state.CanRequestAds);
            QaJson.BoolMember(sb, ref first, "form_shown_this_session", state.ConsentFormShownThisSession);

            QaJson.Comma(sb, ref first);
            QaJson.Key(sb, "signals");
            WriteConsentSignals(state, sb);

            QaJson.Comma(sb, ref first);
            QaJson.Key(sb, "iabtcf");
            WriteIabtcf(state, sb);

            sb.Append('}');
        }

        static void WriteConsentSignals(in SorollaQaState state, StringBuilder sb)
        {
            bool first = true;
            sb.Append('{');
            // Until consent resolves the four signals are not yet known; report "unknown" rather
            // than a misleading granted/denied so a relaunch-persistence gate reads honest state.
            string analytics = SignalValue(state.ConsentSignalsKnown, state.AnalyticsStorageConsent);
            string adStorage = SignalValue(state.ConsentSignalsKnown, state.AdStorageConsent);
            string adPers = SignalValue(state.ConsentSignalsKnown, state.AdPersonalizationConsent);
            string adUser = SignalValue(state.ConsentSignalsKnown, state.AdUserDataConsent);
            QaJson.StringMember(sb, ref first, "analytics_storage", analytics);
            QaJson.StringMember(sb, ref first, "ad_storage", adStorage);
            QaJson.StringMember(sb, ref first, "ad_personalization", adPers);
            QaJson.StringMember(sb, ref first, "ad_user_data", adUser);
            sb.Append('}');
        }

        static string SignalValue(bool known, bool granted)
        {
            if (!known) return "unknown";
            return granted ? "granted" : "denied";
        }

        static void WriteIabtcf(in SorollaQaState state, StringBuilder sb)
        {
            bool first = true;
            sb.Append('{');
            QaJson.BoolMember(sb, ref first, "tc_string_present", state.TcStringPresent);
            QaJson.StringMember(sb, ref first, "purpose_consents", state.PurposeConsents ?? "");
            sb.Append('}');
        }

        static void WriteAdapters(in SorollaQaState state, StringBuilder sb)
        {
            bool first = true;
            sb.Append('{');
            QaJson.StringMember(sb, ref first, "max", state.MaxAdapter);
            QaJson.StringMember(sb, ref first, "adjust", state.AdjustAdapter);
            QaJson.StringMember(sb, ref first, "firebase", state.FirebaseAdapter);
            QaJson.StringMember(sb, ref first, "gameanalytics", state.GameAnalyticsAdapter);
            QaJson.StringMember(sb, ref first, "facebook", state.FacebookAdapter);
            QaJson.BoolMember(sb, ref first, "crashlytics_ready", state.CrashlyticsReady);
            QaJson.StringMember(sb, ref first, "crashlytics_outcome", state.CrashlyticsOutcome);
            sb.Append('}');
        }

        static void WriteIdentity(in SorollaQaState state, StringBuilder sb)
        {
            bool first = true;
            sb.Append('{');
            QaJson.StringMember(sb, ref first, "att", state.Att);
            QaJson.BoolMember(sb, ref first, "advertising_id_present", state.AdvertisingIdPresent);
            QaJson.BoolMember(sb, ref first, "advertising_id_zeroed", state.AdvertisingIdZeroed);
            QaJson.BoolMember(sb, ref first, "adjust_adid_present", state.AdjustAdidPresent);
            QaJson.StringMember(sb, ref first, "attribution_network", state.AttributionNetwork);
            QaJson.StringMember(sb, ref first, "adjust_environment", state.AdjustEnvironment);
            QaJson.BoolMember(sb, ref first, "fb_att_enabled", state.FacebookAttEnabled);
            QaJson.BoolMember(sb, ref first, "fb_att_applied", state.FacebookAttApplied);
            sb.Append('}');
        }

        static void WriteAds(in SorollaQaState state, StringBuilder sb)
        {
            bool first = true;
            sb.Append('{');

            QaJson.Comma(sb, ref first);
            QaJson.Key(sb, "interstitial");
            WriteAdFormat(sb, state.InterstitialLoaded, state.InterstitialCompleted);

            QaJson.Comma(sb, ref first);
            QaJson.Key(sb, "rewarded");
            WriteAdFormat(sb, state.RewardedLoaded, state.RewardedCompleted);

            QaJson.BoolMember(sb, ref first, "revenue_seen", state.AdRevenueSeen);
            sb.Append('}');
        }

        static void WriteAdFormat(StringBuilder sb, bool loaded, bool completed)
        {
            bool first = true;
            sb.Append('{');
            QaJson.BoolMember(sb, ref first, "loaded", loaded);
            QaJson.BoolMember(sb, ref first, "completed", completed);
            sb.Append('}');
        }

        static void WriteProblems(in SorollaQaState state, StringBuilder sb)
        {
            bool first = true;
            sb.Append('{');
            QaJson.IntMember(sb, ref first, "sdk_warnings", state.SdkWarningCount);
            QaJson.IntMember(sb, ref first, "sdk_errors", state.SdkErrorCount);
            QaJson.StringMember(sb, ref first, "last_sdk_error", state.LastSdkError);
            QaJson.IntMember(sb, ref first, "runtime_unique", state.RuntimeProblemUniqueCount);
            QaJson.IntMember(sb, ref first, "runtime_total", state.RuntimeProblemTotalCount);
            QaJson.StringMember(sb, ref first, "runtime_top", state.RuntimeProblemSummary);
            sb.Append('}');
        }
    }
}
