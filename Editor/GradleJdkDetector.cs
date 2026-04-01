using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     Detects JDK 17+ installations on the system.
    ///     Used by BuildValidator to auto-configure org.gradle.java.home
    ///     when Unity bundles an older JDK (e.g., Unity 2022 ships JDK 11).
    /// </summary>
    public static class GradleJdkDetector
    {
        private const string Tag = "[Palette GradleJdkDetector]";
        private const int MinJavaVersion = 17;
        // Gradle 7.x (Unity 2022) supports up to JDK 18. Gradle 8.x (Unity 6) supports JDK 21.
        // Cap the max version to avoid "Unsupported class file major version" errors.
#if UNITY_6000_0_OR_NEWER
        private const int MaxJavaVersion = 21;
#else
        private const int MaxJavaVersion = 18;
#endif

        /// <summary>
        ///     Searches common JDK installation paths for a JDK 17+ home directory.
        ///     Returns the path if found, null otherwise.
        /// </summary>
        public static string FindJdk17OrNewer()
        {
            // 1. Check JAVA_HOME environment variable first
            var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (!string.IsNullOrEmpty(javaHome) && IsJdkCompatible(javaHome))
                return javaHome;

            // 2. Try /usr/libexec/java_home (macOS)
            if (Application.platform == RuntimePlatform.OSXEditor)
            {
                var macPath = RunJavaHome();
                if (macPath != null && IsJdkCompatible(macPath))
                    return macPath;
            }

            // 3. Check common installation paths
            var candidates = GetCandidatePaths();
            foreach (var candidate in candidates)
            {
                if (Directory.Exists(candidate) && IsJdkCompatible(candidate))
                    return candidate;
            }

            Debug.LogWarning($"{Tag} No JDK {MinJavaVersion}-{MaxJavaVersion} found. Install JDK {MinJavaVersion} and restart Unity, or set JAVA_HOME.");
            return null;
        }

        private static bool IsJdkCompatible(string jdkPath)
        {
            var javaBin = Path.Combine(jdkPath, "bin", "java");
            if (Application.platform == RuntimePlatform.WindowsEditor)
                javaBin += ".exe";

            if (!File.Exists(javaBin))
                return false;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = javaBin,
                    Arguments = "-version",
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                // java -version outputs to stderr
                var output = process.StandardError.ReadToEnd();
                process.WaitForExit(5000);

                var major = ParseMajorVersion(output);
                return major >= MinJavaVersion && major <= MaxJavaVersion;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        ///     Parses the major version from java -version output.
        ///     Handles both "1.8.0_xxx" (Java 8) and "17.0.x" (Java 9+) formats.
        /// </summary>
        internal static int ParseMajorVersion(string versionOutput)
        {
            if (string.IsNullOrEmpty(versionOutput))
                return 0;

            // Look for version string in quotes: "17.0.18" or "1.8.0_362"
            var start = versionOutput.IndexOf('"');
            var end = start >= 0 ? versionOutput.IndexOf('"', start + 1) : -1;

            if (start < 0 || end < 0)
                return 0;

            var version = versionOutput.Substring(start + 1, end - start - 1);
            var parts = version.Split('.');

            if (parts.Length == 0)
                return 0;

            if (int.TryParse(parts[0], out var major))
            {
                // "1.8.0" → Java 8, "1.7.0" → Java 7
                if (major == 1 && parts.Length > 1 && int.TryParse(parts[1], out var minor))
                    return minor;
                return major;
            }

            return 0;
        }

        private static string RunJavaHome()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "/usr/libexec/java_home",
                    Arguments = $"-v {MinJavaVersion}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                var output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(5000);

                return process.ExitCode == 0 && Directory.Exists(output) ? output : null;
            }
            catch
            {
                return null;
            }
        }

        private static string[] GetCandidatePaths()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.OSXEditor:
                    return new[]
                    {
                        // Homebrew (Apple Silicon + Intel)
                        "/opt/homebrew/opt/openjdk@17/libexec/openjdk.jdk/Contents/Home",
                        "/usr/local/opt/openjdk@17/libexec/openjdk.jdk/Contents/Home",
                        "/opt/homebrew/opt/openjdk/libexec/openjdk.jdk/Contents/Home",
                        "/usr/local/opt/openjdk/libexec/openjdk.jdk/Contents/Home",
                        // SDKMAN
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".sdkman/candidates/java/current"),
                        // System JDK
                        "/Library/Java/JavaVirtualMachines/jdk-17.jdk/Contents/Home",
                        "/Library/Java/JavaVirtualMachines/temurin-17.jdk/Contents/Home",
                        "/Library/Java/JavaVirtualMachines/zulu-17.jdk/Contents/Home",
                    };

                case RuntimePlatform.WindowsEditor:
                    var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                    return new[]
                    {
                        Path.Combine(programFiles, "Java", "jdk-17"),
                        Path.Combine(programFiles, "Eclipse Adoptium", "jdk-17"),
                        Path.Combine(programFiles, "Zulu", "zulu-17"),
                        Path.Combine(programFiles, "Microsoft", "jdk-17"),
                    };

                case RuntimePlatform.LinuxEditor:
                    return new[]
                    {
                        "/usr/lib/jvm/java-17-openjdk-amd64",
                        "/usr/lib/jvm/java-17-openjdk",
                        "/usr/lib/jvm/temurin-17-jdk-amd64",
                    };

                default:
                    return Array.Empty<string>();
            }
        }
    }
}
