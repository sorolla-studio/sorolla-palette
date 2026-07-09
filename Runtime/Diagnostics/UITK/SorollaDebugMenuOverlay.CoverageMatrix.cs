using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Sorolla.Palette
{
    // Coverage matrix card (Overview tab, spec section 11 item 3): the session-exercise checklist,
    // rendered from SorollaDiagnostics.BuildCoverageMatrixRows() - itself derived ONLY from snapshot
    // facts (never gates.yaml). Never claims a gate "passed"; every row is an exercised/not-exercised
    // fact, and not-exercised rows carry a how-to-trigger hint (WAIT/neutral treatment, per section 8
    // honesty rules - this card must never read as a green checklist for coverage it didn't observe).
    internal sealed partial class SorollaDebugMenuOverlay
    {
        VisualElement BuildCoverageMatrixCard()
        {
            var card = new VisualElement();
            card.AddToClassList("sorolla-debugmenu-matrix-card");

            var title = new Label("SESSION COVERAGE");
            title.AddToClassList("sorolla-debugmenu-matrix-title");
            card.Add(title);

            foreach (SorollaMenuMatrixRow row in SorollaDiagnostics.BuildCoverageMatrixRows())
                card.Add(BuildMatrixRow(row));

            return card;
        }

        static VisualElement BuildMatrixRow(SorollaMenuMatrixRow row)
        {
            var line = new VisualElement();
            line.AddToClassList("sorolla-debugmenu-matrix-row");

            bool neutral = row.IsManualReminder || !row.Exercised;

            var badge = new Label(row.IsManualReminder ? "CHECK" : row.Exercised ? "DONE" : "WAIT");
            badge.AddToClassList("sorolla-debugmenu-severity-badge");
            badge.AddToClassList(neutral ? "sorolla-debugmenu-badge-wait" : "sorolla-debugmenu-badge-pass");
            line.Add(badge);

            var textColumn = new VisualElement();
            textColumn.AddToClassList("sorolla-debugmenu-matrix-row-text");

            var name = new Label(row.Name);
            name.AddToClassList("sorolla-debugmenu-matrix-row-name");
            textColumn.Add(name);

            // Exercised: show the cell fact. Not exercised / manual: show the how-to-trigger hint -
            // turning a not-exercised row into the tester's next action instead of a bare status word.
            var detail = new Label(row.Exercised && !row.IsManualReminder ? row.Cell : row.Hint);
            detail.AddToClassList("sorolla-debugmenu-matrix-row-detail");
            textColumn.Add(detail);

            line.Add(textColumn);
            return line;
        }
    }
}
