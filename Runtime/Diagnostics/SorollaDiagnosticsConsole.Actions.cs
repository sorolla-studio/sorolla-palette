namespace Sorolla.Palette
{
    internal sealed partial class SorollaDiagnosticsConsole
    {
        // The console action buttons and the QA bridge both dispatch through QaActionRegistry: one core,
        // two frontends. The console additionally requests an immediate UI refresh; async outcomes (an ad
        // completing) surface on the console's periodic poll, the same way the bridge sees them via the
        // snapshot.

        void TestRewardedAd() => RunRegistryAction(QaActionRegistry.ShowRewarded);

        void TestInterstitialAd() => RunRegistryAction(QaActionRegistry.ShowInterstitial);

        void TrackVitalsTestEvent() => RunRegistryAction(QaActionRegistry.TrackTestEvent);

        void TrackVitalsLevelStart() => RunRegistryAction(QaActionRegistry.LevelStart);

        void TrackVitalsLevelComplete() => RunRegistryAction(QaActionRegistry.LevelComplete);

        void TrackVitalsEconomyEarn() => RunRegistryAction(QaActionRegistry.EconomyEarn);

        void TrackVitalsEconomySpend() => RunRegistryAction(QaActionRegistry.EconomySpend);

        void ShowPrivacyOptionsProbe() => RunRegistryAction(QaActionRegistry.OpenPrivacyOptions);

        void RefreshConsentProbe() => RunRegistryAction(QaActionRegistry.RefreshConsent);

        void RunRegistryAction(string action)
        {
            QaActionRegistry.TryInvoke(action, null, out _);
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
    }
}
