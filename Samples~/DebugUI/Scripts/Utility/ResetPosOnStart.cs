using UnityEngine;

namespace Sorolla.Palette.DebugUI
{
    [RequireComponent(typeof(RectTransform))]
    public class RectTransformResetPosOnStart : MonoBehaviour
    {
        void Awake() => GetComponent<RectTransform>().anchoredPosition = Vector2.zero;
    }
}
