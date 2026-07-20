using System;
using System.Collections.Generic;
using System.Text;
using Sorolla.Palette.Health;
using UnityEditor;

namespace Sorolla.Palette.Editor.Greenlight
{
    /// <summary>
    ///     The AUDITABLE canonical report export (review F4). The Editor greenlight's flattened display rows
    ///     drop almost everything the comparison instrument needs and hide inert rows; this exporter serializes
    ///     the FULL shared <see cref="HealthReport"/> instead - every row (including NotApplicable and
    ///     OptionalSkipped) with its stable id, definition version, requirement + reason, disposition, outcome,
    ///     required/observed proof, evidence, and fix - plus a build/context fingerprint so a pasted result can
    ///     be tied to the exact game, build, mode, platform, phase, and SDK that produced it. JSON for machine
    ///     comparison, a readable text rendering for a human; the clipboard stays the transport.
    /// </summary>
    static class GreenlightReportExport
    {
        internal const string Schema = "sorolla.greenlight-report/1";

        /// <summary>Identity + context a report was produced under, so a copied result is never ambiguous about
        /// which build/phase it describes (review F4). Captured at evaluation time.</summary>
        internal readonly struct Fingerprint
        {
            public readonly string SdkVersion;
            public readonly string SdkCommit;
            public readonly string ApplicationId;
            public readonly string Platform;
            public readonly string Mode;
            public readonly string AppVersion;
            public readonly string DeviceBuildGuid;
            public readonly string Phase;
            public readonly string GeneratedAtUtc;

            Fingerprint(string sdk, string sdkCommit, string appId, string platform, string mode, string appVersion,
                string deviceBuildGuid, string phase, string generatedAtUtc)
            {
                SdkVersion = sdk; SdkCommit = sdkCommit; ApplicationId = appId; Platform = platform; Mode = mode;
                AppVersion = appVersion; DeviceBuildGuid = deviceBuildGuid; Phase = phase;
                GeneratedAtUtc = generatedAtUtc;
            }

            internal static Fingerprint Capture(EvaluationContext context, string deviceBuildGuid)
            {
                QaBuildIdentity id = QaBuildIdentity.Current();
                return new Fingerprint(
                    Palette.SdkVersion, SdkProvenance.ResolveSdkCommit(),
                    id.ApplicationId, id.Platform, id.Mode, id.AppVersion,
                    string.IsNullOrEmpty(deviceBuildGuid) ? "(no device connected)" : deviceBuildGuid,
                    context?.RequestedPhase.ToString() ?? "(none)",
                    DateTime.UtcNow.ToString("o"));
            }
        }

        /// <summary>The full canonical report as JSON: fingerprint + outcome + validation errors + every row's
        /// stable metadata. Inert rows are NOT dropped (review F4/F9).</summary>
        internal static string ToJson(HealthReport health, Fingerprint fingerprint, EvaluationContext context = null)
        {
            var rows = new List<object>();
            foreach (GateResult r in health?.Rows ?? Array.Empty<GateResult>())
            {
                rows.Add(new Dictionary<string, object>
                {
                    ["id"] = r.GateId,
                    ["version"] = r.DefinitionVersion,
                    ["classification"] = r.Classification.ToString(),
                    ["requirement"] = r.Requirement.ToString(),
                    ["requirement_reason"] = r.RequirementReason ?? "",
                    ["disposition"] = r.Disposition.ToString(),
                    ["outcome"] = r.Outcome.ToString(),
                    ["required_proof"] = ProofNames(r.RequiredProof),
                    ["observed_proof"] = ProofNames(r.ObservedProof),
                    ["evidence"] = r.Evidence ?? "",
                    ["fix"] = r.FixHint ?? "",
                });
            }

            var root = new Dictionary<string, object>
            {
                ["schema"] = Schema,
                ["fingerprint"] = FingerprintObject(fingerprint),
                ["certification"] = CertificationObject(context),
                ["outcome"] = (health?.Outcome ?? GateOutcome.Incomplete).ToString(),
                ["validation_errors"] = new List<object>(health?.ValidationErrors ?? Array.Empty<string>()),
                ["row_count"] = rows.Count,
                ["rows"] = rows,
            };
            return MiniJson.Serialize(root, prettyPrint: true);
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
                          $"platform: {fingerprint.Platform} | mode: {fingerprint.Mode} | phase: {fingerprint.Phase}");
            sb.AppendLine($"device build: {fingerprint.DeviceBuildGuid}");
            sb.AppendLine($"profile: {context?.Profile ?? ReportProfile.Unknown} | " +
                          $"sdk certification: {context?.Certification ?? SdkCertification.Unknown} " +
                          $"({(string.IsNullOrEmpty(context?.CertificationEvidence) ? "no evidence" : context.CertificationEvidence)})");
            sb.AppendLine($"generated: {fingerprint.GeneratedAtUtc}");
            sb.AppendLine();

            foreach (GateResult r in health?.Rows ?? Array.Empty<GateResult>())
            {
                sb.AppendLine($"[{r.Outcome}] {r.GateId} (v{r.DefinitionVersion}, {r.Classification}) " +
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

        /// <summary>Who the report was evaluated FOR and whether a release certificate covered its invariant
        /// gates - without this a Studio report's collapsed rows would be unreadable after the fact. A null
        /// context reports Unknown/Unknown, never an assumed audience.</summary>
        static Dictionary<string, object> CertificationObject(EvaluationContext context) =>
            new Dictionary<string, object>
            {
                ["profile"] = (context?.Profile ?? ReportProfile.Unknown).ToString(),
                ["certification"] = (context?.Certification ?? SdkCertification.Unknown).ToString(),
                ["evidence"] = context?.CertificationEvidence ?? "",
            };

        static Dictionary<string, object> FingerprintObject(Fingerprint f) => new Dictionary<string, object>
        {
            ["sdk_version"] = f.SdkVersion,
            ["sdk_commit"] = f.SdkCommit,
            ["application_id"] = f.ApplicationId,
            ["platform"] = f.Platform,
            ["mode"] = f.Mode,
            ["app_version"] = f.AppVersion,
            ["device_build_guid"] = f.DeviceBuildGuid,
            ["phase"] = f.Phase,
            ["generated_at_utc"] = f.GeneratedAtUtc,
        };

        static List<object> ProofNames(ProofScope proof)
        {
            var names = new List<object>();
            if ((proof & ProofScope.Static) != 0) names.Add("Static");
            if ((proof & ProofScope.DeviceDispatch) != 0) names.Add("DeviceDispatch");
            if ((proof & ProofScope.VendorAccepted) != 0) names.Add("VendorAccepted");
            return names;
        }

        static string ProofString(ProofScope proof) => proof == ProofScope.None ? "None" : string.Join("+", ProofNames(proof));
    }
}
