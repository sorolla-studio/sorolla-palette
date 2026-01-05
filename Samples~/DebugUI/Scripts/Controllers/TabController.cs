using System.Collections.Generic;
using UnityEngine;

namespace Sorolla.Palette.DebugUI
{
    public class TabController : UIComponentBase
    {
        [SerializeField] List<GameObject> tabPages = new List<GameObject>();
        [SerializeField] int defaultTabIndex;

        int CurrentTabIndex { get; set; } = -1;

        void Start()
        {
            if (tabPages.Count == 0)
            {
                Debug.LogError("[Palette Debug UI] TabController has no tab pages assigned.");
                return;
            }
            if (defaultTabIndex < 0) defaultTabIndex = tabPages.Count;
            SetActiveTab(defaultTabIndex);
            SorollaDebugEvents.RaiseTabChanged(defaultTabIndex);
        }

        protected override void SubscribeToEvents() => SorollaDebugEvents.OnTabChanged += HandleTabChanged;

        protected override void UnsubscribeFromEvents() => SorollaDebugEvents.OnTabChanged -= HandleTabChanged;

        void HandleTabChanged(int tabIndex) => SetActiveTab(tabIndex);

        void SetActiveTab(int index)
        {
            if (index < 0 || index >= tabPages.Count) return;
            if (index == CurrentTabIndex) return;

            CurrentTabIndex = index;

            for (int i = 0; i < tabPages.Count; i++)
            {
                tabPages[i].SetActive(i == index);
            }
        }
    }
}
