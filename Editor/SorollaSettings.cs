using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     Central settings for Palette SDK.
    /// </summary>
    public static class SorollaSettings
    {

        // Scripting define symbols
        public const string DefinePrototype = "SOROLLA_PROTOTYPE";
        public const string DefineFull = "SOROLLA_FULL";
        static string ModeKey => $"Sorolla_Mode_{Application.dataPath.GetHashCode()}";

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
                Debug.LogWarning("[Palette] Cannot set mode to None");
                return;
            }

            Debug.Log($"[Palette] Setting mode to: {mode}");

            Mode = mode;

            // Update runtime config asset
            UpdateRuntimeConfig(mode == SorollaMode.Prototype);

            // Apply scripting defines
            DefineSymbols.Apply(mode == SorollaMode.Prototype);

            // Install required SDKs (auto-installs missing dependencies immediately)
            SdkInstaller.InstallRequiredSdks(mode == SorollaMode.Prototype);

            // Uninstall unnecessary SDKs
            SdkInstaller.UninstallUnnecessarySdks(mode == SorollaMode.Prototype);

            AssetDatabase.Refresh();
            Debug.Log("[Palette] Mode switch complete. All required dependencies installed automatically.");
        }

        /// <summary>
        ///     Update runtime config asset to match current mode
        /// </summary>
        static void UpdateRuntimeConfig(bool isPrototype)
        {
            var config = Resources.Load<SorollaConfig>("SorollaConfig");
            if (config == null)
            {
                Debug.LogWarning("[Palette] SorollaConfig not found in Resources. Runtime mode may be incorrect.");
                return;
            }

            if (config.isPrototypeMode != isPrototype)
            {
                config.isPrototypeMode = isPrototype;
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Palette] Updated SorollaConfig.isPrototypeMode = {isPrototype}");
            }
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
