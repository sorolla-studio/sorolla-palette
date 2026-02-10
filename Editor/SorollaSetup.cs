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
    public static class SorollaSetup
    {
        const string SetupVersion = "v7"; // v3.1.0: Firebase required in Full, optional in Prototype

        static SorollaSetup()
        {
            EditorApplication.delayCall += RunSetup;
        }
        static string SetupKey => $"Sorolla_Setup_{SetupVersion}_{Application.dataPath.GetHashCode()}";
        static string FirebaseMigrationKey => $"Sorolla_Firebase31_{Application.dataPath.GetHashCode()}";

        [MenuItem("Palette/Run Setup (Force)")]
        public static void ForceRunSetup()
        {
            EditorPrefs.DeleteKey(SetupKey);
            RunSetup();
        }

        static void RunSetup()
        {
            if (EditorPrefs.GetBool(SetupKey, false))
                return;

            Debug.Log("[Palette] Running initial setup...");

            // Copy link.xml to Assets/ for IL2CPP stripping protection
            CopyLinkXmlToAssets();

            // Configure EDM4U to use Unity's Gradle (fixes Java 17+ compatibility)
            ConfigureEdm4uGradleSettings();

            // Collect all scopes needed for OpenUPM
            var openUpmScopes = new List<string>();
            var dependencies = new Dictionary<string, string>();

            foreach (SdkInfo sdk in SdkRegistry.All.Values)
            {
                if (sdk.Requirement != SdkRequirement.Core)
                    continue;

                // Add scope if needed
                if (!string.IsNullOrEmpty(sdk.Scope))
                    openUpmScopes.Add(sdk.Scope);

                // Add dependency
                dependencies[sdk.PackageId] = sdk.DependencyValue;
            }

            // Add OpenUPM registry with all scopes
            if (openUpmScopes.Count > 0)
            {
                ManifestManager.AddOrUpdateRegistry(
                    "package.openupm.com",
                    "https://package.openupm.com",
                    openUpmScopes.ToArray()
                );
            }

            // Add all core dependencies in one shot - UPM handles resolution order
            ManifestManager.AddDependencies(dependencies);

            EditorPrefs.SetBool(SetupKey, true);
            Debug.Log("[Palette] Setup complete. Package Manager will resolve dependencies.");

            // v3.1.0: Show migration popup once for Firebase upgrade
            if (!EditorPrefs.GetBool(FirebaseMigrationKey, false))
            {
                EditorPrefs.SetBool(FirebaseMigrationKey, true);
                EditorApplication.delayCall += MigrationPopup.Display;
            }
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
            if (File.Exists(destPath))
            {
                Debug.Log("[Palette] link.xml already exists in Assets/, skipping copy.");
                return;
            }

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

        /// <summary>
        ///     Configure EDM4U Gradle settings during initial setup.
        ///     Delegates to Edm4uGradleConfig which also runs on every domain reload.
        /// </summary>
        static void ConfigureEdm4uGradleSettings()
        {
            Edm4uGradleConfig.ConfigureGradleTemplateMode();
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
