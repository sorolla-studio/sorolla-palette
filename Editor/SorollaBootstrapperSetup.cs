using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    /// Automatically configures SorollaBootstrapper script execution order to -1000
    /// to ensure SDK initializes before any client code.
    ///
    /// This is part of the triple-defense strategy for DontDestroyOnLoad conflicts:
    /// - Layer 1: HideFlags check in SorollaBootstrapper.MakePersistent
    /// - Layer 2: Try-catch safety net
    /// - Layer 3: Execution order guarantee (this script)
    /// </summary>
    [InitializeOnLoad]
    public static class SorollaBootstrapperSetup
    {
        const int EXECUTION_ORDER = -1000;
        const string SCRIPT_NAME = "SorollaBootstrapper";

        static SorollaBootstrapperSetup()
        {
            EnsureExecutionOrder();
        }

        static void EnsureExecutionOrder()
        {
            // Find the SorollaBootstrapper script
            var script = FindMonoScript(SCRIPT_NAME);
            if (script == null)
            {
                Debug.LogWarning($"[Palette:Setup] Could not find {SCRIPT_NAME} script for execution order setup");
                return;
            }

            // Check current execution order
            int currentOrder = MonoImporter.GetExecutionOrder(script);

            if (currentOrder == EXECUTION_ORDER)
            {
                // Already configured correctly
                return;
            }

            // Set execution order
            MonoImporter.SetExecutionOrder(script, EXECUTION_ORDER);
            Debug.Log($"[Palette:Setup] Set {SCRIPT_NAME} execution order to {EXECUTION_ORDER} (was {currentOrder})");
        }

        static MonoScript FindMonoScript(string className)
        {
            string[] guids = AssetDatabase.FindAssets($"t:MonoScript {className}");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);

                if (script != null && script.GetClass() != null && script.GetClass().Name == className)
                {
                    return script;
                }
            }

            return null;
        }

        /// <summary>
        /// Menu command to manually verify/reset execution order
        /// </summary>
        [MenuItem("Palette/Tools/Verify Execution Order")]
        static void VerifyExecutionOrderMenu()
        {
            var script = FindMonoScript(SCRIPT_NAME);
            if (script == null)
            {
                EditorUtility.DisplayDialog("Palette Setup",
                    $"Could not find {SCRIPT_NAME} script", "OK");
                return;
            }

            int currentOrder = MonoImporter.GetExecutionOrder(script);

            if (currentOrder == EXECUTION_ORDER)
            {
                EditorUtility.DisplayDialog("Palette Setup",
                    $"✓ {SCRIPT_NAME} execution order is correctly set to {EXECUTION_ORDER}", "OK");
            }
            else
            {
                bool reset = EditorUtility.DisplayDialog("Palette Setup",
                    $"{SCRIPT_NAME} execution order is {currentOrder}\n\n" +
                    $"Reset to {EXECUTION_ORDER}?", "Yes", "No");

                if (reset)
                {
                    MonoImporter.SetExecutionOrder(script, EXECUTION_ORDER);
                    Debug.Log($"[Palette:Setup] Reset {SCRIPT_NAME} execution order to {EXECUTION_ORDER}");
                    EditorUtility.DisplayDialog("Palette Setup",
                        $"✓ Execution order reset to {EXECUTION_ORDER}", "OK");
                }
            }
        }
    }
}
