using System;
using System.Collections.Generic;
using System.Text;
using Sorolla.Palette.Health;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor.Greenlight
{
    /// <summary>
    ///     The AUDITABLE canonical report export (review F4). The Editor greenlight's flattened display rows
    ///     drop almost everything a reviewer needs and hide inert rows; this exporter renders the FULL shared
    ///     <see cref="HealthReport"/> instead - every row (including NotApplicable and OptionalSkipped) with
    ///     its stable id, definition version, requirement + reason, disposition, outcome, required/observed
    ///     proof, evidence, and fix - plus a build/context fingerprint so a pasted result can be tied to the
    ///     exact game, build, mode, platform, phase, and SDK COMMIT that produced it. One readable text
    ///     rendering, clipboard as the transport (the parallel JSON export was deleted 2026-07-22: nothing
    ///     consumed it, and one report beats two that can disagree).
    /// </summary>
    static class GreenlightReportExport
    {
        internal const string Schema = "sorolla.greenlight-report/1";

        /// <summary>Identity + context a report was produced under, so a copied result is never ambiguous about
        /// which build it describes (review F4). Captured at evaluation time.</summary>
        internal readonly struct Fingerprint
        {
            public readonly string SdkVersion;
            public readonly string SdkCommit;
            public readonly string ApplicationId;
            public readonly string Platform;
            public readonly string Mode;
            public readonly string AppVersion;
            public readonly string DeviceBuildGuid;
            public readonly string GeneratedAtUtc;

            Fingerprint(string sdk, string sdkCommit, string appId, string platform, string mode, string appVersion,
                string deviceBuildGuid, string generatedAtUtc)
            {
                SdkVersion = sdk; SdkCommit = sdkCommit; ApplicationId = appId; Platform = platform; Mode = mode;
                AppVersion = appVersion; DeviceBuildGuid = deviceBuildGuid;
                GeneratedAtUtc = generatedAtUtc;
            }

            internal static Fingerprint Capture(string deviceBuildGuid)
            {
                return new Fingerprint(
                    Palette.SdkVersion, SdkProvenance.ResolveSdkCommit(),
                    Application.identifier, PlatformName(EditorUserBuildSettings.activeBuildTarget),
                    ModeName(SorollaSettings.Mode), Application.version,
                    string.IsNullOrEmpty(deviceBuildGuid) ? "(no device connected)" : deviceBuildGuid,
                    DateTime.UtcNow.ToString("o"));
            }

            static string PlatformName(BuildTarget target) => target switch
            {
                BuildTarget.Android => "Android",
                BuildTarget.iOS => "IPhonePlayer",
                _ => target.ToString(),
            };

            static string ModeName(SorollaMode mode) => mode switch
            {
                SorollaMode.Full => "full",
                SorollaMode.Prototype => "prototype",
                _ => "unknown",
            };
        }

        /// <summary>Human-readable rendering of the same canonical report - includes disposition + requirement
        /// so an inert row is not mistaken for an evaluated PASS.</summary>
        internal static string ToText(HealthReport health, Fingerprint fingerprint, EvaluationContext context = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Palette Greenlight Report ({Schema})");
            sb.AppendLine($"outcome: {(health?.Outcome ?? GateOutcome.Incomplete)}");
            sb.AppendLine($"sdk: {fingerprint.SdkVersion} (commit {fingerprint.SdkCommit}) | " +
                          $"app: {fingerprint.ApplicationId} {fingerprint.AppVersion} | " +
                          $"platform: {fingerprint.Platform} | mode: {fingerprint.Mode}");
            sb.AppendLine($"device build: {fingerprint.DeviceBuildGuid}");
            sb.AppendLine($"profile: {context?.Profile ?? ReportProfile.Unknown} | " +
                          $"sdk certification: {context?.Certification ?? SdkCertification.Unknown} " +
                          $"({(string.IsNullOrEmpty(context?.CertificationEvidence) ? "no evidence" : context.CertificationEvidence)})");
            sb.AppendLine($"generated: {fingerprint.GeneratedAtUtc}");
            sb.AppendLine();

            foreach (GateResult r in health?.Rows ?? Array.Empty<GateResult>())
            {
                // Never print an affirmative [Pass] for a result that was not evaluated evidence. Two cases:
                // a deliberate skip/absence (F5 residual, 2026-07-21 audit review), and a gate that does not
                // apply to the platform this report judged (2026-07-23) - the latter carries the default Pass
                // outcome because it never voted, which is exactly why it must not read as one.
                string outcomeLabel =
                    r.Disposition == GateDisposition.NotApplicable ? "NotApplicable"
                    : r.Informational ? "Skipped"
                    : r.Outcome.ToString();
                sb.AppendLine($"[{outcomeLabel}] {r.GateId} (v{r.DefinitionVersion}, {r.Classification}) " +
                              $"req={r.Requirement} disp={r.Disposition} " +
                              $"proof req={ProofString(r.RequiredProof)} obs={ProofString(r.ObservedProof)}");
                if (!string.IsNullOrEmpty(r.RequirementReason))
                    sb.AppendLine($"    reason: {r.RequirementReason}");
                if (!string.IsNullOrEmpty(r.Evidence))
                    sb.AppendLine($"    evidence: {r.Evidence}");
                if (!string.IsNullOrEmpty(r.FixHint))
                    sb.AppendLine($"    fix: {r.FixHint}");
            }

            foreach (string error in health?.ValidationErrors ?? Array.Empty<string>())
                sb.AppendLine($"[INTEGRITY] {error}");

            return sb.ToString();
        }

        static string ProofString(ProofScope proof)
        {
            if (proof == ProofScope.None) return "None";
            var names = new List<string>();
            if ((proof & ProofScope.Static) != 0) names.Add("Static");
            if ((proof & ProofScope.DeviceDispatch) != 0) names.Add("DeviceDispatch");
            if ((proof & ProofScope.VendorAccepted) != 0) names.Add("VendorAccepted");
            return string.Join("+", names);
        }
    }
}
