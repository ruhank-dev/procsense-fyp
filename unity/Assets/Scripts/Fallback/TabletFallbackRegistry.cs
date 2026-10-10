using System.Collections.Generic;
using ProcSense.Core.Actions;

namespace ProcSense.VR.Fallback
{
    /// <summary>
    /// Every emitter that turns a VR gesture into an action registers the same action here, so the tablet can offer it as a button
    /// (plan test T03-5 / T01-5: "Attach sensor" and "Read wristband" must work when a socket or a grab fails).
    /// </summary>
    public static class TabletFallbackRegistry
    {
        public sealed class Entry
        {
            public string Label;
            public SimAction Action;
        }

        private static readonly List<Entry> Entries = new List<Entry>();

        public static IReadOnlyList<Entry> All => Entries;

        public static void Register(string label, SimAction action)
        {
            foreach (var e in Entries)
                if (e.Label == label) return;      // one button per gesture, however many times a scene is loaded
            Entries.Add(new Entry { Label = label, Action = action });
        }

        public static void Clear() => Entries.Clear();
    }
}
