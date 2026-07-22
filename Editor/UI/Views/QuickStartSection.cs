using UnityEngine.UIElements;

namespace Sorolla.Palette.Editor.UI
{
    /// <summary>
    ///     The integration primer at the bottom of the window: how the SDK starts itself, and the three
    ///     API patterns a game actually calls. Static content - no config or check state - so it is built
    ///     once and never refreshed.
    ///     The prose and the code are deliberately separate. CodeSnippetBlock ships a Copy button, which
    ///     promises real pasteable syntax, so the auto-init/ATT facts stay a prose callout and only real,
    ///     minimal, source-verified one-liners go behind a Copy (each checked against Runtime/Palette.Level.cs,
    ///     Runtime/Palette.Economy.cs and Runtime/Palette.cs directly, not against docs or memory).
    /// </summary>
    static class QuickStartSection
    {
        internal static VisualElement Create()
        {
            var container = new VisualElement();

            container.Add(CalloutCard.Create(CalloutCard.Severity.Info, "Auto-Initialization",
                "The SDK auto-initializes when your game starts - no init call, no GameObject required. " +
                "iOS shows the ATT consent dialog automatically."));

            container.Add(SectionHeader.Create("Quick Start"));

            container.Add(CodeSnippetBlock.Create("Level Progression",
                "Palette.Level.Start(1);\nPalette.Level.Complete(1, score: 100);"));
            container.Add(CodeSnippetBlock.Create("Economy",
                "Palette.Economy.Earn(CurrencyId.Coins, 100, EconomySource.LevelReward);"));
            container.Add(CodeSnippetBlock.Create("Custom Event",
                "Palette.TrackEvent(\"tutorial_done\");"));

            return container;
        }
    }
}
