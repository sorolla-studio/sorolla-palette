using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sorolla.Palette
{
    // Console tab (mockup 04, spec 2.4): ports the event/problem stream from the SAME data source
    // the IMGUI console's Console tab reads (SorollaDiagnostics event log + runtime problems) - no
    // second log, no new fact pipeline. Copy/Clear reuse the existing plumbing
    // (SorollaDiagnostics.BuildConsoleSummary/ClearEventLog/ClearRuntimeProblems), matching the IMGUI
    // Console toolbar 1:1 so both surfaces stay in sync.
    //
    // Judgment call (stated for the report): a merged badge-by-kind stream (mockup 04) is built by
    // wrapping both source lists in one lightweight display record and sorting by time, rather than
    // keeping the IMGUI console's two-section "Runtime problems" / "SDK events" layout - the mockup
    // explicitly shows one interleaved stacked list with a per-row kind badge (DROPPED/ECONOMY/LEVEL/
    // CUSTOM), and spec 2.4 says "existing console behavior, kept" for the DATA (newest-first, Copy,
    // Clear), not the two-section layout, which was never part of the approved visual.
    internal sealed partial class SorollaDebugMenuOverlay
    {
        readonly struct ConsoleEntry
        {
            public readonly float TimeSeconds;
            public readonly string BadgeText;
            public readonly string BadgeClass;
            public readonly string Title;
            public readonly List<SorollaDiagnosticPayloadLine> Payload;

            public ConsoleEntry(float timeSeconds, string badgeText, string badgeClass, string title,
                List<SorollaDiagnosticPayloadLine> payload)
            {
                TimeSeconds = timeSeconds;
                BadgeText = badgeText;
                BadgeClass = badgeClass;
                Title = title;
                Payload = payload;
            }
        }

        bool _consoleNewestFirst = true;
        VisualElement _consoleListHost;
        readonly List<SorollaDiagnosticEventLogEntry> _consoleEvents = new List<SorollaDiagnosticEventLogEntry>(40);
        readonly List<SorollaRuntimeProblem> _consoleProblems = new List<SorollaRuntimeProblem>(20);

        internal VisualElement BuildConsoleTab()
        {
            var pane = new VisualElement();
            pane.AddToClassList("sorolla-debugmenu-console-pane");

            pane.Add(BuildConsoleToolbar());

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("sorolla-debugmenu-issues-scroll");
            _consoleListHost = new VisualElement();
            scroll.Add(_consoleListHost);
            pane.Add(scroll);

            RefreshConsoleList();

            return pane;
        }

        VisualElement BuildConsoleToolbar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("sorolla-debugmenu-console-toolbar");

            var summary = new Label();
            summary.AddToClassList("sorolla-debugmenu-console-summary");
            bar.Add(summary);
            _consoleSummaryLabel = summary;

            var controls = new VisualElement();
            controls.AddToClassList("sorolla-debugmenu-console-controls");

            var newest = new Toggle("Newest first") { value = _consoleNewestFirst };
            newest.AddToClassList("sorolla-debugmenu-console-toggle");
            newest.RegisterValueChangedCallback(evt =>
            {
                _consoleNewestFirst = evt.newValue;
                RefreshConsoleList();
            });
            controls.Add(newest);

            var copy = new Button(() => GUIUtility.systemCopyBuffer = SorollaDiagnostics.BuildConsoleSummary())
            {
                text = "Copy",
            };
            copy.AddToClassList("sorolla-debugmenu-console-toolbar-button");
            controls.Add(copy);

            var clear = new Button(() =>
            {
                SorollaDiagnostics.ClearEventLog();
                SorollaDiagnostics.ClearRuntimeProblems();
                RefreshConsoleList();
                RefreshTabBadgeCounts();
            })
            {
                text = "Clear",
            };
            clear.AddToClassList("sorolla-debugmenu-console-toolbar-button");
            controls.Add(clear);

            bar.Add(controls);
            return bar;
        }

        Label _consoleSummaryLabel;

        void RefreshConsoleList()
        {
            SorollaDiagnostics.CopyEventLog(_consoleEvents);
            SorollaDiagnostics.CopyRuntimeProblems(_consoleProblems);

            if (_consoleSummaryLabel != null)
                _consoleSummaryLabel.text = $"{_consoleProblems.Count} problems · {_consoleEvents.Count} events";

            var entries = new List<ConsoleEntry>(_consoleEvents.Count + _consoleProblems.Count);
            foreach (SorollaDiagnosticEventLogEntry e in _consoleEvents)
                entries.Add(ToConsoleEntry(e));
            foreach (SorollaRuntimeProblem p in _consoleProblems)
                entries.Add(ToConsoleEntry(p));

            entries.Sort((a, b) => a.TimeSeconds.CompareTo(b.TimeSeconds));
            if (_consoleNewestFirst)
                entries.Reverse();

            _consoleListHost.Clear();

            if (entries.Count == 0)
            {
                var empty = new Label("No SDK events or runtime problems observed yet.");
                empty.AddToClassList("sorolla-debugmenu-placeholder");
                _consoleListHost.Add(empty);
                return;
            }

            foreach (ConsoleEntry entry in entries)
                _consoleListHost.Add(BuildConsoleRow(entry));
        }

        static ConsoleEntry ToConsoleEntry(SorollaDiagnosticEventLogEntry e)
        {
            (string badgeText, string badgeClass) = BadgeForEventSource(e.Source);
            return new ConsoleEntry(e.TimeSeconds, badgeText, badgeClass,
                $"{SorollaDiagnostics.FormatEventTime(e.TimeSeconds)}  {e.Name}",
                new List<SorollaDiagnosticPayloadLine>(e.PayloadLines));
        }

        static ConsoleEntry ToConsoleEntry(SorollaRuntimeProblem p)
        {
            // DROPPED rows cross-reference Issues instead of re-explaining (design-tokens.md copy
            // rule) - the full WHY/SIGNAL/FIX for the underlying fact already lives there.
            var payload = new List<SorollaDiagnosticPayloadLine>(4)
            {
                new SorollaDiagnosticPayloadLine("source", p.Source),
                new SorollaDiagnosticPayloadLine("count", p.Count.ToString()),
                new SorollaDiagnosticPayloadLine("message", p.Message),
            };
            if (!string.IsNullOrEmpty(p.TopFrame))
                payload.Add(new SorollaDiagnosticPayloadLine("top frame", p.TopFrame));

            return new ConsoleEntry(p.LastTimeSeconds, "DROPPED", "sorolla-debugmenu-badge-fail",
                $"{SorollaDiagnostics.FormatEventTime(p.LastTimeSeconds)}  {p.Type} (see Issues) x{p.Count}", payload);
        }

        static (string text, string cssClass) BadgeForEventSource(string source)
        {
            switch (source)
            {
                case "level": return ("LEVEL", "sorolla-debugmenu-badge-wait");
                case "economy": return ("ECONOMY", "sorolla-debugmenu-badge-pass");
                default: return ("CUSTOM", "sorolla-debugmenu-badge-info");
            }
        }

        static VisualElement BuildConsoleRow(ConsoleEntry entry)
        {
            var row = new VisualElement();
            row.AddToClassList("sorolla-debugmenu-console-row");

            var collapsed = new VisualElement();
            collapsed.AddToClassList("sorolla-debugmenu-console-row-collapsed");

            var badge = new Label(entry.BadgeText);
            badge.AddToClassList("sorolla-debugmenu-severity-badge");
            badge.AddToClassList(entry.BadgeClass);
            collapsed.Add(badge);

            var title = new Label(entry.Title);
            title.AddToClassList("sorolla-debugmenu-console-row-title");
            collapsed.Add(title);

            var chevron = new Label("›");
            chevron.AddToClassList("sorolla-debugmenu-issue-chevron");
            collapsed.Add(chevron);

            row.Add(collapsed);

            var expanded = new VisualElement();
            expanded.AddToClassList("sorolla-debugmenu-console-payload");
            expanded.style.display = DisplayStyle.None;
            foreach (SorollaDiagnosticPayloadLine line in entry.Payload)
            {
                var lineEl = new VisualElement();
                lineEl.AddToClassList("sorolla-debugmenu-console-payload-line");

                var key = new Label(line.Key);
                key.AddToClassList("sorolla-debugmenu-console-payload-key");
                lineEl.Add(key);

                var value = new Label(line.Value);
                value.AddToClassList("sorolla-debugmenu-console-payload-value");
                lineEl.Add(value);

                expanded.Add(lineEl);
            }
            row.Add(expanded);

            collapsed.RegisterCallback<ClickEvent>(_ =>
            {
                bool nowExpanded = expanded.style.display == DisplayStyle.None;
                expanded.style.display = nowExpanded ? DisplayStyle.Flex : DisplayStyle.None;
                chevron.text = nowExpanded ? "⌄" : "›";
            });

            return row;
        }
    }
}
