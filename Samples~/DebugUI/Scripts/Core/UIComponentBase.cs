using UnityEngine;

namespace Sorolla.DebugUI
{
    /// <summary>
    ///     Base class for all SorollaSDK Debug UI components.
    ///     Provides access to theme and common functionality.
    /// </summary>
    public abstract class UIComponentBase : MonoBehaviour
    {
        protected SorollaDebugTheme Theme => SorollaDebugTheme.Instance;

        protected virtual void OnEnable()
        {
            SubscribeToEvents();
            ApplyTheme();
        }

        protected virtual void OnDisable() => UnsubscribeFromEvents();

        protected virtual void SubscribeToEvents() {}
        protected virtual void UnsubscribeFromEvents() {}

        protected virtual void ApplyTheme() {}
    }
}
