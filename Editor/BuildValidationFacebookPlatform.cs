using System.Collections.Generic;

namespace Sorolla.Palette.Editor
{
    public static partial class BuildValidator
    {
        /// <summary>
        ///     Check that the Facebook app is registered for the active build target's platform and
        ///     that the appId/clientToken pair is accepted by the Graph API. Async and non-blocking:
        ///     kicks off a probe if needed and reports whatever <see cref="FacebookPlatformValidator"/>
        ///     currently has cached, never waiting on the network here.
        /// </summary>
        static List<ValidationResult> CheckFacebookPlatformConfig()
        {
            var results = new List<ValidationResult>();
            const CheckCategory category = CheckCategory.FacebookPlatformConfig;

            if (!SdkDetector.IsInstalled(SdkId.Facebook))
            {
                results.Add(Skipped(category, "Facebook not installed"));
                return results;
            }

            if (!SdkConfigDetector.TryGetFacebookCredentials(out string appId, out string clientToken))
            {
                results.Add(Skipped(category, "Facebook app id/client token not set, platform check skipped"));
                return results;
            }

            FacebookPlatformValidator.EnsureChecked(appId, clientToken);
            FacebookPlatformValidator.ProbeResult probe = FacebookPlatformValidator.Current;

            switch (probe.State)
            {
                case FacebookPlatformValidator.ProbeState.NotStarted:
                case FacebookPlatformValidator.ProbeState.Pending:
                    results.Add(Unverifiable(category, "Checking Facebook app platform registration..."));
                    break;

                case FacebookPlatformValidator.ProbeState.Unreachable:
                    results.Add(Unverifiable(category, probe.Detail));
                    break;

                // Awareness-first severity ruling (Arthur, via supervisor): a studio may intentionally
                // ship one platform at a time, so a missing/rejected FB platform registration is not a
                // build blocker - it is a warning with a clear root cause, signal, and fix. The sibling
                // platform's registration is graded from the same Graph response (2026-07-22) and warns
                // too: an app registered for one platform only is invisible from the other platform's
                // build, which is how it survives to ship.
                case FacebookPlatformValidator.ProbeState.PlatformMissing:
                case FacebookPlatformValidator.ProbeState.SiblingPlatformMissing:
                    results.Add(Warning(
                        category,
                        probe.Detail,
                        "FB console -> Settings -> Basic -> Add Platform"));
                    break;

                case FacebookPlatformValidator.ProbeState.CredentialInvalid:
                    // Fix hint omits the "open FacebookSettings.asset" step (product-audit fix cycle
                    // residual, 2026-07-21): the row's "Open FB Settings" button already opens it.
                    results.Add(Warning(
                        category,
                        probe.Detail,
                        "Compare the app id + client token against the Facebook developer console"));
                    break;

                case FacebookPlatformValidator.ProbeState.Verified:
                    results.Add(Valid(category, probe.Detail));
                    break;
            }

            return results;
        }
    }
}
