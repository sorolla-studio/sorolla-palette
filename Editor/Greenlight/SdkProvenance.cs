using System.Diagnostics;
using UnityEditor.PackageManager;

namespace Sorolla.Palette.Editor.Greenlight
{
    /// <summary>
    ///     Resolves the exact SDK source commit LIVE at export time (review B3) - because every unreleased
    ///     commit shares SDK version <c>4.0.0</c>, the version string alone cannot identify which source
    ///     produced a report. This is the SMALLEST editor-side mechanism (NOT the reopened runtime bake, which
    ///     stays Cycle 7): a git-installed package reports the resolved hash Unity already recorded; an
    ///     embedded/local package is resolved from its working tree via git; anything else is honestly
    ///     "unknown". No value is ever fabricated.
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
