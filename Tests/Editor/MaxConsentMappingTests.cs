using NUnit.Framework;
using Sorolla.Palette.Adapters;

namespace Sorolla.Palette.Editor.Tests
{
    /// <summary>
    ///     Truth-table tests for <c>MaxAdapter.ConsentFromCmp</c>: the consent status of a player MAX places
    ///     in a GDPR geography with a supported CMP. Only a recorded CMP answer counts as a refusal (DR-34);
    ///     AppLovin also places Brazil in that geography, where Google shows no form. An unreadable record
    ///     cannot rule a refusal out, so it stays Denied.
    /// </summary>
    [TestFixture]
    public class MaxConsentMappingTests
    {
        // hasUserConsent, tcStringPresent (null = unreadable), gdprApplies (-1 = unset)  ->  status
        [TestCase(true,  true,  1,  ConsentStatus.Obtained)]
        [TestCase(true,  false, 0,  ConsentStatus.Obtained)]
        [TestCase(true,  null,  -1, ConsentStatus.Obtained)]
        [TestCase(false, true,  1,  ConsentStatus.Denied)]        // EU refusal
        [TestCase(false, true,  0,  ConsentStatus.Denied)]        // recorded refusal beats a stale "does not apply"
        [TestCase(false, false, 0,  ConsentStatus.NotApplicable)] // Brazil: no form, CMP says GDPR does not apply
        [TestCase(false, false, 1,  ConsentStatus.Required)]      // EU, form not answered yet
        [TestCase(false, false, -1, ConsentStatus.Required)]      // CMP has not written its record
        [TestCase(false, null,  0,  ConsentStatus.Denied)]
        [TestCase(false, null,  1,  ConsentStatus.Denied)]
        [TestCase(false, null,  -1, ConsentStatus.Denied)]
        public void ConsentFromCmp(bool hasUserConsent, bool? tcStringPresent, int gdprApplies, ConsentStatus expected)
        {
            Assert.AreEqual(expected, MaxAdapter.ConsentFromCmp(hasUserConsent, tcStringPresent, gdprApplies));
        }
    }
}
