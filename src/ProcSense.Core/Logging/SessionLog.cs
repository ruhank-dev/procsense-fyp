using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ProcSense.Core.Actions;

namespace ProcSense.Core.Logging
{
    /// <summary>One logged action as the scorer reads it: its place in the log, the sim step, the verb, its parameters, whether it was accepted and why not.</summary>
    public sealed record LogEntry(int Seq, long Step, string Verb, IReadOnlyDictionary<string, string> Params, bool Accepted, string Reject, IReadOnlyList<string> Effects);

    /// <summary>Append-only record of every action and its outcome. The JSONL text is canonical (fixed key order, invariant numbers, \n) so its SHA-256 matches on every machine.</summary>
    public sealed class SessionLog
    {
        private readonly StringBuilder _jsonl = new StringBuilder();
        private readonly List<LogEntry> _entries = new List<LogEntry>();

        public int Count { get; private set; }

        /// <summary>Every logged action in order (the evidence the scorer points to by Seq).</summary>
        public IReadOnlyList<LogEntry> Entries => _entries;

        /// <summary>The log entry at or just before this sim step, or -1 when nothing was logged yet: the evidence reference of something that happened at that step.</summary>
        public int SeqAtOrBefore(long step)
        {
            for (int i = _entries.Count - 1; i >= 0; i--) if (_entries[i].Step <= step) return _entries[i].Seq;
            return -1;
        }

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
            _entries.Add(new LogEntry(Count, step, a.Verb, new Dictionary<string, string>(a.Params), o.Accepted, o.RejectReason, o.Effects.ToArray()));
            Count++;
        }

        public string ToJsonl() => _jsonl.ToString();

        public string Hash() => JsonText.Sha256Hex(ToJsonl());

        private void Str(string s) => JsonText.Quote(_jsonl, s);
    }
}
