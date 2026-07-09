namespace Sorolla.Palette
{
    /// <summary>Display-only WHY/SIGNAL/FIX split of a diagnostic row (spec section 3).</summary>
    internal readonly struct SorollaDebugMenuDiagnosis
    {
        public readonly string Why;
        public readonly string Signal;
        public readonly string Fix;

        /// <summary>
        ///     True only when WHY/SIGNAL/FIX were derived from real structured content. False means the
        ///     fallback shape below was used (existing rows carry one free-text Detail string, not a
        ///     structured split - that split is the phase-5 content pass, spec sections 8/10).
        /// </summary>
        public readonly bool IsStructured;

        public SorollaDebugMenuDiagnosis(string why, string signal, string fix, bool isStructured)
        {
            Why = why;
            Signal = signal;
            Fix = fix;
            IsStructured = isStructured;
        }
    }

    /// <summary>
    ///     Maps a <see cref="SorollaDiagnosticRow"/> into the three-part WHY/SIGNAL/FIX shape the
    ///     Issues/Overview tabs render. Phase 5 (content pass): prefers the row's own structured
    ///     Why/Signal/Fix when the row-producing site populated them (see SorollaDiagnostics.
    ///     Diagnoses.cs); falls back to the free-text Detail in the old single-line shape for every
    ///     row class that isn't wired yet. Do not special-case individual message strings here - new
    ///     diagnoses get added at the row-producing site, not in this mapper.
    /// </summary>
    internal static class SorollaDebugMenuDiagnosisMapper
    {
        internal const string UnknownFix = "Unknown - tap \"Copy SDK state\" (Actions) and send it to Sorolla.";
        const string UnknownSignal = "—";

        internal static SorollaDebugMenuDiagnosis Map(SorollaDiagnosticRow row)
        {
            if (row.HasStructuredDiagnosis)
                return new SorollaDebugMenuDiagnosis(row.Why, row.Signal, row.Fix, true);

            string why = string.IsNullOrEmpty(row.Detail) ? "No detail recorded." : row.Detail;
            return new SorollaDebugMenuDiagnosis(why, UnknownSignal, UnknownFix, false);
        }
    }
}
