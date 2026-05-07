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

        void TrackVitalsTestEvent()
        {
            Palette.TrackEvent("sorolla_vitals_test", new Dictionary<string, object>
            {
                { "build_type", Debug.isDebugBuild ? "development" : "release" },
                { "platform", Application.platform.ToString() },
                { "uptime_sec", Mathf.RoundToInt(Time.realtimeSinceStartup) },
            });
            RequestDiagnosticsRefresh();
        }

        void TrackVitalsLevelStart()
        {
            Palette.Level.Start(1, world: 1, new Dictionary<string, object>
            {
                { "source", "sorolla_vitals" },
            });
            RequestDiagnosticsRefresh();
        }

        void TrackVitalsLevelComplete()
        {
            Palette.Level.Complete(1, world: 1, score: 123, new Dictionary<string, object>
            {
                { "source", "sorolla_vitals" },
            });
            RequestDiagnosticsRefresh();
        }

        void TrackVitalsEconomyEarn()
        {
            Palette.Economy.Earn(CurrencyId.Coins, 25, EconomySource.AdReward, "sorolla_vitals_reward");
            RequestDiagnosticsRefresh();
        }

        void TrackVitalsEconomySpend()
        {
            Palette.Economy.Spend(CurrencyId.Coins, 10, EconomySink.Booster, "sorolla_vitals_booster");
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

        void RefreshVitalsProbe()
        {
            Palette.RefreshConsentStatus();
            SorollaDiagnostics.RefreshIdentifiers();
            RequestDiagnosticsRefresh();
        }
    }
}
