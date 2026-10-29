using System.Collections.Generic;
using ProcSense.Core.Content;
using ProcSense.VR.Content;

namespace ProcSense.VR.UI.Monitor
{
    /// <summary>One alarm priority: its rank (3 high, 2 medium, 1 low), the symbol and word shown with it (status never by colour alone), and its tone pattern.</summary>
    public sealed class AlarmPriority
    {
        public AlarmPriority(string id, int rank, string symbol, string label, float repeatSeconds, int pulses) { Id = id; Rank = rank; Symbol = symbol; Label = label; RepeatSeconds = repeatSeconds; Pulses = pulses; }
        public string Id { get; }
        public int Rank { get; }
        public string Symbol { get; }
        public string Label { get; }
        public float RepeatSeconds { get; }
        public int Pulses { get; }
    }

    /// <summary>
    /// How the monitor alarms and draws its traces, from content/ui/monitor.json (built by tools/content/build_monitor.py). Which values alarm and their zones come from Core;
    /// this says which zones alarm at which priority, how long an acknowledged alarm stays silent and the trace speeds.
    /// </summary>
    public sealed class MonitorTable
    {
        public const string File = "ui/monitor.json";

        /// <summary>Monitored vital name → the display-zone parameter Core uses for it.</summary>
        public Dictionary<string, string> Monitored { get; } = new Dictionary<string, string>();
        public Dictionary<string, string> ZonePriority { get; } = new Dictionary<string, string>();
        public Dictionary<string, AlarmPriority> Priorities { get; } = new Dictionary<string, AlarmPriority>();
        public Dictionary<string, string> ProbeAdvice { get; } = new Dictionary<string, string>();
        public float SilenceSeconds { get; private set; } = 120;
        public string CycleLabel { get; private set; } = "Cuff cycling";
        public int SampleHz { get; private set; } = 100;
        public float SweepSeconds { get; private set; } = 4;
        public float RespDefault { get; private set; } = 14;

        private static MonitorTable _loaded;

        /// <summary>The table (read once); tests may replace it.</summary>
        public static MonitorTable Loaded
        {
            get
            {
                if (_loaded != null) return _loaded;
                try { _loaded = Parse(UnityContentReader.ForThisPlatform().ReadText(File)); }
                catch (System.Exception e) { UnityEngine.Debug.LogWarning("[ProcSense] no monitor table: " + e.Message); _loaded = Defaults(); }
                return _loaded;
            }
            set => _loaded = value;
        }

        /// <summary>The shipped values, for when the file cannot be read (the editor without content).</summary>
        public static MonitorTable Defaults()
        {
            var t = new MonitorTable();
            t.Monitored["spo2"] = "spo2"; t.Monitored["heart_rate"] = "heart_rate"; t.Monitored["systolic_bp"] = "systolic_bp_low";
            t.ZonePriority["emergency"] = "high"; t.ZonePriority["worry"] = "medium"; t.ZonePriority["watch"] = "low";
            t.Priorities["high"] = new AlarmPriority("high", 3, "!!!", "HIGH", 2.5f, 10);
            t.Priorities["medium"] = new AlarmPriority("medium", 2, "!!", "MEDIUM", 6f, 3);
            t.Priorities["low"] = new AlarmPriority("low", 1, "!", "LOW", 16f, 2);
            return t;
        }

        public static MonitorTable Parse(string json)
        {
            var root = MiniJson.Parse(json) as Dictionary<string, object> ?? throw new System.FormatException("monitor table is not an object");
            var t = new MonitorTable();
            var alarms = (Dictionary<string, object>)root["alarms"];
            foreach (var kv in (Dictionary<string, object>)alarms["monitored"]) t.Monitored[kv.Key] = (string)kv.Value;
            foreach (var kv in (Dictionary<string, object>)alarms["zone_priority"]) t.ZonePriority[kv.Key] = (string)kv.Value;
            foreach (var kv in (Dictionary<string, object>)alarms["priorities"])
            {
                var p = (Dictionary<string, object>)kv.Value;
                t.Priorities[kv.Key] = new AlarmPriority(kv.Key, (int)Num(p["rank"]), (string)p["symbol"], (string)p["label"], (float)Num(p["repeat_seconds"]), (int)Num(p["pulses"]));
            }
            t.SilenceSeconds = (float)Num(alarms["silence_seconds"]);
            foreach (var kv in (Dictionary<string, object>)root["probe"]) t.ProbeAdvice[kv.Key] = (string)kv.Value;
            t.CycleLabel = (string)((Dictionary<string, object>)root["nibp"])["cycle_label"];
            var traces = (Dictionary<string, object>)root["traces"];
            t.SampleHz = (int)Num(traces["sample_hz"]);
            t.SweepSeconds = (float)Num(traces["sweep_seconds"]);
            t.RespDefault = (float)Num(traces["resp_default"]);
            return t;
        }

        private static double Num(object o) => System.Convert.ToDouble(o, System.Globalization.CultureInfo.InvariantCulture);
    }
}
