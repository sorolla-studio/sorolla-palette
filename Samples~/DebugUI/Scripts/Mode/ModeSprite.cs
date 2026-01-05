using UnityEngine;
using UnityEngine.UI;

namespace Sorolla.Palette.DebugUI
{
    /// <summary>
    ///     Changes Image sprite based on SDK mode.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class ModeSprite : ModeComponentBase
    {
        [SerializeField] Sprite prototypeSprite;
        [SerializeField] Sprite fullSprite;

        Image _image;

        void Awake() => _image = GetComponent<Image>();

        protected override void ApplyTheme() => _image.sprite = IsPrototype ? prototypeSprite : fullSprite;
    }
}
