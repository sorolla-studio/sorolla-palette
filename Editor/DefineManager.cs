using System.Linq;
using UnityEditor;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace SorollaPalette.Editor
{
    /// <summary>
    ///     Centralized define symbol management for Sorolla Palette
    ///     Eliminates DRY violations by providing unified define management
    /// </summary>
    public static class DefineManager
    {
        /// <summary>
        ///     Set a define symbol enabled/disabled for all build targets
        /// </summary>
        public static void SetDefineEnabled(string define, bool enabled)
        {
            SetDefineEnabledForTarget(NamedBuildTarget.Android, define, enabled);
            SetDefineEnabledForTarget(NamedBuildTarget.iOS, define, enabled);
        }

        /// <summary>
        ///     Set a define symbol enabled/disabled for a specific build target
        /// </summary>
        public static void SetDefineEnabledForTarget(NamedBuildTarget buildTarget, string define, bool enabled)
        {
            var defines = PlayerSettings.GetScriptingDefineSymbols(buildTarget)
                .Split(';')
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Distinct()
                .ToList();

            var changed = false;

            if (enabled && !defines.Contains(define))
            {
                defines.Add(define);
                changed = true;
            }
            else if (!enabled && defines.Contains(define))
            {
                defines.Remove(define);
                changed = true;
            }

            if (changed)
            {
                var newSymbols = string.Join(";", defines);
                PlayerSettings.SetScriptingDefineSymbols(buildTarget, newSymbols);
                Debug.Log($"[DefineManager] Updated defines for {buildTarget}: {newSymbols}");
            }
        }

        /// <summary>
        ///     Apply mode-specific defines
        /// </summary>
        public static void ApplyModeDefines(string mode)
        {
            // Only manage mode defines. SDK defines are handled by versionDefines in asmdefs.
            if (mode == SorollaConstants.ModePrototype)
            {
                SetDefineEnabled(SorollaConstants.DefinePrototype, true);
                SetDefineEnabled(SorollaConstants.DefineFull, false);
            }
            else if (mode == SorollaConstants.ModeFull)
            {
                SetDefineEnabled(SorollaConstants.DefinePrototype, false);
                SetDefineEnabled(SorollaConstants.DefineFull, true);
            }
        }
    }
}