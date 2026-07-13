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
    ///     it records WHO asserted the gate, WHEN, against WHICH build identity, and with WHAT proof scope -
    ///     so it can be checked for freshness and identity match instead of being an unscoped no-op (B-10).
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
        public string timestampUtc;   // ISO-8601 UTC
        public string applicationId;
        public string platform;
        public string mode;
        public string appVersion;
        public string outcome;        // asserted outcome, e.g. "Pass"
        public string proofScope;     // the proof class claimed, e.g. "VendorAccepted" / "DeviceDispatch"
    }

    [Serializable]
    internal class QaAttestationFile
    {
        public List<QaAttestationRecord> attestations = new List<QaAttestationRecord>();
    }

    /// <summary>The trusted current build identity an attestation is checked against.</summary>
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

        /// <summary>Captured from the current Editor project/build state (the same fields the on-device
        /// snapshot binds), so an attestation made against one build is stale after the build identity moves.</summary>
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

    internal enum AttestationValidity { Valid, Stale, Invalid, Missing }

    /// <summary>
    ///     Pure validation of an attestation record against the current identity + the gate's required proof
    ///     scope (Cycle 4b). Rejects an unknown schema, a future timestamp, missing identity, or the wrong
    ///     proof scope (all <see cref="AttestationValidity.Invalid"/>); an identity mismatch or an expired
    ///     record is <see cref="AttestationValidity.Stale"/> (→ INCOMPLETE, not a pass). Testable without any
    ///     file or editor state.
    /// </summary>
    internal static class QaAttestationValidator
    {
        internal const string Schema = "1";
        internal static readonly TimeSpan MaxAge = TimeSpan.FromDays(14);

        internal static AttestationValidity Evaluate(
            QaAttestationRecord record, QaBuildIdentity expected, ProofScope requiredScope, DateTime nowUtc,
            out string reason)
        {
            if (record == null) { reason = "no attestation recorded"; return AttestationValidity.Missing; }

            if (record.schema != Schema)
            {
                reason = $"unknown attestation schema '{record.schema ?? "(none)"}'";
                return AttestationValidity.Invalid;
            }
            if (string.IsNullOrEmpty(record.applicationId) || string.IsNullOrEmpty(record.appVersion) ||
                string.IsNullOrEmpty(record.mode) || string.IsNullOrEmpty(record.platform))
            {
                reason = "attestation is missing build identity";
                return AttestationValidity.Invalid;
            }
            if (!ScopeMatches(record.proofScope, requiredScope))
            {
                reason = $"attestation proof scope '{record.proofScope}' does not match the gate's required scope";
                return AttestationValidity.Invalid;
            }
            if (!DateTime.TryParse(record.timestampUtc, null,
                    System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                    out DateTime ts))
            {
                reason = "attestation timestamp is unparseable";
                return AttestationValidity.Invalid;
            }
            if (ts > nowUtc.AddMinutes(5))
            {
                reason = "attestation timestamp is in the future";
                return AttestationValidity.Invalid;
            }

            // Identity mismatch or expiry = stale evidence (the attestation was real, but not for THIS build).
            if (record.applicationId != expected.ApplicationId || record.mode != expected.Mode ||
                record.appVersion != expected.AppVersion || record.platform != expected.Platform)
            {
                reason = "attestation was made against a different game/build - re-attest";
                return AttestationValidity.Stale;
            }
            if (nowUtc - ts > MaxAge)
            {
                reason = $"attestation is older than {MaxAge.Days} days - re-attest";
                return AttestationValidity.Stale;
            }

            reason = $"attested by {record.actor} at {record.timestampUtc}";
            return AttestationValidity.Valid;
        }

        static bool ScopeMatches(string recordScope, ProofScope required) =>
            Enum.TryParse(recordScope, out ProofScope parsed) && parsed == required;
    }

    /// <summary>
    ///     Project-scoped, atomic persistence for attestations under <c>UserSettings/</c> (per-machine QA
    ///     evidence, not committed). One record per gate id - a fresh attestation replaces the prior one;
    ///     duplicate ids in the file are treated as a conflict and rejected (Invalid) so a corrupted file
    ///     cannot grandfather a gate.
    /// </summary>
    internal static class QaAttestationStore
    {
        static string FilePath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "UserSettings", "SorollaQaAttestations.json"));

        internal static List<QaAttestationRecord> Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new List<QaAttestationRecord>();
                QaAttestationFile file = JsonUtility.FromJson<QaAttestationFile>(File.ReadAllText(FilePath));
                return file?.attestations ?? new List<QaAttestationRecord>();
            }
            catch
            {
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
            List<QaAttestationRecord> list = Load();
            list.RemoveAll(r => r == null || r.gateId == record.gateId);
            list.Add(record);
            string json = JsonUtility.ToJson(new QaAttestationFile { attestations = list }, prettyPrint: true);

            string dir = Path.GetDirectoryName(FilePath);
            Directory.CreateDirectory(dir);
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, json);
            File.Copy(tmp, FilePath, overwrite: true); // copy+delete = atomic replace on the same volume
            File.Delete(tmp);
        }

        /// <summary>Records an attestation for a gate against the CURRENT build identity, claiming the gate's
        /// required proof scope. This is the action the greenlight "Attest" button drives.</summary>
        internal static void AttestCurrent(string gateId, string gateVersion, string phase, ProofScope requiredScope)
        {
            QaBuildIdentity id = QaBuildIdentity.Current();
            Record(new QaAttestationRecord
            {
                schema = QaAttestationValidator.Schema,
                gateId = gateId,
                gateVersion = gateVersion,
                phase = phase,
                actor = Environment.UserName,
                timestampUtc = DateTime.UtcNow.ToString("o"),
                applicationId = id.ApplicationId,
                platform = id.Platform,
                mode = id.Mode,
                appVersion = id.AppVersion,
                outcome = "Pass",
                proofScope = requiredScope.ToString(),
            });
        }

        internal static void Clear(string gateId)
        {
            List<QaAttestationRecord> list = Load();
            list.RemoveAll(r => r == null || r.gateId == gateId);
            string json = JsonUtility.ToJson(new QaAttestationFile { attestations = list }, prettyPrint: true);
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(FilePath, json);
        }
    }
}
