using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Sorolla.Palette.Editor.Tests
{
    [TestFixture]
    public class EconomyItemIdSanitizerTests
    {
        [TestCase("OilCapacity", "oil_capacity")]
        [TestCase("BoatUpgrade", "boat_upgrade")]
        [TestCase("MoneyEarn", "money_earn")]
        [TestCase("SlingshotPower", "slingshot_power")]
        [TestCase("APIKeyReward", "api_key_reward")]
        [TestCase("boat_3", "boat_3")]
        [TestCase("Triple Reward Bonus", "triple_reward_bonus")]
        [TestCase("shop.reward:bonus", "shop_reward_bonus")]
        public void EconomyItemIdsNormalizeToSnakeCase(string input, string expected)
        {
            Assert.AreEqual(expected, Sanitize(input));
        }

        static string Sanitize(string value)
        {
            Type economyType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("Sorolla.Palette.Palette+Economy"))
                .FirstOrDefault(type => type != null);

            Assert.NotNull(economyType, "Palette.Economy type should be loaded.");

            MethodInfo sanitize = economyType.GetMethod("Sanitize", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(sanitize, "Palette.Economy.Sanitize should exist.");

            return (string)sanitize.Invoke(null, new object[] { value });
        }
    }
}
