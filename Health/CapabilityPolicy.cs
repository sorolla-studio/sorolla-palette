using System;

namespace Sorolla.Palette.Health
{
    /// <summary>How a supported SDK capability participates in each mode.</summary>
    internal enum CapabilityRule
    {
        Core,
        FullRequired,
        FullOnly,
        Optional,
    }

    /// <summary>
    ///     One resolved applicability decision shared by editor health and runtime Vitals.
    ///     Required describes the mode contract. Included describes the actual package set.
    ///     Dependent checks exist only when Applicable is true.
    /// </summary>
    internal readonly struct CapabilityState
    {
        public readonly bool Required;
        public readonly bool Included;
        public readonly bool Applicable;

        internal CapabilityState(bool required, bool included, bool applicable)
        {
            Required = required;
            Included = included;
            Applicable = applicable;
        }
    }

    /// <summary>
    ///     Pure capability policy. Producers supply trusted mode/package facts; consumers never infer
    ///     applicability from UI labels, validation results, or stale configuration values.
    /// </summary>
    internal static class CapabilityPolicy
    {
        /// <summary>
        ///     The ONE module → rule table. The editor gate catalog and the runtime capability accessors both
        ///     read it, so a vendor cannot be "required in Full" on one surface and optional on the other.
        ///     Unknown modules throw rather than defaulting: a new module must declare its rule here once.
        /// </summary>
        internal static CapabilityRule RuleFor(SdkModule module)
        {
            switch (module)
            {
                case SdkModule.GameAnalytics:
                case SdkModule.Facebook:
                    return CapabilityRule.Core;
                case SdkModule.FirebaseApp:
                case SdkModule.FirebaseAnalytics:
                case SdkModule.FirebaseCrashlytics:
                case SdkModule.FirebaseRemoteConfig:
                case SdkModule.Firebase:
                case SdkModule.AppLovinMax:
                    return CapabilityRule.FullRequired;
                case SdkModule.Adjust:
                    return CapabilityRule.FullOnly;
                case SdkModule.UnityIap:
                    return CapabilityRule.Optional;
                default:
                    throw new ArgumentOutOfRangeException(nameof(module), module,
                        "No capability rule declared for this module.");
            }
        }

        internal static CapabilityState Resolve(EvalMode mode, SdkModule installedModules, SdkModule module) =>
            Resolve(mode, installedModules, module, RuleFor(module));

        internal static CapabilityState Resolve(
            EvalMode mode, SdkModule installedModules, SdkModule module, CapabilityRule rule)
        {
            bool installed = (installedModules & module) != 0;
            bool required = rule == CapabilityRule.Core ||
                            (mode == EvalMode.Full &&
                             (rule == CapabilityRule.FullRequired || rule == CapabilityRule.FullOnly));
            bool allowed = rule != CapabilityRule.FullOnly || mode == EvalMode.Full;
            return new CapabilityState(required, installed, allowed && installed);
        }

        /// <summary>
        ///     Requirement for a check below the package-availability root. Missing required packages are
        ///     reported by the root gate, so their dependent checks are NotApplicable rather than duplicates.
        /// </summary>
        internal static Func<EvaluationContext, RequirementDecision> Dependent(SdkModule module) => context =>
        {
            if (context.Mode == EvalMode.Unknown)
                return new RequirementDecision(Requirement.Unknown, "SDK mode is unknown (no config)");
            if (!context.ModulesResolved)
                return new RequirementDecision(Requirement.Unknown, "package manifest could not be resolved");

            CapabilityState state = Resolve(context.Mode, context.InstalledModules, module);
            if (!state.Applicable)
                return new RequirementDecision(Requirement.NotApplicable,
                    state.Required
                        ? "required package is absent; the package gate owns the failure"
                        : "capability is not included in this mode");

            return new RequirementDecision(
                state.Required ? Requirement.Required : Requirement.Optional,
                state.Required ? "included and required in Full mode" : "included optional capability");
        };

        /// <summary>
        ///     A multi-package suite is inspectable when any part is included in Prototype. In Full, the root
        ///     package gate owns an incomplete suite, so dependent checks start only after every module exists.
        /// </summary>
        internal static Func<EvaluationContext, RequirementDecision> FullSuiteDependent(
            SdkModule suite) => context =>
        {
            if (context.Mode == EvalMode.Unknown)
                return new RequirementDecision(Requirement.Unknown, "SDK mode is unknown (no config)");
            if (!context.ModulesResolved)
                return new RequirementDecision(Requirement.Unknown, "package manifest could not be resolved");

            SdkModule included = context.InstalledModules & suite;
            if (context.Mode == EvalMode.Full)
            {
                if (included != suite)
                    return new RequirementDecision(Requirement.NotApplicable,
                        "required package suite is incomplete; the package gate owns the failure");
                return new RequirementDecision(Requirement.Required,
                    "complete suite included and required in Full mode");
            }

            return included == SdkModule.None
                ? new RequirementDecision(Requirement.NotApplicable,
                    "capability is not included in Prototype")
                : new RequirementDecision(Requirement.Optional,
                    "included optional capability");
        };
    }
}
