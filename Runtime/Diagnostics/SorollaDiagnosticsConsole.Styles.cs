using UnityEngine;

namespace Sorolla.Palette
{
    internal sealed partial class SorollaDiagnosticsConsole
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

        void EnsureStyles()
        {
            UpdateUiScale();

            int titleSize = Mathf.RoundToInt(_titleFontSize * _uiScale);
            int textSize = Mathf.RoundToInt(_textFontSize * _uiScale);
            int smallSize = Mathf.RoundToInt(_smallFontSize * _uiScale);

            if (_titleStyle != null && _rowNameInlineStyle != null &&
                _titleStyle.fontSize == titleSize && Mathf.Approximately(_stylesUiScale, _uiScale)) return;

            _stylesUiScale = _uiScale;
            RebuildTextures();

            int padX = Mathf.RoundToInt(_panelPadX * _uiScale);
            int padY = Mathf.RoundToInt(_panelPadY * _uiScale);
            int rowPadX = Mathf.RoundToInt(_rowPadX * _uiScale);
            int rowPadY = Mathf.RoundToInt(_rowPadY * _uiScale);
            int buttonPadX = Mathf.RoundToInt(_buttonPadX * _uiScale);
            int buttonPadY = Mathf.RoundToInt(_buttonPadY * _uiScale);

            _panelStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(padX, padX, padY, padY),
                normal = { background = null },
            };
            _panelStyle.onNormal.background = null;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = titleSize,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = new Color(0.95f, 0.96f, 0.97f, 1f) },
            };

            _sectionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = textSize,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = new Color(0.5f, 0.9f, 0.94f, 1f) },
            };

            _sectionButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = textSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true,
                normal = { textColor = new Color(0.82f, 0.9f, 0.94f, 1f), background = _sectionBackground },
                hover = { textColor = Color.white, background = _buttonActiveBackground },
                active = { textColor = Color.white, background = _buttonActiveBackground },
                padding = new RectOffset(rowPadX + 3, rowPadX + 3, rowPadY + 2, rowPadY + 2),
            };

            _rowNameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = textSize,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                normal = { textColor = new Color(0.95f, 0.97f, 0.98f, 1f) },
            };

            _rowNameInlineStyle = new GUIStyle(_rowNameStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
            };

            _detailStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = smallSize,
                wordWrap = true,
                normal = { textColor = new Color(0.8f, 0.84f, 0.88f, 1f) },
            };

            _miniDetailStyle = new GUIStyle(_detailStyle)
            {
                fontSize = Mathf.Max(10, smallSize - 1),
                normal = { textColor = new Color(0.72f, 0.77f, 0.82f, 1f) },
            };

            _badgeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = smallSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
                padding = new RectOffset(rowPadX, rowPadX, 0, 0),
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = smallSize,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.92f, 0.94f, 0.96f, 1f), background = _buttonBackground },
                hover = { textColor = Color.white, background = _buttonActiveBackground },
                active = { textColor = Color.white, background = _buttonActiveBackground },
                padding = new RectOffset(buttonPadX, buttonPadX, buttonPadY, buttonPadY),
            };

            _selectedButtonStyle = new GUIStyle(_buttonStyle)
            {
                normal = { textColor = Color.white, background = _buttonSelectedBackground },
                hover = { textColor = Color.white, background = _buttonSelectedBackground },
                active = { textColor = Color.white, background = _buttonSelectedBackground },
            };

            _tabStyle = new GUIStyle(_buttonStyle)
            {
                fontSize = textSize,
                normal = { textColor = new Color(0.68f, 0.73f, 0.78f, 1f), background = _tabBackground },
                hover = { textColor = Color.white, background = _buttonActiveBackground },
                active = { textColor = Color.white, background = _activeTabBackground },
            };

            _activeTabStyle = new GUIStyle(_tabStyle)
            {
                normal = { textColor = Color.white, background = _activeTabBackground },
                hover = { textColor = Color.white, background = _activeTabBackground },
                active = { textColor = Color.white, background = _activeTabBackground },
            };

            _summaryStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(rowPadX + 3, rowPadX + 3, rowPadY + 3, rowPadY + 3),
                normal = { background = _summaryBackground },
            };

            _rowStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(rowPadX, rowPadX, rowPadY + 1, rowPadY + 1),
                normal = { background = _rowBackground },
            };

            _rowAltStyle = new GUIStyle(_rowStyle)
            {
                normal = { background = _rowAltBackground },
            };

            _rowProblemStyle = new GUIStyle(_rowStyle)
            {
                normal = { background = _rowProblemBackground },
            };

            _rowWarningStyle = new GUIStyle(_rowStyle)
            {
                normal = { background = _rowWarningBackground },
            };
        }

        void UpdateUiScale()
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

            _panelBackground = CreateTexture(new Color(0.01f, 0.012f, 0.016f, 1f));
            _summaryBackground = CreateTexture(new Color(0.045f, 0.055f, 0.065f, 1f));
            _rowBackground = CreateTexture(new Color(0.055f, 0.067f, 0.08f, 1f));
            _rowAltBackground = CreateTexture(new Color(0.07f, 0.085f, 0.1f, 1f));
            _rowProblemBackground = CreateTexture(new Color(0.12f, 0.055f, 0.06f, 1f));
            _rowWarningBackground = CreateTexture(new Color(0.17f, 0.145f, 0.08f, 1f));
            _sectionBackground = CreateTexture(new Color(0.105f, 0.13f, 0.155f, 1f));
            _buttonBackground = CreateTexture(new Color(0.16f, 0.18f, 0.21f, 1f));
            _buttonActiveBackground = CreateTexture(new Color(0.22f, 0.25f, 0.29f, 1f));
            _buttonSelectedBackground = CreateTexture(new Color(0.12f, 0.26f, 0.31f, 1f));
            _tabBackground = CreateTexture(new Color(0.055f, 0.065f, 0.08f, 1f));
            _activeTabBackground = CreateTexture(new Color(0.08f, 0.32f, 0.38f, 1f));
            _passBackground = CreateTexture(new Color(0.12f, 0.58f, 0.32f, 1f));
            _warnBackground = CreateTexture(new Color(1f, 0.78f, 0.28f, 1f));
            _failBackground = CreateTexture(new Color(0.9f, 0.25f, 0.28f, 1f));
            _waitBackground = CreateTexture(new Color(0.31f, 0.49f, 0.86f, 1f));
            _infoBackground = CreateTexture(new Color(0.42f, 0.46f, 0.5f, 1f));
        }

        void DestroyStyleResources()
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

        Texture2D SeverityBackground(SorollaDiagnosticSeverity severity)
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

        static Color BadgeTextColor(SorollaDiagnosticSeverity severity)
        {
            return severity == SorollaDiagnosticSeverity.Warning ? new Color(0.12f, 0.09f, 0.02f) : Color.white;
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
            Destroy(texture);
            texture = null;
        }

        Texture2D GetPanelBackground()
        {
            if (_panelBackground == null)
                _panelBackground = CreateTexture(new Color(0.01f, 0.012f, 0.016f, 1f));
            return _panelBackground;
        }
    }
}
