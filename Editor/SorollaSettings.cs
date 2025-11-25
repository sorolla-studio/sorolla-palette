using UnityEditor;
using UnityEngine;

namespace Sorolla.Editor
{
    /// <summary>
    ///     Central settings for Sorolla SDK.
    /// </summary>
    public static class SorollaSettings
    {
        private static string ModeKey => $"Sorolla_Mode_{Application.dataPath.GetHashCode()}";

        // Scripting define symbols
        public const string DefinePrototype = "SOROLLA_PROTOTYPE";
        public const string DefineFull = "SOROLLA_FULL";

        /// <summary>
        ///     Current SDK mode
        /// </summary>
        public static SorollaMode Mode
        {
            get => (SorollaMode)EditorPrefs.GetInt(ModeKey, (int)SorollaMode.None);
            private set => EditorPrefs.SetInt(ModeKey, (int)value);
        }

        /// <summary>
        ///     Whether a mode has been selected
        /// </summary>
        public static bool IsConfigured => Mode != SorollaMode.None;

        /// <summary>
        ///     Whether currently in Prototype mode
        /// </summary>
        public static bool IsPrototype => Mode == SorollaMode.Prototype;

        /// <summary>
        ///     Set mode and apply all necessary changes
        /// </summary>
        public static void SetMode(SorollaMode mode)
        {
            if (mode == SorollaMode.None)
            {
                Debug.LogWarning("[Sorolla] Cannot set mode to None");
                return;
            }

            Debug.Log($"[Sorolla] Setting mode to: {mode}");

            Mode = mode;

            // Apply scripting defines
            DefineSymbols.Apply(mode == SorollaMode.Prototype);

            // Install required SDKs
            SdkInstaller.InstallRequiredSdks(mode == SorollaMode.Prototype);

            // Uninstall unnecessary SDKs
            SdkInstaller.UninstallUnnecessarySdks(mode == SorollaMode.Prototype);

            AssetDatabase.Refresh();
            Debug.Log($"[Sorolla] Mode switch complete.");
        }

        /// <summary>
        ///     Switch to Prototype mode
        /// </summary>
        public static void SetPrototypeMode() => SetMode(SorollaMode.Prototype);

        /// <summary>
        ///     Switch to Full mode
        /// </summary>
        public static void SetFullMode() => SetMode(SorollaMode.Full);
    }
}
