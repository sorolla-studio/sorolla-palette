using NUnit.Framework;
using Sorolla.Palette;
using Sorolla.Palette.Editor;
using Sorolla.Palette.Editor.Greenlight;
using Sorolla.Palette.Health;
using UnityEngine;

namespace Sorolla.Palette.Editor.Tests
{
    /// <summary>
    ///     The REAL producer boundary (review C5): a serialized <see cref="SorollaConfig"/> → the
    ///     <see cref="SorollaSettings"/> accessors → the <see cref="GreenlightAdapter.BuildContext"/> the
    ///     evaluator runs on. Synthetic <c>EvaluationContext.IntendedTargets/CommerceTargets</c> tests never
    ///     exercise this seam, which is exactly where an old asset omits the new fields and where the
    ///     enum-mapping lives. Uses an injected config so no Resources asset is touched.
    /// </summary>
    public class SorollaSettingsTargetsTests
    {
        SorollaConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<SorollaConfig>();
            SorollaSettings.ConfigOverride = _config;
        }

        [TearDown]
        public void TearDown()
        {
            SorollaSettings.ConfigOverride = null;
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void AndroidOnly_MapsAndroidDistribution()
        {
            _config.distributionPlatforms = SorollaPlatforms.Android;
            Assert.AreEqual(DistributionTargets.Android, SorollaSettings.IntendedTargets);
        }

        [Test]
        public void IosOnly_MapsIosDistribution()
        {
            _config.distributionPlatforms = SorollaPlatforms.iOS;
            Assert.AreEqual(DistributionTargets.iOS, SorollaSettings.IntendedTargets);
        }

        [Test]
        public void BothPlatforms_MapsBoth()
        {
            _config.distributionPlatforms = SorollaPlatforms.Android | SorollaPlatforms.iOS;
            Assert.AreEqual(HealthEnums.AllTargetBits, SorollaSettings.IntendedTargets);
        }

        [Test]
        public void Undeclared_MapsNone()
        {
            // A fresh config leaves both enums at None - the undeclared, fail-closed state.
            Assert.AreEqual(DistributionTargets.None, SorollaSettings.IntendedTargets);
            Assert.AreEqual(DistributionTargets.None, SorollaSettings.CommerceTargets);
        }

        [Test]
        public void LegacyAssetWithoutFields_ReadsAsUndeclaredNone()
        {
            // An old SorollaConfig.asset predating these fields deserializes the missing enums as their default
            // (None) - identical to explicitly undeclared, so the greenlight fails those gates closed, not open.
            var legacy = ScriptableObject.CreateInstance<SorollaConfig>(); // no target fields ever assigned
            SorollaSettings.ConfigOverride = legacy;
            try
            {
                Assert.AreEqual(DistributionTargets.None, SorollaSettings.IntendedTargets);
                Assert.AreEqual(DistributionTargets.None, SorollaSettings.CommerceTargets);
            }
            finally
            {
                SorollaSettings.ConfigOverride = _config;
                Object.DestroyImmediate(legacy);
            }
        }

        [Test]
        public void Commerce_MapsIndependentlyOfDistribution()
        {
            _config.distributionPlatforms = SorollaPlatforms.Android;
            _config.commercePlatforms = SorollaPlatforms.iOS;
            Assert.AreEqual(DistributionTargets.Android, SorollaSettings.IntendedTargets);
            Assert.AreEqual(DistributionTargets.iOS, SorollaSettings.CommerceTargets);
        }

        [Test]
        public void BuildContext_SurfacesBothTargetAxesFromConfig()
        {
            // The full seam: config → settings → the context the evaluator actually runs on.
            _config.distributionPlatforms = SorollaPlatforms.Android;
            _config.commercePlatforms = SorollaPlatforms.iOS;
            EvaluationContext ctx = GreenlightAdapter.BuildContext();
            Assert.AreEqual(DistributionTargets.Android, ctx.IntendedTargets);
            Assert.AreEqual(DistributionTargets.iOS, ctx.CommerceTargets);
        }
    }
}
