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
                    // The reminder that valid credentials do NOT prove the active platform is registered is
                    // back on this row (2026-07-22). It had moved to the GameAnalytics Platform Registered
                    // attestation row to avoid two owners; that row was deleted with the attestation
                    // mechanism, so this row owns it again - otherwise the one place a studio could learn
                    // that its platform is missing from the dashboard would be nowhere. GameAnalytics accepts
                    // events for an unregistered platform on valid credentials, so the probe passing here is
                    // genuinely not the same fact.
                    // The message carries the short form because a passing row renders its message but
                    // suppresses fix text; the long form rides in the copied report, which keeps every row.
                    results.Add(Valid(category, $"{probe.Detail} (platform registration not verified)",
                        "Confirm the ACTIVE platform is added to this game in the GameAnalytics dashboard - "
                        + "valid keys do not prove the platform exists there, and GameAnalytics accepts events "
                        + "for an unregistered platform."));
                    break;
            }

            return results;
        }
    }
}
