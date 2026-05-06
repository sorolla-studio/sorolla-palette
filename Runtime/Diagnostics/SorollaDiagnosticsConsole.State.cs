using System.Collections.Generic;

namespace Sorolla.Palette
{
    internal sealed partial class SorollaDiagnosticsConsole
    {
        void RefreshDerivedState()
        {
            for (int i = 0; i < _severityCounts.Length; i++)
                _severityCounts[i] = 0;
            _issueCount = 0;

            foreach (KeyValuePair<string, SectionSummary> item in _sectionSummaries)
                item.Value.Reset();

            foreach (SorollaDiagnosticRow row in _rows)
            {
                _severityCounts[SeverityIndex(row.Severity)]++;
                if (SorollaDiagnostics.IsIssueSeverity(row.Severity))
                    _issueCount++;
                GetSectionSummary(row.Group).Add(row.Severity);
            }

            foreach (KeyValuePair<string, SectionSummary> item in _sectionSummaries)
            {
                if (!item.Value.Active) continue;
                item.Value.RebuildCountsText();

                SectionState state = GetSectionState(item.Key);
                bool hasIssues = item.Value.HasIssues;
                if (!state.Initialized)
                {
                    state.Expanded = hasIssues;
                    state.HadIssue = hasIssues;
                    state.Initialized = true;
                }
                else if (hasIssues && !state.HadIssue && !state.UserToggled)
                {
                    state.Expanded = true;
                }

                state.HadIssue = hasIssues;
            }

            if (!_filterInitialized)
            {
                _filter = SorollaDiagnostics.IsIssueSeverity(OverallSeverity()) ? RowFilter.Issues : RowFilter.All;
                _filterInitialized = true;
            }

            if (!_activeTabInitialized)
            {
                _activeTab = _issueCount > 0 ? ConsoleTab.Issues : ConsoleTab.Overview;
                _activeTabInitialized = true;
            }

            PruneExpandedConsoleRows();
        }

        SectionState GetSectionState(string group)
        {
            if (!_sectionStates.TryGetValue(group, out SectionState state))
            {
                state = new SectionState();
                _sectionStates[group] = state;
            }

            return state;
        }

        SectionSummary GetSectionSummary(string group)
        {
            if (!_sectionSummaries.TryGetValue(group, out SectionSummary summary))
            {
                summary = new SectionSummary();
                _sectionSummaries[group] = summary;
            }

            return summary;
        }

        bool SectionHasVisibleRows(string group)
        {
            return _sectionSummaries.TryGetValue(group, out SectionSummary summary)
                && summary.HasRowsFor(_filter);
        }

        bool MatchesFilter(SorollaDiagnosticRow row)
        {
            switch (_filter)
            {
                case RowFilter.Issues:
                    return SorollaDiagnostics.IsIssueSeverity(row.Severity);
                case RowFilter.Fail:
                    return row.Severity == SorollaDiagnosticSeverity.Fail;
                case RowFilter.Warn:
                    return row.Severity == SorollaDiagnosticSeverity.Warning;
                case RowFilter.Wait:
                    return row.Severity == SorollaDiagnosticSeverity.Waiting;
                case RowFilter.Pass:
                    return row.Severity == SorollaDiagnosticSeverity.Pass;
                default:
                    return true;
            }
        }

        string SectionCountsText(string group)
        {
            return _sectionSummaries.TryGetValue(group, out SectionSummary summary)
                ? summary.CountsText
                : string.Empty;
        }

        static void AppendCount(ref string text, int count, string label)
        {
            if (count == 0) return;
            if (!string.IsNullOrEmpty(text)) text += ", ";
            text += count + " " + label;
        }

        string OverallLabel()
        {
            SorollaDiagnosticSeverity severity = OverallSeverity();
            return severity == SorollaDiagnosticSeverity.Fail ? "Overall FAIL" :
                severity == SorollaDiagnosticSeverity.Warning ? "Overall WARN" :
                severity == SorollaDiagnosticSeverity.Waiting ? "Overall WAIT" : "Overall PASS";
        }

        SorollaDiagnosticSeverity OverallSeverity()
        {
            if (_severityCounts[SeverityIndex(SorollaDiagnosticSeverity.Fail)] > 0)
                return SorollaDiagnosticSeverity.Fail;
            if (_severityCounts[SeverityIndex(SorollaDiagnosticSeverity.Warning)] > 0)
                return SorollaDiagnosticSeverity.Warning;
            if (_severityCounts[SeverityIndex(SorollaDiagnosticSeverity.Waiting)] > 0)
                return SorollaDiagnosticSeverity.Waiting;
            return SorollaDiagnosticSeverity.Pass;
        }

        static int SeverityIndex(SorollaDiagnosticSeverity severity)
        {
            switch (severity)
            {
                case SorollaDiagnosticSeverity.Waiting:
                    return 1;
                case SorollaDiagnosticSeverity.Pass:
                    return 2;
                case SorollaDiagnosticSeverity.Warning:
                    return 3;
                case SorollaDiagnosticSeverity.Fail:
                    return 4;
                default:
                    return 0;
            }
        }

        void PruneExpandedConsoleRows()
        {
            if (_expandedConsoleRows.Count == 0) return;

            _staleExpandedConsoleRows.Clear();
            foreach (int eventId in _expandedConsoleRows)
            {
                if (!ContainsEvent(eventId))
                    _staleExpandedConsoleRows.Add(eventId);
            }

            for (int i = 0; i < _staleExpandedConsoleRows.Count; i++)
                _expandedConsoleRows.Remove(_staleExpandedConsoleRows[i]);
        }

        bool ContainsEvent(int eventId)
        {
            for (int i = 0; i < _events.Count; i++)
            {
                if (_events[i].Id == eventId)
                    return true;
            }

            return false;
        }
    }
}
