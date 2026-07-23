using System;
using System.Collections.Generic;
using Sorolla.Palette.Editor.Greenlight;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sorolla.Palette.Editor.UI
{
    /// <summary>
    ///     A vendor's own validity signal - is it installed, and is it configured - separate from its check
    ///     rows. <see cref="Phase"/> spells out the whole lifecycle: the two install phases plus the
    ///     Fail/Warn/Pass config scale. Build &amp; Project and Device &amp; QA have no such signal;
    ///     their group header is derived purely from the rows rendered under it.
    /// </summary>
    sealed class VendorStatus
    {
        internal enum Phase
        {
            Installing,
            NotInstalled,
            /// <summary>An installed vendor the game has switched OFF. Neutral grey, never green: nothing
            /// was verified here, so an affirmative pass would be a lie about a vendor that isn't running.</summary>
            Disabled,
            Fail,
            Warn,
            Pass,
        }

        internal Phase State;
        internal string Text;
        internal bool Optional;
        internal string ActionLabel;
        internal Action Action;
        internal bool ActionEnabled = true;
    }

    /// <summary>
    ///     Computes each vendor group's <see cref="VendorStatus"/>. Read-only by contract: these run during
    ///     UI construction, so anything that WRITES (the MAX settings sync used to be called from here) must
    ///     live in the auto-fix pass instead, or the window touches disk on every repaint.
    /// </summary>
    sealed class VendorStatusProbe
    {
        readonly SorollaConfig _config;
        readonly HashSet<string> _installingPackages;
        readonly ConfigInputsView _inputs;
        readonly Action<VisualElement> _focusField;
        readonly Action _revalidate;

        internal VendorStatusProbe(SorollaConfig config, HashSet<string> installingPackages,
            ConfigInputsView inputs, Action<VisualElement> focusField, Action revalidate)
        {
            _config = config;
            _installingPackages = installingPackages;
            _inputs = inputs;
            _focusField = focusField;
            _revalidate = revalidate;
        }

        /// <summary>Dispatches to each vendor's own computation; null for the groups with no such signal
        /// (Build &amp; Project, Device &amp; QA), whose header derives purely from their visible rows.</summary>
        internal VendorStatus For(GreenlightAdapter.VendorGroup id) => id switch
        {
            GreenlightAdapter.VendorGroup.GameAnalytics => GameAnalytics(),
            GreenlightAdapter.VendorGroup.Facebook => Generic(
                SdkRegistry.All[SdkId.Facebook], SdkConfigDetector.GetFacebookStatus(), "Set App ID",
                SdkConfigDetector.OpenFacebookSettings, isRequired: true),
            GreenlightAdapter.VendorGroup.Firebase => Firebase(),
            GreenlightAdapter.VendorGroup.AppLovinMax => Max(),
            GreenlightAdapter.VendorGroup.Adjust => Generic(
                SdkRegistry.All[SdkId.Adjust], SdkConfigDetector.GetAdjustStatus(_config), "Enter app token below",
                () => _focusField(_inputs.AdjustAppTokenField), isRequired: !SorollaSettings.IsPrototype),
            _ => null,
        };

        /// <summary>The shape shared by every vendor whose only signal is "installed? configured?"
        /// (Facebook, Adjust). GameAnalytics/MAX/Firebase have their own methods because their
        /// "configured" question is not a single ConfigStatus.</summary>
        VendorStatus Generic(SdkInfo sdk, SdkConfigDetector.ConfigStatus configStatus, string configHint,
            Action openSettings, bool isRequired)
        {
            if (_installingPackages.Contains(sdk.PackageId))
                return Installing();

            if (!SdkDetector.IsInstalled(sdk))
                return NotInstalled(sdk, isRequired);

            if (configStatus == SdkConfigDetector.ConfigStatus.Configured)
                return new VendorStatus
                {
                    State = VendorStatus.Phase.Pass,
                    Text = "Configured",
                    Optional = !isRequired,
                    ActionLabel = openSettings != null ? "Edit" : null,
                    Action = openSettings,
                };

            return new VendorStatus
            {
                State = VendorStatus.Phase.Warn,
                Text = configHint,
                Optional = !isRequired,
                ActionLabel = openSettings != null ? "Configure" : null,
                Action = openSettings,
            };
        }

        /// <summary>GameAnalytics' own signal is install state only. Key health is graded by the Platform Keys
        /// CHECK - for the active build target - whose row escalates this header through the
        /// shared worst-of merge; the header cannot disagree with the row below it, which it could when both
        /// sides graded the same fact independently (the header said ERROR while its own child row said WARN).
        /// The caption naming BOTH platforms rides here as the header's detail text: it is the one place that
        /// still tells a studio what the platform it is not building would need, without grading it.</summary>
        VendorStatus GameAnalytics()
        {
            SdkInfo sdk = SdkRegistry.All[SdkId.GameAnalytics];
            if (_installingPackages.Contains(sdk.PackageId))
                return Installing();
            if (!SdkDetector.IsInstalled(sdk))
                return new VendorStatus { State = VendorStatus.Phase.Fail };

            return new VendorStatus
            {
                State = VendorStatus.Phase.Pass,
                Text = SdkConfigDetector.GetGameAnalyticsPlatformDetail(),
                ActionLabel = "Edit",
                Action = SdkConfigDetector.OpenGameAnalyticsSettings,
            };
        }

        VendorStatus Max()
        {
            SdkInfo sdk = SdkRegistry.All[SdkId.AppLovinMAX];
            if (_installingPackages.Contains(sdk.PackageId))
                return Installing();
            if (!SdkDetector.IsInstalled(sdk))
                return NotInstalled(sdk, isRequired: !SorollaSettings.IsPrototype);

            // Read-only: the key sync itself is an auto-fix and runs in the validation pass.
            if (MaxSettingsSanitizer.IsSdkKeyConfigured())
                return new VendorStatus { State = VendorStatus.Phase.Pass, Text = "Auto-synced" };

            return new VendorStatus
            {
                State = VendorStatus.Phase.Fail, Text = "Auto-sync failed",
                ActionLabel = "Refresh", Action = _revalidate,
            };
        }

        /// <summary>Like <see cref="GameAnalytics"/>: install state only. Config-file health (BOTH platforms'
        /// google-services.json / GoogleService-Info.plist, each matched against the app id it must carry) is
        /// graded by the two Firebase config checks, whose rows escalate this header. The old own-state
        /// grading read only the ACTIVE platform through a name-substring asset search, so it could show
        /// "Configured" on a stray file while the authoritative row disagreed.</summary>
        VendorStatus Firebase()
        {
            bool isRequired = !SorollaSettings.IsPrototype;
            bool isInstalling = _installingPackages.Contains("com.google.firebase.app") ||
                                _installingPackages.Contains("com.google.firebase.analytics") ||
                                _installingPackages.Contains("com.google.firebase.crashlytics") ||
                                _installingPackages.Contains("com.google.firebase.remote-config");

            if (isInstalling)
                return Installing();

            if (!SdkDetector.IsInstalled(SdkId.FirebaseAnalytics))
            {
                VendorStatus state = isRequired
                    ? new VendorStatus { State = VendorStatus.Phase.Fail, Text = "Auto-installs on mode switch" }
                    : new VendorStatus { State = VendorStatus.Phase.NotInstalled, Optional = true };
                if (!isRequired) // Prototype - Full mode auto-installs Firebase, so no manual Install button
                {
                    state.ActionLabel = "Install";
                    state.ActionEnabled = !EditorApplication.isPlaying;
                    state.Action = () =>
                    {
                        SdkInstaller.Install(SdkId.FirebaseApp);
                        SdkInstaller.Install(SdkId.FirebaseAnalytics);
                        SdkInstaller.Install(SdkId.FirebaseCrashlytics);
                        SdkInstaller.Install(SdkId.FirebaseRemoteConfig);
                    };
                }
                return state;
            }

            return new VendorStatus
            {
                State = VendorStatus.Phase.Pass, Optional = !isRequired,
                ActionLabel = "Console", Action = () => Application.OpenURL("https://console.firebase.google.com/"),
            };
        }

        static VendorStatus Installing() =>
            new VendorStatus { State = VendorStatus.Phase.Installing, Text = "Installing..." };

        VendorStatus NotInstalled(SdkInfo sdk, bool isRequired)
        {
            bool isAutoInstalled = sdk.IsRequiredFor(SorollaSettings.IsPrototype);
            var state = new VendorStatus
            {
                State = isRequired ? VendorStatus.Phase.Fail : VendorStatus.Phase.NotInstalled,
                Text = isAutoInstalled ? "Auto-installs on mode switch" : null,
                Optional = !isRequired,
            };
            if (!isAutoInstalled)
            {
                state.ActionLabel = "Install";
                state.Action = () => SdkInstaller.Install(sdk.Id);
                state.ActionEnabled = !EditorApplication.isPlaying;
            }
            return state;
        }
    }
}
