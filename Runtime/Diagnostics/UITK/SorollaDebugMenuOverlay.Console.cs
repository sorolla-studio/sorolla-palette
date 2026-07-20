using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sorolla.Palette
{
    // The Console tab renders the diagnostics event ring and runtime-problem list directly. It keeps
    // the legacy ordering, filters, copy/clear controls, expansion details, and scrolling behavior.
    internal sealed partial class SorollaDebugMenuOverlay
    {
        enum ConsoleFilter
        {
            All,
            Problems,
            Events,
        }

        readonly struct ConsoleEntry
        {
            public readonly int Id;
            public readonly bool IsProblem;
            public readonly float TimeSeconds;
            public readonly string BadgeText;
            public readonly string BadgeClass;
            public readonly string Title;
            public readonly SorollaDiagnosticPayloadLine[] Payload;

            public ConsoleEntry(int id, bool isProblem, float timeSeconds, string badgeText, string badgeClass,
                string title, SorollaDiagnosticPayloadLine[] payload)
            {
                Id = id;
                IsProblem = isProblem;
                TimeSeconds = timeSeconds;
                BadgeText = badgeText;
                BadgeClass = badgeClass;
                Title = title;
                Payload = payload;
            }
        }

        bool _consoleNewestFirst = true;
        ConsoleFilter _consoleFilter;
        Label _consoleSummaryLabel;
        VisualElement _consoleListHost;
        readonly Button[] _consoleFilterButtons = new Button[3];
        readonly List<SorollaDiagnosticEventLogEntry> _consoleEvents = new List<SorollaDiagnosticEventLogEntry>(40);
        readonly List<SorollaRuntimeProblem> _consoleProblems = new List<SorollaRuntimeProblem>(20);
        readonly List<ConsoleEntry> _consoleEntries = new List<ConsoleEntry>(60);
        readonly HashSet<int> _expandedConsoleEvents = new HashSet<int>();
        readonly HashSet<int> _expandedConsoleProblems = new HashSet<int>();
        int _consoleContentVersion = int.MinValue;

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

            RefreshConsoleList(true);
            return pane;
        }

        VisualElement BuildConsoleToolbar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("sorolla-debugmenu-console-toolbar");

            _consoleSummaryLabel = new Label();
            _consoleSummaryLabel.AddToClassList("sorolla-debugmenu-console-summary");
            bar.Add(_consoleSummaryLabel);

            var controls = new VisualElement();
            controls.AddToClassList("sorolla-debugmenu-console-controls");

            var newest = new Toggle("Newest first") { value = _consoleNewestFirst };
            newest.AddToClassList("sorolla-debugmenu-console-toggle");
            newest.RegisterValueChangedCallback(evt =>
            {
                _consoleNewestFirst = evt.newValue;
                RefreshConsoleList(true);
            });
            controls.Add(newest);

            var copy = new Button(() => GUIUtility.systemCopyBuffer = SorollaDiagnostics.BuildConsoleSummary())
            {
                text = "Copy",
            };
            copy.AddToClassList("sorolla-debugmenu-console-toolbar-button");
            controls.Add(copy);

            var clear = new Button(ClearConsole) { text = "Clear" };
            clear.AddToClassList("sorolla-debugmenu-console-toolbar-button");
            controls.Add(clear);
            bar.Add(controls);

            AddConsoleFilterButton(bar, ConsoleFilter.All, "All");
            AddConsoleFilterButton(bar, ConsoleFilter.Problems, "Problems");
            AddConsoleFilterButton(bar, ConsoleFilter.Events, "Events");
            RefreshConsoleFilterButtons();

            return bar;
        }

        void AddConsoleFilterButton(VisualElement parent, ConsoleFilter filter, string label)
        {
            var button = new Button(() => SetConsoleFilter(filter)) { text = label };
            button.AddToClassList("sorolla-debugmenu-console-toolbar-button");
            _consoleFilterButtons[(int)filter] = button;
            parent.Add(button);
        }

        void SetConsoleFilter(ConsoleFilter filter)
        {
            if (_consoleFilter == filter) return;
            _consoleFilter = filter;
            RefreshConsoleFilterButtons();
            RefreshConsoleList(true);
        }

        void RefreshConsoleFilterButtons()
        {
            for (int i = 0; i < _consoleFilterButtons.Length; i++)
            {
                Button button = _consoleFilterButtons[i];
                if (button != null)
                    button.EnableInClassList("sorolla-debugmenu-tab-active", i == (int)_consoleFilter);
            }
        }

        void ClearConsole()
        {
            SorollaDiagnostics.ClearEventLog();
            SorollaDiagnostics.ClearRuntimeProblems();
            _expandedConsoleEvents.Clear();
            _expandedConsoleProblems.Clear();
            RefreshConsoleList(true);
        }

        void RefreshConsoleList(bool force = false)
        {
            if (_consoleListHost == null) return;

            SorollaDiagnostics.CopyEventLog(_consoleEvents);
            SorollaDiagnostics.CopyRuntimeProblems(_consoleProblems);

            int version = ComputeConsoleContentVersion();
            if (!force && version == _consoleContentVersion) return;
            _consoleContentVersion = version;

            _consoleSummaryLabel.text =
                $"{_consoleProblems.Count} {Pluralize("problem", _consoleProblems.Count)} · "
                + $"{_consoleEvents.Count} {Pluralize("event", _consoleEvents.Count)}";

            PruneExpandedRows();
            _consoleEntries.Clear();
            if (_consoleFilter != ConsoleFilter.Problems)
            {
                foreach (SorollaDiagnosticEventLogEntry entry in _consoleEvents)
                    _consoleEntries.Add(ToConsoleEntry(entry));
            }
            if (_consoleFilter != ConsoleFilter.Events)
            {
                foreach (SorollaRuntimeProblem problem in _consoleProblems)
                    _consoleEntries.Add(ToConsoleEntry(problem));
            }

            _consoleEntries.Sort((a, b) => a.TimeSeconds.CompareTo(b.TimeSeconds));
            if (_consoleNewestFirst)
                _consoleEntries.Reverse();

            _consoleListHost.Clear();
            if (_consoleEntries.Count == 0)
            {
                var empty = new Label(ConsoleEmptyMessage());
                empty.AddToClassList("sorolla-debugmenu-placeholder");
                _consoleListHost.Add(empty);
                return;
            }

            foreach (ConsoleEntry entry in _consoleEntries)
                _consoleListHost.Add(BuildConsoleRow(entry));
        }

        int ComputeConsoleContentVersion()
        {
            unchecked
            {
                int version = 17;
                foreach (SorollaDiagnosticEventLogEntry entry in _consoleEvents)
                    version = version * 31 + entry.Id;
                foreach (SorollaRuntimeProblem problem in _consoleProblems)
                {
                    version = version * 31 + problem.Id;
                    version = version * 31 + problem.Count;
                }
                return version;
            }
        }

        void PruneExpandedRows()
        {
            _expandedConsoleEvents.RemoveWhere(id => !ContainsEvent(id));
            _expandedConsoleProblems.RemoveWhere(id => !ContainsProblem(id));
        }

        bool ContainsEvent(int id)
        {
            for (int i = 0; i < _consoleEvents.Count; i++)
            {
                if (_consoleEvents[i].Id == id) return true;
            }
            return false;
        }

        bool ContainsProblem(int id)
        {
            for (int i = 0; i < _consoleProblems.Count; i++)
            {
                if (_consoleProblems[i].Id == id) return true;
            }
            return false;
        }

        string ConsoleEmptyMessage()
        {
            switch (_consoleFilter)
            {
                case ConsoleFilter.Problems: return "No runtime problems observed yet.";
                case ConsoleFilter.Events: return "No SDK events observed yet.";
                default: return "No SDK events or runtime problems observed yet.";
            }
        }

        static ConsoleEntry ToConsoleEntry(SorollaDiagnosticEventLogEntry entry)
        {
            (string badgeText, string badgeClass) = BadgeForEventSource(entry.Source);
            return new ConsoleEntry(
                entry.Id,
                false,
                entry.TimeSeconds,
                badgeText,
                badgeClass,
                $"{SorollaDiagnostics.FormatEventTime(entry.TimeSeconds)}  {entry.Name}",
                entry.PayloadLines);
        }

        static ConsoleEntry ToConsoleEntry(SorollaRuntimeProblem problem)
        {
            var payload = new[]
            {
                new SorollaDiagnosticPayloadLine("source", problem.Source),
                new SorollaDiagnosticPayloadLine("severity", SorollaDiagnostics.SeverityLabel(problem.Severity)),
                new SorollaDiagnosticPayloadLine("message", problem.Message),
                new SorollaDiagnosticPayloadLine("top frame", problem.TopFrame),
                new SorollaDiagnosticPayloadLine("first seen", SorollaDiagnostics.FormatEventTime(problem.FirstTimeSeconds)),
                new SorollaDiagnosticPayloadLine("last seen", SorollaDiagnostics.FormatEventTime(problem.LastTimeSeconds)),
                new SorollaDiagnosticPayloadLine("stack", problem.StackTrace),
            };
            return new ConsoleEntry(
                problem.Id,
                true,
                problem.LastTimeSeconds,
                SorollaDiagnostics.SeverityLabel(problem.Severity),
                BadgeSeverityClass(problem.Severity),
                $"{SorollaDiagnostics.FormatEventTime(problem.LastTimeSeconds)}  {problem.Type} x{problem.Count}",
                payload);
        }

        static (string text, string cssClass) BadgeForEventSource(string source)
        {
            string label = string.IsNullOrEmpty(source) ? "EVENT" : source.ToUpperInvariant();
            switch (source)
            {
                case "level": return (label, "sorolla-debugmenu-badge-wait");
                case "economy": return (label, "sorolla-debugmenu-badge-pass");
                case "ads": return (label, "sorolla-debugmenu-badge-warn");
                default: return (label, "sorolla-debugmenu-badge-info");
            }
        }

        VisualElement BuildConsoleRow(ConsoleEntry entry)
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

            bool isExpanded = IsConsoleEntryExpanded(entry);
            var chevron = new Label(isExpanded ? "⌄" : "›");
            chevron.AddToClassList("sorolla-debugmenu-issue-chevron");
            collapsed.Add(chevron);
            row.Add(collapsed);

            VisualElement expanded = BuildConsolePayload(entry);
            expanded.style.display = isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            row.Add(expanded);

            collapsed.RegisterCallback<ClickEvent>(_ =>
            {
                bool nowExpanded = expanded.style.display == DisplayStyle.None;
                expanded.style.display = nowExpanded ? DisplayStyle.Flex : DisplayStyle.None;
                chevron.text = nowExpanded ? "⌄" : "›";
                SetConsoleEntryExpanded(entry, nowExpanded);
            });

            return row;
        }

        static VisualElement BuildConsolePayload(ConsoleEntry entry)
        {
            var expanded = new VisualElement();
            expanded.AddToClassList("sorolla-debugmenu-console-payload");

            if (entry.Payload.Length == 0)
            {
                var empty = new Label("No payload");
                empty.AddToClassList("sorolla-debugmenu-placeholder");
                expanded.Add(empty);
            }
            else
            {
                foreach (SorollaDiagnosticPayloadLine payload in entry.Payload)
                {
                    var line = new VisualElement();
                    line.AddToClassList("sorolla-debugmenu-console-payload-line");

                    var key = new Label(payload.Key);
                    key.AddToClassList("sorolla-debugmenu-console-payload-key");
                    line.Add(key);

                    var value = new Label(string.IsNullOrEmpty(payload.Value) ? "None" : payload.Value);
                    value.AddToClassList("sorolla-debugmenu-console-payload-value");
                    line.Add(value);
                    expanded.Add(line);
                }
            }

            var copy = new Button(() => GUIUtility.systemCopyBuffer = BuildConsoleRowCopyText(entry))
            {
                text = "Copy",
            };
            copy.AddToClassList("sorolla-debugmenu-action-button");
            copy.AddToClassList("sorolla-debugmenu-action-button-ghost");
            expanded.Add(copy);
            return expanded;
        }

        bool IsConsoleEntryExpanded(ConsoleEntry entry) =>
            entry.IsProblem
                ? _expandedConsoleProblems.Contains(entry.Id)
                : _expandedConsoleEvents.Contains(entry.Id);

        void SetConsoleEntryExpanded(ConsoleEntry entry, bool expanded)
        {
            HashSet<int> set = entry.IsProblem ? _expandedConsoleProblems : _expandedConsoleEvents;
            if (expanded) set.Add(entry.Id);
            else set.Remove(entry.Id);
        }

        static string BuildConsoleRowCopyText(ConsoleEntry entry)
        {
            var sb = new StringBuilder(256);
            sb.AppendLine(entry.Title);
            foreach (SorollaDiagnosticPayloadLine line in entry.Payload)
                sb.Append(line.Key).Append(": ").AppendLine(line.Value);
            return sb.ToString();
        }

        static string Pluralize(string noun, int count) => count == 1 ? noun : noun + "s";
    }
}
