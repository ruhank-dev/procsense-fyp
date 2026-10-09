using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ProcSense.Core.Content
{
    /// <summary>
    /// Small strict JSON reader: no reflection, so it is safe under Unity IL2CPP stripping, and no dependencies.
    /// Produces null, bool, string, long, double, List&lt;object?&gt; and Dictionary&lt;string, object?&gt;.
    /// </summary>
    public static class MiniJson
    {
        public static object? Parse(string text)
        {
            int i = 0;
            var value = ReadValue(text, ref i);
            SkipWs(text, ref i);
            if (i != text.Length) throw Fail(text, i, "unexpected text after the JSON value");
            return value;
        }

        private static object? ReadValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) throw Fail(s, i, "unexpected end of file");
            char c = s[i];
            if (c == '{') return ReadObject(s, ref i);
            if (c == '[') return ReadArray(s, ref i);
            if (c == '"') return ReadString(s, ref i);
            if (Literal(s, ref i, "true")) return true;
            if (Literal(s, ref i, "false")) return false;
            if (Literal(s, ref i, "null")) return null;
            return ReadNumber(s, ref i);
        }

        private static Dictionary<string, object?> ReadObject(string s, ref int i)
        {
            var d = new Dictionary<string, object?>();
            i++; // {
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return d; }
            while (true)
            {
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != '"') throw Fail(s, i, "expected a quoted key");
                string key = ReadString(s, ref i);
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != ':') throw Fail(s, i, "expected ':'");
                i++;
                if (d.ContainsKey(key)) throw Fail(s, i, "duplicate key '" + key + "'");
                d[key] = ReadValue(s, ref i);
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == '}') { i++; return d; }
                throw Fail(s, i, "expected ',' or '}'");
            }
        }

        private static List<object?> ReadArray(string s, ref int i)
        {
            var l = new List<object?>();
            i++; // [
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return l; }
            while (true)
            {
                l.Add(ReadValue(s, ref i));
                SkipWs(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == ']') { i++; return l; }
                throw Fail(s, i, "expected ',' or ']'");
            }
        }

        private static string ReadString(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++; // opening quote
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case '"': case '\\': case '/': sb.Append(e); break;
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (i + 4 > s.Length || !int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                            throw Fail(s, i, "bad \\u escape");
                        sb.Append((char)code);
                        i += 4;
                        break;
                    default: throw Fail(s, i, "bad escape '\\" + e + "'");
                }
            }
            throw Fail(s, i, "unterminated string");
        }

        private static object ReadNumber(string s, ref int i)
        {
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            string t = s.Substring(start, i - start);
            if (t.Length == 0) throw Fail(s, start, "unexpected character '" + s[start] + "'");
            bool isInt = t.IndexOfAny(new[] { '.', 'e', 'E' }) < 0;
            if (isInt && long.TryParse(t, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long l)) return l;
            if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) return d;
            throw Fail(s, start, "bad number '" + t + "'");
        }

        private static bool Literal(string s, ref int i, string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) return false;
            i += word.Length;
            return true;
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && (s[i] == ' ' || s[i] == '\n' || s[i] == '\r' || s[i] == '\t')) i++;
        }

        private static ContentException Fail(string s, int i, string what)
        {
            int line = 1;
            for (int k = 0; k < i && k < s.Length; k++) if (s[k] == '\n') line++;
            return new ContentException("invalid JSON at line " + line.ToString(CultureInfo.InvariantCulture) + ": " + what);
        }
    }

    public sealed class ContentException : Exception
    {
        public ContentException(string message) : base(message) { }
    }
}
