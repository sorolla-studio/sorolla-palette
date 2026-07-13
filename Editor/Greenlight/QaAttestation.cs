using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sorolla.Palette.Health;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor.Greenlight
{
    /// <summary>
    ///     A minimal scoped attestation for a manual QA gate (Cycle 4b). Unlike the legacy EditorPrefs tick,
    ///     it records WHO asserted the gate, WHEN, against WHICH build identity + gate version + phase, the
    ///     proof scope claimed, a human evidence note, and (for device-session gates) the connected build's
    ///     GUID - so it can be validated field-by-field instead of being an unscoped no-op (B-10).
    ///     Serialized via <see cref="JsonUtility"/>, so fields are public and plain.
    /// </summary>
    [Serializable]
    internal class QaAttestationRecord
    {
        public string schema;
        public string gateId;
        public string gateVersion;
        public string phase;
        public string actor;
        public string timestampUtc;    // ISO-8601 UTC
        public string applicationId;
        public string platform;
        public string mode;
        public string appVersion;
        public string outcome;         // asserted outcome, must be "Pass"
        public string proofScope;      // the proof class claimed, e.g. "VendorAccepted" / "DeviceDispatch"
        public string evidenceNote;    // human note ("I did X and observed Y"); required for vendor gates (C45-06)
        public string deviceBuildGuid; // the connected snapshot's build GUID at attest time (device gates, C45-05)
    }

    [Serializable]
    internal class QaAttestationFile
    {
        public List<QaAttestationRecord> attestations = new List<QaAttestationRecord>();
    }

    /// <summary>The trusted current APP identity an attestation is checked against (app-version scope, not
    /// exact-binary; exact build is bound separately via the device build GUID for device gates).</summary>
    internal readonly struct QaBuildIdentity
    {
        public readonly string ApplicationId;
        public readonly string Platform;
        public readonly string Mode;
        public readonly string AppVersion;

        public QaBuildIdentity(string appId, string platform, string mode, string appVersion)
        {
            ApplicationId = appId; Platform = platform; Mode = mode; AppVersion = appVersion;
        }

        internal static QaBuildIdentity Current() => new QaBuildIdentity(
            Application.identifier,
            PlatformName(EditorUserBuildSettings.activeBuildTarget),
            ModeName(SorollaSettings.Mode),
            Application.version);

        internal static string PlatformName(BuildTarget target) => target switch
        {
            BuildTarget.Android => "Android",
            BuildTarget.iOS => "IPhonePlayer",
            _ => target.ToString(),
        };

        internal static string ModeName(SorollaMode mode) => mode switch
        {
            SorollaMode.Full => "full",
            SorollaMode.Prototype => "prototype",
            _ => "unknown",
        };
    }

    /// <summary>What the evaluator expects an attestation for a specific gate to bind to, right now: the
    /// gate's exact id + CURRENT definition version + requested phase + required proof scope + app identity,
    /// and (for device-session gates) the connected build GUID to require. A record that doesn't match these
    /// is Stale or Invalid, never a silent pass (review C45-01/C45-05).</summary>
    internal readonly struct QaAttestationExpectation
    {
        public readonly string GateId;
        public readonly string GateVersion;
        public readonly string Phase;
        public readonly ProofScope RequiredScope;
        public readonly QaBuildIdentity Identity;
        public readonly string DeviceBuildGuid; // current connected snapshot GUID, or null when no device

        public QaAttestationExpectation(
            string gateId, string gateVersion, string phase, ProofScope requiredScope,
            QaBuildIdentity identity, string deviceBuildGuid)
        {
            GateId = gateId; GateVersion = gateVersion; Phase = phase; RequiredScope = requiredScope;
            Identity = identity; DeviceBuildGuid = deviceBuildGuid;
        }
    }

    internal enum AttestationValidity { Valid, Stale, Invalid, Missing }

    /// <summary>
    ///     Pure validation of an attestation record against the full expectation (review C45-01/05/06):
    ///     structural problems (unknown schema, wrong gate id, blank actor, non-Pass outcome, missing
    ///     identity, wrong proof scope, missing required evidence note, unparseable/future timestamp) are
    ///     <see cref="AttestationValidity.Invalid"/>; a gate-version/phase change, an app-identity mismatch, a
    ///     device-build-GUID mismatch, or an expired record are <see cref="AttestationValidity.Stale"/> (the
    ///     attestation was real but not for THIS build/version - → INCOMPLETE, never a pass). Testable without
    ///     any file or editor state.
    /// </summary>
    internal static class QaAttestationValidator
    {
        internal const string Schema = "1";
        internal static readonly TimeSpan MaxAge = TimeSpan.FromDays(14);

        internal static AttestationValidity Evaluate(
            QaAttestationRecord record, QaAttestationExpectation expected, DateTime nowUtc, out string reason)
        {
            if (record == null) { reason = "no attestation recorded"; return AttestationValidity.Missing; }

            // ── Structural (Invalid): can never be a valid attestation ──
            if (record.schema != Schema)
            { reason = $"unknown attestation schema '{record.schema ?? "(none)"}'"; return AttestationValidity.Invalid; }
            if (record.gateId != expected.GateId)
            { reason = $"attestation gate id '{record.gateId}' does not match '{expected.GateId}'"; return AttestationValidity.Invalid; }
            if (string.IsNullOrWhiteSpace(record.actor))
            { reason = "attestation has no actor"; return AttestationValidity.Invalid; }
            if (record.outcome != "Pass")
            { reason = $"attestation asserts a non-Pass outcome '{record.outcome}'"; return AttestationValidity.Invalid; }
            if (string.IsNullOrEmpty(record.applicationId) || string.IsNullOrEmpty(record.appVersion) ||
                string.IsNullOrEmpty(record.mode) || string.IsNullOrEmpty(record.platform))
            { reason = "attestation is missing build identity"; return AttestationValidity.Invalid; }
            if (!ScopeMatches(record.proofScope, expected.RequiredScope))
            { reason = $"attestation proof scope '{record.proofScope}' does not match the gate's required scope"; return AttestationValidity.Invalid; }
            // Vendor/dashboard attestations must carry a human evidence note (C45-06).
            if ((expected.RequiredScope & ProofScope.VendorAccepted) != 0 && string.IsNullOrWhiteSpace(record.evidenceNote))
            { reason = "a vendor/dashboard attestation requires an evidence note"; return AttestationValidity.Invalid; }
            if (!DateTime.TryParse(record.timestampUtc, null,
                    System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                    out DateTime ts))
            { reason = "attestation timestamp is unparseable"; return AttestationValidity.Invalid; }
            if (ts > nowUtc.AddMinutes(5))
            { reason = "attestation timestamp is in the future"; return AttestationValidity.Invalid; }

            // ── Stale: was real, but not for THIS build/version/phase ──
            if (record.gateVersion != expected.GateVersion)
            { reason = $"gate definition changed (attested v{record.gateVersion}, now v{expected.GateVersion}) - re-attest"; return AttestationValidity.Stale; }
            if (record.phase != expected.Phase)
            { reason = $"attestation phase '{record.phase}' does not match the requested phase '{expected.Phase}'"; return AttestationValidity.Stale; }
            if (record.applicationId != expected.Identity.ApplicationId || record.mode != expected.Identity.Mode ||
                record.appVersion != expected.Identity.AppVersion || record.platform != expected.Identity.Platform)
            { reason = "attestation was made against a different game/build - re-attest"; return AttestationValidity.Stale; }

            // Device-session gates bind to the exact connected build GUID (C45-05): app-version scope alone is
            // not exact-build, so a different same-version binary cannot inherit the attestation.
            if ((expected.RequiredScope & ProofScope.DeviceDispatch) != 0)
            {
                if (string.IsNullOrEmpty(record.deviceBuildGuid))
                { reason = "device-session attestation was not bound to a connected build - re-attest with a device connected"; return AttestationValidity.Stale; }
                if (string.IsNullOrEmpty(expected.DeviceBuildGuid))
                { reason = "no connected device build to verify the attestation against - reconnect the device"; return AttestationValidity.Stale; }
                if (record.deviceBuildGuid != expected.DeviceBuildGuid)
                { reason = "attestation was made against a different device build (GUID mismatch) - re-attest"; return AttestationValidity.Stale; }
            }

            if (nowUtc - ts > MaxAge)
            { reason = $"attestation is older than {MaxAge.Days} days - re-attest"; return AttestationValidity.Stale; }

            reason = $"attested by {record.actor} at {record.timestampUtc}";
            return AttestationValidity.Valid;
        }

        static bool ScopeMatches(string recordScope, ProofScope required) =>
            Enum.TryParse(recordScope, out ProofScope parsed) && parsed == required;
    }

    /// <summary>
    ///     Project-scoped persistence for attestations under <c>UserSettings/</c> (per-machine QA evidence,
    ///     not committed). Writes are atomic same-volume replaces that keep the prior valid file as a backup
    ///     until the replace succeeds; a corrupt file is reported explicitly (not silently emptied) so the
    ///     adapter can surface INCOMPLETE integrity evidence (review C45-04). Tests inject a temp path via
    ///     <see cref="PathOverride"/> so they never touch live evidence (C45-03).
    /// </summary>
    internal static class QaAttestationStore
    {
        /// <summary>Test-only override for the storage path; null = the live UserSettings location.</summary>
        internal static string PathOverride;

        static string FilePath => PathOverride ??
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "UserSettings", "SorollaQaAttestations.json"));

        /// <summary>Loads records; <paramref name="corrupt"/> is true when the file exists but cannot be
        /// parsed - the caller must treat that as INCOMPLETE, not an empty (no-attestations) set.</summary>
        internal static List<QaAttestationRecord> Load(out bool corrupt)
        {
            corrupt = false;
            string path = FilePath;
            if (!File.Exists(path)) return new List<QaAttestationRecord>();
            try
            {
                QaAttestationFile file = JsonUtility.FromJson<QaAttestationFile>(File.ReadAllText(path));
                if (file == null) { corrupt = true; return new List<QaAttestationRecord>(); }
                return file.attestations ?? new List<QaAttestationRecord>();
            }
            catch
            {
                corrupt = true;
                return new List<QaAttestationRecord>();
            }
        }

        /// <summary>Returns the single record for a gate id, or null. Duplicate ids → a synthetic conflict
        /// record (unknown schema) so the validator rejects it rather than picking one arbitrarily.</summary>
        internal static QaAttestationRecord ForGate(IReadOnlyList<QaAttestationRecord> records, string gateId)
        {
            List<QaAttestationRecord> matches = records.Where(r => r != null && r.gateId == gateId).ToList();
            if (matches.Count == 0) return null;
            if (matches.Count > 1) return new QaAttestationRecord { gateId = gateId, schema = "conflict" };
            return matches[0];
        }

        internal static void Record(QaAttestationRecord record)
        {
            List<QaAttestationRecord> list = Load(out _);
            list.RemoveAll(r => r == null || r.gateId == record.gateId);
            list.Add(record);
            AtomicWrite(new QaAttestationFile { attestations = list });
        }

        internal static void Clear(string gateId)
        {
            List<QaAttestationRecord> list = Load(out _);
            list.RemoveAll(r => r == null || r.gateId == gateId);
            AtomicWrite(new QaAttestationFile { attestations = list });
        }

        /// <summary>Same-volume atomic replace: write a temp file, then <see cref="File.Replace"/> it over the
        /// target keeping a backup (recovered on failure); first write uses Move. The prior valid file is
        /// never lost mid-write (review C45-04).</summary>
        static void AtomicWrite(QaAttestationFile file)
        {
            string path = FilePath;
            string dir = Path.GetDirectoryName(path);
            Directory.CreateDirectory(dir);
            string tmp = path + ".tmp";
            string bak = path + ".bak";
            File.WriteAllText(tmp, JsonUtility.ToJson(file, prettyPrint: true));
            try
            {
                if (File.Exists(path))
                {
                    File.Replace(tmp, path, bak); // atomic on the same volume, prior file preserved in .bak
                    if (File.Exists(bak)) File.Delete(bak);
                }
                else
                {
                    File.Move(tmp, path);
                }
            }
            finally
            {
                if (File.Exists(tmp)) File.Delete(tmp);
            }
        }
    }
}
