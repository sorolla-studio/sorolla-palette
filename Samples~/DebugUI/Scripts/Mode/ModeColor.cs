using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sorolla.Palette.DebugUI
{
    /// <summary>
    ///     Changes Image or Text color based on SDK mode.
    /// </summary>
    public class ModeColor : ModeComponentBase
    {
        [SerializeField] Color prototypeColor = Color.white;
        [SerializeField] Color fullColor = Color.white;

        Image _image;
        TMP_Text _text;

        void Awake()
        {
            _image = GetComponent<Image>();
            _text = GetComponent<TMP_Text>();
        }

        protected override void ApplyTheme()
        {
            Color color = IsPrototype ? prototypeColor : fullColor;
            if (_image) _image.color = color;
            if (_text) _text.color = color;
        }
    }
}
