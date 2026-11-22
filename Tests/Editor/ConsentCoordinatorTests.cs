using NUnit.Framework;
using Sorolla.Palette.Adapters;
using Sorolla.Palette.ATT;

namespace Sorolla.Palette.Editor.Tests
{
    /// <summary>
    ///     Truth-table tests for <c>ConsentCoordinator.Resolve</c> - the one place consent rules live.
    ///     Pins the four resolved signals (analytics / ad_storage / ad_personalization /
    ///     advertiser_tracking) against every GDPR/UMP state x ATT status x ads-present, so a future
    ///     refactor of the consent core (AR-1 / AR-2) cannot silently drift a compliance decision.
    ///     Pure logic, no Unity runtime - matches the existing EditMode logic-test suite (sanitizers,
    ///     validators). <c>Resolve</c> is internal; reached directly via InternalsVisibleTo, not reflection.
    /// </summary>
    [TestFixture]
    public class ConsentCoordinatorTests
    {
        // gdpr, adPurposes, att, adsPresent  ->  analytics, adStorage, adPersonalization, advertiserTracking

        // --- Full mode (ad module compiled: adsPresent = true) ---
        // ATT gates only ad_personalization + advertiser_tracking; ad_storage follows GDPR alone.
        // Undecided EEA (Required / Unknown: UMP not completed) keeps analytics ON - only a confirmed
        // GDPR Denied turns it off (DR-34), and Denied turns off all four signals regardless of ATT.
        [TestCase(ConsentStatus.Obtained,      true,  ATTBridge.AuthorizationStatus.Authorized,     true,  true,  true,  true,  true)]
        [TestCase(ConsentStatus.Obtained,      true,  ATTBridge.AuthorizationStatus.Denied,         true,  true,  true,  false, false)]
        [TestCase(ConsentStatus.Obtained,      true,  ATTBridge.AuthorizationStatus.NotDetermined,  true,  true,  true,  false, false)]
        [TestCase(ConsentStatus.Obtained,      true,  ATTBridge.AuthorizationStatus.Restricted,     true,  true,  true,  false, false)]
        [TestCase(ConsentStatus.NotApplicable, true,  ATTBridge.AuthorizationStatus.Authorized,     true,  true,  true,  true,  true)]
        [TestCase(ConsentStatus.NotApplicable, true,  ATTBridge.AuthorizationStatus.Denied,         true,  true,  true,  false, false)]
        [TestCase(ConsentStatus.Required,      true,  ATTBridge.AuthorizationStatus.Authorized,     true,  true,  false, false, false)]
        [TestCase(ConsentStatus.Required,      true,  ATTBridge.AuthorizationStatus.Denied,         true,  true,  false, false, false)]
        [TestCase(ConsentStatus.Unknown,       true,  ATTBridge.AuthorizationStatus.Authorized,     true,  true,  false, false, false)]
        [TestCase(ConsentStatus.Denied,        true,  ATTBridge.AuthorizationStatus.Authorized,     true,  false, false, false, false)]
        // A GDPR answer that grants storage (Purpose 1) but refuses an ad purpose (3, 4 or 7) keeps
        // ad_storage and denies the personalized-ad signals, whatever ATT says.
        [TestCase(ConsentStatus.Obtained,      false, ATTBridge.AuthorizationStatus.Authorized,     true,  true,  true,  false, false)]
        // --- Prototype mode (no ad module: adsPresent = false) ---
        // ad_storage / ad_personalization can NEVER be granted; advertiser_tracking still can
        // (Facebook attribution is not gated on ads being present - the FB-1 regression guard).
        [TestCase(ConsentStatus.NotApplicable, true,  ATTBridge.AuthorizationStatus.Authorized,     false, true,  false, false, true)]
        [TestCase(ConsentStatus.NotApplicable, true,  ATTBridge.AuthorizationStatus.Denied,         false, true,  false, false, false)]
        [TestCase(ConsentStatus.Unknown,       true,  ATTBridge.AuthorizationStatus.Authorized,     false, true,  false, false, false)]
        [TestCase(ConsentStatus.Denied,        true,  ATTBridge.AuthorizationStatus.Authorized,     false, false, false, false, false)]
        public void Resolve_MatchesTruthTable(
            ConsentStatus gdpr, bool adPurposes, ATTBridge.AuthorizationStatus att, bool adsPresent,
            bool analytics, bool adStorage, bool adPersonalization, bool advertiserTracking)
        {
            ConsentCoordinator.ConsentSignals s = ConsentCoordinator.Resolve(gdpr, adPurposes, att, adsPresent);
            string ctx = $"(gdpr={gdpr}, adPurposes={adPurposes}, att={att}, adsPresent={adsPresent})";
            Assert.AreEqual(analytics,          s.Analytics,          $"analytics {ctx}");
            Assert.AreEqual(adStorage,          s.AdStorage,          $"ad_storage {ctx}");
            Assert.AreEqual(adPersonalization,  s.AdPersonalization,  $"ad_personalization {ctx}");
            Assert.AreEqual(advertiserTracking, s.AdvertiserTracking, $"advertiser_tracking {ctx}");
        }

        // The privacy-options entry point follows the same GDPR status the signals above do, so a
        // player UMP resolved as outside GDPR is not offered a button for a form that governs
        // nothing. UMP still owns whether a form exists at all (the bool); this only stops its
        // requirement status from outvoting an explicit "GDPR does not apply".
        //
        // gdpr, umpRequiresPrivacyOptions  ->  offered
        [TestCase(ConsentStatus.Obtained,      true,  true)]
        [TestCase(ConsentStatus.Denied,        true,  true)]
        [TestCase(ConsentStatus.Required,      true,  true)]
        // Record unread or unwritten: keep the button rather than strand a player who cannot
        // otherwise reach their own consent choices.
        [TestCase(ConsentStatus.Unknown,       true,  true)]
        // Brazil: AppLovin's Gdpr geography makes UMP answer REQUIRED while UMP's own record says
        // IABTCF_gdprApplies=0. No button.
        [TestCase(ConsentStatus.NotApplicable, true,  false)]
        // UMP says there is no form to reopen: never a button, whatever the region.
        [TestCase(ConsentStatus.Obtained,      false, false)]
        [TestCase(ConsentStatus.Required,      false, false)]
        [TestCase(ConsentStatus.NotApplicable, false, false)]
        public void OffersPrivacyOptions_FollowsResolvedGdprStatus(
            ConsentStatus gdpr, bool umpRequiresPrivacyOptions, bool offered)
        {
            Assert.AreEqual(offered, ConsentCoordinator.OffersPrivacyOptions(gdpr, umpRequiresPrivacyOptions),
                $"(gdpr={gdpr}, umpRequiresPrivacyOptions={umpRequiresPrivacyOptions})");
        }
    }
}
