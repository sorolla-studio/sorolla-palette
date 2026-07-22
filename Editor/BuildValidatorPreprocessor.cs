using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     Auto-run validation before builds.
    /// </summary>
    public class BuildValidatorPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            Debug.Log("[Palette BuildValidator] Running pre-build validation...");

            // Run all auto-fixes before validation
            var fixes = BuildValidator.RunAutoFixes();
            foreach (string fix in fixes)
                Debug.Log($"[Palette BuildValidator] Auto-fix: {fix}");

            var results = BuildValidator.RunAllChecks();
            var errors = results.Where(r => r.Status == BuildValidator.ValidationStatus.Error).ToList();

            if (errors.Count > 0)
            {
                foreach (BuildValidator.ValidationResult error in errors)
                    Debug.LogError($"[Palette BuildValidator] ERROR: {error.Message}");

                throw new BuildFailedException(
                    $"Build validation failed with {errors.Count} error(s). " +
                    "Open Tools > Sorolla Palette SDK for details."
                );
            }

            // Release-readiness warnings (no release keystore, Adjust still in sandbox) are about store
            // submission, and a development build is by definition not that - warning on every dev/QA build
            // is the noise that trains people to ignore the log. The build tells us which kind it is, so
            // nothing has to be configured or remembered (2026-07-22, replacing the deleted profile knob).
            bool releaseBuild = (report.summary.options & BuildOptions.Development) == 0;
            var warnings = results
                .Where(r => r.Status == BuildValidator.ValidationStatus.Warning)
                .Where(r => releaseBuild || !Greenlight.GreenlightAdapter.IsReleaseOnly(r.Category))
                .ToList();
            foreach (BuildValidator.ValidationResult warning in warnings)
                Debug.LogWarning($"[Palette BuildValidator] WARNING: {warning.Message}");

            if (warnings.Count > 0)
                Debug.Log($"[Palette BuildValidator] Pre-build validation passed with {warnings.Count} warning(s)");
            else
                Debug.Log("[Palette BuildValidator] Pre-build validation passed");
        }
    }
}
