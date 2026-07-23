using System;
using System.Collections.Generic;
using UnityEngine;

namespace Sorolla.Palette
{
    /// <summary>
    ///     The single QA action registry. The on-screen debug menu renders its ordered catalog and the
    ///     bridge exposes the same actions over <c>POST /qa/exec</c>, so both invoke the exact same code.
    ///     Actions are parameterless delegations to existing SDK surfaces; the delegate signature accepts
    ///     an args bag without exposing any additional action surface.
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

        readonly struct Registration
        {
            public readonly string Name;
            public readonly Action<IDictionary<string, object>> Invoke;

            public Registration(string name, Action<IDictionary<string, object>> invoke)
            {
                Name = name;
                Invoke = invoke;
            }
        }

        static readonly Registration[] s_actions = BuildRegistrations();

        static readonly string[] s_actionNames = BuildActionNames();

        /// <summary>The complete ordered action catalog shared by the debug menu and bridge dispatcher.</summary>
        internal static IReadOnlyList<string> ActionNames => s_actionNames;

        /// <summary>
        ///     Dispatches the named action on the calling (main) thread and returns immediately
        ///     (fire-and-ack: the snapshot is the source of truth for the outcome). Returns false with
        ///     <paramref name="detail"/> = "unknown_action" when the name is not registered.
        /// </summary>
        internal static bool TryInvoke(string name, IDictionary<string, object> args, out string detail)
        {
            if (!string.IsNullOrEmpty(name))
            {
                for (int i = 0; i < s_actions.Length; i++)
                {
                    Registration registration = s_actions[i];
                    if (registration.Name != name) continue;

                    registration.Invoke(args);
                    detail = null;
                    return true;
                }
            }

            detail = "unknown_action";
            return false;
        }

        static string[] BuildActionNames()
        {
            var names = new string[s_actions.Length];
            for (int i = 0; i < s_actions.Length; i++)
                names[i] = s_actions[i].Name;
            return names;
        }

        static Registration[] BuildRegistrations()
        {
            var actions = new List<Registration>(10);
            if (SorollaRuntimeCapabilities.MaxCompiled)
            {
                actions.Add(new Registration(ShowRewarded, _ => DoShowRewarded()));
                actions.Add(new Registration(ShowInterstitial, _ => DoShowInterstitial()));
                actions.Add(new Registration(OpenPrivacyOptions, _ => DoOpenPrivacyOptions()));
                actions.Add(new Registration(ResetConsent, _ => DoResetConsent()));
                actions.Add(new Registration(RefreshConsent, _ => DoRefreshConsent()));
            }

            actions.Add(new Registration(TrackTestEvent, _ => DoTrackTestEvent()));
            actions.Add(new Registration(LevelStart, _ => DoLevelStart()));
            actions.Add(new Registration(LevelComplete, _ => DoLevelComplete()));
            actions.Add(new Registration(EconomyEarn, _ => DoEconomyEarn()));
            actions.Add(new Registration(EconomySpend, _ => DoEconomySpend()));
            return actions.ToArray();
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
