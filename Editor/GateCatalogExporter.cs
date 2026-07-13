using System.Collections.Generic;
using System.IO;
using Sorolla.Palette.Health;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     Emits the canonical gate catalog (ids + per-gate versions + phases + required proof) as a
    ///     machine-readable JSON artifact (review C3-08). This is the ONE-SOURCE hand-off: the private
    ///     gates.yaml workflow validates against this export rather than re-deriving the id list from prose.
    ///     The SDK ships only the emitter; the private-side divergence check lives in sorolla-docs
    ///     (qa/scripts/validate-gate-catalog.py).
    /// </summary>
    public static class GateCatalogExporter
    {
        public const string Schema = "sorolla.gate-catalog/1";

        /// <summary>Serializes the canonical catalog to a JSON string (id, version, phases, required proof).</summary>
        public static string Serialize()
        {
            var gates = new List<object>();
            foreach (GateDefinition def in GateCatalog.Canonical.All)
            {
                gates.Add(new Dictionary<string, object>
                {
                    ["id"] = def.Id,
                    ["version"] = def.Version,
                    ["phases"] = FlagNames(def.Phases),
                    ["required_proof"] = ProofNames(def.RequiredProof),
                });
            }

            var root = new Dictionary<string, object>
            {
                ["schema"] = Schema,
                ["sdk_version"] = Palette.SdkVersion,
                ["gate_count"] = gates.Count,
                ["gates"] = gates,
            };
            return MiniJson.Serialize(root, prettyPrint: true);
        }

        /// <summary>Writes the export to <paramref name="path"/> (callable from CI / a script). Returns the path.</summary>
        public static string Export(string path)
        {
            string json = Serialize();
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, json);
            return path;
        }

        [MenuItem("Palette/QA/Export Gate Catalog (JSON)")]
        static void ExportMenu()
        {
            string path = EditorUtility.SaveFilePanel(
                "Export Gate Catalog", Application.dataPath, "gate-catalog", "json");
            if (string.IsNullOrEmpty(path)) return;
            Export(path);
            Debug.Log($"[Palette] Exported {GateCatalog.Canonical.All.Count} gate definitions to {path}");
        }

        static List<object> FlagNames(GatePhase phases)
        {
            var names = new List<object>();
            if ((phases & GatePhase.PreBuild) != 0) names.Add("PreBuild");
            if ((phases & GatePhase.QaPass) != 0) names.Add("QaPass");
            if ((phases & GatePhase.ReleaseShip) != 0) names.Add("ReleaseShip");
            return names;
        }

        static List<object> ProofNames(ProofScope proof)
        {
            var names = new List<object>();
            if ((proof & ProofScope.Static) != 0) names.Add("Static");
            if ((proof & ProofScope.DeviceDispatch) != 0) names.Add("DeviceDispatch");
            if ((proof & ProofScope.VendorAccepted) != 0) names.Add("VendorAccepted");
            return names;
        }
    }
}
