using System.Diagnostics;
using Sorolla.Palette.Health;
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

        /// <summary>How this project consumes the SDK, expressed in the terms the evaluator needs: which
        /// report audience applies and whether a release certificate covers the invariant gates.</summary>
        internal readonly struct Origin
        {
            public readonly ReportProfile Profile;
            public readonly SdkCertification Certification;
            public readonly string Evidence;

            internal Origin(ReportProfile profile, SdkCertification certification, string evidence)
            {
                Profile = profile;
                Certification = certification;
                Evidence = evidence;
            }
        }

        /// <summary>
        ///     Tag-as-certificate resolution. A git dependency pinned to the version tag matching the package's
        ///     own version is a certified release (Sorolla's release process tags only a commit that passed the
        ///     full internal pass on the reference game). A branch/commit pin is a studio report with NO
        ///     certificate. An embedded/local package is Sorolla working on the SDK itself, so it gets the
        ///     STRICTER full-depth profile. Anything unresolvable fails closed: Studio + Unknown certification.
        ///     <para>
        ///     Note (2026-07-22): the certificate currently changes only the report's printed provenance line,
        ///     because resolving it against gate rows requires at least one Invariant gate and the catalog has
        ///     none since the human-attested gates were deleted. What actually stops a studio sitting silently
        ///     on the development line is now the direct <c>build.sdk_pin</c> check, which warns in the studio
        ///     window with the fix. Add an Invariant gate and this resolution becomes load-bearing again.
        ///     </para>
        /// </summary>
        internal static Origin ResolveOrigin()
        {
            try
            {
                PackageInfo pkg = PackageInfo.FindForAssembly(typeof(SdkProvenance).Assembly);
                if (pkg == null)
                    return new Origin(ReportProfile.Studio, SdkCertification.Unknown,
                        "package info unavailable - the SDK's install source could not be identified");

                if (pkg.source == PackageSource.Embedded || pkg.source == PackageSource.Local ||
                    pkg.source == PackageSource.LocalTarball)
                    // Evidence states the SOURCE only; the consuming line says what that source means.
                    return new Origin(ReportProfile.SorollaFull, SdkCertification.Uncertified,
                        $"embedded/local SDK working tree at {ResolveSdkCommit()}");

                if (pkg.source == PackageSource.Git)
                {
                    string revision = pkg.git?.revision;
                    string hash = pkg.git?.hash;
                    if (string.IsNullOrEmpty(revision))
                        return new Origin(ReportProfile.Studio, SdkCertification.Uncertified,
                            "git dependency with no pinned ref (tracking a branch) - no release certificate");
                    return IsVersionTag(revision, pkg.version)
                        ? new Origin(ReportProfile.Studio, SdkCertification.CertifiedRelease,
                            $"tag {revision} (commit {hash}) matching package version {pkg.version}")
                        : new Origin(ReportProfile.Studio, SdkCertification.Uncertified,
                            $"git ref '{revision}' is not the release tag for version {pkg.version} - no release certificate");
                }

                return new Origin(ReportProfile.Studio, SdkCertification.Unknown,
                    $"unsupported package source '{pkg.source}' - certification could not be established");
            }
            catch
            {
                return new Origin(ReportProfile.Studio, SdkCertification.Unknown, Unknown);
            }
        }

        /// <summary>Whether a git ref IS the release tag for <paramref name="version"/> (<c>v1.2.3</c> or
        /// <c>1.2.3</c>). Deliberately exact: a ref that merely CONTAINS the version is not a certificate.</summary>
        internal static bool IsVersionTag(string revision, string version)
        {
            if (string.IsNullOrWhiteSpace(revision) || string.IsNullOrWhiteSpace(version))
                return false;
            string r = revision.Trim();
            return r == version.Trim() || r == "v" + version.Trim();
        }

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
