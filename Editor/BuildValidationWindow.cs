using UnityEditor;

namespace Sorolla.Editor
{
    /// <summary>
    ///     Redirects to main SorollaWindow which includes Build Health section.
    /// </summary>
    public static class BuildValidationMenu
    {
        [MenuItem("SorollaSDK/Tools/Validate Build")]
        public static void ShowWindow()
        {
            SorollaWindow.ShowWindow();
        }
    }
}
