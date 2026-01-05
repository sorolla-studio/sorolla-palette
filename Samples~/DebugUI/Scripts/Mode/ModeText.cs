using TMPro;
using UnityEngine;

namespace Sorolla.Palette.DebugUI
{
    [RequireComponent(typeof(TMP_Text))]
    public class ModeText : ModeComponentBase
    {
        [SerializeField] string prototypeString = "Prototype Mode";
        [SerializeField] string fullString = "Full Mode";

        TMP_Text _text;

        void Awake() => _text = GetComponent<TMP_Text>();

        protected override void ApplyTheme() => _text.text = IsPrototype ? prototypeString : fullString;
    }
}
