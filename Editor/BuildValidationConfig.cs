using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    public static partial class BuildValidator
    {
        /// <summary>
        ///     Check that SorollaConfig settings match installed SDKs, AND that the asset lives at the
        ///     exact path (<see cref="ExpectedConfigPath"/>) Resources.Load/runtime require - not just
        ///     "some SorollaConfig exists somewhere" (F14, 2026-07-21 audit: this window's own
        ///     asset-finder, AssetDatabase.FindAssets, can load/edit a SorollaConfig outside Resources/
        ///     entirely, which Resources.Load then can never resolve - the hero header, this check, and
        ///     the Create-button availability disagreed as a result). Severity stays Warning pending an
        ///     escalation ruling (F14's severity question is a separate judgment call, not touched here);
        ///     only the path validation and the fix action are mechanical.
        /// </summary>
        const string ExpectedConfigPath = "Assets/Resources/SorollaConfig.asset";

        static List<ValidationResult> CheckConfigSync(Dictionary<string, object> dependencies)
        {
            var results = new List<ValidationResult>();

            var config = Resources.Load<SorollaConfig>("SorollaConfig");
            if (config == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:SorollaConfig");
                if (guids.Length > 0)
                {
                    string actualPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                    results.Add(Warning(
                        CheckCategory.ConfigSync,
                        $"SorollaConfig exists at '{actualPath}', not '{ExpectedConfigPath}'.\n" +
                        "  Resources.Load (and the runtime) cannot find it there, so the SDK silently runs unconfigured.",
                        // Fix points at the in-window Create button, not menu prose (F14) - creating a
                        // fresh asset there is simpler and safer than moving the misplaced one blind.
                        $"Click \"Create Configuration Asset\" in this window, or move the existing asset to {ExpectedConfigPath}"));
                }
                else
                {
                    results.Add(Warning(
                        CheckCategory.ConfigSync,
                        "SorollaConfig not found.",
                        "Click \"Create Configuration Asset\" in this window"));
                }

                return results;
            }

            string configPath = AssetDatabase.GetAssetPath(config);
            if (configPath != ExpectedConfigPath)
            {
                // Resources.Load scans EVERY folder literally named "Resources" anywhere under Assets, not
                // just the top-level one - a config in a different Resources/ folder still resolves here
                // but isn't at the canonical path other tooling assumes.
                results.Add(Warning(
                    CheckCategory.ConfigSync,
                    $"SorollaConfig resolves via Resources.Load but lives at '{configPath}', not the canonical '{ExpectedConfigPath}'.",
                    $"Move the asset to {ExpectedConfigPath}"));
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

            // Auto-install missing required SDKs. The installer also restores their registries.
            if (!SdkDetector.AreAllRequiredInstalled(config.isPrototypeMode))
            {
                string modeName = config.isPrototypeMode ? "Prototype" : "Full";
                Debug.Log($"{Tag} Auto-fixing: Installing missing required SDKs for {modeName} mode...");
                SdkInstaller.InstallRequiredSdks(config.isPrototypeMode);
                changed = true;
            }
            else if (SdkInstaller.EnsureRequiredRegistries(config.isPrototypeMode))
            {
                // Installed assemblies do not prove their manifest registry entries still exist.
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
