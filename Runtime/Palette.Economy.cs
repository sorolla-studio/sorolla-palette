using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Sorolla.Palette.Adapters;
#if GAMEANALYTICS_INSTALLED
using GameAnalyticsSDK;
#endif

namespace Sorolla.Palette
{
    /// <summary>
    ///     In-game currency. Curated by Sorolla. Add a new value via SDK PR when a game
    ///     introduces a currency that isn't listed - fails at compile time rather than
    ///     silently fragmenting analytics with typo'd strings.
    /// </summary>
    public enum CurrencyId
    {
        Coins,
        Gems,
        Stars,
        Energy,
        Lives,
        Other,
    }

    /// <summary>
    ///     Source category for <see cref="Palette.Economy.Earn"/>. Curated by Sorolla so
    ///     cross-game analytics aggregate correctly. Use <see cref="Other"/> if no existing
    ///     category fits - logs a warning so the taxonomy can be extended in a patch release.
    /// </summary>
    public enum EconomySource
    {
        LevelReward,
        DailyBonus,
        AdReward,
        IapGrant,
        Achievement,
        Gift,
        Starter,
        Other,
    }

    /// <summary>
    ///     Sink category for <see cref="Palette.Economy.Spend"/>. Curated by Sorolla so
    ///     cross-game analytics aggregate correctly. Use <see cref="Other"/> if no existing
    ///     category fits - logs a warning so the taxonomy can be extended in a patch release.
    /// </summary>
    public enum EconomySink
    {
        Booster,
        Continue,
        Unlock,
        Cosmetic,
        ShopPurchase,
        Upgrade,
        Other,
    }

    /// <summary>
    ///     The exact strings Palette emits to GameAnalytics for economy events: the currency slot and the
    ///     item-type slot. GameAnalytics silently drops any resource event whose currency or item type is
    ///     absent from its own whitelists, so the editor check that compares a project's whitelists against
    ///     what will actually be sent reads this list rather than re-deriving the naming convention.
    /// </summary>
    internal static class EconomyVocabulary
    {
        internal static string[] Currencies() => Names(typeof(CurrencyId));

        internal static string[] ItemTypes()
        {
            string[] sources = Names(typeof(EconomySource));
            string[] sinks = Names(typeof(EconomySink));
            var all = new List<string>(sources.Length + sinks.Length);
            all.AddRange(sources);
            foreach (string sink in sinks)
                if (!all.Contains(sink))
                    all.Add(sink);
            return all.ToArray();
        }

        internal static string ToSnake(Enum value)
        {
            string s = value.ToString();
            var sb = new StringBuilder(s.Length + 4);
            for (int i = 0; i < s.Length; i++)
            {
                if (i > 0 && char.IsUpper(s[i])) sb.Append('_');
                sb.Append(char.ToLowerInvariant(s[i]));
            }
            return sb.ToString();
        }

        static string[] Names(Type enumType)
        {
            Array values = Enum.GetValues(enumType);
            var names = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
                names[i] = ToSnake((Enum)values.GetValue(i));
            return names;
        }
    }

    public static partial class Palette
    {
        /// <summary>
        ///     Typed economy tracking. Curated <see cref="CurrencyId"/> + <see cref="EconomySource"/>
        ///     / <see cref="EconomySink"/> so cross-game analytics aggregate correctly and typos are impossible.
        /// </summary>
        public static class Economy
        {
            static readonly HashSet<string> s_reservedFirebaseEconomyParams = new HashSet<string>
            {
                "virtual_currency_name",
                "value",
                "source",
                "source_item",
                "item_name",
                "sink",
            };

            /// <summary>Track currency earned. Fires earn_virtual_currency (Firebase) / GameAnalytics Source event.</summary>
            /// <param name="extraParams">Optional structured params. Sent to Firebase only - GameAnalytics receives the curated resource fields (currency, amount, source, itemId), not these.</param>
            public static void Earn(CurrencyId currency, int amount, EconomySource source, string itemId = null,
                Dictionary<string, object> extraParams = null)
                => Track(flowSource: true, currency, amount, EnumToSnake(source), source == EconomySource.Other,
                    itemId, extraParams);

            /// <summary>Track currency spent. Fires spend_virtual_currency (Firebase) / GameAnalytics Sink event.</summary>
            /// <param name="extraParams">Optional structured params. Sent to Firebase only - GameAnalytics receives the curated resource fields (currency, amount, sink, itemId), not these.</param>
            public static void Spend(CurrencyId currency, int amount, EconomySink sink, string itemId = null,
                Dictionary<string, object> extraParams = null)
                => Track(flowSource: false, currency, amount, EnumToSnake(sink), sink == EconomySink.Other,
                    itemId, extraParams);

