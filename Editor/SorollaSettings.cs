using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     SDK operating mode
    /// </summary>
    public enum SorollaMode
    {
        /// <summary>No mode selected yet (first run)</summary>
        None,
        /// <summary>Prototype: Core SDKs only (for rapid UA testing)</summary>
        Prototype,
        /// <summary>Full: Core SDKs + MAX + Adjust (for production)</summary>
        Full
    }

    /// <summary>
    ///     Central settings for Palette SDK.
    /// </summary>
    public static class SorollaSettings
    {
        public const string LegacyDefinePrototype = "SOROLLA_PROTOTYPE";
        public const string LegacyDefineFull = "SOROLLA_FULL";
        const string ConfigResourcePath = "SorollaConfig";
        const string ConfigAssetPath = "Assets/Resources/SorollaConfig.asset";

        /// <summary>
        ///     Current SDK mode. The source of truth is the git-tracked
        ///     Assets/Resources/SorollaConfig.asset file, not machine-local EditorPrefs.
        /// </summary>
        public static SorollaMode Mode
        {
            get
            {
                var config = LoadRuntimeConfig();
                if (config != null)
                    return config.isPrototypeMode ? SorollaMode.Prototype : SorollaMode.Full;

                return SorollaMode.None;
            }
        }

        /// <summary>
        ///     Whether a mode has been selected
        /// </summary>
        public static bool IsConfigured => Mode != SorollaMode.None;

        /// <summary>
        ///     Whether currently in Prototype mode
        /// </summary>
        public static bool IsPrototype => Mode == SorollaMode.Prototype;

        public static bool HasRuntimeConfig => LoadRuntimeConfig() != null;

        public static bool SyncFromRuntimeConfig()
        {
            var config = LoadRuntimeConfig();
            if (config == null)
                return false;

            return DefineSymbols.RemoveLegacyModeDefines();
        }

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

            // Update runtime config asset
            UpdateRuntimeConfig(mode == SorollaMode.Prototype);
            DefineSymbols.RemoveLegacyModeDefines();

            // Install required SDKs
            SdkInstaller.InstallRequiredSdks(mode == SorollaMode.Prototype);

            // Uninstall unnecessary SDKs
            SdkInstaller.UninstallUnnecessarySdks(mode == SorollaMode.Prototype);

            AssetDatabase.Refresh();
            Debug.Log("[Palette] Mode switch complete.");
        }

        /// <summary>
        ///     Update runtime config asset to match current mode
        /// </summary>
        static void UpdateRuntimeConfig(bool isPrototype)
        {
            var config = GetOrCreateRuntimeConfig();

            if (config.isPrototypeMode != isPrototype)
            {
                config.isPrototypeMode = isPrototype;
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Palette] Updated SorollaConfig.isPrototypeMode = {isPrototype}");
            }
        }

        static SorollaConfig LoadRuntimeConfig() => Resources.Load<SorollaConfig>(ConfigResourcePath);

        static SorollaConfig GetOrCreateRuntimeConfig()
        {
            var config = LoadRuntimeConfig();
            if (config != null)
                return config;

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");

            config = AssetDatabase.LoadAssetAtPath<SorollaConfig>(ConfigAssetPath);
            if (config != null)
                return config;

            config = ScriptableObject.CreateInstance<SorollaConfig>();
            AssetDatabase.CreateAsset(config, ConfigAssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Palette] Config created at: {ConfigAssetPath}");
            return config;
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
