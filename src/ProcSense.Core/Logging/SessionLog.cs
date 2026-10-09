using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ProcSense.Core.Actions;

namespace ProcSense.Core.Logging
{
    /// <summary>Append-only record of every action and its outcome. The JSONL text is canonical (fixed key order, invariant numbers, \n) so its SHA-256 matches on every machine.</summary>
    public sealed class SessionLog
    {
        private readonly StringBuilder _jsonl = new StringBuilder();

        public int Count { get; private set; }

        public void Append(long step, SimAction a, ActionOutcome o)
        {
            var sb = _jsonl;
            sb.Append("{\"seq\":").Append(Count.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"step\":").Append(step.ToString(CultureInfo.InvariantCulture));
            sb.Append(",\"verb\":"); Str(a.Verb);
            sb.Append(",\"target\":"); Str(a.Target);
            sb.Append(",\"channel\":"); Str(a.Channel.ToString().ToLowerInvariant());
            sb.Append(",\"params\":{");
            bool first = true;
            foreach (var kv in a.Params) // SortedDictionary: ordinal order
            {
                if (!first) sb.Append(',');
                first = false;
                Str(kv.Key); sb.Append(':'); Str(kv.Value);
            }
            sb.Append("},\"accepted\":").Append(o.Accepted ? "true" : "false");
            sb.Append(",\"reject\":"); Str(o.RejectReason);
            sb.Append(",\"effects\":[");
            for (int i = 0; i < o.Effects.Count; i++)
            {
                if (i > 0) sb.Append(',');
                Str(o.Effects[i]);
            }
            sb.Append("]}\n");
            Count++;
        }

        public string ToJsonl() => _jsonl.ToString();

        public string Hash()
        {
            using (var sha = SHA256.Create())
            {
                var hex = new StringBuilder();
                foreach (byte b in sha.ComputeHash(Encoding.UTF8.GetBytes(ToJsonl())))
                    hex.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return hex.ToString();
            }
        }

        private void Str(string s)
        {
            _jsonl.Append('"');
            foreach (char c in s)
            {
                if (c == '"' || c == '\\') _jsonl.Append('\\').Append(c);
                else if (c < 0x20) _jsonl.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                else _jsonl.Append(c);
            }
            _jsonl.Append('"');
        }
    }
}
