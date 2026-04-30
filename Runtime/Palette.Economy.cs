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

    public static partial class Palette
    {
        /// <summary>
        ///     Typed economy tracking. Curated <see cref="CurrencyId"/> + <see cref="EconomySource"/>
        ///     / <see cref="EconomySink"/> so cross-game analytics aggregate correctly and typos are impossible.
        /// </summary>
        public static class Economy
        {
            /// <summary>Track currency earned. Fires earn_virtual_currency (Firebase) / GA Source event.</summary>
            public static void Earn(CurrencyId currency, int amount, EconomySource source, string itemId = null,
                Dictionary<string, object> extraParams = null)
                => Track(flowSource: true, currency, amount, EnumToSnake(source), source == EconomySource.Other,
                    itemId, extraParams);

            /// <summary>Track currency spent. Fires spend_virtual_currency (Firebase) / GA Sink event.</summary>
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
                string effectiveItemId = string.IsNullOrWhiteSpace(itemId) ? category : Sanitize(itemId);

                QueueOrExecute(() =>
                {
#if GAMEANALYTICS_INSTALLED
                    GAResourceFlowType gaFlow = flowSource ? GAResourceFlowType.Source : GAResourceFlowType.Sink;
                    GameAnalyticsAdapter.TrackResourceEvent(gaFlow, currencyName, amount, category, effectiveItemId);
#else
                    GameAnalyticsAdapter.TrackResourceEvent(flowSource ? "source" : "sink", currencyName, amount, category, effectiveItemId);
#endif

#if FIREBASE_ANALYTICS_INSTALLED
                    FirebaseAdapter.TrackResourceEvent(flowSource ? "source" : "sink", currencyName, amount,
                        category, effectiveItemId, extraParams);
#endif
                });
            }

            static string EnumToSnake(Enum e)
            {
                string s = e.ToString();
                var sb = new StringBuilder(s.Length + 4);
                for (int i = 0; i < s.Length; i++)
                {
                    if (i > 0 && char.IsUpper(s[i])) sb.Append('_');
                    sb.Append(char.ToLowerInvariant(s[i]));
                }
                return sb.ToString();
            }

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
