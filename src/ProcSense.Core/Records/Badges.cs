using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcSense.Core.Records
{
    /// <summary>
    /// The EMR's new-document badges (p0-patient-records-emr-s05): for each tab, the documents that arrived during the session and have not been opened yet. A document is seen once it is
    /// opened anywhere (the EMR or the folder), so a badge never sticks. It lives in Core, so a replay of the log gives the same badges.
    /// </summary>
    public sealed class Badges
    {
        private readonly SortedDictionary<string, SortedSet<string>> _unseen = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);

        internal Badges(IEnumerable<string> tabs)
        {
            foreach (var t in tabs) _unseen[t] = new SortedSet<string>(StringComparer.Ordinal);
        }

        /// <summary>Unseen documents per tab (every tab is listed, 0 when nothing is new).</summary>
        public IReadOnlyDictionary<string, int> Counts => _unseen.ToDictionary(kv => kv.Key, kv => kv.Value.Count, StringComparer.Ordinal);

        /// <summary>The ids of the unseen documents of one tab, in id order ("" tab or an unknown one: none).</summary>
        public IReadOnlyList<string> Unseen(string tab) => _unseen.TryGetValue(tab, out var set) ? set.ToList() : new List<string>();

        /// <summary>The number on a tab's badge (0: no badge).</summary>
        public int Count(string tab) => _unseen.TryGetValue(tab, out var set) ? set.Count : 0;

        internal void Add(string tab, string documentId)
        {
            if (_unseen.TryGetValue(tab, out var set)) set.Add(documentId);
        }

        internal bool Seen(string documentId)
        {
            bool any = false;
            foreach (var set in _unseen.Values) any |= set.Remove(documentId);
            return any;
        }
    }
}
