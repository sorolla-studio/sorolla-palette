namespace Sorolla.DebugUI
{
    /// <summary>
    ///     Base class for components that change based on SDK mode (Prototype vs Full).
    /// </summary>
    public abstract class ModeComponentBase : UIComponentBase
    {
        protected bool IsPrototype => SorollaSDK.Config == null || SorollaSDK.Config.isPrototypeMode;

        void Start()
        {
            if (SorollaSDK.IsInitialized)
                ApplyTheme();
            else
                SorollaSDK.OnInitialized += OnSorollaInitialized;
        }

        void OnDestroy()
        {
            SorollaSDK.OnInitialized -= OnSorollaInitialized;
        }

        void OnSorollaInitialized() => ApplyTheme();

        void HandleModeChanged(SorollaMode mode) => ApplyTheme();

        protected override void UnsubscribeFromEvents() => SorollaDebugEvents.OnModeChanged -= HandleModeChanged;

        protected override void SubscribeToEvents() => SorollaDebugEvents.OnModeChanged += HandleModeChanged;

        protected abstract override void ApplyTheme();
    }
}

