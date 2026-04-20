using System;
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
        ///     Typed economy tracking. Replaces <see cref="TrackResource"/>.
        ///     Curated <see cref="CurrencyId"/> + <see cref="EconomySource"/> / <see cref="EconomySink"/>
        ///     so cross-game analytics aggregate correctly and typos are impossible.
        /// </summary>
        public static class Economy
        {
            /// <summary>Track currency earned. Fires earn_virtual_currency (Firebase) / GA Source event.</summary>
            public static void Earn(CurrencyId currency, int amount, EconomySource source, string itemId = null)
                => Track(flowSource: true, currency, amount, EnumToSnake(source), source == EconomySource.Other, itemId);

            /// <summary>Track currency spent. Fires spend_virtual_currency (Firebase) / GA Sink event.</summary>
            public static void Spend(CurrencyId currency, int amount, EconomySink sink, string itemId = null)
                => Track(flowSource: false, currency, amount, EnumToSnake(sink), sink == EconomySink.Other, itemId);

            static void Track(bool flowSource, CurrencyId currency, int amount, string category, bool isOther, string itemId)
            {
                if (!EnsureInit()) return;
                string verb = flowSource ? "Earn" : "Spend";

                if (amount <= 0)
                {
                    Debug.LogWarning($"{Tag} Economy.{verb}: amount={amount} must be > 0. Event dropped.");
                    return;
                }

                if (isOther)
                    Debug.LogWarning($"{Tag} Economy.{verb} used '{category}' category (itemId='{itemId}'). Consider adding a curated Economy{(flowSource ? "Source" : "Sink")} enum value in a patch release.");

                string currencyName = EnumToSnake(currency);
                string effectiveItemId = string.IsNullOrWhiteSpace(itemId) ? category : Sanitize(itemId);

#if GAMEANALYTICS_INSTALLED
                GAResourceFlowType gaFlow = flowSource ? GAResourceFlowType.Source : GAResourceFlowType.Sink;
                GameAnalyticsAdapter.TrackResourceEvent(gaFlow, currencyName, amount, category, effectiveItemId);
#else
                GameAnalyticsAdapter.TrackResourceEvent(flowSource ? "source" : "sink", currencyName, amount, category, effectiveItemId);
#endif

#if FIREBASE_ANALYTICS_INSTALLED
                FirebaseAdapter.TrackResourceEvent(flowSource ? "source" : "sink", currencyName, amount, category, effectiveItemId, null);
#endif
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

            static string Sanitize(string s) => s.Trim().ToLowerInvariant().Replace(' ', '_');
        }
    }
}
