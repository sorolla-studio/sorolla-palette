using System;
using System.Linq;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     SDK installation detection
    /// </summary>
    public static class SdkDetector
    {
        /// <summary>
        ///     Check if an SDK is installed
        /// </summary>
        public static bool IsInstalled(SdkId id)
        {
            if (!SdkRegistry.All.TryGetValue(id, out var info))
                return false;

            return CheckTypes(info.DetectionTypes) || CheckAssemblies(info.DetectionAssemblies);
        }

        /// <summary>
        ///     Check if an SDK is installed (by info)
        /// </summary>
        public static bool IsInstalled(SdkInfo info)
        {
            return CheckTypes(info.DetectionTypes) || CheckAssemblies(info.DetectionAssemblies);
        }

        /// <summary>
        ///     Check if all required SDKs for a mode are installed
        /// </summary>
        public static bool AreAllRequiredInstalled(bool isPrototype)
        {
            foreach (var sdk in SdkRegistry.GetRequired(isPrototype))
                if (!IsInstalled(sdk))
                    return false;
            return true;
        }

        private static bool CheckTypes(string[] typeNames)
        {
            return typeNames != null && typeNames.Any(t => Type.GetType(t) != null);
        }

        private static bool CheckAssemblies(string[] assemblyNames)
        {
            if (assemblyNames == null || assemblyNames.Length == 0)
                return false;

            var loaded = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var name in assemblyNames)
            {
                var term = name.ToLowerInvariant();
                if (loaded.Any(a => a.GetName().Name.ToLowerInvariant().Contains(term)))
                    return true;
            }
            return false;
        }
    }
}
