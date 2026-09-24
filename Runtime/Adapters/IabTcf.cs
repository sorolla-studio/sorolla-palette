using System;
using UnityEngine;

namespace Sorolla.Palette.Adapters
{
    /// <summary>
    ///     The IAB TCF v2 record a CMP (Google UMP) keeps on the device. iOS CMPs and the Editor store it
    ///     in standard user defaults, which Unity maps to PlayerPrefs. Android stores it in the default
    ///     SharedPreferences (per the IAB spec), reachable only via JNI - Unity PlayerPrefs is a different
    ///     file there. Main thread only (PlayerPrefs throws elsewhere).
    /// </summary>
    internal static class IabTcf
    {
        /// <summary>
        ///     Returns false when the record could not be read. That is NOT "no TC string": an absent
        ///     string is positive evidence that nobody answered the CMP, an unreadable record is not.
        ///     <paramref name="gdprApplies"/>: 1 applies, 0 does not apply, -1 unset (the IAB spec writes
        ///     booleans as integers on mobile). Only the TC-string PRESENCE is exposed, never the string;
        ///     purpose bits are the consent decision a QA gate checks, not PII.
        /// </summary>
        internal static bool Read(out bool tcStringPresent, out int gdprApplies, out string purposeConsents)
        {
            tcStringPresent = false;
            gdprApplies = -1;
            purposeConsents = "";
            try
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                using var context = activity.Call<AndroidJavaObject>("getApplicationContext");
                using var prefsManager = new AndroidJavaClass("android.preference.PreferenceManager");
                using var prefs = prefsManager.CallStatic<AndroidJavaObject>("getDefaultSharedPreferences", context);
                tcStringPresent = !string.IsNullOrEmpty(prefs.Call<string>("getString", "IABTCF_TCString", null));
                purposeConsents = prefs.Call<string>("getString", "IABTCF_PurposeConsents", null) ?? "";
                gdprApplies = ReadGdprApplies(() => prefs.Call<int>("getInt", "IABTCF_gdprApplies", -1));
#else
                tcStringPresent = !string.IsNullOrEmpty(PlayerPrefs.GetString("IABTCF_TCString", ""));
                purposeConsents = PlayerPrefs.GetString("IABTCF_PurposeConsents", "");
                gdprApplies = ReadGdprApplies(() => PlayerPrefs.GetInt("IABTCF_gdprApplies", -1));
#endif
                return true;
            }
            catch (Exception e)
            {
                PaletteLog.Verbose($"[Palette] IABTCF read failed: {e.Message}");
                tcStringPresent = false;
                purposeConsents = "";
                return false;
            }
        }

        // A CMP that stores gdprApplies with a non-integer type makes the typed read throw. That only
        // loses the applicability hint (-1 = unset), not the TC-string evidence read above it.
        static int ReadGdprApplies(Func<int> read)
        {
            try
            {
                return read();
            }
            catch (Exception e)
            {
                PaletteLog.Verbose($"[Palette] IABTCF_gdprApplies read failed: {e.Message}");
                return -1;
            }
        }
    }
}
