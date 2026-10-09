using System.Globalization;
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

        public string Hash() => JsonText.Sha256Hex(ToJsonl());

        private void Str(string s) => JsonText.Quote(_jsonl, s);
    }
}
