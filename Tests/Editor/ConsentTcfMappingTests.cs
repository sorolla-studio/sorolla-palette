using NUnit.Framework;
using Sorolla.Palette.Adapters;

namespace Sorolla.Palette.Editor.Tests
{
    /// <summary>
    ///     Truth-table tests for <c>ConsentCoordinator.FromTcfRecord</c>: the GDPR status read from the IAB
    ///     TCF record Google UMP keeps on the device. Only a recorded answer counts as a refusal, and it
    ///     outranks a later "does not apply" (DR-34). A record UMP has not written, or cannot be read, is
    ///     Unknown, never NotApplicable.
    /// </summary>
    [TestFixture]
    public class ConsentTcfMappingTests
    {
        // readable, tcStringPresent, gdprApplies (-1 = unset), purposeConsents  ->  status
        [TestCase(true,  true,  1,  "1111", ConsentStatus.Obtained)]      // consent region, accepted
        [TestCase(true,  true,  1,  "0111", ConsentStatus.Denied)]        // consent region, refused Purpose 1
        [TestCase(true,  true,  1,  "",     ConsentStatus.Denied)]        // answered, no purpose granted
        [TestCase(true,  true,  1,  null,   ConsentStatus.Denied)]
        [TestCase(true,  true,  0,  "0",    ConsentStatus.Denied)]        // recorded refusal beats a later "does not apply"
        [TestCase(true,  true,  0,  "1",    ConsentStatus.Obtained)]
        [TestCase(true,  false, 1,  null,   ConsentStatus.Required)]      // consent region, form not answered yet
        [TestCase(true,  false, 0,  null,   ConsentStatus.NotApplicable)] // UMP: GDPR does not apply
        [TestCase(true,  false, -1, null,   ConsentStatus.Unknown)]       // UMP has not written its record
        [TestCase(false, false, -1, null,   ConsentStatus.Unknown)]       // record unreadable
        [TestCase(false, true,  1,  "1",    ConsentStatus.Unknown)]       // unreadable: the fields are not evidence
        public void FromTcfRecord(bool readable, bool tcStringPresent, int gdprApplies, string purposeConsents, ConsentStatus expected)
        {
            Assert.AreEqual(expected, ConsentCoordinator.FromTcfRecord(readable, tcStringPresent, gdprApplies, purposeConsents));
        }
    }
}
