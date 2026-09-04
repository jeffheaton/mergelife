using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HeatonCAApp
{
    /// <summary>
    /// Minimal JSON for the app's own files (catalog/zoo metadata): objects are
    /// Dictionary&lt;string, object&gt;, arrays List&lt;object&gt;, numbers double, plus
    /// string/bool/null. Culture-invariant both ways. Unity has no System.Text.Json;
    /// this stays dependency-free like the package's test-side reader.
    /// </summary>
    public static class AppJson
    {
        // ---- writing ---------------------------------------------------------------

        public static string Write(object value)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value);
            return sb.ToString();
        }

        private static void WriteValue(StringBuilder sb, object value)
        {
            switch (value)
            {
                case null:
                    sb.Append("null");
                    break;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    break;
                case string s:
                    WriteString(sb, s);
                    break;
                case double d:
                    sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case float f:
                    sb.Append(((double)f).ToString("R", CultureInfo.InvariantCulture));
                    break;
                case int i:
                    sb.Append(i.ToString(CultureInfo.InvariantCulture));
                    break;
                case long l:
                    sb.Append(l.ToString(CultureInfo.InvariantCulture));
                    break;
                case uint u:
                    sb.Append(u.ToString(CultureInfo.InvariantCulture));
                    break;
                case IDictionary<string, object> obj:
                {
                    sb.Append('{');
                    bool first = true;
                    foreach (var pair in obj)
                    {
                        if (!first)
                            sb.Append(',');
                        first = false;
                        WriteString(sb, pair.Key);
                        sb.Append(':');
                        WriteValue(sb, pair.Value);
                    }
                    sb.Append('}');
                    break;
                }
                case IEnumerable<object> list:
                {
                    sb.Append('[');
                    bool first = true;
                    foreach (object item in list)
                    {
                        if (!first)
                            sb.Append(',');
                        first = false;
                        WriteValue(sb, item);
                    }
                    sb.Append(']');
                    break;
                }
                default:
                    throw new ArgumentException($"AppJson cannot serialize {value.GetType().Name}");
            }
        }

        private static void WriteString(StringBuilder sb, string value)
        {
            sb.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ---- parsing ---------------------------------------------------------------

        public static object Parse(string text)
        {
            int pos = 0;
            object value = ParseValue(text, ref pos);
            SkipWhitespace(text, ref pos);
            if (pos != text.Length)
                throw new FormatException($"trailing JSON content at {pos}");
            return value;
        }

        public static Dictionary<string, object> Obj(object o) => (Dictionary<string, object>)o;

        public static List<object> Arr(object o) => (List<object>)o;

        public static string Str(object o) => (string)o;

        public static double Num(object o) => (double)o;

        public static int Int(object o) => (int)(double)o;

        private static object ParseValue(string text, ref int pos)
        {
            SkipWhitespace(text, ref pos);
            char c = text[pos];
            switch (c)
            {
                case '{':
                {
                    var result = new Dictionary<string, object>();
                    pos++;
                    SkipWhitespace(text, ref pos);
                    if (text[pos] == '}')
                    {
                        pos++;
                        return result;
                    }
                    while (true)
                    {
                        SkipWhitespace(text, ref pos);
                        string key = ParseString(text, ref pos);
                        SkipWhitespace(text, ref pos);
                        if (text[pos++] != ':')
                            throw new FormatException($"expected ':' at {pos - 1}");
                        result[key] = ParseValue(text, ref pos);
                        SkipWhitespace(text, ref pos);
                        char next = text[pos++];
                        if (next == '}')
                            return result;
                        if (next != ',')
                            throw new FormatException($"expected ',' or '}}' at {pos - 1}");
                    }
                }
                case '[':
                {
                    var result = new List<object>();
                    pos++;
                    SkipWhitespace(text, ref pos);
                    if (text[pos] == ']')
                    {
                        pos++;
                        return result;
                    }
                    while (true)
                    {
                        result.Add(ParseValue(text, ref pos));
                        SkipWhitespace(text, ref pos);
                        char next = text[pos++];
                        if (next == ']')
                            return result;
                        if (next != ',')
                            throw new FormatException($"expected ',' or ']' at {pos - 1}");
                    }
                }
                case '"':
                    return ParseString(text, ref pos);
                case 't':
                    Expect(text, ref pos, "true");
                    return true;
                case 'f':
                    Expect(text, ref pos, "false");
                    return false;
                case 'n':
                    Expect(text, ref pos, "null");
                    return null;
                default:
                {
                    int start = pos;
                    while (pos < text.Length
                           && (char.IsDigit(text[pos]) || "+-.eE".IndexOf(text[pos]) >= 0))
                        pos++;
                    return double.Parse(text.Substring(start, pos - start), CultureInfo.InvariantCulture);
                }
            }
        }

        private static string ParseString(string text, ref int pos)
        {
            if (text[pos++] != '"')
                throw new FormatException($"expected '\"' at {pos - 1}");
            var sb = new StringBuilder();
            while (true)
            {
                char c = text[pos++];
                if (c == '"')
                    return sb.ToString();
                if (c != '\\')
                {
                    sb.Append(c);
                    continue;
                }
                char escape = text[pos++];
                switch (escape)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        sb.Append((char)int.Parse(
                            text.Substring(pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        pos += 4;
                        break;
                    default:
                        throw new FormatException($"unknown escape '\\{escape}'");
                }
            }
        }

        private static void Expect(string text, ref int pos, string literal)
        {
            if (string.CompareOrdinal(text, pos, literal, 0, literal.Length) != 0)
                throw new FormatException($"expected '{literal}' at {pos}");
            pos += literal.Length;
        }

        private static void SkipWhitespace(string text, ref int pos)
        {
            while (pos < text.Length && char.IsWhiteSpace(text[pos]))
                pos++;
        }
    }
}
