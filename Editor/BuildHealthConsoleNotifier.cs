using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     Surface Build Health issues in the console on domain reload so studios see problems
    ///     early, even if they don't open the Palette Configuration window.
    /// </summary>
    [InitializeOnLoad]
    internal static class BuildHealthConsoleNotifier
    {
        static BuildHealthConsoleNotifier()
        {
            EditorApplication.delayCall += CheckHealthOnReload;
        }

        static void CheckHealthOnReload()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
                EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
                return;

            try
            {
                // Auto-fix manifest, Gradle, and MAX issues on domain reload
                var fixes = BuildValidator.RunAutoFixes();
                foreach (string fix in fixes)
                    Debug.Log($"[Palette] Auto-fixed: {fix}");

                if (fixes.Count > 0)
                    AssetDatabase.Refresh();

                var results = BuildValidator.RunAllChecks();
                int errorCount = results.Count(r => r.Status == BuildValidator.ValidationStatus.Error);

                if (fixes.Count > 0 && errorCount == 0)
                    Debug.Log($"[Palette] Build Health: {fixes.Count} issue(s) detected and auto-fixed.");
                else if (fixes.Count > 0)
                    Debug.LogWarning(
                        $"[Palette] Build Health: {fixes.Count} auto-fixed, {errorCount} remaining. Open Tools > Sorolla Palette SDK.");
                else if (errorCount > 0)
                    Debug.LogWarning(
                        $"[Palette] Build Health: {errorCount} issue(s) require manual attention. Open Tools > Sorolla Palette SDK.");
            }
            catch
            {
                // Silently ignore - domain reload timing can cause transient failures
            }
        }
    }
}
