using UnityEditor;
using UnityEngine;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     Which phase of the gate inventory Build Health is checking against. QaPass is the
    ///     default: release-scoped checks (sandbox mode, keystore, SDK pin) only fire in Release.
    ///     Display/scoping concept for the Phase 3 checks only - existing checks keep their current
    ///     severity and scope regardless of profile.
    /// </summary>
    public enum ValidationProfile
    {
        QaPass,
        Release,
    }

    /// <summary>
    ///     Machine-local (EditorPrefs), project-scoped current validation profile. Same
    ///     project-scoping convention as <see cref="SorollaSetup"/>'s setup key.
    /// </summary>
    public static class BuildValidationProfileSettings
    {
        const ValidationProfile DefaultProfile = ValidationProfile.QaPass;

        public static ValidationProfile Current
        {
            get => (ValidationProfile)EditorPrefs.GetInt(PrefKey, (int)DefaultProfile);
            set => EditorPrefs.SetInt(PrefKey, (int)value);
        }

        public static bool IsRelease => Current == ValidationProfile.Release;

        static string PrefKey => $"Sorolla_ValidationProfile_{Application.dataPath.GetHashCode()}";
    }
}
