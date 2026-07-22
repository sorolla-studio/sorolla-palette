namespace Sorolla.Palette.Editor.Greenlight
{
    /// <summary>
    ///     Display status of one greenlight row. Owned by the report model, not by the UI component that
    ///     draws it: the evaluator, the group-header worst-of merge, and the view filter all reason about
    ///     this vocabulary, so a rendering component is the wrong home for it (it was
    ///     <c>CheckRow.Status</c>, which made every non-UI consumer depend on a widget).
    /// </summary>
    internal enum RowStatus
    {
        Pass,
        Warn,
        Fail,
        /// <summary>Required evidence is missing or not yet gathered - pending, not failing.</summary>
        Wait,
        /// <summary>Neutral notice, not a pass/fail/pending - e.g. an optional vendor is absent, so the
        /// check deliberately did not run. Never renders as an affirmative green check.</summary>
        Info,
    }
}
