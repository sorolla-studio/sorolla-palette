using System.Globalization;
using System.Text;

namespace Sorolla.Palette
{
    /// <summary>
    ///     Minimal hand-rolled JSON writer for the QA bridge snapshot. No external dependency
    ///     (Newtonsoft exists in games but is not assumed in the SDK). The snapshot is a fixed,
    ///     known shape, so callers append members directly and manage commas with
    ///     <see cref="Comma"/>; that keeps optional/conditional fields safe without a tree model.
    /// </summary>
    internal static class QaJson
    {
        /// <summary>Writes a comma before every member except the first one in the current object/array.</summary>
        internal static void Comma(StringBuilder sb, ref bool first)
        {
            if (!first) sb.Append(',');
            first = false;
        }

        /// <summary>Appends <c>"key":</c>.</summary>
        internal static void Key(StringBuilder sb, string key)
        {
            AppendString(sb, key);
            sb.Append(':');
        }

        /// <summary>Appends a string member: <c>"key":"value"</c> (value escaped, null becomes JSON null).</summary>
        internal static void StringMember(StringBuilder sb, ref bool first, string key, string value)
        {
            Comma(sb, ref first);
            Key(sb, key);
            AppendString(sb, value);
        }

        /// <summary>Appends a boolean member: <c>"key":true</c>.</summary>
        internal static void BoolMember(StringBuilder sb, ref bool first, string key, bool value)
        {
            Comma(sb, ref first);
            Key(sb, key);
            sb.Append(value ? "true" : "false");
        }

        /// <summary>Appends an integer member: <c>"key":42</c>.</summary>
        internal static void IntMember(StringBuilder sb, ref bool first, string key, long value)
        {
            Comma(sb, ref first);
            Key(sb, key);
            sb.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>Appends a numeric member: <c>"key":0.038</c>.</summary>
        internal static void DoubleMember(StringBuilder sb, ref bool first, string key, double value)
        {
            Comma(sb, ref first);
            Key(sb, key);
            sb.Append(value.ToString("0.######", CultureInfo.InvariantCulture));
        }

        /// <summary>Appends a JSON string literal (escaped), or <c>null</c> when <paramref name="value"/> is null.</summary>
        internal static void AppendString(StringBuilder sb, string value)
        {
            if (value == null)
            {
                sb.Append("null");
                return;
            }

            sb.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
