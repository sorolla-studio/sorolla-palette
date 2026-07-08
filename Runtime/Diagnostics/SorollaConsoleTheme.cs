using UnityEngine;

namespace Sorolla.Palette
{
    /// <summary>
    ///     Owns the diagnostics console's IMGUI theme: the resolution-scaled GUIStyles, the flat
    ///     background textures they draw, the ui-scale computation, and their build/rebuild/destroy
    ///     lifecycle. Extracted from the console MonoBehaviour so the styling concern (the bulk of
    ///     its fields) lives in one place. Styles are public because the console's draw code reads
    ///     them; the backing textures stay private behind SeverityBackground/GetPanelBackground.
    /// </summary>
    internal sealed class SorollaConsoleTheme
    {
        const float DefaultTitleFontSize = 24f;
        const float DefaultTextFontSize = 14.7f;
        const float DefaultSmallFontSize = 14.3f;
        const float DefaultPanelPadX = 9.5f;
        const float DefaultPanelPadY = 8.1f;
        const float DefaultRowPadX = 2f;
        const float DefaultRowPadY = 1f;
        const float DefaultButtonPadX = 4f;
        const float DefaultButtonPadY = 2f;
        const float DefaultScaleReferenceShortSide = 470.4f;
        const float DefaultMinUiScale = 1f;
        const float DefaultMaxUiScale = 2.7f;

        // Design tokens - canonical values + hex in docs/ui-overhaul/TOKENS.md; mirror any change
        // there and into Editor/UI/tokens.uss in the same cycle.
        static readonly Color TokenBgPage = new Color(0.01f, 0.012f, 0.016f, 1f);
        static readonly Color TokenBgCard = new Color(0.055f, 0.067f, 0.08f, 1f);
        static readonly Color TokenBgCardAlt = new Color(0.07f, 0.085f, 0.1f, 1f);
        static readonly Color TokenBgSection = new Color(0.105f, 0.13f, 0.155f, 1f);
        static readonly Color TokenBgElevated = new Color(0.16f, 0.18f, 0.21f, 1f);
        static readonly Color TokenBgElevatedHover = new Color(0.22f, 0.25f, 0.29f, 1f);
        static readonly Color TokenBgAccentMuted = new Color(0.12f, 0.26f, 0.31f, 1f);
        static readonly Color TokenBgAccent = new Color(0.08f, 0.32f, 0.38f, 1f);
        static readonly Color TokenBgSummary = new Color(0.045f, 0.055f, 0.065f, 1f);

        static readonly Color TokenStatusPass = new Color(0.12f, 0.58f, 0.32f, 1f);
        static readonly Color TokenStatusWarn = new Color(1f, 0.78f, 0.28f, 1f);
        static readonly Color TokenStatusFail = new Color(0.9f, 0.25f, 0.28f, 1f);
        static readonly Color TokenStatusWait = new Color(0.31f, 0.49f, 0.86f, 1f);
        static readonly Color TokenStatusInfo = new Color(0.42f, 0.46f, 0.5f, 1f);

        static readonly Color TokenStatusFailTint = new Color(0.12f, 0.055f, 0.06f, 1f);
        static readonly Color TokenStatusWarnTint = new Color(0.17f, 0.145f, 0.08f, 1f);

        static readonly Color TokenTextPrimary = new Color(0.95f, 0.96f, 0.97f, 1f);
        static readonly Color TokenTextPrimaryAlt = new Color(0.95f, 0.97f, 0.98f, 1f);
        static readonly Color TokenTextSecondary = new Color(0.8f, 0.84f, 0.88f, 1f);
        static readonly Color TokenTextTertiary = new Color(0.72f, 0.77f, 0.82f, 1f);
        static readonly Color TokenTextAccent = new Color(0.5f, 0.9f, 0.94f, 1f);
        static readonly Color TokenTextAccentMuted = new Color(0.82f, 0.9f, 0.94f, 1f);
        static readonly Color TokenTextTabInactive = new Color(0.68f, 0.73f, 0.78f, 1f);
        static readonly Color TokenTextOnWarn = new Color(0.12f, 0.09f, 0.02f, 1f);

        float _titleFontSize = DefaultTitleFontSize;
        float _textFontSize = DefaultTextFontSize;
        float _smallFontSize = DefaultSmallFontSize;
        float _panelPadX = DefaultPanelPadX;
        float _panelPadY = DefaultPanelPadY;
        float _rowPadX = DefaultRowPadX;
        float _rowPadY = DefaultRowPadY;
        float _buttonPadX = DefaultButtonPadX;
        float _buttonPadY = DefaultButtonPadY;
        float _scaleReferenceShortSide = DefaultScaleReferenceShortSide;
        float _minUiScale = DefaultMinUiScale;
        float _maxUiScale = DefaultMaxUiScale;
        float _stylesUiScale = -1f;
        float _uiScale = 1f;

