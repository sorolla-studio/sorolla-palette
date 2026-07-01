using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace Sorolla.Palette.Editor
{
    public static partial class BuildValidator
    {
        /// <summary>
        ///     Validate Gradle configuration for Java 17 compatibility.
        ///     Firebase 23.x, AppLovin MAX 13.x, and Kotlin 2.x all require Java 17 bytecode.
        ///     Unity 2022 bundles JDK 11 — Gradle must be pointed at JDK 17+ or dexing fails.
        /// </summary>
        static List<ValidationResult> CheckGradleConfig()
        {
            var results = new List<ValidationResult>();

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                results.Add(Valid(CheckCategory.GradleConfig, "Gradle checks skipped (not Android)"));
                return results;
            }

            bool hasIssues = false;

            // Check compileOptions in both Gradle templates
            foreach (string templatePath in GradleTemplatePaths)
            {
                if (!File.Exists(templatePath)) continue;
                string gradle = File.ReadAllText(templatePath);
                if (HasJava11CompileOptions(gradle))
                {
                    hasIssues = true;
                    string fileName = Path.GetFileName(templatePath);
                    results.Add(Error(
                        CheckCategory.GradleConfig,
                        $"{fileName} has Java 11 compileOptions!\n" +
                        $"  Firebase 23.x, AppLovin MAX 13.x, and Kotlin 2.x require Java {RequiredJavaVersion}.\n" +
                        $"  Change sourceCompatibility and targetCompatibility to VERSION_{RequiredJavaVersion}.",
                        "Open Palette > Configuration and click Refresh in Build Health"));
                }
            }

            // The Custom Gradle Properties Template must be enabled - the androidx/jetifier block lives
            // there. org.gradle.java.home is intentionally NOT expected inside the committed template
            // anymore: on Unity 2022 it is injected at build time into the generated gradle.properties
            // by GradlePropertiesFixer (B-16). So only flag the template's absence.
            if (!File.Exists(GradlePropertiesPath))
            {
                hasIssues = true;
                results.Add(Warning(
                    CheckCategory.GradleConfig,
                    "gradleTemplate.properties not found.\n" +
                    "  Enable Custom Gradle Properties Template in Player Settings > Publishing Settings.",
                    "Enable Custom Gradle Properties Template"));
            }

            if (!hasIssues)
                results.Add(Valid(CheckCategory.GradleConfig, "Gradle config OK"));

            return results;
        }

        internal static bool HasJava11CompileOptions(string gradle) =>
            !string.IsNullOrEmpty(gradle) &&
            gradle.Contains("VERSION_11") &&
            (gradle.Contains("sourceCompatibility") || gradle.Contains("targetCompatibility"));

        internal static bool MissingGradleJavaHome(string gradleProperties) =>
            string.IsNullOrEmpty(gradleProperties) ||
            !gradleProperties.Contains("org.gradle.java.home");

        /// <summary>
        ///     Remove the buildscript { ... } block from a Gradle file using brace matching.
        ///     Returns the original string if no buildscript block is found.
        /// </summary>
        internal static string RemoveBuildscriptBlock(string gradle)
        {
            int idx = gradle.IndexOf("buildscript", StringComparison.Ordinal);
            if (idx < 0) return gradle;

            // Find opening brace
            int braceStart = gradle.IndexOf('{', idx);
            if (braceStart < 0) return gradle;

            // Match closing brace
            int depth = 1;
            int pos = braceStart + 1;
            while (pos < gradle.Length && depth > 0)
            {
                if (gradle[pos] == '{') depth++;
                else if (gradle[pos] == '}') depth--;
                pos++;
            }

            if (depth != 0) return gradle; // Unbalanced braces, don't touch

            // Include any trailing newlines
            while (pos < gradle.Length && (gradle[pos] == '\n' || gradle[pos] == '\r'))
            {
                pos++;
            }

            // Include any leading whitespace/newlines before "buildscript"
            int blockStart = idx;
            while (blockStart > 0 && (gradle[blockStart - 1] == ' ' || gradle[blockStart - 1] == '\t' ||
                                      gradle[blockStart - 1] == '\n' || gradle[blockStart - 1] == '\r'))
            {
                blockStart--;
            }

            return gradle.Substring(0, blockStart) + gradle.Substring(pos);
        }

        /// <summary>
        ///     Check for R8 version pins and Kotlin stdlib forcing that conflict with the current AGP version.
        ///     Unity 2022 (AGP 7.4.2) needs R8 8.1.56+ pin for Kotlin 2.0 metadata.
        ///     Unity 6 (AGP 8.x) bundles modern R8 - the pin must be removed.
        /// </summary>
        static List<ValidationResult> CheckR8AgpConfig()
        {
            var results = new List<ValidationResult>();

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                results.Add(Valid(CheckCategory.GradleConfig, "R8/AGP checks skipped (not Android)"));
                return results;
            }

            bool hasIssues = false;

            // Check R8 pin in baseProjectTemplate.gradle
            if (File.Exists(BaseProjectTemplatePath))
            {
                string gradle = File.ReadAllText(BaseProjectTemplatePath);
                if (HasR8Pin(gradle))
                {
#if UNITY_6000_0_OR_NEWER
                    hasIssues = true;
                    results.Add(Error(
                        CheckCategory.GradleConfig,
                        "baseProjectTemplate.gradle has an R8 version pin!\n" +
                        "  AGP 8.x bundles modern R8 that handles Kotlin 2.0 natively.\n" +
                        "  The pin causes NoSuchMethodError during dexing.\n" +
                        "  Remove the buildscript { ... } block from baseProjectTemplate.gradle.",
                        "Open Palette > Configuration and click Refresh in Build Health"));
#endif
                    // Unity 2022: R8 pin is expected and correct - no warning needed
                }
            }

            // Check Kotlin stdlib version forcing in mainTemplate.gradle
            if (File.Exists(MainTemplatePath))
            {
                string gradle = File.ReadAllText(MainTemplatePath);
                if (ForcesKotlinStdlibVersion(gradle))
                {
#if UNITY_6000_0_OR_NEWER
                    hasIssues = true;
                    results.Add(Warning(
                        CheckCategory.GradleConfig,
                        "mainTemplate.gradle forces Kotlin stdlib to an older version.\n" +
                        "  AGP 8.x handles Kotlin 2.0 metadata natively.\n" +
                        "  Consider removing the resolutionStrategy block.",
                        "Remove the resolutionStrategy.eachDependency block for kotlin-stdlib"));
#endif
                    // Unity 2022: Kotlin forcing is expected and correct - no warning needed
                }
            }

            if (!hasIssues)
                results.Add(Valid(CheckCategory.GradleConfig, "R8/AGP config OK"));

            return results;
        }

        internal static bool HasR8Pin(string gradle) =>
            !string.IsNullOrEmpty(gradle) &&
            gradle.Contains("com.android.tools:r8");

        internal static bool ForcesKotlinStdlibVersion(string gradle) =>
            !string.IsNullOrEmpty(gradle) &&
            gradle.Contains("kotlin-stdlib") &&
            gradle.Contains("useVersion");
    }
}
