using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.VR.ActionEmitters;
using UnityEngine;

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
            public ActionEmitter Owner { get; internal set; }
            public string Label { get; internal set; }
            public SimAction Action { get; internal set; }
        }

        private static readonly List<Entry> Entries = new List<Entry>();

        public static IReadOnlyList<Entry> All => Entries;
        public static int Revision { get; private set; }

        public static void Register(ActionEmitter owner, string label, SimAction action)
        {
            Unregister(owner);
            if (owner == null || !owner.isActiveAndEnabled || string.IsNullOrEmpty(label) || string.IsNullOrEmpty(action?.Verb)) return;
            Entries.Add(new Entry { Owner = owner, Label = label, Action = action });
            Revision++;
        }

        public static void Unregister(ActionEmitter owner)
        {
            if (Entries.RemoveAll(e => ReferenceEquals(e.Owner, owner)) > 0) Revision++;
        }

        public static bool SameAction(SimAction a, SimAction b) =>
            a.Verb == b.Verb && a.Target == b.Target && a.Channel == b.Channel && a.Params.SequenceEqual(b.Params);

        /// <summary>Resolve a current owner at click time: a stale button must not send an old action.</summary>
        public static ActionOutcome Fire(SimAction action)
        {
            foreach (var entry in Entries)
                if (entry.Owner != null && entry.Owner.isActiveAndEnabled && SameAction(entry.Action, action))
                    return entry.Owner.Fire();
            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear()
        {
            Entries.Clear();
            Revision++;
        }
    }
}
