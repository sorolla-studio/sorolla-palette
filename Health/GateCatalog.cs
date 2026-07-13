using System;
using System.Collections.Generic;
using System.Linq;

namespace Sorolla.Palette.Health
{
    /// <summary>Stable gate-id vocabulary - string constants, never magic strings. Populated in Cycle 4
    /// when ids are assigned to the Build Health rows and device facts.</summary>
    internal static class GateIds
    {
        // Cycle 4 assigns the canonical ids here.
    }

    /// <summary>
    ///     The one canonical, code-defined gate catalog the SDK ships (not a ScriptableObject or YAML, so it
    ///     is grep/diff/compile-checked and has no optional-asset failure mode - DR-133). Cycle 3 ships the
    ///     shape + <see cref="Validate"/>; Cycle 4 populates <see cref="All"/>. The private gates.yaml
    ///     workflow references the same string ids without any portfolio data shipping here (design note
    ///     section 4).
    /// </summary>
    internal sealed class GateCatalog
    {
        readonly IReadOnlyList<GateDefinition> _definitions;
        readonly Dictionary<string, GateDefinition> _byId;

        internal GateCatalog(IReadOnlyList<GateDefinition> definitions)
        {
            _definitions = definitions ?? Array.Empty<GateDefinition>();
            _byId = _definitions.ToDictionary(d => d.Id, d => d);
        }

        internal IReadOnlyList<GateDefinition> All => _definitions;

        /// <summary>Looks a definition up by id. Throws on an unknown id by default (no silent null); pass
        /// <paramref name="throwIfMissing"/> = false to probe.</summary>
        internal GateDefinition ById(string id, bool throwIfMissing = true)
        {
            if (_byId.TryGetValue(id, out GateDefinition def))
                return def;
            if (throwIfMissing)
                throw new KeyNotFoundException($"No gate definition with id '{id}' in the catalog.");
            return null;
        }

        /// <summary>The shipped canonical catalog. Empty in Cycle 3; ids assigned in Cycle 4.</summary>
        internal static GateCatalog Canonical { get; } = new GateCatalog(Array.Empty<GateDefinition>());

        /// <summary>
        ///     Fails loud on a malformed catalog: a duplicate id, an unreachable definition (no phase), or a
        ///     definition that is never <see cref="Applicability.Applicable"/> under any supported context.
        ///     Returns the list of problems (empty = valid). Pure function so it is unit-testable against
        ///     synthetic catalogs; run as an Editor test over <see cref="Canonical"/> once ids are assigned.
        /// </summary>
        internal static IReadOnlyList<string> Validate(
            IEnumerable<GateDefinition> definitions, IReadOnlyList<EvaluationContext> supportedContexts)
        {
            var problems = new List<string>();
            List<GateDefinition> defs = definitions?.ToList() ?? new List<GateDefinition>();

            foreach (IGrouping<string, GateDefinition> group in defs.GroupBy(d => d.Id))
                if (group.Count() > 1)
                    problems.Add($"Duplicate gate id: '{group.Key}' ({group.Count()} definitions)");

            foreach (GateDefinition def in defs)
            {
                if (def.Phases == GatePhase.None)
                    problems.Add($"Unreachable gate '{def.Id}': no phase (Phases == None)");

                if (def.Applicability != null && supportedContexts != null && supportedContexts.Count > 0 &&
                    supportedContexts.All(ctx => def.Applicability(ctx).Value != Applicability.Applicable))
                    problems.Add($"Unreachable gate '{def.Id}': never Applicable under any supported context");
            }

            return problems;
        }
    }
}
