using System.IO;
using System.Linq;
using UnityEngine;

#if UNITY_ANDROID
using UnityEditor.Android;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     Runs after AppLovin's post-processor to re-enable DexingArtifactTransform.
    ///     AppLovin (callbackOrder = int.MaxValue - 10) disables it for Unity &lt; 6,
    ///     but modern Firebase/Kotlin/AppLovin JARs require the modern D8 pipeline.
    ///     Unity 6+ uses AGP 8.x — AppLovin skips the injection there, so this is a no-op.
    /// </summary>
    public class GradlePropertiesFixer : IPostGenerateGradleAndroidProject
    {
        private const string DexingProperty = "android.enableDexingArtifactTransform";

        // Run after AppLovin (int.MaxValue - 10)
        public int callbackOrder => int.MaxValue;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
#if UNITY_6000_0_OR_NEWER
            // Unity 6+ (AGP 8.x): AppLovin does not inject =false, R8 8.5+ handles Kotlin 2.0
            // natively. Nothing to fix.
#else
            // Unity 2022 and earlier (AGP 7.4.2): AppLovin injects =false which breaks D8
            // on Kotlin 2.0 metadata in applovin-sdk 13.x / firebase 23.x. Override to true.

            // path points to unityLibrary — gradle.properties is one level up
            var gradlePropertiesPath = Path.GetFullPath(Path.Combine(path, "..", "gradle.properties"));

            if (!File.Exists(gradlePropertiesPath))
            {
                Debug.LogWarning("[Palette] gradle.properties not found, skipping dexing fix");
                return;
            }

            var lines = File.ReadAllLines(gradlePropertiesPath);
            var updated = lines
                .Where(line => !line.Contains(DexingProperty))
                .ToList();

            updated.Add($"{DexingProperty}=true");

            File.WriteAllText(gradlePropertiesPath, string.Join("\n", updated) + "\n");
            Debug.Log("[Palette] Enabled DexingArtifactTransform (overriding AppLovin legacy dexing)");
#endif
        }
    }
}
#endif
