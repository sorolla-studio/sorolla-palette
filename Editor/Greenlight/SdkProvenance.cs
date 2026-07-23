using System.Diagnostics;
using UnityEditor.PackageManager;

namespace Sorolla.Palette.Editor.Greenlight
{
    /// <summary>
    ///     Resolves the exact SDK source commit for the editor build receipt. Every unreleased commit shares
    ///     SDK version <c>4.0.0</c>, so the version string cannot identify which source produced a binary.
    ///     A git-installed package reports Unity's resolved hash; an embedded/local package resolves its
    ///     working-tree commit; anything else is honestly unknown. No runtime commit injection is needed.
    /// </summary>
    static class SdkProvenance
    {
        internal const string Unknown = "unknown (provenance unavailable)";

        internal static string ResolveSdkCommit()
        {
            try
            {
                PackageInfo pkg = PackageInfo.FindForAssembly(typeof(SdkProvenance).Assembly);
                if (pkg == null)
                    return "unknown (package info unavailable)";

                // Git-URL consumers: Unity already resolved the hash into packages-lock.json.
                if (pkg.source == PackageSource.Git && pkg.git != null && !string.IsNullOrEmpty(pkg.git.hash))
                    return $"{pkg.git.hash} (git package)";

                // Embedded / local: resolve live from the package working tree.
                string head = RunGit(pkg.resolvedPath, "rev-parse HEAD");
                if (string.IsNullOrEmpty(head))
                    return Unknown;
                bool dirty = !string.IsNullOrEmpty(RunGit(pkg.resolvedPath, "status --porcelain --untracked-files=no"));
                return dirty ? $"{head}-dirty" : head;
            }
            catch
            {
                return Unknown;
            }
        }

        static string RunGit(string workingDir, string args)
        {
            if (string.IsNullOrEmpty(workingDir))
                return null;
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo("git", args)
                    {
                        WorkingDirectory = workingDir,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                    },
                };
                process.Start();
                string stdout = process.StandardOutput.ReadToEnd();
                process.WaitForExit(3000);
                return process.ExitCode == 0 ? stdout.Trim() : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
