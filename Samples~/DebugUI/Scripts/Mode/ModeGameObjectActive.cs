using UnityEngine;

namespace Sorolla.Palette.DebugUI
{
    /// <summary>
    ///     Controls active state of a target GameObject based on SDK mode.
    ///     Target is a separate reference to avoid breaking event subscriptions.
    /// </summary>
    public class ModeGameObjectActive : ModeComponentBase
    {
        [SerializeField] GameObject target;
        [SerializeField] ActiveMode mode = ActiveMode.AlwaysActive;

        protected override void ApplyTheme()
        {
            bool shouldBeActive = mode switch
            {
                ActiveMode.AlwaysActive => true,
                ActiveMode.PrototypeOnly => IsPrototype,
                ActiveMode.FullOnly => !IsPrototype,
                _ => true,
            };

            target.SetActive(shouldBeActive);
        }

        enum ActiveMode
        {
            AlwaysActive,
            PrototypeOnly,
            FullOnly,
        }
    }
}
