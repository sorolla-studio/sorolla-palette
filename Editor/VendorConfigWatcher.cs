using System;
using UnityEditor;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     Fires when a vendor configuration asset the report grades is written, so the window re-evaluates
    ///     instead of showing the state that existed when it opened. SorollaConfig is already tracked field by
    ///     field by the window; these are the OTHER inputs a studio edits while the window is open - the
    ///     GameAnalytics key pair, the MAX SDK key, the Facebook app id, and the two Firebase config files -
    ///     none of which the window could previously notice.
    /// </summary>
    class VendorConfigWatcher : AssetPostprocessor
    {
        /// <summary>Raised once per import batch that touched a watched file.</summary>
        internal static event Action Changed;

        static readonly string[] WatchedPaths =
        {
            "/SorollaConfig.asset",
            "/GameAnalytics/Settings.asset",
            "/AppLovinSettings.asset",
            "/FacebookSettings.asset",
            "/google-services.json",
            "/GoogleService-Info.plist",
        };

        static void OnPostprocessAllAssets(
            string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (Changed == null) return;
            if (Watched(imported) || Watched(deleted) || Watched(moved) || Watched(movedFrom))
                Changed();
        }

        static bool Watched(string[] paths)
        {
            foreach (string path in paths)
            foreach (string watched in WatchedPaths)
                if (path.EndsWith(watched, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
