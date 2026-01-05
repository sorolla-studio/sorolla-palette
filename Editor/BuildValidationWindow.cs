using UnityEditor;

namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     Redirects to main SorollaWindow which includes Build Health section.
    /// </summary>
    public static class BuildValidationMenu
    {
        [MenuItem("Palette/Tools/Validate Build")]
        public static void ShowWindow()
        {
            SorollaWindow.ShowWindow();
        }
    }
}
