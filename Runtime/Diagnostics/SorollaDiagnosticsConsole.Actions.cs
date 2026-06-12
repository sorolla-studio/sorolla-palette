using System.Collections.Generic;
using UnityEngine;

namespace Sorolla.Palette
{
    internal sealed partial class SorollaDiagnosticsConsole
    {
        void TestRewardedAd()
        {
            Palette.ShowRewardedAd(
                () =>
                {
                    SorollaDiagnostics.RecordEventDispatch("vitals", "test_rewarded_complete");
                    RequestDiagnosticsRefresh();
                },
                () =>
                {
                    SorollaDiagnostics.RecordEventDispatch("vitals", "test_rewarded_failed");
                    RequestDiagnosticsRefresh();
                });
            RequestDiagnosticsRefresh();
        }

        void TestInterstitialAd()
        {
            Palette.ShowInterstitialAd(
                () =>
                {
                    SorollaDiagnostics.RecordEventDispatch("vitals", "test_interstitial_complete");
                    RequestDiagnosticsRefresh();
                },
                () =>
                {
                    SorollaDiagnostics.RecordEventDispatch("vitals", "test_interstitial_failed");
                    RequestDiagnosticsRefresh();
                });
            RequestDiagnosticsRefresh();
        }

        // Wraps a console/bridge test action so its events are tagged for vendors and excluded from the
        // game-integration health counters (DR-33/DR-60). The tag param carried in each call additionally
        // marks the event for Firebase; the scope covers the schema-fixed paths (progression/economy).
        static void RunTestAction(System.Action action)
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

        void TrackVitalsTestEvent()
        {
            RunTestAction(() => Palette.TrackEvent("sorolla_vitals_test", new Dictionary<string, object>
            {
                { SorollaDiagnostics.QaTestEventParam, true },
                { "build_type", Debug.isDebugBuild ? "development" : "release" },
                { "platform", Application.platform.ToString() },
                { "uptime_sec", Mathf.RoundToInt(Time.realtimeSinceStartup) },
            }));
            RequestDiagnosticsRefresh();
        }

        void TrackVitalsLevelStart()
        {
            RunTestAction(() => Palette.Level.Start(1, world: 1, new Dictionary<string, object>
            {
                { SorollaDiagnostics.QaTestEventParam, true },
                { "source", "sorolla_vitals" },
            }));
            RequestDiagnosticsRefresh();
        }

        void TrackVitalsLevelComplete()
        {
            RunTestAction(() => Palette.Level.Complete(1, world: 1, score: 123, new Dictionary<string, object>
            {
                { SorollaDiagnostics.QaTestEventParam, true },
                { "source", "sorolla_vitals" },
            }));
            RequestDiagnosticsRefresh();
        }

        void TrackVitalsEconomyEarn()
        {
            RunTestAction(() => Palette.Economy.Earn(CurrencyId.Coins, 25, EconomySource.AdReward, "sorolla_vitals_reward",
                new Dictionary<string, object> { { SorollaDiagnostics.QaTestEventParam, true } }));
            RequestDiagnosticsRefresh();
        }

        void TrackVitalsEconomySpend()
        {
            RunTestAction(() => Palette.Economy.Spend(CurrencyId.Coins, 10, EconomySink.Booster, "sorolla_vitals_booster",
                new Dictionary<string, object> { { SorollaDiagnostics.QaTestEventParam, true } }));
            RequestDiagnosticsRefresh();
        }

        void ShowPrivacyOptionsProbe()
        {
            Palette.ShowPrivacyOptions(() =>
            {
                SorollaDiagnostics.RecordEventDispatch("vitals", "privacy_options_closed");
                SorollaDiagnostics.RefreshIdentifiers();
                RequestDiagnosticsRefresh();
            });
            RequestDiagnosticsRefresh();
        }

        void ToggleQaBridge()
        {
            if (QaBridgeServer.IsArmed)
                QaBridgeServer.Disarm();
            else
                QaBridgeServer.Arm();
            RequestDiagnosticsRefresh();
        }

        void RefreshConsentProbe()
        {
            Palette.RefreshConsentStatus();
            SorollaDiagnostics.RecordEventDispatch("vitals", "consent_refreshed", new Dictionary<string, object>
            {
                { "status", Palette.ConsentStatus.ToString() },
                { "can_request_ads", Palette.CanRequestAds },
            });
            SorollaDiagnostics.RefreshIdentifiers();
            RequestDiagnosticsRefresh();
        }
    }
}