            static void Track(bool flowSource, CurrencyId currency, int amount, string category, bool isOther,
                string itemId, Dictionary<string, object> extraParams)
            {
                string verb = flowSource ? "Earn" : "Spend";

                if (amount <= 0)
                {
                    PaletteLog.Warning($"{Tag} Economy.{verb}: amount={amount} must be > 0. Event dropped.");
                    return;
                }

                if (isOther)
                    PaletteLog.Warning($"{Tag} Economy.{verb} used '{category}' category (itemId='{itemId}'). Consider adding a curated Economy{(flowSource ? "Source" : "Sink")} enum value in a patch release.");

                string currencyName = EnumToSnake(currency);
                // Sanitize once. sanitizedItemId is null when the caller didn't provide one OR
                // when Sanitize stripped the input to empty (e.g. itemId="___"). Preserved nullable
                // for Firebase so it emits the granular slot only when the game actually supplied
                // usable content. effectiveItemId synthesizes from the category for GameAnalytics,
                // whose native call rejects empty strings.
                string sanitizedItemId = string.IsNullOrWhiteSpace(itemId) ? null : Sanitize(itemId);
                if (string.IsNullOrEmpty(sanitizedItemId)) sanitizedItemId = null;
                string effectiveItemId = sanitizedItemId ?? category;
                Dictionary<string, object> allowedExtraParams = FilterReservedEconomyExtraParams(extraParams);

                QueueOrExecute(() =>
                {
                    SorollaDiagnostics.RecordEconomy(flowSource);
                    var diagnosticParams = allowedExtraParams != null
                        ? new Dictionary<string, object>(allowedExtraParams)
                        : new Dictionary<string, object>();
                    diagnosticParams["virtual_currency_name"] = currencyName;
                    diagnosticParams["value"] = amount;
                    if (flowSource)
                    {
                        diagnosticParams["source"] = category;
                        if (sanitizedItemId != null)
                            diagnosticParams["source_item"] = sanitizedItemId;
                    }
                    else
                    {
                        if (sanitizedItemId != null)
                            diagnosticParams["item_name"] = sanitizedItemId;
                        diagnosticParams["sink"] = category;
                    }
                    SorollaDiagnostics.RecordEventDispatch("economy",
                        flowSource ? "earn_virtual_currency" : "spend_virtual_currency",
                        diagnosticParams);

#if GAMEANALYTICS_INSTALLED
                    GAResourceFlowType gaFlow = flowSource ? GAResourceFlowType.Source : GAResourceFlowType.Sink;
                    GameAnalyticsAdapter.TrackResourceEvent(gaFlow, currencyName, amount, category, effectiveItemId);
#else
                    GameAnalyticsAdapter.TrackResourceEvent(flowSource ? "source" : "sink", currencyName, amount, category, effectiveItemId);
#endif

#if FIREBASE_ANALYTICS_INSTALLED
                    FirebaseAdapter.TrackResourceEvent(flowSource ? "source" : "sink", currencyName, amount,
                        category, sanitizedItemId, allowedExtraParams);
#endif
                });
            }

            static Dictionary<string, object> FilterReservedEconomyExtraParams(Dictionary<string, object> extraParams)
            {
                if (extraParams == null || extraParams.Count == 0) return null;

                Dictionary<string, object> allowed = null;
                foreach (KeyValuePair<string, object> kvp in extraParams)
                {
                    string sanitizedKey = EventNameSanitizer.SanitizeParamName(kvp.Key);
                    if (sanitizedKey == null) continue;

                    if (s_reservedFirebaseEconomyParams.Contains(sanitizedKey))
                    {
                        PaletteLog.Warning($"{Tag} Economy extra param '{kvp.Key}' is reserved for Sorolla's Firebase economy schema and was rejected. Use the source/sink argument or itemId instead.");
                        continue;
                    }

                    allowed ??= new Dictionary<string, object>();
                    allowed[kvp.Key] = kvp.Value;
                }

                return allowed;
            }

            static string EnumToSnake(Enum e) => EconomyVocabulary.ToSnake(e);

            static string Sanitize(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return string.Empty;

                string trimmed = s.Trim();
                var sb = new StringBuilder(trimmed.Length + 4);
                bool previousWasUnderscore = false;

                for (int i = 0; i < trimmed.Length; i++)
                {
                    char c = trimmed[i];
                    if (char.IsLetterOrDigit(c))
                    {
                        if (ShouldInsertWordBoundary(trimmed, i) && !previousWasUnderscore)
                        {
                            sb.Append('_');
                        }

                        sb.Append(char.ToLowerInvariant(c));
                        previousWasUnderscore = false;
                    }
                    else if (sb.Length > 0 && !previousWasUnderscore)
                    {
                        sb.Append('_');
                        previousWasUnderscore = true;
                    }
                }

                if (previousWasUnderscore && sb.Length > 0)
                    sb.Length--;

                return sb.ToString();
            }

            static bool ShouldInsertWordBoundary(string value, int index)
            {
                if (index <= 0 || !char.IsUpper(value[index]))
                    return false;

                char previous = value[index - 1];
                if (!char.IsLetterOrDigit(previous))
                    return false;

                if (char.IsLower(previous) || char.IsDigit(previous))
                    return true;

                return char.IsUpper(previous)
                       && index + 1 < value.Length
                       && char.IsLower(value[index + 1]);
            }
        }
    }
}
