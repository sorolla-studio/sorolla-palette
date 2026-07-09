using UnityEngine;
using UnityEngine.UIElements;

namespace Sorolla.Palette
{
    // Phase-1 spike (debug-menu overhaul): inert parallel code proving the injected-UIDocument
    // bootstrap gate. Opened only via the existing IMGUI console's "Open new menu (preview)"
    // action button; the IMGUI console itself stays untouched and shipping. Zero scene setup -
    // GameObject + UIDocument + PanelSettings are created entirely from code on open,
    // DontDestroyOnLoad, and fully torn down on close. Display-only: header facts are read
    // straight off the existing diagnostics/snapshot pipeline (BuildHeaderContext, Application.*),
    // no new fact pipeline. Tab content is 4 empty placeholder panes - phase 2+ fills them in.
    internal sealed class SorollaDebugMenuOverlay : MonoBehaviour
    {
        const string HostName = "[Palette SDK Debug Menu]";
        const string ThemeResourcePath = "SorollaDebugMenuTheme";
        const string UssResourcePath = "SorollaDebugMenuRuntime";
        const int PanelSortingOrder = 32000;

        static SorollaDebugMenuOverlay s_instance;

        PanelSettings _panelSettings;
        GameObject _createdEventSystem;
        readonly VisualElement[] _tabPanes = new VisualElement[4];
        readonly Button[] _tabButtons = new Button[4];
        int _activeTabIndex;

        internal static bool IsOpen => s_instance != null;

        internal static void Open()
        {
            if (s_instance != null) return;

            var host = new GameObject(HostName);
            try
            {
                DontDestroyOnLoad(host);
            }
            catch
            {
                // Matches SorollaDiagnosticsConsole.EnsureStandalone: tolerate very-early calls
                // before the scene is ready for DontDestroyOnLoad.
            }

            s_instance = host.AddComponent<SorollaDebugMenuOverlay>();
            s_instance.Build();
        }

        internal static void Close()
        {
            if (s_instance == null) return;
            Destroy(s_instance.gameObject);
        }

        internal static void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        void Build()
        {
            var document = gameObject.AddComponent<UIDocument>();
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _panelSettings.name = "SorollaDebugMenuPanelSettings (runtime, code-created)";
            _panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>(ThemeResourcePath);
            _panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _panelSettings.sortingOrder = PanelSortingOrder;
            document.panelSettings = _panelSettings;

            StyleSheet runtimeUss = Resources.Load<StyleSheet>(UssResourcePath);

            VisualElement root = document.rootVisualElement;
            root.AddToClassList("sorolla-debugmenu-root");
            if (runtimeUss != null)
                root.styleSheets.Add(runtimeUss);
            else
                Debug.LogWarning("[Palette] Debug menu spike: runtime USS not found at Resources/" + UssResourcePath);

            root.Add(BuildHeader());
            root.Add(BuildTabBar());

            VisualElement content = new VisualElement();
            content.AddToClassList("sorolla-debugmenu-content");
            for (int i = 0; i < _tabPanes.Length; i++)
            {
                _tabPanes[i] = BuildPlaceholderPane(TabLabel(i));
                _tabPanes[i].style.display = i == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                content.Add(_tabPanes[i]);
            }
            root.Add(content);

            SetActiveTab(0);

            _createdEventSystem = SorollaDebugMenuEventSystemFactory.CreateIfMissing();
        }

        VisualElement BuildHeader()
        {
            var header = new VisualElement();
            header.AddToClassList("sorolla-debugmenu-header");

            var titleRow = new VisualElement();
            titleRow.AddToClassList("sorolla-debugmenu-header-row");

            var badge = new VisualElement();
            badge.AddToClassList("sorolla-debugmenu-badge");
            badge.AddToClassList("sorolla-debugmenu-badge-placeholder");
            var badgeDot = new VisualElement();
            badgeDot.AddToClassList("sorolla-debugmenu-badge-dot");
            badge.Add(badgeDot);
            var badgeLabel = new Label("VERDICT —");
            badgeLabel.AddToClassList("sorolla-debugmenu-badge-label");
            badge.Add(badgeLabel);
            titleRow.Add(badge);

            var title = new Label("Palette Debug Menu (preview)");
            title.AddToClassList("sorolla-debugmenu-title");
            titleRow.Add(title);

            var close = new Button(Close) { text = "Close" };
            close.AddToClassList("sorolla-debugmenu-close");
            titleRow.Add(close);

            header.Add(titleRow);

            // Real facts, reused from the existing diagnostics/snapshot pipeline - no new fact
            // pipeline: same BuildHeaderContext() the IMGUI console's DrawBuildContext calls, plus
            // Application.identifier the same way DrawBuildContext already does.
            var appLine = new Label($"{Application.identifier}  |  {Application.platform}  |  {(Debug.isDebugBuild ? "Dev" : "Release")} build");
            appLine.AddToClassList("sorolla-debugmenu-context-line");
            header.Add(appLine);

            var sdkLine = new Label(SorollaDiagnostics.BuildHeaderContext());
            sdkLine.AddToClassList("sorolla-debugmenu-context-line");
            header.Add(sdkLine);

            return header;
        }

        VisualElement BuildTabBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("sorolla-debugmenu-tabs");

            for (int i = 0; i < _tabButtons.Length; i++)
            {
                int index = i;
                var button = new Button(() => SetActiveTab(index)) { text = TabLabel(i) };
                button.AddToClassList("sorolla-debugmenu-tab");
                _tabButtons[i] = button;
                bar.Add(button);
            }

            return bar;
        }

        static VisualElement BuildPlaceholderPane(string tabLabel)
        {
            var pane = new VisualElement();
            var label = new Label($"{tabLabel} (placeholder - phase 2+ fills this in)");
            label.AddToClassList("sorolla-debugmenu-placeholder");
            pane.Add(label);
            return pane;
        }

        void SetActiveTab(int index)
        {
            _activeTabIndex = index;
            for (int i = 0; i < _tabPanes.Length; i++)
            {
                _tabPanes[i].style.display = i == index ? DisplayStyle.Flex : DisplayStyle.None;
                _tabButtons[i].RemoveFromClassList("sorolla-debugmenu-tab-active");
                if (i == index)
                    _tabButtons[i].AddToClassList("sorolla-debugmenu-tab-active");
            }
        }

        static string TabLabel(int index)
        {
            switch (index)
            {
                case 0: return "Issues";
                case 1: return "Overview";
                case 2: return "Console";
                default: return "Actions";
            }
        }

        void OnDestroy()
        {
            if (s_instance == this)
                s_instance = null;

            if (_panelSettings != null)
                Destroy(_panelSettings);

            if (_createdEventSystem != null)
                Destroy(_createdEventSystem);
        }
    }
}
