using System.Collections.Generic;

namespace Sorolla.Palette
{
    internal sealed partial class SorollaDiagnosticsConsole
    {
        void RefreshDerivedState()
        {
            for (int i = 0; i < _severityCounts.Length; i++)
            {
                _severityCounts[i] = 0;
                _healthCounts[i] = 0;
            }
            _problemCount = 0;

            foreach (KeyValuePair<string, SectionSummary> item in _sectionSummaries)
                item.Value.Reset();

            foreach (SorollaDiagnosticRow row in _rows)
            {
                _severityCounts[SeverityIndex(row.Severity)]++;
                if (SorollaDiagnostics.NeedsAttention(row.Severity))
                    _problemCount++;
                if (SorollaDiagnostics.DrivesHealth(row))
                    _healthCounts[SeverityIndex(row.Severity)]++;
                GetSectionSummary(row.Group).Add(row.Severity);
            }

            foreach (KeyValuePair<string, SectionSummary> item in _sectionSummaries)
            {
                if (!item.Value.Active) continue;
                item.Value.RebuildCountsText();

                SectionState state = GetSectionState(item.Key);
                bool hasProblems = item.Value.HasProblems;
                if (!state.Initialized)
                {
                    state.Expanded = hasProblems;
                    state.HadProblem = hasProblems;
                    state.Initialized = true;
                }
                else if (hasProblems && !state.HadProblem && !state.UserToggled)
                {
                    state.Expanded = true;
                }

                state.HadProblem = hasProblems;
            }

            if (!_filterInitialized)
            {
                _filter = SorollaDiagnostics.NeedsAttention(OverallSeverity()) ? RowFilter.Problems : RowFilter.All;
                _filterInitialized = true;
            }

            if (!_activeTabInitialized)
            {
                _activeTab = ConsoleTab.Vitals;
                _activeTabInitialized = true;
            }

            PruneExpandedConsoleRows();
            PruneExpandedRuntimeProblems();
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
                case RowFilter.Problems:
                    return SorollaDiagnostics.NeedsAttention(row.Severity);
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
            return severity == SorollaDiagnosticSeverity.Fail ? "Required FAIL" :
                severity == SorollaDiagnosticSeverity.Warning ? "Required WARN" :
                severity == SorollaDiagnosticSeverity.Waiting ? "Required WAIT" : "Required PASS";
        }

        SorollaDiagnosticSeverity OverallSeverity()
        {
            if (_healthCounts[SeverityIndex(SorollaDiagnosticSeverity.Fail)] > 0)
                return SorollaDiagnosticSeverity.Fail;
            if (_healthCounts[SeverityIndex(SorollaDiagnosticSeverity.Warning)] > 0)
                return SorollaDiagnosticSeverity.Warning;
            if (_healthCounts[SeverityIndex(SorollaDiagnosticSeverity.Waiting)] > 0)
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

        void PruneExpandedRuntimeProblems()
        {
            if (_expandedRuntimeProblems.Count == 0) return;

            _staleExpandedConsoleRows.Clear();
            foreach (int problemId in _expandedRuntimeProblems)
            {
                if (!ContainsRuntimeProblem(problemId))
                    _staleExpandedConsoleRows.Add(problemId);
            }

            for (int i = 0; i < _staleExpandedConsoleRows.Count; i++)
                _expandedRuntimeProblems.Remove(_staleExpandedConsoleRows[i]);
        }

        bool ContainsRuntimeProblem(int problemId)
        {
            for (int i = 0; i < _runtimeProblems.Count; i++)
            {
                if (_runtimeProblems[i].Id == problemId)
                    return true;
            }

            return false;
        }
    }
}
