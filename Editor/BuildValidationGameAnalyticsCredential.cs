using System.Collections.Generic;

namespace Sorolla.Palette.Editor
{
    public static partial class BuildValidator
    {
        /// <summary>
        ///     Check that the GameAnalytics game key/secret key pair for the active build target is a
        ///     live, matched credential accepted by the GA collector. Async and non-blocking: kicks off
        ///     a probe if needed and reports whatever <see cref="GameAnalyticsCredentialValidator"/>
        ///     currently has cached, never waiting on the network here.
        ///
        ///     SCOPE: this only proves the credentials are live. It cannot confirm the active platform
        ///     is registered in the GA dashboard - the collector accepts any platform string with valid
        ///     credentials (greenlight probe spike 2026-07-10). The fix text for a pass always reminds
        ///     the studio to verify platform registration manually; do not drop that reminder.
        /// </summary>
        static List<ValidationResult> CheckGameAnalyticsCredential()
        {
            var results = new List<ValidationResult>();
            const CheckCategory category = CheckCategory.GameAnalyticsCredentialProbe;

            if (!SdkDetector.IsInstalled(SdkId.GameAnalytics))
            {
                results.Add(Skipped(category, "GameAnalytics not installed"));
                return results;
            }

            if (!SdkConfigDetector.TryGetGameAnalyticsCredentials(out string gameKey, out string secretKey))
            {
                results.Add(Skipped(category, "GameAnalytics game key/secret key not set, credential probe skipped"));
                return results;
            }

            GameAnalyticsCredentialValidator.EnsureChecked(gameKey, secretKey);
            GameAnalyticsCredentialValidator.ProbeResult probe = GameAnalyticsCredentialValidator.Current;

            switch (probe.State)
            {
                case GameAnalyticsCredentialValidator.ProbeState.NotStarted:
                case GameAnalyticsCredentialValidator.ProbeState.Pending:
                    results.Add(Unverifiable(category, "Checking GameAnalytics credentials..."));
                    break;

                case GameAnalyticsCredentialValidator.ProbeState.Unreachable:
                    results.Add(Unverifiable(category, probe.Detail));
                    break;

                case GameAnalyticsCredentialValidator.ProbeState.CredentialInvalid:
                    results.Add(Warning(
                        category,
                        probe.Detail,
                        "Verify GameAnalytics Settings.asset game key + secret key against the GameAnalytics dashboard"));
                    break;

                case GameAnalyticsCredentialValidator.ProbeState.CredentialsValid:
                    // The "also verify the active platform is added" reminder moved off this row (refuter
                    // follow-up, 2026-07-21): it now duplicates the GameAnalytics Platform Registered
                    // attestation row, which lives in this same vendor group as of the manual-gate regroup
                    // and says the identical thing with its own action - one owner, not two.
                    results.Add(Valid(category, probe.Detail));
                    break;
            }

            return results;
        }
    }
}
