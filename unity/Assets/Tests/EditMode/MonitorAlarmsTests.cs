using System.Linq;
using NUnit.Framework;
using ProcSense.Core.Observation;
using ProcSense.VR.UI.Monitor;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>The monitor's alarms from Core's display zones, the silence after an acknowledgement and the trace shapes (p0-vr-interaction-ui-s04, p0-voice-audio-s05).</summary>
    public class MonitorAlarmsTests
    {
        private static ObservedVital V(string name, string value, string zone) => new ObservedVital(name, value, "08:00:00", zone);
        private static MonitorTable Table => MonitorTable.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "..", "..", "content", MonitorTable.File)));

        [Test]
        public void The_shipped_table_matches_the_defaults()
        {
            var t = Table;
            var d = MonitorTable.Defaults();
            CollectionAssert.AreEquivalent(d.Monitored, t.Monitored);
            CollectionAssert.AreEquivalent(d.ZonePriority, t.ZonePriority);
            foreach (var p in d.Priorities.Keys) Assert.AreEqual(d.Priorities[p].Rank, t.Priorities[p].Rank, p);
            Assert.AreEqual(120, t.SilenceSeconds);
        }

        [Test]
        public void Only_monitored_values_in_an_alarm_zone_alarm_worst_first_with_a_symbol_and_a_word()
        {
            var alarms = new MonitorAlarms(Table);
            alarms.Update(new[] { V("spo2", "86", "emergency"), V("heart_rate", "104", "watch"), V("systolic_bp", "120", "normal"), V("temperature_c", "39.4", "emergency") }, 0);
            Assert.AreEqual(new[] { "spo2", "heart_rate" }, alarms.Active.Select(a => a.Vital).ToArray());   // temperature is written down, not monitored
            Assert.AreEqual("!!! HIGH  SpO2 86", alarms.Active[0].Text);
            Assert.AreEqual("! LOW  Pulse 104", alarms.Active[1].Text);
            Assert.AreEqual("high", alarms.Sounding.Priority.Id);
        }

        [Test]
        public void Acknowledging_silences_until_the_time_ends_a_worse_zone_or_a_new_episode()
        {
            var alarms = new MonitorAlarms(Table);
            alarms.Update(new[] { V("heart_rate", "115", "worry") }, 0);
            CollectionAssert.AreEqual(new[] { "heart_rate" }, alarms.Acknowledge(0));
            Assert.IsNull(alarms.Sounding);
            Assert.AreEqual("!! MEDIUM  Pulse 115  (silenced)", alarms.Active[0].Text);   // the light and the text stay

            alarms.Update(new[] { V("heart_rate", "118", "worry") }, 60);
            Assert.IsNull(alarms.Sounding, "still silenced at 60 s");
            alarms.Update(new[] { V("heart_rate", "118", "worry") }, 121);
            Assert.AreEqual("heart_rate", alarms.Sounding.Vital, "the silence ended");

            alarms.Acknowledge(121);
            alarms.Update(new[] { V("heart_rate", "135", "emergency") }, 130);
            Assert.AreEqual("high", alarms.Sounding.Priority.Id, "worse than what was silenced sounds at once");

            alarms.Acknowledge(130);
            alarms.Update(new[] { V("heart_rate", "80", "normal") }, 131);
            Assert.IsEmpty(alarms.Active);
            alarms.Update(new[] { V("heart_rate", "112", "worry") }, 132);
            Assert.IsNotNull(alarms.Sounding, "back in range re-arms the alarm");
        }

        [Test]
        public void Nothing_sounds_with_nothing_to_alarm_on()
        {
            var alarms = new MonitorAlarms(Table);
            alarms.Update(System.Array.Empty<ObservedVital>(), 0);
            Assert.IsNull(alarms.Sounding);
            Assert.IsEmpty(alarms.Acknowledge(0));
        }

        [Test]
        public void The_traces_peak_where_they_should_and_repeat_every_cycle()
        {
            Assert.Greater(TraceShapes.Sample(TraceKind.Ecg, .39f), .8f, "the R wave");
            Assert.Less(Mathf.Abs(TraceShapes.Sample(TraceKind.Ecg, .9f)), .05f, "the baseline after T");
            Assert.Greater(TraceShapes.Sample(TraceKind.Pleth, .22f), .9f, "the systolic peak");
            Assert.Less(TraceShapes.Sample(TraceKind.Capno, .2f), .05f, "no CO2 breathing in");
            Assert.Greater(TraceShapes.Sample(TraceKind.Capno, .8f), .9f, "the plateau breathing out");
            Assert.AreEqual(TraceShapes.Sample(TraceKind.Pleth, .3f), TraceShapes.Sample(TraceKind.Pleth, 1.3f), 1e-5);
        }
    }
}
