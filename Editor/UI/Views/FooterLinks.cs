using UnityEngine;
using UnityEngine.UIElements;

namespace Sorolla.Palette.Editor.UI
{
    /// <summary>The window's footer: documentation, source, and the issue tracker, as text-styled links.</summary>
    static class FooterLinks
    {
        const string RepoUrl = "https://github.com/sorolla-studio/sorolla-palette";
        internal const string IssuesUrl = RepoUrl + "/issues";

        internal static VisualElement Create()
        {
            var row = new VisualElement();
            row.AddToClassList("sorolla-footer-links");

            row.Add(Link("Documentation", RepoUrl + "#readme"));
            row.Add(Separator());
            row.Add(Link("GitHub", RepoUrl));
            row.Add(Separator());
            row.Add(Link("Report Issue", IssuesUrl));

            return row;
        }

        static Button Link(string label, string url)
        {
            var button = new Button(() => Application.OpenURL(url)) { text = label };
            button.AddToClassList("sorolla-footer-link");
            return button;
        }

        static Label Separator()
        {
            var label = new Label("|");
            label.AddToClassList("sorolla-footer-separator");
            return label;
        }
    }
}
