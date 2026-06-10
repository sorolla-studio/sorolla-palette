using System;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;

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
            Assert.AreEqual(expected, DecodeJwsEnvironment(BuildJws($@"{{""environment"":""{environment}""}}")));
        }

        [Test]
        public void DecodeJwsEnvironment_UnknownClaimReturnsUnknown()
        {
            Assert.AreEqual("unknown", DecodeJwsEnvironment(BuildJws(@"{""environment"":""QA""}")));
        }

        [Test]
        public void DecodeJwsEnvironment_MalformedJwsReturnsNull()
        {
            Assert.IsNull(DecodeJwsEnvironment("header.not-base64.signature"));
        }

        [Test]
        public void DecodeJwsEnvironment_MissingClaimReturnsNull()
        {
            Assert.IsNull(DecodeJwsEnvironment(BuildJws(@"{}")));
        }

        static string DecodeJwsEnvironment(string jws)
        {
            Type paletteType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("Sorolla.Palette.Palette"))
                .FirstOrDefault(type => type != null);

            Assert.NotNull(paletteType, "Palette type should be loaded.");

            MethodInfo decode = paletteType.GetMethod("DecodeJwsEnvironment", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(decode, "Palette.DecodeJwsEnvironment should exist.");

            return (string)decode.Invoke(null, new object[] { jws });
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
