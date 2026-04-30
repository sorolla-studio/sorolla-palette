using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    public static partial class BuildValidator
    {
        /// <summary>
        ///     Manual validation via console (use BuildValidationWindow for UI).
        /// </summary>
        public static void ValidateBuildConsole()
        {
            Debug.Log($"{Tag} Starting validation...");

            var results = RunAllChecks();
            DisplayResults(results);
        }

        /// <summary>
        ///     Display validation results to console and dialog.
        /// </summary>
        static void DisplayResults(List<ValidationResult> results)
        {
            int errors = results.Count(r => r.Status == ValidationStatus.Error);
            int warnings = results.Count(r => r.Status == ValidationStatus.Warning);

            // Log each result
            foreach (ValidationResult result in results)
            {
                string fixText = string.IsNullOrEmpty(result.Fix) ? "" : $"\n  Fix: {result.Fix}";

                switch (result.Status)
                {
                    case ValidationStatus.Error:
                        Debug.LogError($"{Tag} ERROR: {result.Message}{fixText}");
                        break;
                    case ValidationStatus.Warning:
                        Debug.LogWarning($"{Tag} WARNING: {result.Message}{fixText}");
                        break;
                    default:
                        Debug.Log($"{Tag} {result.Message}");
                        break;
                }
            }

            // Summary
            if (errors == 0 && warnings == 0)
            {
                Debug.Log($"{Tag} Validation passed - no issues found");
                EditorUtility.DisplayDialog(
                    "Build Validation",
                    "All checks passed. Build is ready.",
                    "OK"
                );
            }
            else
            {
                string summary = $"Validation complete: {errors} error(s), {warnings} warning(s)";
                Debug.Log($"{Tag} {summary}");

                EditorUtility.DisplayDialog(
                    "Build Validation",
                    $"{summary}\n\nCheck Console for details.",
                    "OK"
                );
            }
        }
    }
}
