using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sorolla.Palette.DebugUI
{
    public class NavButton : UIComponentBase, IPointerClickHandler
    {
        [SerializeField] int tabIndex;
        [SerializeField] Image backgroundHighlight;

        void Start() => SetSelected(tabIndex == 0);

        public void OnPointerClick(PointerEventData eventData) => SorollaDebugEvents.RaiseTabChanged(tabIndex);

        protected override void SubscribeToEvents() => SorollaDebugEvents.OnTabChanged += HandleTabChanged;

        protected override void UnsubscribeFromEvents() => SorollaDebugEvents.OnTabChanged -= HandleTabChanged;

        void HandleTabChanged(int tabIndex) => SetSelected(tabIndex == this.tabIndex);

        void SetSelected(bool selected) => backgroundHighlight.enabled = selected;
    }
}
