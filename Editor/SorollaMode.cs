namespace Sorolla.Palette.Editor
{
    /// <summary>
    ///     SDK operating mode
    /// </summary>
    public enum SorollaMode
    {
        /// <summary>No mode selected yet (first run)</summary>
        None,
        /// <summary>Prototype: GA + Facebook for rapid UA testing</summary>
        Prototype,
        /// <summary>Full: GA + MAX + Adjust for production</summary>
        Full
    }
}
