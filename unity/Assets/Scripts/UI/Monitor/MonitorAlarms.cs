using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Observation;

namespace ProcSense.VR.UI.Monitor
{
    /// <summary>One sounding (or silenced) alarm on the monitor.</summary>
    public sealed class MonitorAlarm
    {
        public MonitorAlarm(string vital, string value, string zone, AlarmPriority priority, bool silenced) { Vital = vital; Value = value; Zone = zone; Priority = priority; Silenced = silenced; }
        public string Vital { get; }
        public string Value { get; }
        public string Zone { get; }
        public AlarmPriority Priority { get; }
        public bool Silenced { get; }

        /// <summary>"!!! HIGH  SpO2 86" (the symbol and word carry the priority, not the colour alone).</summary>
        public string Text => Priority.Symbol + " " + Priority.Label + "  " + MonitorAlarms.Short(Vital) + " " + Value + (Silenced ? "  (silenced)" : "");
    }

    /// <summary>
    /// The monitor's alarms from the display zones Core publishes for the values it shows. Nothing here decides anything clinical: a monitored value in an alarming zone
    /// sounds at that zone's priority; acknowledging it silences the sound for the table's time (the light and the text stay); a value that leaves the zone re-arms it,
    /// and a worse priority on the same value sounds again at once. The acknowledgement itself is a Core action (acknowledge_alarm), so the log has it.
    /// </summary>
    public sealed class MonitorAlarms
    {
        private readonly MonitorTable _table;
        private readonly Dictionary<string, (int rank, float until)> _silenced = new Dictionary<string, (int, float)>();
        private readonly List<MonitorAlarm> _active = new List<MonitorAlarm>();

        public MonitorAlarms(MonitorTable table) { _table = table; }

        /// <summary>Alarms now, worst first.</summary>
        public IReadOnlyList<MonitorAlarm> Active => _active;

        /// <summary>The worst alarm that is still sounding, or null.</summary>
        public MonitorAlarm Sounding => _active.FirstOrDefault(a => !a.Silenced);

        public void Update(IReadOnlyList<ObservedVital> vitals, float now)
        {
            _active.Clear();
            foreach (var v in vitals ?? (IReadOnlyList<ObservedVital>)System.Array.Empty<ObservedVital>())
            {
                if (!_table.Monitored.ContainsKey(v.Name)) continue;
                if (string.IsNullOrEmpty(v.Zone) || !_table.ZonePriority.TryGetValue(v.Zone, out var pid) || !_table.Priorities.TryGetValue(pid, out var priority))
                {
                    _silenced.Remove(v.Name);   // back in range: the next alarm on this value sounds
                    continue;
                }
                bool silenced = _silenced.TryGetValue(v.Name, out var s) && s.until > now && s.rank >= priority.Rank;
                if (!silenced) _silenced.Remove(v.Name);
                _active.Add(new MonitorAlarm(v.Name, v.Value, v.Zone, priority, silenced));
            }
            _active.Sort((a, b) => b.Priority.Rank != a.Priority.Rank ? b.Priority.Rank.CompareTo(a.Priority.Rank) : string.CompareOrdinal(a.Vital, b.Vital));
        }

        /// <summary>Silences every alarm sounding now; returns the vitals it silenced (each one is logged as an acknowledge_alarm action).</summary>
        public IReadOnlyList<string> Acknowledge(float now)
        {
            var done = new List<string>();
            for (int i = 0; i < _active.Count; i++)
            {
                var a = _active[i];
                if (a.Silenced) continue;
                _silenced[a.Vital] = (a.Priority.Rank, now + _table.SilenceSeconds);
                _active[i] = new MonitorAlarm(a.Vital, a.Value, a.Zone, a.Priority, true);
                done.Add(a.Vital);
            }
            return done;
        }

        public static string Short(string vital) => vital switch
        {
            "spo2" => "SpO2", "heart_rate" => "Pulse", "systolic_bp" => "BP sys", _ => vital.Replace('_', ' '),
        };
    }
}
