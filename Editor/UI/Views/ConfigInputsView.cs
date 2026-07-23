using System.Collections.Generic;
using System.Text.RegularExpressions;
using Sorolla.Palette.Editor.Greenlight;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Sorolla.Palette.Editor.UI
{
    /// <summary>
    ///     Every editable SorollaConfig field in the window, built per vendor group so a vendor's status,
    ///     its check rows, and its config all live under one foldout instead of a separate "SDK Keys"
    ///     section that had to be kept in sync by hand.
    ///     All controls are BOUND (bindingPath / ValidatedField.CreateBound), never hand-written value
    ///     callbacks: a bound control routes its edit through the SerializedObject, which is what the window
    ///     watches to re-run validation, so a field and the check that grades it can never disagree.
    /// </summary>
    sealed class ConfigInputsView
    {
        static readonly Regex MaxAdUnitFormat = new Regex("^[0-9a-f]{16}$");

        readonly SorollaConfig _config;

        internal ConfigInputsView(SorollaConfig config, SerializedObject serializedConfig)
        {
            _config = config;
            SerializedConfig = serializedConfig;
        }

        internal SerializedObject SerializedConfig { get; }

        /// <summary>The Adjust app-token control, so a check row telling the studio to "enter it below"
        /// can scroll to and focus the actual field. Null while Adjust inputs are not rendered.</summary>
        internal VisualElement AdjustAppTokenField { get; private set; }

        /// <summary>Inputs for one group, or an empty list for the groups with nothing to configure
        /// (GameAnalytics / Facebook / Firebase keep their settings in their own vendor assets, and
        /// duplicating them here would be a second source of truth).</summary>
        internal List<VisualElement> BuildFor(GreenlightAdapter.VendorGroup group)
        {
            var inputs = new List<VisualElement>();

            // SorollaSettings.GetOrCreateRuntimeConfig() creates the asset when it is missing, so null here
            // means the create itself failed (unwritable Assets/Resources, or a non-SorollaConfig asset
            // squatting the exact path the runtime's Resources.Load needs). Say that instead of offering a
            // Create button that would take the same failing path again.
            if (_config == null || SerializedConfig == null)
            {
                if (group == GreenlightAdapter.VendorGroup.BuildAndProject)
                    inputs.Add(new HelpBox(
                        "Assets/Resources/SorollaConfig.asset could not be created. The SDK reads that exact " +
                        "path at runtime - check that Assets/Resources is writable and nothing else occupies it.",
                        HelpBoxMessageType.Error));
                return inputs;
            }

            switch (group)
            {
                // MAX ad units. ValidatedField applies at the LEAF level (each Android/iOS string), not to
                // the PlatformAdUnitId struct: a leaf binds a real SerializedProperty, which avoids the
                // double-label and [Header]-decorator problems a struct-level PropertyField hits.
                case GreenlightAdapter.VendorGroup.AppLovinMax:
                    if (SdkDetector.IsInstalled(SdkId.AppLovinMAX))
                    {
                        inputs.Add(AdUnitFoldout("Rewarded", "rewardedAdUnit"));
                        inputs.Add(AdUnitFoldout("Interstitial", "interstitialAdUnit"));
                        // "(optional)" casing matches every other optional-vendor label in this window.
                        inputs.Add(AdUnitFoldout("Banner (optional)", "bannerAdUnit"));
                    }
                    break;

                // Adjust (Full mode only). adjustAppToken is the ONE documented hard build gate
                // (BuildValidationVendorSettings / SdkConfigDetector: empty or length <= 5 fails a Full-mode
                // build) - Invalid state + subtext while unresolved, no subtext once valid. Sandbox Mode is
                // deliberately NOT here: its checkbox renders under its own check row, beside the warning
                // that explains it (see SandboxModeToggle).
                case GreenlightAdapter.VendorGroup.Adjust:
                    if (!SorollaSettings.IsPrototype && SdkDetector.IsInstalled(SdkId.Adjust))
                    {
                        AdjustAppTokenField = ValidatedField.CreateBound(
                            SerializedConfig.FindProperty("adjustAppToken"), "App Token",
                            value => !string.IsNullOrEmpty(value) && value.Length > 5
                                ? (ValidatedField.State.Valid, (string)null)
                                : (ValidatedField.State.Invalid, "Required for Full-mode builds"));
                        inputs.Add(AdjustAppTokenField);
                        inputs.Add(OptionalField("adjustPurchaseEventToken", "Purchase Event Token"));
                    }
                    else
                    {
                        AdjustAppTokenField = null;
                    }
                    break;

                // Verbose Logging sits with the check row that reports it, so the warning and the switch
                // that clears it are never in different groups. Toggle rather than PropertyField: a
                // PropertyField would render verboseLogging's own [Header("Logging")] as a stray section
                // line above the checkbox.
                case GreenlightAdapter.VendorGroup.BuildAndProject:
                    inputs.Add(new Toggle("Verbose Logging")
                    {
                        bindingPath = "verboseLogging",
                        tooltip = "Enable verbose debug output for all vendor SDKs. Forced OFF in release builds.",
                    });
                    break;
            }

            return inputs;
        }

        /// <summary>The Adjust sandbox switch, rendered under its own check row rather than in the field
        /// list above - the warning it answers used to say "untick Sandbox Mode in the Adjust group below",
        /// making the reader hunt for the control being described.</summary>
        internal static VisualElement SandboxModeToggle()
        {
            var toggle = new Toggle("Sandbox Mode")
            {
                bindingPath = "adjustSandboxMode",
                tooltip = "Send Adjust events to the sandbox environment. Use once to verify events arrive, " +
                          "then turn it off: sandbox events are excluded from Adjust's live dashboards and attribution.",
            };
            toggle.AddToClassList("sorolla-check-row-action");
            return toggle;
        }

        /// <summary>A plain optional string field: no documented required/invalid rule, so its state is
        /// just filled-vs-empty and there is no subtext to show.</summary>
        VisualElement OptionalField(string propertyName, string label) =>
            ValidatedField.CreateBound(SerializedConfig.FindProperty(propertyName), label, value =>
                string.IsNullOrEmpty(value)
                    ? (ValidatedField.State.None, (string)null)
                    : (ValidatedField.State.Valid, (string)null));

        VisualElement AdUnitFoldout(string label, string propertyPath)
        {
            var foldout = new Foldout { text = label, value = true };
            foldout.Add(AdUnitField("Android", propertyPath + ".android"));
            foldout.Add(AdUnitField("iOS", propertyPath + ".ios"));
            return foldout;
        }

        /// <summary>Empty is fully neutral (Arthur, confirmed twice) - no icon, no color, as calm as an
        /// untouched stock field, since ads are optional at prototype stage and Banner is explicitly
        /// optional. A non-empty value is checked against the MAX ad-unit id format (16 lowercase hex):
        /// a malformed id is amber (the warn color, not Invalid's fail red) - a soft warning, not a failure.</summary>
        VisualElement AdUnitField(string label, string propertyPath) =>
            ValidatedField.CreateBound(SerializedConfig.FindProperty(propertyPath), label, value =>
            {
                if (string.IsNullOrEmpty(value))
                    return (ValidatedField.State.None, (string)null);
                return MaxAdUnitFormat.IsMatch(value)
                    ? (ValidatedField.State.Valid, (string)null)
                    : (ValidatedField.State.Required, "Doesn't look like a MAX ad unit ID");
            });
    }
}
