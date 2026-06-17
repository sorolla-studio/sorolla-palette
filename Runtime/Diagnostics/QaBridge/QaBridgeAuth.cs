using System;
using System.Net;

namespace Sorolla.Palette
{
    /// <summary>
    ///     Single-rule QA bridge password gate. The password is intentionally code-owned instead of
    ///     inspector-owned so game builds cannot accidentally ship with an edited/blank value.
    /// </summary>
    internal static class QaBridgeAuth
    {
        internal const string HeaderName = "X-Sorolla-QA-Password";
        internal const string QueryName = "qa_password";
        internal const string ForbiddenJson = "{\"ok\":false,\"detail\":\"qa_password_required\"}";

        const string ExpectedPassword = "BbooN5RPF3aBRR6f5Vxz5pPhYXATlGAr";
        const string BearerPrefix = "Bearer ";

        internal static bool IsAuthorized(HttpListenerRequest request)
        {
            if (request == null) return false;

            string supplied = request.Headers[HeaderName];
            if (string.IsNullOrEmpty(supplied))
                supplied = BearerPassword(request.Headers["Authorization"]);
            if (string.IsNullOrEmpty(supplied))
                supplied = request.QueryString[QueryName];

            return FixedTimeEquals(supplied, ExpectedPassword);
        }

        static string BearerPassword(string authorization)
        {
            if (string.IsNullOrEmpty(authorization)) return null;
            return authorization.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase)
                ? authorization.Substring(BearerPrefix.Length).Trim()
                : null;
        }

        static bool FixedTimeEquals(string left, string right)
        {
            if (left == null || right == null) return false;

            int diff = left.Length ^ right.Length;
            int count = Math.Min(left.Length, right.Length);
            for (int i = 0; i < count; i++)
                diff |= left[i] ^ right[i];

            return diff == 0;
        }
    }
}
