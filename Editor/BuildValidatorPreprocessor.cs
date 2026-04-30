using System.Linq;
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
                    "Open Palette > Configuration for details."
                );
            }

            // Log warnings but don't block build
            var warnings = results.Where(r => r.Status == BuildValidator.ValidationStatus.Warning).ToList();
            foreach (BuildValidator.ValidationResult warning in warnings)
                Debug.LogWarning($"[Palette BuildValidator] WARNING: {warning.Message}");

            if (warnings.Count > 0)
                Debug.Log($"[Palette BuildValidator] Pre-build validation passed with {warnings.Count} warning(s)");
            else
                Debug.Log("[Palette BuildValidator] Pre-build validation passed");
        }
    }
}
