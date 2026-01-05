namespace Sorolla.Palette.DebugUI
{
    /// <summary>
    ///     Base class for components that change based on SDK mode (Prototype vs Full).
    /// </summary>
    public abstract class ModeComponentBase : UIComponentBase
    {
        protected bool IsPrototype => Palette.Config == null || Palette.Config.isPrototypeMode;

        void Start()
        {
            if (Palette.IsInitialized)
                ApplyTheme();
            else
                Palette.OnInitialized += OnSorollaInitialized;
        }

        void OnDestroy()
        {
            Palette.OnInitialized -= OnSorollaInitialized;
        }

        void OnSorollaInitialized() => ApplyTheme();

        void HandleModeChanged(SorollaMode mode) => ApplyTheme();

        protected override void UnsubscribeFromEvents() => SorollaDebugEvents.OnModeChanged -= HandleModeChanged;

        protected override void SubscribeToEvents() => SorollaDebugEvents.OnModeChanged += HandleModeChanged;

        protected abstract override void ApplyTheme();
    }
}

