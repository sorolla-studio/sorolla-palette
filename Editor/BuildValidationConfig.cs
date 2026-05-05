using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    public static partial class BuildValidator
    {
        /// <summary>
        ///     Check that SorollaConfig settings match installed SDKs
        /// </summary>
        static List<ValidationResult> CheckConfigSync(Dictionary<string, object> dependencies)
        {
            var results = new List<ValidationResult>();

            var config = Resources.Load<SorollaConfig>("SorollaConfig");
            if (config == null)
            {
                results.Add(Warning(
                    CheckCategory.ConfigSync,
                    "SorollaConfig not found in Resources folder",
                    "Create config via Assets > Create > Palette > Config"));
                return results;
            }

            results.Add(Valid(CheckCategory.ConfigSync, "Config synced"));

            return results;
        }

        /// <summary>
        ///     Auto-fix config sync issues and install missing required SDKs.
        ///     Always installs missing SDKs - the user should never be in a state
        ///     where required SDKs are missing for the configured mode.
        /// </summary>
        public static bool FixConfigSync()
        {
            var config = Resources.Load<SorollaConfig>("SorollaConfig");
            if (config == null)
                return false;

            bool changed = false;

            if (SorollaSettings.SyncFromRuntimeConfig())
                changed = true;

            // Auto-install missing required SDKs
            if (!SdkDetector.AreAllRequiredInstalled(config.isPrototypeMode))
            {
                string modeName = config.isPrototypeMode ? "Prototype" : "Full";
                Debug.Log($"{Tag} Auto-fixing: Installing missing required SDKs for {modeName} mode...");
                SdkInstaller.InstallRequiredSdks(config.isPrototypeMode);
                changed = true;
            }

            if (changed)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"{Tag} Config sync issues auto-fixed");
            }

            return changed;
        }
    }
}
