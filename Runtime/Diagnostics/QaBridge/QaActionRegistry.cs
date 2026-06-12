using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sorolla.Palette
{
    /// <summary>
    ///     The single QA action registry: one core, two frontends. The on-screen debug console renders
    ///     these as buttons and the bridge exposes them over <c>POST /qa/exec</c>, so both invoke the
    ///     exact same code. v1 ships the generic SDK actions (ads, consent, SDK-smoke events) as
    ///     parameterless delegations to existing SDK surfaces. The delegate signature already accepts an
    ///     args bag so game-registered actions (Phase 3) slot in without re-shaping the registry.
    ///
    ///     Actions MUST be invoked on the Unity main thread (the bridge drains exec on its Update pump).
    /// </summary>
    internal static class QaActionRegistry
    {
        internal const string ShowRewarded = "show_rewarded";
        internal const string ShowInterstitial = "show_interstitial";
        internal const string OpenPrivacyOptions = "open_privacy_options";
        internal const string ResetConsent = "reset_consent";
        internal const string RefreshConsent = "refresh_consent";
        internal const string TrackTestEvent = "track_test_event";
        internal const string LevelStart = "level_start";
        internal const string LevelComplete = "level_complete";
        internal const string EconomyEarn = "economy_earn";
        internal const string EconomySpend = "economy_spend";

        static readonly Dictionary<string, Action<IDictionary<string, object>>> s_actions =
            new Dictionary<string, Action<IDictionary<string, object>>>
            {
                { ShowRewarded, _ => DoShowRewarded() },
                { ShowInterstitial, _ => DoShowInterstitial() },
                { OpenPrivacyOptions, _ => DoOpenPrivacyOptions() },
                { ResetConsent, _ => DoResetConsent() },
                { RefreshConsent, _ => DoRefreshConsent() },
                { TrackTestEvent, _ => DoTrackTestEvent() },
                { LevelStart, _ => DoLevelStart() },
                { LevelComplete, _ => DoLevelComplete() },
                { EconomyEarn, _ => DoEconomyEarn() },
                { EconomySpend, _ => DoEconomySpend() },
            };

        /// <summary>
        ///     Dispatches the named action on the calling (main) thread and returns immediately
        ///     (fire-and-ack: the snapshot is the source of truth for the outcome). Returns false with
        ///     <paramref name="detail"/> = "unknown_action" when the name is not registered.
        /// </summary>
        internal static bool TryInvoke(string name, IDictionary<string, object> args, out string detail)
        {
            if (string.IsNullOrEmpty(name) || !s_actions.TryGetValue(name, out Action<IDictionary<string, object>> action))
            {
                detail = "unknown_action";
                return false;
            }

            action(args);
            detail = null;
            return true;
        }

        // Generic SDK actions. Ad and consent actions drive SDK/MAX mechanics and are recorded via the
        // ads/vitals diagnostics paths (they do not touch the game-integration health counters). The
        // event-generating actions run inside a test-action scope and carry the QA tag, so they are
        // excluded from health counters and filterable for Firebase (DR-33/DR-60).

        static void DoShowRewarded()
        {
            Palette.ShowRewardedAd(
                () => SorollaDiagnostics.RecordEventDispatch("vitals", "test_rewarded_complete"),
                () => SorollaDiagnostics.RecordEventDispatch("vitals", "test_rewarded_failed"));
        }

        static void DoShowInterstitial()
        {
            Palette.ShowInterstitialAd(
                () => SorollaDiagnostics.RecordEventDispatch("vitals", "test_interstitial_complete"),
                () => SorollaDiagnostics.RecordEventDispatch("vitals", "test_interstitial_failed"));
        }

        static void DoOpenPrivacyOptions()
        {
            Palette.ShowPrivacyOptions(() =>
            {
                SorollaDiagnostics.RecordEventDispatch("vitals", "privacy_options_closed");
                SorollaDiagnostics.RefreshIdentifiers();
            });
        }

        static void DoResetConsent()
        {
            // AppLovin MAX MaxSdk.CmpService.ShowCmpForExistingUser both RE-SHOWS the consent form AND
            // RESETS the user's existing consent (verified against AppLovin/Axon docs 2026-06-12). It is
            // the same supported call behind open_privacy_options - there is no separate reset API - so
            // re-testing a consent scenario no longer needs a reinstall (only iOS ATT still does).
            // reset_consent records a distinct marker so a QA run can tell a consent re-test apart from a
            // settings-screen privacy-options open.
            SorollaDiagnostics.RecordEventDispatch("vitals", "consent_reset_requested");
            Palette.ShowPrivacyOptions(() =>
            {
                SorollaDiagnostics.RecordEventDispatch("vitals", "consent_reset_closed");
                SorollaDiagnostics.RefreshIdentifiers();
            });
        }

        static void DoRefreshConsent()
        {
            Palette.RefreshConsentStatus();
            SorollaDiagnostics.RecordEventDispatch("vitals", "consent_refreshed", new Dictionary<string, object>
            {
                { "status", Palette.ConsentStatus.ToString() },
                { "can_request_ads", Palette.CanRequestAds },
            });
            SorollaDiagnostics.RefreshIdentifiers();
        }

        static void DoTrackTestEvent()
        {
            RunTestAction(() => Palette.TrackEvent("sorolla_vitals_test", new Dictionary<string, object>
            {
                { SorollaDiagnostics.QaTestEventParam, true },
                { "build_type", Debug.isDebugBuild ? "development" : "release" },
                { "platform", Application.platform.ToString() },
                { "uptime_sec", Mathf.RoundToInt(Time.realtimeSinceStartup) },
            }));
        }

        static void DoLevelStart()
        {
            RunTestAction(() => Palette.Level.Start(1, world: 1, new Dictionary<string, object>
            {
                { SorollaDiagnostics.QaTestEventParam, true },
                { "source", "sorolla_vitals" },
            }));
        }

        static void DoLevelComplete()
        {
            RunTestAction(() => Palette.Level.Complete(1, world: 1, score: 123, new Dictionary<string, object>
            {
                { SorollaDiagnostics.QaTestEventParam, true },
                { "source", "sorolla_vitals" },
            }));
        }

        static void DoEconomyEarn()
        {
            RunTestAction(() => Palette.Economy.Earn(CurrencyId.Coins, 25, EconomySource.AdReward, "sorolla_vitals_reward",
                new Dictionary<string, object> { { SorollaDiagnostics.QaTestEventParam, true } }));
        }

        static void DoEconomySpend()
        {
            RunTestAction(() => Palette.Economy.Spend(CurrencyId.Coins, 10, EconomySink.Booster, "sorolla_vitals_booster",
                new Dictionary<string, object> { { SorollaDiagnostics.QaTestEventParam, true } }));
        }

        static void RunTestAction(Action action)
        {
            SorollaDiagnostics.BeginTestAction();
            try
            {
                action();
            }
            finally
            {
                SorollaDiagnostics.EndTestAction();
            }
        }
    }
}
