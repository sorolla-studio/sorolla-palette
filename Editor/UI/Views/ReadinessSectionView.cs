using System;
using System.Collections.Generic;
using System.Linq;
using Sorolla.Palette.Editor.Greenlight;
using Sorolla.Palette.Health;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sorolla.Palette.Editor.UI
{
    /// <summary>
    ///     The window's verdict section: one vendor group per foldout, each carrying that vendor's status
    ///     pill, its check rows, and its config inputs.
    ///     The rendering contract, in one place: ONE group list, ONE pure view filter with no per-group
    ///     exceptions outside the shared capability policy, header status derived AFTER filtering from the
    ///     same rows rendered below it (so a
    ///     header can never contradict its children), one shared row element and one shared button style.
    ///     Nothing here may infer capability visibility from labels or local mode checks.
    /// </summary>
    sealed class ReadinessSectionView
    {
        /// <summary>Names what the section answers - can this integration ship - rather than the internal
        /// mechanism behind it ("Greenlight" meant nothing to a studio reading it cold).</summary>
        const string SectionTitle = "Launch Readiness";

        static readonly GreenlightAdapter.VendorGroup[] GroupOrder =
        {
            GreenlightAdapter.VendorGroup.GameAnalytics,
            GreenlightAdapter.VendorGroup.Facebook,
            GreenlightAdapter.VendorGroup.Firebase,
            GreenlightAdapter.VendorGroup.AppLovinMax,
            GreenlightAdapter.VendorGroup.Adjust,
            GreenlightAdapter.VendorGroup.BuildAndProject,
            GreenlightAdapter.VendorGroup.DeviceAndQa,
            GreenlightAdapter.VendorGroup.TikTok,
        };

        /// <summary>Checks a studio should see even when green (Arthur ruling 2026-07-21 ~17:55: "keep the
        /// important ones") - the facts a studio acts on or asks about: is every SDK present at the right
        /// version in the right mode, is the Android build config sane, and did the per-game credentials it
        /// supplied itself actually check out. "No row" reads as "not checked", which is exactly the doubt
        /// these rows exist to remove. The internal-hygiene rest (registries, config sync, EDM4U, gradle java
        /// home, addressables, pin) stays hidden while passing; Copy Report still carries every one of them.</summary>
        static readonly HashSet<string> VisibleWhenPassing = new HashSet<string>
        {
            GateIds.BuildRequiredSdks,
            GateIds.BuildSdkVersions,
            GateIds.BuildModeConsistency,
            GateIds.BuildAndroidManifest,
            GateIds.BuildGradleConfig,
            GateIds.BuildDevelopmentBuild,
            GateIds.BuildGameAnalyticsKeys,
            GateIds.BuildGameAnalyticsCredentials,
            GateIds.BuildFirebaseConfigAndroid,
            GateIds.BuildFirebaseConfigIos,
            // Sandbox mode carries its own toggle on the row, so the row must stay visible when the check
            // passes (sandbox off) - otherwise the only way to turn sandbox ON for a verification run would
            // be to go find the raw config asset.
            GateIds.BuildAdjustSandboxMode,
        };

        // Per-group expand/collapse memory for the window session; the attention-based default only applies
        // the first time a group id is seen.
        readonly Dictionary<string, bool> _groupExpanded = new Dictionary<string, bool>();

        readonly VisualElement _container;
        readonly VisualElement _headerActionsHost;
        readonly ConfigInputsView _configInputs;
        readonly VendorStatusProbe _vendorStatus;
        readonly GreenlightDeviceSnapshot.State _snapshotState;
        readonly Action _onRefresh;
        readonly Action _onConnectDevice;
        readonly Action _onModeSwitch;

        internal ReadinessSectionView(VisualElement container, VisualElement headerActionsHost,
            ConfigInputsView configInputs, VendorStatusProbe vendorStatus,
            GreenlightDeviceSnapshot.State snapshotState,
            Action onRefresh, Action onConnectDevice, Action onModeSwitch)
        {
            _container = container;
            _headerActionsHost = headerActionsHost;
            _configInputs = configInputs;
            _vendorStatus = vendorStatus;
            _snapshotState = snapshotState;
            _onRefresh = onRefresh;
            _onConnectDevice = onConnectDevice;
            _onModeSwitch = onModeSwitch;
        }

        /// <summary>Clear-and-rebuild from a fresh report. A group renders iff it has at least one visible
        /// row or one input - no "all clear" lines, no count-only headers with nothing beneath them.</summary>
        internal void Refresh(GreenlightEvaluator.Report report, IReadOnlyList<string> autoFixLog)
        {
            _container.Clear();
            _container.Add(BuildHeader(report));

            RefreshHeaderActions(report);

            // The AUTO-FIXED log reports edits the SDK just made to the studio's own project (manifest,
            // gradle templates, AppLovin settings). A studio is entitled to see those.
            foreach (string fix in autoFixLog)
            {
                var fixLabel = new Label($"AUTO-FIXED: {fix}");
                fixLabel.AddToClassList("sorolla-type-small");
                _container.Add(fixLabel);
            }

            bool anyOpen = false;
            foreach (GroupModel group in BuildGroups(report))
            {
                List<GreenlightEvaluator.Row> visibleRows = group.Rows.Where(RowVisible).ToList();
                if (visibleRows.Count == 0 && group.Inputs.Count == 0) continue;
                anyOpen = true;

                var rowElements = new List<VisualElement>();
                foreach (GreenlightEvaluator.Row row in visibleRows)
                    rowElements.Add(BuildRow(row, group.Id));
                rowElements.AddRange(group.Inputs);

                _container.Add(BuildGroupSection(group, visibleRows, rowElements));
            }

            if (!anyOpen)
            {
                // The empty state must agree with the badge beside it (F1 ruling, 2026-07-21). Since the
                // human-attested gates were deleted, a clean studio setup CAN reach HEALTHY, so the two cases
                // are genuinely different: green, or still waiting on evidence the studio can produce.
                var clear = new Label(report.Outcome == GateOutcome.Pass
                    ? "Your setup is clean - everything the SDK can check is green."
                    : "Your setup is clean - the remaining evidence comes from a run on a connected device.");
                clear.AddToClassList("sorolla-type-small");
                _container.Add(clear);
            }

            if (_configInputs.SerializedConfig != null)
                _container.Bind(_configInputs.SerializedConfig);
        }

        VisualElement BuildHeader(GreenlightEvaluator.Report report)
        {
            var header = new VisualElement();

            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            titleRow.style.marginBottom = 8;

            var titleLabel = new Label(SectionTitle);
            titleLabel.AddToClassList("sorolla-type-section");
            titleRow.Add(titleLabel);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            titleRow.Add(spacer);

            titleRow.Add(StatusBadge.Create(
                GreenlightEvaluator.VerdictLabel(report.Outcome, report.FailCount, report.WarnCount),
                GreenlightEvaluator.BadgeSeverity(report.Outcome)));
            header.Add(titleRow);

            // The report judges ONE platform - the build target Unity is set to (2026-07-23) - so it says
            // which, and where the other platform's checks went. Without this line a studio that switched
            // target would see rows appear and disappear with no stated cause.
            var scope = new Label(ScopeLine(report.Context));
            scope.AddToClassList("sorolla-type-small");
            header.Add(scope);

            // The verdict pill alone says "not green" without saying how far off. The counts say it in one
            // line, and they cover every evaluated gate - including the passing ones the row filter hides -
            // so the pass count is the visible proof that the list below is a filtered view, not everything.
            var counts = new Label($"{report.FailCount} fail · {report.WarnCount} warn · " +
                                   $"{report.WaitCount} pending · {report.PassCount} pass");
            counts.AddToClassList("sorolla-type-small");
            counts.style.marginBottom = 8;
            header.Add(counts);

            return header;
        }

        /// <summary>Names the platform this report judged, and says where the other platform's checks are.
        /// Off-mobile there is no mobile build to judge, so it points at the two targets that are checkable
        /// rather than naming one of them as missing.</summary>
        static string ScopeLine(EvaluationContext context)
        {
            switch (context?.Platform)
            {
                case EvalPlatform.Android:
                    return "Judging the Android build target. Switch platform in Build Settings to check iOS.";
                case EvalPlatform.iOS:
                    return "Judging the iOS build target. Switch platform in Build Settings to check Android.";
                default:
                    return $"Judging the {EditorUserBuildSettings.activeBuildTarget} build target. " +
                           "Switch platform in Build Settings to Android or iOS for the mobile checks.";
            }
        }

        /// <summary>The window-wide actions (Refresh / Connect Device / Copy Report), fixed in the header
        /// below the hero - one home for global actions, no in-content duplicates. Repopulated on every
        /// refresh so the Connect button's state tracks the snapshot phase.</summary>
        void RefreshHeaderActions(GreenlightEvaluator.Report report)
        {
            if (_headerActionsHost == null) return;
            _headerActionsHost.Clear();

            VisualElement container = SorollaTheme.CreateSectionContainer();

            var actionsRow = new VisualElement();
            actionsRow.style.flexDirection = FlexDirection.Row;
            actionsRow.style.flexWrap = Wrap.Wrap;

            var refreshButton = new Button(_onRefresh) { text = "Refresh" };
            refreshButton.AddToClassList("sorolla-button-small");
            actionsRow.Add(refreshButton);

            bool connecting = _snapshotState.Phase == GreenlightDeviceSnapshot.Phase.Running;
            bool mobileTarget = report.Context?.Platform == EvalPlatform.Android ||
                                report.Context?.Platform == EvalPlatform.iOS;
            var connectButton = new Button(_onConnectDevice)
            {
                text = connecting ? "Connecting..." : mobileTarget
                    ? "Connect Device (Android/iOS USB)"
                    : "Select Android or iOS to connect",
                tooltip = mobileTarget
                    ? "Pulls the live QA snapshot from the connected device over USB. "
                    + "Android uses adb (USB debugging on); iOS uses iproxy from libimobiledevice (device unlocked + trusted). "
                    + "Keep the game foregrounded on the device while connecting."
                    : "The device bridge is available only for Android and iOS build targets.",
            };
            connectButton.AddToClassList("sorolla-button-small");
            connectButton.SetEnabled(mobileTarget && !connecting);
            actionsRow.Add(connectButton);

            // Copy the AUDITABLE canonical report: the readable rendering carries every row's
            // disposition/requirement/proof plus a build fingerprint (including the receipt-bound SDK commit), so a
            // pasted result is unambiguous - and it includes the rows this view filters out, which is what
            // makes a single studio-curated window safe to ship.
            var copyButton = new Button(() =>
                    EditorGUIUtility.systemCopyBuffer =
                        GreenlightReportExport.ToText(report.Health, report.Fingerprint, report.Context))
                { text = "Copy Report" };
            copyButton.AddToClassList("sorolla-button-small");
            actionsRow.Add(copyButton);

            container.Add(actionsRow);
            _headerActionsHost.Add(container);
        }

        /// <summary>One group section in the shared section-header style: foldout arrow + title + rule +
        /// status detail + optional action button + status pill, above a collapsible rows container.
        /// Effective status = the vendor's own state, escalated (never downgraded) by the worst VISIBLE row.
        /// Only the arrow toggles folding, so the header's Edit/Console/Install button stays a plain click.</summary>
        VisualElement BuildGroupSection(GroupModel group, List<GreenlightEvaluator.Row> visibleRows,
            List<VisualElement> rowElements)
        {
            string title = group.Title;
            string detail;
            string badgeText;
            StatusBadge.Severity badgeSeverity;
            string actionLabel = null;
            Action action = null;
            bool actionEnabled = true;

            RowStatus rowsWorst = WorstOfRows(visibleRows);
            if (group.Status != null)
            {
                VendorStatus own = group.Status;
                detail = own.Text;
                actionLabel = own.ActionLabel;
                action = own.Action;
                actionEnabled = own.ActionEnabled;
                if (own.Optional) title = $"{title} (optional)";

                (badgeText, badgeSeverity) = own.State switch
                {
                    VendorStatus.Phase.Installing => ("INSTALLING", StatusBadge.Severity.Wait),
                    VendorStatus.Phase.NotInstalled => ("NOT INSTALLED", StatusBadge.Severity.Gated),
                    VendorStatus.Phase.Disabled => ("DISABLED", StatusBadge.Severity.Gated),
                    VendorStatus.Phase.Fail => BadgeFor(RowStatus.Fail),
                    VendorStatus.Phase.Warn => BadgeFor(RowStatus.Warn),
                    _ => BadgeFor(rowsWorst), // Pass: visible rows may escalate, never downgrade
                };
            }
            else
            {
                (badgeText, badgeSeverity) = BadgeFor(rowsWorst);
                int issues = visibleRows.Count(r => r.Status != RowStatus.Pass && r.Status != RowStatus.Info);
                // A lone item doesn't need a number (round-3 ruling) - "1 need attention" reads oddly.
                detail = issues == 0 ? null : issues == 1 ? "Needs attention" : $"{issues} need attention";
            }

            bool attention = badgeSeverity != StatusBadge.Severity.Pass && badgeSeverity != StatusBadge.Severity.Gated;
            string persistKey = group.Id.ToString();
            bool expanded = _groupExpanded.TryGetValue(persistKey, out bool remembered) ? remembered : attention;

            var container = new VisualElement();
            container.style.marginBottom = 8;

            var headerRow = new VisualElement();
            headerRow.AddToClassList("sorolla-section-header");
            headerRow.AddToClassList("sorolla-section-header-foldable");

            var arrow = new Label(expanded ? "▾" : "▸");
            arrow.AddToClassList("sorolla-section-header-arrow");
            arrow.pickingMode = PickingMode.Position;
            headerRow.Add(arrow);

            var titleLabel = new Label(title.ToUpperInvariant());
            titleLabel.AddToClassList("sorolla-section-header-label");
            headerRow.Add(titleLabel);

            var rule = new VisualElement();
            rule.AddToClassList("sorolla-section-header-rule");
            headerRow.Add(rule);

            if (!string.IsNullOrEmpty(detail))
            {
                var detailLabel = new Label(detail);
                detailLabel.AddToClassList("sorolla-type-small");
                detailLabel.style.marginRight = 6;
                headerRow.Add(detailLabel);
            }

            if (!string.IsNullOrEmpty(actionLabel))
            {
                var button = new Button(() => action?.Invoke()) { text = actionLabel };
                button.AddToClassList("sorolla-button-small");
                button.style.marginRight = 6;
                button.SetEnabled(actionEnabled);
                // The whole header row folds the group, so the action must opt out: clicking Edit /
                // Console / Install performs its action without also folding.
                button.RegisterCallback<ClickEvent>(e => e.StopPropagation());
                headerRow.Add(button);
            }

            headerRow.Add(StatusBadge.Create(badgeText, badgeSeverity));
            container.Add(headerRow);

            var rowsWrap = new VisualElement();
            rowsWrap.style.marginLeft = 20;
            rowsWrap.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (VisualElement row in rowElements)
                rowsWrap.Add(row);
            container.Add(rowsWrap);

            headerRow.RegisterCallback<ClickEvent>(_ =>
            {
                bool nowExpanded = rowsWrap.style.display == DisplayStyle.None;
                rowsWrap.style.display = nowExpanded ? DisplayStyle.Flex : DisplayStyle.None;
                arrow.text = nowExpanded ? "▾" : "▸";
                _groupExpanded[persistKey] = nowExpanded;
            });

            return container;
        }

        /// <summary>A check row plus any in-editor remedy control for that gate. Every row here is
        /// machine-checked and carries real fix text - the manual attestation rows (and their Attest/Open
        /// Dashboard affordances) were deleted 2026-07-22.</summary>
        VisualElement BuildRow(GreenlightEvaluator.Row row, GreenlightAdapter.VendorGroup group)
        {
            var container = new VisualElement();

            // Pass rows suppress Fix text and remedy buttons entirely (product-audit finding F4,
            // 2026-07-21): a green row with mandatory "Fix:" homework and an action button pointing at
            // nothing-to-act-on is the glyph-vs-text contradiction family. (The GA credential probe's
            // platform-registration caveat rides in that row's own message rather than as fix text,
            // precisely so a passing row can still state what it did not prove.) Info rows - a deliberate
            // skip or absence - get the same treatment: a skip is not a caveat to resolve.
            bool isPass = row.Status == RowStatus.Pass || row.Status == RowStatus.Info;
            container.Add(CheckRow.Create(TrimGroupPrefix(row.Label, group), row.Status, row.Detail,
                isPass ? null : row.Fix));

            // No per-row "Open GA/FB Settings" buttons: every row that had one sits under a group header
            // whose Edit button performs the identical action (the duplicate affordance was noise).

            // The device snapshot row's remedy is the Connect Device button in the window header - one home,
            // no per-row duplicate. The row still surfaces the failure REASON: Connect attempts used to fail
            // completely silently (F3), every failure path writing DetailMessage with nothing rendering it.
            if (row.GateId == GateIds.DeviceVitals && !isPass)
            {
                bool settledWithoutSnapshot = _snapshotState.Phase == GreenlightDeviceSnapshot.Phase.Done &&
                                              _snapshotState.Outcome != GreenlightDeviceSnapshot.Outcome.Parsed;
                if (settledWithoutSnapshot && !string.IsNullOrEmpty(_snapshotState.DetailMessage))
                    container.Add(CheckRow.SubLine(_snapshotState.DetailMessage));
            }

            // Sandbox mode is the one check whose remedy is a single boolean, so the control belongs ON the
            // row rather than in the vendor's field list further down. Rendered on a passing row too, so
            // turning sandbox ON for a verification run is possible from here as well.
            if (row.GateId == GateIds.BuildAdjustSandboxMode)
                container.Add(ConfigInputsView.SandboxModeToggle());

            // Mode Consistency's fix is literally this window's own hero-header mode switch (F6), so render
            // it as a row action instead of prose alone. The switch always targets "the other mode" (only
            // two exist), so it is correct whichever direction this row's issue points.
            if (row.GateId == GateIds.BuildModeConsistency && !isPass)
                container.Add(RowAction("Switch Mode", _onModeSwitch, enabled: !EditorApplication.isPlaying));

            // Report Integrity is the synthetic row, and the ONLY row with no gate id (a schema/contract
            // error rather than a gate result). Its fix says "report it to Sorolla" with no channel to do
            // so (F13.9), so point it straight at the issue tracker.
            if (row.GateId == null && !isPass)
                container.Add(RowAction("Report Issue", () => Application.OpenURL(FooterLinks.IssuesUrl)));

            return container;
        }

        static Button RowAction(string label, Action action, bool enabled = true)
        {
            var button = new Button(action) { text = label };
            button.AddToClassList("sorolla-button-small");
            button.AddToClassList("sorolla-check-row-action");
            button.SetEnabled(enabled);
            return button;
        }

        /// <summary>The ONE pure view filter: every row that needs attention, plus the
        /// <see cref="VisibleWhenPassing"/> whitelist. Every remaining row is machine-checked and
        /// studio-actionable, so there is no per-row exception left.</summary>
        static bool RowVisible(GreenlightEvaluator.Row row) =>
            row.Status != RowStatus.Pass && row.Status != RowStatus.Info ||
            VisibleWhenPassing.Contains(row.GateId);

        /// <summary>Header pill for a group's effective status, in the report's own vocabulary.</summary>
        static (string text, StatusBadge.Severity severity) BadgeFor(RowStatus status) => status switch
        {
            RowStatus.Fail => ("ERROR", StatusBadge.Severity.Fail),
            RowStatus.Warn => ("WARN", StatusBadge.Severity.Advisory),
            RowStatus.Wait => ("INCOMPLETE", StatusBadge.Severity.Wait),
            _ => ("GREEN", StatusBadge.Severity.Pass), // Pass and Info both read as clean at group level
        };

        /// <summary>Worst status among a set of rows - the ONE place this is computed, fed by whatever the
        /// caller already filtered to be visible. Computing it from the pre-filtered list (not a separate
        /// side-channel query) is what keeps a header from ever contradicting what is rendered below it.</summary>
        static RowStatus WorstOfRows(IEnumerable<GreenlightEvaluator.Row> rows)
        {
            RowStatus worst = RowStatus.Pass;
            foreach (GreenlightEvaluator.Row r in rows)
            {
                if (r.Status == RowStatus.Fail) return RowStatus.Fail; // can't get worse
                if (r.Status == RowStatus.Warn) worst = RowStatus.Warn;
                else if (r.Status == RowStatus.Wait && worst != RowStatus.Warn) worst = RowStatus.Wait;
            }
            return worst;
        }

        /// <summary>Builds the group list from evaluator rows plus config state - the single data model the
        /// render reads. Grouping key is the gate's own catalog category, never label string-matching.</summary>
        List<GroupModel> BuildGroups(GreenlightEvaluator.Report report)
        {
            var grouped = new Dictionary<GreenlightAdapter.VendorGroup, List<GreenlightEvaluator.Row>>();
            foreach (GreenlightEvaluator.Row row in report.Rows)
            {
                GreenlightAdapter.VendorGroup id = GreenlightAdapter.GroupFor(row.GateId);
                if (!grouped.TryGetValue(id, out List<GreenlightEvaluator.Row> list))
                    grouped[id] = list = new List<GreenlightEvaluator.Row>();
                list.Add(row);
            }

            var groups = new List<GroupModel>();
            foreach (GreenlightAdapter.VendorGroup id in GroupOrder)
            {
                if (!GroupApplies(id, report.Context))
                    continue;

                grouped.TryGetValue(id, out List<GreenlightEvaluator.Row> rows);
                groups.Add(new GroupModel
                {
                    Id = id,
                    Title = GroupTitle(id),
                    Rows = rows ?? new List<GreenlightEvaluator.Row>(),
                    // Inputs are built BEFORE the status: the Adjust/TikTok statuses offer a "focus that
                    // field" action, and the fields have to exist for it to point at anything.
                    Inputs = _configInputs.BuildFor(id),
                    Status = _vendorStatus.For(id),
                });
            }
            return groups;
        }

        static bool GroupApplies(
            GreenlightAdapter.VendorGroup group,
            EvaluationContext context)
        {
            if (group != GreenlightAdapter.VendorGroup.Adjust || context == null)
                return true;

            CapabilityState adjust = CapabilityPolicy.Resolve(
                context.Mode,
                context.InstalledModules,
                SdkModule.Adjust,
                CapabilityRule.FullOnly);
            return adjust.Required || adjust.Applicable;
        }

        /// <summary>Child rows inside a vendor group drop the redundant vendor name from their own label -
        /// "GameAnalytics Platform Keys" reads as "Platform Keys" once indented under a "GameAnalytics"
        /// header. Display-only: the gate id and the exported report keep the full name.</summary>
        static string TrimGroupPrefix(string label, GreenlightAdapter.VendorGroup group)
        {
            string prefix = group switch
            {
                GreenlightAdapter.VendorGroup.GameAnalytics => "GameAnalytics ",
                GreenlightAdapter.VendorGroup.Facebook => "Facebook ",
                GreenlightAdapter.VendorGroup.Firebase => "Firebase ",
                GreenlightAdapter.VendorGroup.AppLovinMax => "MAX ",
                GreenlightAdapter.VendorGroup.Adjust => "Adjust ",
                _ => null,
            };
            return !string.IsNullOrEmpty(prefix) && !string.IsNullOrEmpty(label) && label.StartsWith(prefix)
                ? label.Substring(prefix.Length)
                : label;
        }

        static string GroupTitle(GreenlightAdapter.VendorGroup group) => group switch
        {
            GreenlightAdapter.VendorGroup.GameAnalytics => "GameAnalytics",
            GreenlightAdapter.VendorGroup.Facebook => "Facebook",
            GreenlightAdapter.VendorGroup.Firebase => "Firebase",
            GreenlightAdapter.VendorGroup.AppLovinMax => "AppLovin MAX",
            GreenlightAdapter.VendorGroup.Adjust => "Adjust",
            GreenlightAdapter.VendorGroup.BuildAndProject => "Build & Project",
            GreenlightAdapter.VendorGroup.DeviceAndQa => "Device & QA",
            GreenlightAdapter.VendorGroup.TikTok => "TikTok (optional)",
            _ => group.ToString(),
        };

        /// <summary>One group: title, the vendor's own status (null for the non-vendor groups), every check
        /// row the catalog routed here (the view filter applies later, at render time), and every config
        /// input (rendered unconditionally - the filter never touches inputs, only check rows).</summary>
        sealed class GroupModel
        {
            internal GreenlightAdapter.VendorGroup Id;
            internal string Title;
            internal List<GreenlightEvaluator.Row> Rows = new List<GreenlightEvaluator.Row>();
            internal List<VisualElement> Inputs = new List<VisualElement>();
            internal VendorStatus Status;
        }
    }
}
