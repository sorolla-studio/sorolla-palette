using System.Collections.Generic;
using UnityEngine;

namespace Sorolla.Palette
{
    internal sealed partial class SorollaDiagnosticsConsole
    {
        void TestRewardedAd()
        {
            Palette.ShowRewardedAd(
                () => SorollaDiagnostics.RecordEventDispatch("vitals", "test_rewarded_complete"),
                () => SorollaDiagnostics.RecordEventDispatch("vitals", "test_rewarded_failed"));
        }

        void TestInterstitialAd()
        {
            Palette.ShowInterstitialAd(
                () => SorollaDiagnostics.RecordEventDispatch("vitals", "test_interstitial_complete"),
                () => SorollaDiagnostics.RecordEventDispatch("vitals", "test_interstitial_failed"));
        }

        void TrackVitalsTestEvent()
        {
            Palette.TrackEvent("sorolla_vitals_test", new Dictionary<string, object>
            {
                { "build_type", Debug.isDebugBuild ? "development" : "release" },
                { "platform", Application.platform.ToString() },
                { "uptime_sec", Mathf.RoundToInt(Time.realtimeSinceStartup) },
            });
        }
    }
}
