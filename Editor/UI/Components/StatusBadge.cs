using UnityEngine.UIElements;

namespace Sorolla.Palette.Editor.UI
{
    /// <summary>
    /// Small colored pill labeling a status. BLOCKER/ADVISORY/FULL/PASS/FAIL/WAIT render as solid
    /// fills (confident, resolved states). GATED renders as a muted solid fill (blocked on
    /// something else finishing first). UNVERIFIABLE renders as an outline-only "ghost" pill with
    /// no fill - it must not look as confident as a real pass/fail, since the loop has no way to
    /// confirm it (see PLAN.md's "verified-column honesty" and the QA-bridge honesty rule in
    /// CLAUDE.md's known runtime landmines).
    /// </summary>
    static class StatusBadge
    {
        internal enum Severity
        {
            Blocker,
            Advisory,
            Full,
            Pass,
            Fail,
            Wait,
            Gated,
            Unverifiable,
        }

        internal static VisualElement Create(string text, Severity severity)
        {
            var pill = new VisualElement();
            pill.AddToClassList("sorolla-badge");
            pill.AddToClassList(ClassFor(severity));

            var label = new Label(text);
            label.AddToClassList("sorolla-badge-label");
            pill.Add(label);
            return pill;
        }

        static string ClassFor(Severity severity)
        {
            switch (severity)
            {
                case Severity.Blocker:
                case Severity.Fail:
                    return "sorolla-badge-fail";
                case Severity.Advisory:
                    return "sorolla-badge-warn";
                case Severity.Full:
                case Severity.Pass:
                    return "sorolla-badge-pass";
                case Severity.Wait:
                    return "sorolla-badge-wait";
                case Severity.Gated:
                    return "sorolla-badge-gated";
                case Severity.Unverifiable:
                    return "sorolla-badge-unverifiable";
                default:
                    return "sorolla-badge-gated";
            }
        }
    }
}
