using System;
using System.Text;
using UnityEngine;
using Sorolla.Palette.Adapters;

namespace Sorolla.Palette.Purchasing
{
    /// <summary>
    ///     Decodes and normalizes the client-observed store environment (production/sandbox/xcode)
    ///     for a purchase. Pure functions, no dependency on Unity IAP types.
    /// </summary>
    internal static class StoreEnvironmentResolver
    {
        const string Tag = "[Palette:StoreEnvironmentResolver]";

        [Serializable] class JwsEnvironmentClaim { public string environment; }

        /// <summary>
        ///     Apple's jwsRepresentation is a JWS compact token (header.payload.signature, base64url).
        ///     The decoded payload JSON carries an `environment` claim: "Production"|"Sandbox"|"Xcode"
        ///     (App Store Server API environment field). This is a client-observed label only: we do
        ///     not verify the signature here, and canonical revenue still needs server-side / Adjust
        ///     verification. Returns a bounded lower-case value, or null if absent/undecodable.
        /// </summary>
        public static string DecodeJwsEnvironment(string jws)
        {
            if (string.IsNullOrEmpty(jws)) return null;
            string[] parts = jws.Split('.');
            if (parts.Length < 2 || string.IsNullOrEmpty(parts[1])) return null;
            try
            {
                string payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
                JwsEnvironmentClaim claim = JsonUtility.FromJson<JwsEnvironmentClaim>(payloadJson);
                string env = claim?.environment;
                return string.IsNullOrEmpty(env) ? null : NormalizeStoreEnvironment(env);
            }
            catch (Exception e)
            {
                PaletteLog.Verbose($"{Tag} Could not decode JWS environment claim: {e.Message}");
                return null;
            }
        }

        public static string NormalizeStoreEnvironment(string storeEnvironment)
        {
            if (string.IsNullOrEmpty(storeEnvironment)) return "unknown";

            string normalized = storeEnvironment.Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "production":
                case "sandbox":
                case "xcode":
                    return normalized;
                default:
                    return "unknown";
            }
        }

        static byte[] Base64UrlDecode(string input)
        {
            string s = input.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4)
            {
                case 2: s += "=="; break;
                case 3: s += "="; break;
            }
            return Convert.FromBase64String(s);
        }
    }
}
