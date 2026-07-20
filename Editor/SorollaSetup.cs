using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     Auto-setup on package import.
    ///     Configures manifest.json with registries and core dependencies in one shot.
    ///     Unity Package Manager handles resolution order automatically.
    /// </summary>
    [InitializeOnLoad]
    internal static class SorollaSetup
    {
        const string SetupVersion = "v7"; // v3.1.0: Firebase required in Full, optional in Prototype

        static SorollaSetup()
        {
            EditorApplication.delayCall += RunSetup;
        }
        static string SetupKey => $"Sorolla_Setup_{SetupVersion}_{Application.dataPath.GetHashCode()}";

        static void RunSetup()
        {
            // This repair is idempotent and must not be hidden behind the one-time dependency gate.
            CopyLinkXmlToAssets();

            if (EditorPrefs.GetBool(SetupKey, false))
                return;

            Debug.Log("[Palette] Running initial setup...");

            var dependencies = new Dictionary<string, string>();

            foreach (SdkInfo sdk in SdkRegistry.All.Values)
            {
                if (sdk.Requirement != SdkRequirement.Core)
                    continue;

                // Add dependency
                dependencies[sdk.PackageId] = sdk.DependencyValue;
            }

            SdkInstaller.EnsureCoreRegistries();

            // Add all core dependencies in one shot - UPM handles resolution order
            ManifestManager.AddDependencies(dependencies);

            EditorPrefs.SetBool(SetupKey, true);
            Debug.Log("[Palette] Setup complete. Package Manager will resolve dependencies.");

        }

        /// <summary>
        ///     Copy link.xml from package to Assets/ for IL2CPP code stripping protection.
        ///     Unity does NOT auto-include link.xml from UPM packages, so we must copy it.
        /// </summary>
        static void CopyLinkXmlToAssets()
        {
            const string sourcePath = "Packages/com.sorolla.sdk/Runtime/link.xml";
            const string destPath = "Assets/Sorolla.link.xml";

            // Skip if already exists (don't overwrite user modifications)
            if (File.Exists(destPath)) return;

            if (!File.Exists(sourcePath))
            {
                Debug.LogWarning($"[Palette] Source link.xml not found at {sourcePath}");
                return;
            }

            try
            {
                File.Copy(sourcePath, destPath);
                AssetDatabase.Refresh();
                Debug.Log($"[Palette] Copied link.xml to {destPath} for IL2CPP stripping protection.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Palette] Failed to copy link.xml: {e.Message}");
            }
        }

    }

    /// <summary>
    ///     Ensures EDM4U Gradle template mode on every domain reload.
    ///     Separate from SorollaSetup to avoid the one-time setup gate — EDM4U may load
    ///     after SorollaSetup runs on first import, causing a race condition where the
    ///     bundled Gradle 5.1.1 is used instead of Unity's Gradle (Java 17+ incompatible).
    ///     This is idempotent (checks before setting) so running every reload is safe.
    /// </summary>
    [InitializeOnLoad]
    internal static class Edm4uGradleConfig
    {
        static Edm4uGradleConfig()
        {
            EditorApplication.delayCall += ConfigureGradleTemplateMode;
        }

        internal static void ConfigureGradleTemplateMode()
        {
            // Find EDM4U's SettingsDialog type via reflection (avoids hard dependency)
            Type settingsType = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                settingsType = assembly.GetType("GooglePlayServices.SettingsDialog");
                if (settingsType != null)
                    break;
            }

            if (settingsType == null)
                return; // EDM4U not loaded yet

            try
            {
                const BindingFlags staticFlags = BindingFlags.Public | BindingFlags.Static;
                bool changed = false;

                // PatchMainTemplateGradle - tells EDM4U to patch mainTemplate.gradle
                var mainTemplateProp = settingsType.GetProperty("PatchMainTemplateGradle", staticFlags);
                if (mainTemplateProp != null && !(bool)mainTemplateProp.GetValue(null))
                {
                    mainTemplateProp.SetValue(null, true);
                    changed = true;
                }

                // PatchPropertiesTemplateGradle - tells EDM4U to patch gradleTemplate.properties
                var propertiesProp = settingsType.GetProperty("PatchPropertiesTemplateGradle", staticFlags);
                if (propertiesProp != null && !(bool)propertiesProp.GetValue(null))
                {
                    propertiesProp.SetValue(null, true);
                    changed = true;
                }

                // PatchSettingsTemplateGradle - tells EDM4U to patch settingsTemplate.gradle (Unity 2022.2+)
                var settingsProp = settingsType.GetProperty("PatchSettingsTemplateGradle", staticFlags);
                if (settingsProp != null && !(bool)settingsProp.GetValue(null))
                {
                    settingsProp.SetValue(null, true);
                    changed = true;
                }

                if (changed)
                    Debug.Log("[Palette] Configured EDM4U to use Unity's Gradle templates (Java 17+ compatibility).");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Palette] Could not configure EDM4U settings: {e.Message}");
            }
        }
    }
}
