using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     Helper methods for SorollaConfig asset management.
    ///     Centralized config creation to avoid duplication.
    /// </summary>
    public static class SorollaConfigHelper
    {
        /// <summary>
        ///     Ensure SorollaConfig exists in Resources folder.
        ///     Creates it automatically if missing.
        /// </summary>
        /// <returns>The config asset (existing or newly created)</returns>
        public static SorollaConfig EnsureConfigExists()
        {
            string[] guids = AssetDatabase.FindAssets("t:SorollaConfig");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<SorollaConfig>(path);
            }

            return CreateConfig();
        }

        /// <summary>
        ///     Create a new SorollaConfig asset in Resources folder.
        /// </summary>
        /// <returns>The newly created config asset</returns>
        public static SorollaConfig CreateConfig()
        {
            var config = ScriptableObject.CreateInstance<SorollaConfig>();

            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
                Debug.Log("[Palette] Created Assets/Resources folder.");
            }

            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Resources/SorollaConfig.asset");
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Palette] SorollaConfig created at: {path}");
            return config;
        }
    }
}
