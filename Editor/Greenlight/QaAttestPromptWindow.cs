using System;
using Sorolla.Palette.Health;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor.Greenlight
{
    /// <summary>
    ///     Modal prompt for recording a HUMAN manual attestation (review C45-06). One click can no longer
    ///     manufacture a PASS: the tester must read the gate-specific affirmation, supply an evidence note
    ///     (required for vendor/dashboard gates), and - for device-session gates - have the device they ran
    ///     the session on connected so the attestation binds to that exact build GUID. The window is explicit
    ///     that this is a human attestation, not machine-observed proof.
    /// </summary>
    internal class QaAttestPromptWindow : EditorWindow
    {
        GreenlightManualChecklist.Descriptor _descriptor;
        string _deviceBuildGuid;
        bool _noteRequired;
        bool _deviceGate;
        string _note = "";
        Action _onDone;

        internal static void Show(GreenlightManualChecklist.Descriptor descriptor, string deviceBuildGuid, Action onDone)
        {
            GateDefinition def = GateCatalog.Canonical.ById(descriptor.GateId, throwIfMissing: false);
            ProofScope required = def?.RequiredProof ?? ProofScope.None;

            var window = CreateInstance<QaAttestPromptWindow>();
            window.titleContent = new GUIContent("Attest QA Gate");
            window._descriptor = descriptor;
            window._deviceBuildGuid = deviceBuildGuid;
            window._noteRequired = (required & ProofScope.VendorAccepted) != 0;
            window._deviceGate = (required & ProofScope.DeviceDispatch) != 0;
            window._onDone = onDone;
            window.minSize = new Vector2(480, 300);
            window.ShowUtility();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField(_descriptor.Label, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "This records a HUMAN attestation, not machine-observed proof. Attest only what you actually " +
                "performed and observed on THIS build. Your name and time are recorded.", MessageType.Warning);

            EditorGUILayout.LabelField("What to verify:", EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField(_descriptor.Fix, EditorStyles.wordWrappedLabel);

            if (_deviceGate)
            {
                if (string.IsNullOrEmpty(_deviceBuildGuid))
                    EditorGUILayout.HelpBox(
                        "No connected device build. Connect the device you ran the session on (Connect Device) " +
                        "before attesting - this gate binds to that build's GUID.", MessageType.Error);
                else
                    EditorGUILayout.LabelField($"Binds to connected device build GUID: {_deviceBuildGuid}", EditorStyles.miniLabel);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                _noteRequired ? "Evidence note (required - what you did and observed):" : "Evidence note (optional):",
                EditorStyles.miniBoldLabel);
            _note = EditorGUILayout.TextArea(_note, GUILayout.Height(64));

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Cancel", GUILayout.Width(90))) Close();

                bool canAttest = (!_noteRequired || !string.IsNullOrWhiteSpace(_note)) &&
                                 (!_deviceGate || !string.IsNullOrEmpty(_deviceBuildGuid));
                using (new EditorGUI.DisabledScope(!canAttest))
                {
                    if (GUILayout.Button("I performed this — Attest", GUILayout.Width(200)))
                    {
                        if (GreenlightAdapter.AttestManualGate(_descriptor.GateId, _note, _deviceBuildGuid))
                        {
                            _onDone?.Invoke();
                            Close();
                        }
                        else
                        {
                            EditorUtility.DisplayDialog("Attestation rejected",
                                "The attestation was not recorded: a vendor gate needs an evidence note, and a " +
                                "device gate needs a connected device build.", "OK");
                        }
                    }
                }
            }
        }
    }
}
