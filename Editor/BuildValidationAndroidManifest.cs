using System.Collections.Generic;

namespace Sorolla.Palette.Editor
{
    public static partial class BuildValidator
    {
        /// <summary>
        ///     Check Android manifest health. Uses pre-computed diagnostics from Sanitize
        ///     when available (after RunSafeAutoFixes), falls back to fresh detection.
        /// </summary>
        static List<ValidationResult> CheckAndroidManifest(
            AndroidManifestSanitizer.ManifestDiagnostics diagnostics = null)
        {
            var results = new List<ValidationResult>();
            bool hasIssues = false;

            // Use pre-computed diagnostics from SanitizeWithDiagnostics if available
            var orphaned = diagnostics?.RemainingOrphans ?? AndroidManifestSanitizer.DetectOrphanedEntries();
            var duplicates = diagnostics?.RemainingDuplicates ?? AndroidManifestSanitizer.DetectDuplicateActivities();
            string wrongActivity = diagnostics?.RemainingWrongActivity ?? AndroidManifestSanitizer.DetectWrongMainActivity();
            string themeMismatch = diagnostics?.RemainingThemeMismatch ?? AndroidManifestSanitizer.DetectThemeMismatch();
            string launcherIssue = diagnostics?.RemainingLauncherIssue ?? AndroidManifestSanitizer.DetectLauncherManifestIssue();

            if (orphaned.Count > 0)
            {
                hasIssues = true;
                foreach ((SdkId sdkId, string[] entries) in orphaned)
                {
                    string sdkName = SdkRegistry.All[sdkId].Name;
                    results.Add(Error(
                        CheckCategory.AndroidManifest,
                        $"AndroidManifest.xml has {sdkName} entries but SDK is not installed!\n" +
                        $"  Found patterns: {string.Join(", ", entries)}\n" +
                        "  This WILL crash at runtime.",
                        "Click Refresh above and re-check."));
                }
            }

            if (duplicates.Count > 0)
            {
                hasIssues = true;
                results.Add(Error(
                    CheckCategory.AndroidManifest,
                    "AndroidManifest.xml has duplicate activity declarations!\n" +
                    $"  Duplicates: {string.Join(", ", duplicates)}\n" +
                    "  This WILL cause build failures.",
                    "Click Refresh above and re-check."));
            }

            if (wrongActivity != null)
            {
                hasIssues = true;
                results.Add(Error(
                    CheckCategory.AndroidManifest,
                    "AndroidManifest.xml has wrong main activity!\n" +
                    $"  Found: {wrongActivity}\n" +
                    $"  Expected: {AndroidManifestSanitizer.GetExpectedMainActivity()}\n" +
                    "  The app WILL crash on launch.",
                    "Click Refresh above and re-check."));
            }

            if (themeMismatch != null)
            {
                hasIssues = true;
                results.Add(Error(
                    CheckCategory.AndroidManifest,
                    "AndroidManifest.xml activity theme issue!\n" +
                    $"  {themeMismatch}\n" +
                    "  This WILL cause a Gradle merge conflict on build.",
                    "Click Refresh above and re-check."));
            }

            if (launcherIssue != null)
            {
                hasIssues = true;
                results.Add(Error(
                    CheckCategory.AndroidManifest,
                    $"LauncherManifest.xml issue: {launcherIssue}\n" +
                    "  The app will install but fail to launch.",
                    "Click Refresh above and re-check."));
            }

            if (!hasIssues)
                results.Add(Valid(CheckCategory.AndroidManifest, "Manifest clean"));

            return results;
        }
    }
}
