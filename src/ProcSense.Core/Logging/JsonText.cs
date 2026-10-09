using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ProcSense.Core.Logging
{
    /// <summary>Canonical JSON text pieces shared by the session log and the observable snapshot.</summary>
    public static class JsonText
    {
        public static StringBuilder Quote(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                else sb.Append(c);
            }
            return sb.Append('"');
        }

        public static string Sha256Hex(string text)
        {
            using (var sha = SHA256.Create())
            {
                var hex = new StringBuilder();
                foreach (byte b in sha.ComputeHash(Encoding.UTF8.GetBytes(text)))
                    hex.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return hex.ToString();
            }
        }
    }
}
