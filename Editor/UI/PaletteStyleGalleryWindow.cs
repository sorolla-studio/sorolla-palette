using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sorolla.Palette.Editor.UI
{
    /// <summary>
    /// Dev-only isolation surface for the UI-overhaul loop: renders every planned editor
    /// component/state on one scrollable page so a component can be iterated and screenshotted
    /// without opening the real Palette window. Sections start as placeholders and get replaced
    /// by real components one phase-2 cycle at a time (see docs/ui-overhaul/PLAN.md).
    /// Ships inside the SDK package but is editor-only tooling, never referenced at runtime.
    /// </summary>
    sealed class PaletteStyleGalleryWindow : EditorWindow
    {
        const string TokensUssPath = "Packages/com.sorolla.sdk/Editor/UI/tokens.uss";

        static readonly (string Label, StatusBadge.Severity Severity)[] BadgeVariants =
        {
            ("BLOCKER", StatusBadge.Severity.Blocker),
            ("ADVISORY", StatusBadge.Severity.Advisory),
            ("FULL", StatusBadge.Severity.Full),
            ("PASS", StatusBadge.Severity.Pass),
            ("FAIL", StatusBadge.Severity.Fail),
            ("WAIT", StatusBadge.Severity.Wait),
            ("GATED", StatusBadge.Severity.Gated),
            ("UNVERIFIABLE", StatusBadge.Severity.Unverifiable),
        };

        [MenuItem("Palette/UI Lab/Style Gallery")]
        static void Open() => GetWindow<PaletteStyleGalleryWindow>("Palette Style Gallery");

        static readonly (string Label, string BgClass)[] BgSwatches =
        {
            ("bg-page", "sorolla-bg-page"),
            ("bg-card", "sorolla-bg-card"),
            ("bg-card-alt", "sorolla-bg-card-alt"),
            ("bg-section", "sorolla-bg-section"),
            ("bg-elevated", "sorolla-bg-elevated"),
            ("bg-elevated-hover", "sorolla-bg-elevated-hover"),
            ("bg-accent-muted", "sorolla-bg-accent-muted"),
            ("bg-accent", "sorolla-bg-accent"),
            ("fail-tint", "sorolla-status-fail-tint-bg"),
            ("warn-tint", "sorolla-status-warn-tint-bg"),
        };

        static readonly (string Label, string PillClass)[] StatusPills =
        {
            ("PASS", "sorolla-pill-pass"),
            ("WARN", "sorolla-pill-warn"),
            ("FAIL", "sorolla-pill-fail"),
            ("WAIT", "sorolla-pill-wait"),
            ("INFO", "sorolla-pill-info"),
        };

        static readonly (string Text, string TypeClass)[] TypeScale =
        {
            ("Title / editor-type-title 18px", "sorolla-type-title"),
            ("Section / editor-type-section 13px", "sorolla-type-section"),
            ("Body / editor-type-body 12px", "sorolla-type-body"),
            ("Small / editor-type-small 11px", "sorolla-type-small"),
        };

        Button _skinToggleButton;
        ScrollView _content;
        bool _forcedLight;

        void CreateGUI()
        {
            rootVisualElement.AddToClassList("sorolla-root");
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(TokensUssPath);
            if (styleSheet != null)
                rootVisualElement.styleSheets.Add(styleSheet);

            _forcedLight = !EditorGUIUtility.isProSkin;
            _skinToggleButton = BuildSkinToggle();
            rootVisualElement.Add(_skinToggleButton);

            _content = new ScrollView(ScrollViewMode.Vertical);
            _content.style.flexGrow = 1;
            rootVisualElement.Add(_content);

            RebuildForSkin();
        }

        /// <summary>Debug-only forced-skin toggle (item 11, Arthur's revised theme ruling) so the
        /// light-mode token ramp can be visually checked WITHOUT
        /// InternalEditorUtility.SwitchSkinAndRepaintAllViews (that call re-skins the whole editor
        /// via a global pref and previously timed out Coplay at 60s - see LOOP.md amendments); this
        /// button only touches this one dev-only window. Real bug found and fixed while building
        /// this: toggling rootVisualElement's sorolla-skin-* class on an ALREADY-BUILT live tree
        /// does not make UI Toolkit re-resolve the custom properties on existing children (verified:
        /// resolvedStyle.backgroundColor stayed at the old value after the class list changed,
        /// confirmed by class + repaint) - a fresh window built with the class already present
        /// resolves correctly. Fix: RebuildForSkin() below fully clears and rebuilds the content
        /// section's children AFTER changing the class, so every child is constructed fresh under
        /// the new class - the same path that works for a brand-new window.</summary>
        Button BuildSkinToggle()
        {
            var button = new Button { text = _forcedLight ? "Force Dark Mode" : "Force Light Mode" };
            button.clicked += () =>
            {
                _forcedLight = !_forcedLight;
                RebuildForSkin();
            };
            return button;
        }

        void RebuildForSkin()
        {
            rootVisualElement.RemoveFromClassList("sorolla-skin-dark");
            rootVisualElement.RemoveFromClassList("sorolla-skin-light");
            rootVisualElement.AddToClassList(_forcedLight ? "sorolla-skin-light" : "sorolla-skin-dark");
            _skinToggleButton.text = _forcedLight ? "Force Dark Mode" : "Force Light Mode";

            _content.Clear();
            _content.Add(BuildTokenSwatchSection());
            _content.Add(BuildStatusBadgeSection());
            _content.Add(BuildCalloutCardSection());
            _content.Add(BuildSectionHeaderSection());
            _content.Add(BuildCheckRowSection());
            _content.Add(BuildValidatedFieldSection());
            _content.Add(BuildCodeSnippetSection());
            _content.Add(BuildHeroHeaderSection());
        }

        static VisualElement BuildHeroHeaderSection()
        {
            var container = new VisualElement();
            container.AddToClassList("gallery-section");

            var title = new Label("HeroHeader");
            title.AddToClassList("gallery-section-title");
            container.Add(title);

            container.Add(HeroHeader.Create("Palette SDK", "v3.18.2 - Plug & Play Publisher Stack", modeIsFull: true, onSwitchRequested: () => { }));
            container.Add(HeroHeader.Create("Palette SDK", "v3.18.2 - Plug & Play Publisher Stack", modeIsFull: false, onSwitchRequested: () => { }));

            return container;
        }

        static VisualElement BuildCodeSnippetSection()
        {
            var container = new VisualElement();
            container.AddToClassList("gallery-section");

            var title = new Label("CodeSnippetBlock");
            title.AddToClassList("gallery-section-title");
            container.Add(title);

            container.Add(CodeSnippetBlock.Create("Quick Start",
                "Palette.Level.Start(1);\nPalette.Level.Complete(1, score: 100);"));

            return container;
        }

        static VisualElement BuildValidatedFieldSection()
        {
            var container = new VisualElement();
            container.AddToClassList("gallery-section");

            var title = new Label("ValidatedField");
            title.AddToClassList("gallery-section-title");
            container.Add(title);

            container.Add(ValidatedField.Create("MAX Ad Unit ID (Rewarded, Android)", "98e422a19d0a8049",
                ValidatedField.State.Valid));
            container.Add(ValidatedField.Create("Adjust App Token", "",
                ValidatedField.State.Required, "Required for Full-mode builds - see Adjust dashboard."));
            container.Add(ValidatedField.Create("Facebook App ID", "abc",
                ValidatedField.State.Invalid, "Must be a numeric Facebook App ID."));

            return container;
        }

        static VisualElement BuildCheckRowSection()
        {
            var container = new VisualElement();
            container.AddToClassList("gallery-section");

            var title = new Label("CheckRow / CollapsibleCheckGroup");
            title.AddToClassList("gallery-section-title");
            container.Add(title);

            container.Add(CheckRow.Create("Required SDKs", CheckRow.Status.Pass));
            container.Add(CheckRow.Create("MAX Settings", CheckRow.Status.Warn, "SYNCING"));
            container.Add(CheckRow.Create("Adjust app token", CheckRow.Status.Fail));
            container.Add(CheckRow.Create("Firebase Config Files", CheckRow.Status.Wait));

            var groupRows = new[]
            {
                CheckRow.Create("Scoped Registries", CheckRow.Status.Pass),
                CheckRow.Create("Android Manifest", CheckRow.Status.Pass),
                CheckRow.Create("Gradle Configuration", CheckRow.Status.Pass),
            };
            container.Add(CollapsibleCheckGroup.Create("3 checks passing", groupRows, startExpanded: true));

            return container;
        }

        static VisualElement BuildSectionHeaderSection()
        {
            var container = new VisualElement();
            container.AddToClassList("gallery-section");

            var title = new Label("SectionHeader");
            title.AddToClassList("gallery-section-title");
            container.Add(title);

            container.Add(SectionHeader.Create("SDK Overview", "Refresh"));
            container.Add(SectionHeader.Create("Build Health"));

            return container;
        }

        static VisualElement BuildCalloutCardSection()
        {
            var container = new VisualElement();
            container.AddToClassList("gallery-section");

            var title = new Label("CalloutCard");
            title.AddToClassList("gallery-section-title");
            container.Add(title);

            container.Add(CalloutCard.Create(CalloutCard.Severity.Blocker, "Adjust app token missing",
                "Full-mode builds fail until an Adjust app token is set in SorollaConfig.", "Open Config"));
            container.Add(CalloutCard.Create(CalloutCard.Severity.Advisory, "Duplicate EDM4U detected",
                "Both the embedded and UPM External Dependency Manager are present at the same version.", "Fix"));
            container.Add(CalloutCard.Create(CalloutCard.Severity.Success, "Build Health checks passing",
                "All required SDKs, manifest, and gradle configuration checks are green."));
            container.Add(CalloutCard.Create(CalloutCard.Severity.Info, "2 adapters can't be verified",
                "The QA bridge can only confirm init did not throw, not that vendor network calls succeed."));

            return container;
        }

        static VisualElement BuildStatusBadgeSection()
        {
            var container = new VisualElement();
            container.AddToClassList("gallery-section");

            var title = new Label("StatusBadge");
            title.AddToClassList("gallery-section-title");
            container.Add(title);

            var row = new VisualElement();
            row.AddToClassList("sorolla-swatch-row");
            foreach ((string label, StatusBadge.Severity severity) in BadgeVariants)
                row.Add(StatusBadge.Create(label, severity));
            container.Add(row);

            return container;
        }

        static VisualElement BuildTokenSwatchSection()
        {
            var container = new VisualElement();
            container.AddToClassList("gallery-section");

            var title = new Label("Design Tokens (TOKENS.md swatch sheet)");
            title.AddToClassList("gallery-section-title");
            container.Add(title);

            var bgRow = new VisualElement();
            bgRow.AddToClassList("sorolla-swatch-row");
            foreach ((string label, string bgClass) in BgSwatches)
            {
                var swatch = new VisualElement();
                swatch.AddToClassList("sorolla-swatch");
                swatch.AddToClassList(bgClass);
                var swatchLabel = new Label(label);
                swatchLabel.AddToClassList("sorolla-swatch-label");
                swatch.Add(swatchLabel);
                bgRow.Add(swatch);
            }
            container.Add(bgRow);

            var pillRow = new VisualElement();
            pillRow.AddToClassList("sorolla-swatch-row");
            foreach ((string label, string pillClass) in StatusPills)
            {
                var pill = new VisualElement();
                pill.AddToClassList("sorolla-swatch-pill");
                pill.AddToClassList(pillClass);
                var pillLabel = new Label(label);
                pillLabel.AddToClassList("sorolla-swatch-pill-label");
                pill.Add(pillLabel);
                pillRow.Add(pill);
            }
            container.Add(pillRow);

            foreach ((string text, string typeClass) in TypeScale)
            {
                var typeLabel = new Label(text);
                typeLabel.AddToClassList(typeClass);
                container.Add(typeLabel);
            }

            return container;
        }

    }
}
