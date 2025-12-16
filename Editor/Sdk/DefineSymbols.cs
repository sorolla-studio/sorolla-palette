using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Sorolla.Editor
{
    /// <summary>
    ///     Manages scripting define symbols for SorollaSDK SDK.
    /// </summary>
    public static class DefineSymbols
    {
        /// <summary>
        ///     Apply mode-specific defines
        /// </summary>
        public static void Apply(bool isPrototype)
        {
            Set(SorollaSettings.DefinePrototype, isPrototype);
            Set(SorollaSettings.DefineFull, !isPrototype);
        }

        /// <summary>
        ///     Set a define symbol enabled/disabled for all build targets
        /// </summary>
        public static void Set(string define, bool enabled)
        {
            SetForTarget(NamedBuildTarget.Android, define, enabled);
            SetForTarget(NamedBuildTarget.iOS, define, enabled);
        }

        /// <summary>
        ///     Set a define symbol for a specific build target
        /// </summary>
        public static void SetForTarget(NamedBuildTarget target, string define, bool enabled)
        {
            var defines = PlayerSettings.GetScriptingDefineSymbols(target)
                .Split(';')
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Distinct()
                .ToList();

            bool hasDefine = defines.Contains(define);

            if (enabled && !hasDefine)
            {
                defines.Add(define);
            }
            else if (!enabled && hasDefine)
            {
                defines.Remove(define);
            }
            else
            {
                return; // No change needed
            }

            string newSymbols = string.Join(";", defines);
            PlayerSettings.SetScriptingDefineSymbols(target, newSymbols);
            Debug.Log($"[SorollaSDK] Defines for {target}: {newSymbols}");
        }
    }
}
