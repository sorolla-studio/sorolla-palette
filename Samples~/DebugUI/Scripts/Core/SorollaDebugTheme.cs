using UnityEngine;

namespace Sorolla.DebugUI
{
    [CreateAssetMenu(fileName = "SorollaDebugTheme", menuName = "SorollaSDK/Debug UI Theme")]
    public class SorollaDebugTheme : ScriptableObject
    {

        // Singleton access for runtime
        static SorollaDebugTheme _instance;
        [Header("Background Colors")]
        public Color canvasBackground = new Color(0.05f, 0.05f, 0.06f, 1f); // #0D0D0F
        public Color cardBackground = new Color(0.1f, 0.1f, 0.12f, 1f); // #1A1A1E
        public Color cardBackgroundLight = new Color(0.15f, 0.15f, 0.17f, 1f); // #262629

        [Header("Accent Colors")]
        public Color accentPurple = new Color(0.42f, 0.36f, 0.91f, 1f); // #6B5CE7
        public Color accentGreen = new Color(0.30f, 0.69f, 0.31f, 1f); // #4CAF50
        public Color accentRed = new Color(0.91f, 0.30f, 0.30f, 1f); // #E74C4C
        public Color accentYellow = new Color(1f, 0.76f, 0.03f, 1f); // #FFC107
        public Color accentOrange = new Color(1f, 0.60f, 0f, 1f); // #FF9800
        public Color accentCyan = new Color(0.0f, 0.74f, 0.83f, 1f); // #00BCD4

        [Header("Text Colors")]
        public Color textPrimary = Color.white;
        public Color textSecondary = new Color(0.53f, 0.53f, 0.53f, 1f); // #888888
        public Color textDisabled = new Color(0.4f, 0.4f, 0.4f, 1f); // #666666

        [Header("Status Colors")]
        public Color statusIdle = new Color(0.4f, 0.4f, 0.4f, 1f);
        public Color statusActive = new Color(0.30f, 0.69f, 0.31f, 1f);
        public Color statusError = new Color(0.91f, 0.30f, 0.30f, 1f);
        public Color statusWarning = new Color(1f, 0.76f, 0.03f, 1f);

        [Header("UI Dimensions")]
        public float cardCornerRadius = 12f;
        public float buttonCornerRadius = 8f;
        public float standardPadding = 16f;
        public float smallPadding = 8f;
        public float cardSpacing = 12f;
        public float buttonHeight = 48f;
        public float minTouchTarget = 44f;

        [Header("Typography Sizes")]
        public float titleSize = 18f;
        public float subtitleSize = 14f;
        public float bodySize = 14f;
        public float captionSize = 12f;
        public float sectionHeaderSize = 12f;

        [Header("Accent Bar")]
        public float accentBarWidth = 4f;
        public static SorollaDebugTheme Instance
        {
            get {
                if (_instance == null)
                {
                    _instance = Resources.Load<SorollaDebugTheme>("SorollaDebugTheme");
                }
                return _instance;
            }
        }
    }
}
