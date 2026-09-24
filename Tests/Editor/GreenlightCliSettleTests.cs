using NUnit.Framework;
using UnityEditor.PackageManager;

namespace Sorolla.Palette.Editor.Tests
{
    /// <summary>
    ///     The CLI waits for credential probes so the report carries real verdicts instead of "Checking...".
    ///     The wait only keys on Pending, but the validators also render "Checking..." for NotStarted, so an
    ///     unclaimed probe on the first tick used to land in the report as Incomplete. The settle pass
    ///     re-runs the checks (which is what CLAIMS such a probe) and then re-tests this predicate.
    /// </summary>
    public class GreenlightCliSettleTests
    {
        const double Deadline = 100;

        [Test]
        public void PendingProbe_BeforeDeadline_KeepsWaiting()
        {
            Assert.IsTrue(GreenlightCli.ShouldKeepWaiting(true, now: 10, deadline: Deadline));
        }

        [Test]
        public void SettledProbe_BeforeDeadline_Writes()
        {
            Assert.IsFalse(GreenlightCli.ShouldKeepWaiting(false, now: 10, deadline: Deadline));
        }

        /// <summary>The timeout is a bound, not a suggestion: past the deadline the report is written
        /// with whatever the probes have, rather than hanging a CI run forever.</summary>
        [Test]
        public void PendingProbe_PastDeadline_Writes()
        {
            Assert.IsFalse(GreenlightCli.ShouldKeepWaiting(true, now: Deadline, deadline: Deadline));
            Assert.IsFalse(GreenlightCli.ShouldKeepWaiting(true, now: 101, deadline: Deadline));
        }
    }

    /// <summary>
    ///     The SDK version is stated in three places that must agree: package.json (what the Package
    ///     Manager and the SDK-pin check read), the SdkVersion constant (what every QA snapshot and
    ///     report stamps), and the changelog. 4.0.2 shipped with the constant left at 4.0.1 and needed a
    ///     follow-up commit; this pins the two machine-readable ones together.
    /// </summary>
    public class SdkVersionConsistencyTests
    {
        [Test]
        public void SdkVersionConstant_MatchesPackageManifest()
        {
            // Asked of the Package Manager rather than by parsing a guessed path: this is the value the
            // manifest actually resolves to, and it works whether the SDK is embedded or in PackageCache.
            PackageInfo package = PackageInfo.FindForAssembly(typeof(Palette).Assembly);

            Assert.NotNull(package, "Could not resolve the package that owns Sorolla.Runtime.");
            Assert.AreEqual(package.version, Palette.SdkVersion,
                "Palette.SdkVersion and package.json disagree - bump both in the release commit.");
        }
    }
}
