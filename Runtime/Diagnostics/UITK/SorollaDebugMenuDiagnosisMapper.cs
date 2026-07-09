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
    ///     Maps an existing <see cref="SorollaDiagnosticRow"/> (one free-text Detail string) into the
    ///     three-part WHY/SIGNAL/FIX shape the Issues tab renders. As of phase 2, every row uses the
    ///     fallback shape - the underlying data model has no structured WHY/SIGNAL/FIX fields yet, and
    ///     rewriting the diagnostic message strings themselves is explicitly out of scope (phase-5
    ///     content pass, spec section 8). Do not special-case individual message strings here.
    /// </summary>
    internal static class SorollaDebugMenuDiagnosisMapper
    {
        internal const string UnknownFix = "Unknown - tap \"Copy SDK state\" (Actions) and send it to Sorolla.";
        const string UnknownSignal = "—";

        internal static SorollaDebugMenuDiagnosis Map(SorollaDiagnosticRow row)
        {
            string why = string.IsNullOrEmpty(row.Detail) ? "No detail recorded." : row.Detail;
            return new SorollaDebugMenuDiagnosis(why, UnknownSignal, UnknownFix, false);
        }
    }
}
