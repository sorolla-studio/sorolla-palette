using UnityEditor;
using UnityEngine.UIElements;

namespace Sorolla.Palette.Editor.UI
{
    /// <summary>Shared chrome for every section of the SDK window: the theme scope class plus the tokens
    /// stylesheet. One home, so a section can never be styled by accident of where it was created.</summary>
    static class SorollaTheme
    {
        const string TokensUssPath = "Packages/com.sorolla.sdk/Editor/UI/tokens.uss";

        internal static VisualElement CreateSectionContainer()
        {
            var container = new VisualElement();
            container.AddToClassList("sorolla-root");
            container.AddToClassList(EditorGUIUtility.isProSkin ? "sorolla-skin-dark" : "sorolla-skin-light");
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(TokensUssPath);
            if (styleSheet != null)
                container.styleSheets.Add(styleSheet);
            return container;
        }
    }
}
