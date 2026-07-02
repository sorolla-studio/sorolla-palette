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
        // gdpr, att, adsPresent  ->  analytics, adStorage, adPersonalization, advertiserTracking

        // --- Full mode (ad module compiled: adsPresent = true) ---
        [TestCase(ConsentStatus.Obtained,      ATTBridge.AuthorizationStatus.Authorized,     true,  true,  true,  true,  true)]
        [TestCase(ConsentStatus.Obtained,      ATTBridge.AuthorizationStatus.Denied,         true,  true,  true,  false, false)]
        [TestCase(ConsentStatus.Obtained,      ATTBridge.AuthorizationStatus.NotDetermined,  true,  true,  true,  false, false)]
        [TestCase(ConsentStatus.Obtained,      ATTBridge.AuthorizationStatus.Restricted,     true,  true,  true,  false, false)]
        [TestCase(ConsentStatus.NotApplicable, ATTBridge.AuthorizationStatus.Authorized,     true,  true,  true,  true,  true)]
        [TestCase(ConsentStatus.NotApplicable, ATTBridge.AuthorizationStatus.Denied,         true,  true,  true,  false, false)]
        [TestCase(ConsentStatus.Required,      ATTBridge.AuthorizationStatus.Authorized,     true,  true,  false, false, false)]
        [TestCase(ConsentStatus.Required,      ATTBridge.AuthorizationStatus.Denied,         true,  true,  false, false, false)]
        [TestCase(ConsentStatus.Unknown,       ATTBridge.AuthorizationStatus.Authorized,     true,  true,  false, false, false)]
        [TestCase(ConsentStatus.Denied,        ATTBridge.AuthorizationStatus.Authorized,     true,  false, false, false, false)]
        // --- Prototype mode (no ad module: adsPresent = false) ---
        // ad_storage / ad_personalization can NEVER be granted; advertiser_tracking still can
        // (Facebook attribution is not gated on ads being present - the FB-1 regression guard).
        [TestCase(ConsentStatus.NotApplicable, ATTBridge.AuthorizationStatus.Authorized,     false, true,  false, false, true)]
        [TestCase(ConsentStatus.NotApplicable, ATTBridge.AuthorizationStatus.Denied,         false, true,  false, false, false)]
        [TestCase(ConsentStatus.Unknown,       ATTBridge.AuthorizationStatus.Authorized,     false, true,  false, false, false)]
        [TestCase(ConsentStatus.Denied,        ATTBridge.AuthorizationStatus.Authorized,     false, false, false, false, false)]
        public void Resolve_MatchesTruthTable(
            ConsentStatus gdpr, ATTBridge.AuthorizationStatus att, bool adsPresent,
            bool analytics, bool adStorage, bool adPersonalization, bool advertiserTracking)
        {
            ConsentCoordinator.ConsentSignals s = ConsentCoordinator.Resolve(gdpr, att, adsPresent);
            string ctx = $"(gdpr={gdpr}, att={att}, adsPresent={adsPresent})";
            Assert.AreEqual(analytics,          s.Analytics,          $"analytics {ctx}");
            Assert.AreEqual(adStorage,          s.AdStorage,          $"ad_storage {ctx}");
            Assert.AreEqual(adPersonalization,  s.AdPersonalization,  $"ad_personalization {ctx}");
            Assert.AreEqual(advertiserTracking, s.AdvertiserTracking, $"advertiser_tracking {ctx}");
        }

        // Prototype attributes installs (Facebook) when ATT is authorized, while every in-app-ad
        // signal stays denied - the pre-unification Facebook behavior the consent split had to keep.
        [Test]
        public void Prototype_AttributesInstalls_ButGrantsNoAdSignals()
        {
            ConsentCoordinator.ConsentSignals s =
                ConsentCoordinator.Resolve(ConsentStatus.NotApplicable, ATTBridge.AuthorizationStatus.Authorized, adsPresent: false);
            Assert.IsTrue(s.AdvertiserTracking, "Prototype must attribute installs when ATT authorized");
            Assert.IsTrue(s.Analytics,          "analytics stays on in Prototype");
            Assert.IsFalse(s.AdStorage,         "no ad_storage without an ad module");
            Assert.IsFalse(s.AdPersonalization, "no ad_personalization without an ad module");
        }

        // An undecided EEA user (CMP flow not completed) must keep analytics ON: undetermined is not
        // a confirmed decline. Locks the resolver side of the DR-34 under-counting concern - only a
        // confirmed GDPR Denied turns analytics off.
        [Test]
        public void EeaUndecided_KeepsAnalyticsOn()
        {
            Assert.IsTrue(ConsentCoordinator.Resolve(ConsentStatus.Required, ATTBridge.AuthorizationStatus.Authorized, true).Analytics);
            Assert.IsTrue(ConsentCoordinator.Resolve(ConsentStatus.Unknown,  ATTBridge.AuthorizationStatus.Authorized, true).Analytics);
        }

        // A confirmed GDPR decline turns off all four signals regardless of ATT.
        [Test]
        public void GdprDenied_DisablesAllFourSignals()
        {
            ConsentCoordinator.ConsentSignals s =
                ConsentCoordinator.Resolve(ConsentStatus.Denied, ATTBridge.AuthorizationStatus.Authorized, adsPresent: true);
            Assert.IsFalse(s.Analytics);
            Assert.IsFalse(s.AdStorage);
            Assert.IsFalse(s.AdPersonalization);
            Assert.IsFalse(s.AdvertiserTracking);
        }

        // Full mode, ATT denied: ad_storage still follows the GDPR ad-consent decision, but
        // personalization and attribution (which additionally require ATT) drop.
        [Test]
        public void FullAttDeny_KeepsAdStorage_DropsPersonalizationAndAttribution()
        {
            ConsentCoordinator.ConsentSignals s =
                ConsentCoordinator.Resolve(ConsentStatus.Obtained, ATTBridge.AuthorizationStatus.Denied, adsPresent: true);
            Assert.IsTrue(s.AdStorage,          "ad_storage follows GDPR, not ATT");
            Assert.IsFalse(s.AdPersonalization, "personalization needs ATT");
            Assert.IsFalse(s.AdvertiserTracking, "attribution needs ATT");
            Assert.IsTrue(s.Analytics);
        }
    }
}
