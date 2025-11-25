using UnityEngine;

namespace Sorolla.ATT
{
    /// <summary>
    ///     Handles orientation/layout switching for the ATT Context Screen.
    ///     Automatically shows landscape or portrait layout based on screen orientation.
    /// </summary>
    public class AutoSwitchLayout : MonoBehaviour
    {
        [Header("Layout References")]
        [Tooltip("Root object for portrait layout")]
        [SerializeField] GameObject portraitLayout;
        
        [Tooltip("Root object for landscape layout")]
        [SerializeField] GameObject landscapeLayout;

        ScreenOrientation lastOrientation;

        void Start()
        {
            UpdateLayout();
        }

        void Update()
        {
            if (Screen.orientation != lastOrientation)
            {
                UpdateLayout();
            }
        }

        void UpdateLayout()
        {
            lastOrientation = Screen.orientation;
            
            var isPortrait = lastOrientation == ScreenOrientation.Portrait || 
                             lastOrientation == ScreenOrientation.PortraitUpsideDown;

            if (portraitLayout != null)
                portraitLayout.SetActive(isPortrait);
            
            if (landscapeLayout != null)
                landscapeLayout.SetActive(!isPortrait);
        }
    }
}
