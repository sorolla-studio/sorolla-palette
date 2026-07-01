using System;
using System.Text;
using NUnit.Framework;
using Sorolla.Palette.Purchasing;

namespace Sorolla.Palette.Editor.Tests
{
    [TestFixture]
    public class PurchaseStoreEnvironmentTests
    {
        [TestCase("Production", "production")]
        [TestCase("Sandbox", "sandbox")]
        [TestCase("Xcode", "xcode")]
        [TestCase(" sandbox ", "sandbox")]
        public void DecodeJwsEnvironment_ReturnsBoundedEnvironment(string environment, string expected)
        {
            Assert.AreEqual(expected, StoreEnvironmentResolver.DecodeJwsEnvironment(BuildJws($@"{{""environment"":""{environment}""}}")));
        }

        [Test]
        public void DecodeJwsEnvironment_UnknownClaimReturnsUnknown()
        {
            Assert.AreEqual("unknown", StoreEnvironmentResolver.DecodeJwsEnvironment(BuildJws(@"{""environment"":""QA""}")));
        }

        [Test]
        public void DecodeJwsEnvironment_MalformedJwsReturnsNull()
        {
            Assert.IsNull(StoreEnvironmentResolver.DecodeJwsEnvironment("header.not-base64.signature"));
        }

        [Test]
        public void DecodeJwsEnvironment_MissingClaimReturnsNull()
        {
            Assert.IsNull(StoreEnvironmentResolver.DecodeJwsEnvironment(BuildJws(@"{}")));
        }

        static string BuildJws(string payloadJson)
        {
            return "e30." + Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson)) + ".sig";
        }

        static string Base64UrlEncode(byte[] bytes)
        {
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }
}
