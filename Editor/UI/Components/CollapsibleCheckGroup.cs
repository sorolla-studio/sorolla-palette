using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Sorolla.Palette.Editor.UI
{
    /// <summary>
    /// Foldout summarizing a group of CheckRows (e.g. "12 checks passing"). Built on UI Toolkit's
    /// built-in Foldout so collapse/expand is native, not hand-rolled.
    /// </summary>
    static class CollapsibleCheckGroup
    {
        internal static Foldout Create(string summary, IEnumerable<VisualElement> rows, bool startExpanded = false)
        {
            var foldout = new Foldout { text = summary, value = startExpanded };
            foldout.AddToClassList("sorolla-check-group");
            foreach (VisualElement row in rows)
                foldout.Add(row);
            return foldout;
        }
    }
}