        public GUIStyle TitleStyle;
        public GUIStyle SectionStyle;
        public GUIStyle SectionButtonStyle;
        public GUIStyle RowNameStyle;
        public GUIStyle RowNameInlineStyle;
        public GUIStyle DetailStyle;
        public GUIStyle MiniDetailStyle;
        public GUIStyle BadgeStyle;
        public GUIStyle ButtonStyle;
        public GUIStyle SelectedButtonStyle;
        public GUIStyle TabStyle;
        public GUIStyle ActiveTabStyle;
        public GUIStyle PanelStyle;
        public GUIStyle SummaryStyle;
        public GUIStyle RowStyle;
        public GUIStyle RowAltStyle;
        public GUIStyle RowProblemStyle;
        public GUIStyle RowWarningStyle;

        Texture2D _panelBackground;
        Texture2D _summaryBackground;
        Texture2D _rowBackground;
        Texture2D _rowAltBackground;
        Texture2D _rowProblemBackground;
        Texture2D _rowWarningBackground;
        Texture2D _sectionBackground;
        Texture2D _buttonBackground;
        Texture2D _buttonActiveBackground;
        Texture2D _buttonSelectedBackground;
        Texture2D _tabBackground;
        Texture2D _activeTabBackground;
        Texture2D _passBackground;
        Texture2D _warnBackground;
        Texture2D _failBackground;
        Texture2D _waitBackground;
        Texture2D _infoBackground;

        public float UiScale => _uiScale;

        public void EnsureStyles()
        {
            UpdateUiScale();

            int titleSize = Mathf.RoundToInt(_titleFontSize * _uiScale);
            int textSize = Mathf.RoundToInt(_textFontSize * _uiScale);
            int smallSize = Mathf.RoundToInt(_smallFontSize * _uiScale);

            if (TitleStyle != null && RowNameInlineStyle != null &&
                TitleStyle.fontSize == titleSize && Mathf.Approximately(_stylesUiScale, _uiScale)) return;

            _stylesUiScale = _uiScale;
            RebuildTextures();

            int padX = Mathf.RoundToInt(_panelPadX * _uiScale);
            int padY = Mathf.RoundToInt(_panelPadY * _uiScale);
            int rowPadX = Mathf.RoundToInt(_rowPadX * _uiScale);
            int rowPadY = Mathf.RoundToInt(_rowPadY * _uiScale);
            int buttonPadX = Mathf.RoundToInt(_buttonPadX * _uiScale);
            int buttonPadY = Mathf.RoundToInt(_buttonPadY * _uiScale);

            PanelStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(padX, padX, padY, padY),
                normal = { background = null },
            };
            PanelStyle.onNormal.background = null;

            TitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = titleSize,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = TokenTextPrimary },
            };

            SectionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = textSize,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = TokenTextAccent },
            };

            SectionButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = textSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                normal = { textColor = TokenTextAccentMuted, background = _sectionBackground },
                hover = { textColor = Color.white, background = _buttonActiveBackground },
                active = { textColor = Color.white, background = _buttonActiveBackground },
                padding = new RectOffset(rowPadX + 3, rowPadX + 3, rowPadY + 2, rowPadY + 2),
            };

            RowNameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = textSize,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = TokenTextPrimaryAlt },
            };

            RowNameInlineStyle = new GUIStyle(RowNameStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
            };

            DetailStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = smallSize,
                wordWrap = true,
                normal = { textColor = TokenTextSecondary },
            };

            MiniDetailStyle = new GUIStyle(DetailStyle)
            {
                fontSize = Mathf.Max(10, smallSize - 1),
                normal = { textColor = TokenTextTertiary },
            };

            BadgeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = smallSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                padding = new RectOffset(rowPadX, rowPadX, 0, 0),
            };

            ButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = smallSize,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.92f, 0.94f, 0.96f, 1f), background = _buttonBackground },
                hover = { textColor = Color.white, background = _buttonActiveBackground },
                active = { textColor = Color.white, background = _buttonActiveBackground },
                padding = new RectOffset(buttonPadX, buttonPadX, buttonPadY, buttonPadY),
            };

            SelectedButtonStyle = new GUIStyle(ButtonStyle)
            {
                normal = { textColor = Color.white, background = _buttonSelectedBackground },
                hover = { textColor = Color.white, background = _buttonSelectedBackground },
                active = { textColor = Color.white, background = _buttonSelectedBackground },
            };

            TabStyle = new GUIStyle(ButtonStyle)
            {
                fontSize = textSize,
                normal = { textColor = TokenTextTabInactive, background = _tabBackground },
                hover = { textColor = Color.white, background = _buttonActiveBackground },
                active = { textColor = Color.white, background = _activeTabBackground },
            };

            ActiveTabStyle = new GUIStyle(TabStyle)
            {
                normal = { textColor = Color.white, background = _activeTabBackground },
                hover = { textColor = Color.white, background = _activeTabBackground },
                active = { textColor = Color.white, background = _activeTabBackground },
            };

            SummaryStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(rowPadX + 3, rowPadX + 3, rowPadY + 3, rowPadY + 3),
                normal = { background = _summaryBackground },
            };

            RowStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(rowPadX, rowPadX, rowPadY + 1, rowPadY + 1),
                normal = { background = _rowBackground },
            };

            RowAltStyle = new GUIStyle(RowStyle)
            {
                normal = { background = _rowAltBackground },
            };

            RowProblemStyle = new GUIStyle(RowStyle)
            {
                normal = { background = _rowProblemBackground },
            };

            RowWarningStyle = new GUIStyle(RowStyle)
            {
                normal = { background = _rowWarningBackground },
            };
        }

        public void UpdateUiScale()
        {
            float shortSide = Mathf.Min(Screen.width, Screen.height);
            float reference = Mathf.Max(1f, _scaleReferenceShortSide);
            float minScale = Mathf.Max(0.1f, _minUiScale);
            float maxScale = Mathf.Max(minScale, _maxUiScale);
            _uiScale = shortSide > 0f ? Mathf.Clamp(shortSide / reference, minScale, maxScale) : minScale;
        }

        void RebuildTextures()
        {
            DestroyStyleResources();

            _panelBackground = CreateTexture(TokenBgPage);
            _summaryBackground = CreateTexture(TokenBgSummary);
            _rowBackground = CreateTexture(TokenBgCard);
            _rowAltBackground = CreateTexture(TokenBgCardAlt);
            _rowProblemBackground = CreateTexture(TokenStatusFailTint);
            _rowWarningBackground = CreateTexture(TokenStatusWarnTint);
            _sectionBackground = CreateTexture(TokenBgSection);
            _buttonBackground = CreateTexture(TokenBgElevated);
            _buttonActiveBackground = CreateTexture(TokenBgElevatedHover);
            _buttonSelectedBackground = CreateTexture(TokenBgAccentMuted);
            _tabBackground = CreateTexture(TokenBgCard);
            _activeTabBackground = CreateTexture(TokenBgAccent);
            _passBackground = CreateTexture(TokenStatusPass);
            _warnBackground = CreateTexture(TokenStatusWarn);
            _failBackground = CreateTexture(TokenStatusFail);
            _waitBackground = CreateTexture(TokenStatusWait);
            _infoBackground = CreateTexture(TokenStatusInfo);
        }

        public void DestroyStyleResources()
        {
            DestroyTexture(ref _panelBackground);
            DestroyTexture(ref _summaryBackground);
            DestroyTexture(ref _rowBackground);
            DestroyTexture(ref _rowAltBackground);
            DestroyTexture(ref _rowProblemBackground);
            DestroyTexture(ref _rowWarningBackground);
            DestroyTexture(ref _sectionBackground);
            DestroyTexture(ref _buttonBackground);
            DestroyTexture(ref _buttonActiveBackground);
            DestroyTexture(ref _buttonSelectedBackground);
            DestroyTexture(ref _tabBackground);
            DestroyTexture(ref _activeTabBackground);
            DestroyTexture(ref _passBackground);
            DestroyTexture(ref _warnBackground);
            DestroyTexture(ref _failBackground);
            DestroyTexture(ref _waitBackground);
            DestroyTexture(ref _infoBackground);
        }

        public Texture2D SeverityBackground(SorollaDiagnosticSeverity severity)
        {
            switch (severity)
            {
                case SorollaDiagnosticSeverity.Pass:
                    return _passBackground;
                case SorollaDiagnosticSeverity.Warning:
                    return _warnBackground;
                case SorollaDiagnosticSeverity.Fail:
                    return _failBackground;
                case SorollaDiagnosticSeverity.Waiting:
                    return _waitBackground;
                default:
                    return _infoBackground;
            }
        }

        public static Color BadgeTextColor(SorollaDiagnosticSeverity severity)
        {
            return severity == SorollaDiagnosticSeverity.Warning ? TokenTextOnWarn : Color.white;
        }

        static Texture2D CreateTexture(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        static void DestroyTexture(ref Texture2D texture)
        {
            if (texture == null) return;
            Object.Destroy(texture);
            texture = null;
        }

        public Texture2D GetPanelBackground()
        {
            if (_panelBackground == null)
                _panelBackground = CreateTexture(new Color(0.01f, 0.012f, 0.016f, 1f));
            return _panelBackground;
        }
    }
}
