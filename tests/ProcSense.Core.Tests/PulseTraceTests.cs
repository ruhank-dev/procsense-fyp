using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ProcSense.Core.Content;
using ProcSense.Core.Physiology;
using ProcSense.Core.Replay;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>The Pulse adapter (p1-pulse-physiology-s02): a recorded Pulse run replays behind IPhysiologyEngine, a session is deterministic and verifiable with it, the default engine is untouched, and the golden runs complete with it selected.</summary>
    public class PulseTraceTests
    {
        private static readonly ContentPackage Content = TestCase.Package();
        private static readonly PulseTrace Adrenaline = Content.PulseTraces.Traces["PS-ADRENALINE"];
        private static readonly PhysioEffect Effect = Content.Physio.FindEffect("EFF-ADRENALINE")!;

        private static PulseTrace Tiny(string anchor = "EFF-ADRENALINE") => new PulseTrace("T", "1", "abc", 10, new[] { new PulseTraceAction(10, anchor) },
            new[] { Sample(0, 70, 120), Sample(10, 70, 120), Sample(20, 90, 100), Sample(30, 80, 110) });

        private static PulseTraceSample Sample(double t, double hr, double sbp) => new PulseTraceSample(t, new Dictionary<string, double> { ["HR"] = hr, ["SBP"] = sbp, ["DBP"] = 70, ["RR"] = 12, ["SpO2"] = 98, ["Temp"] = 36.5 });

        private static PulseTraceEngine Engine(PulseTrace t)
        {
            var e = new PulseTraceEngine(Content.Physio, t);
            e.Initialise(new Dictionary<string, double> { ["HR"] = 80, ["SBP"] = 130, ["DBP"] = 80, ["RR"] = 14, ["SpO2"] = 97, ["Temp"] = 37 }, new Random.SimRandom(1));
            return e;
        }

        [Fact]
        public void The_content_carries_the_recorded_runs_with_the_engine_they_came_from()
        {
            Assert.Equal(new[] { "PS-ACUTE-STRESS", "PS-ADRENALINE", "PS-BLEED-FLUID", "PS-OPIOID-NALOXONE" }, Content.PulseTraces.Traces.Keys.OrderBy(k => k, StringComparer.Ordinal));
            Assert.All(Content.PulseTraces.Traces.Values, t => { Assert.Equal("4.4.0", t.EngineVersion); Assert.Equal(7, t.EngineGitHash.Length); Assert.True(t.Duration >= 900); });
            Assert.Equal("EFF-ADRENALINE", Adrenaline.Anchor.Key);
        }

        [Fact]
        public void Interpolation_is_linear_and_held_outside_the_recording()
        {
            var t = Tiny();
            Assert.Equal(80, t.At("HR", 15), 9);
            Assert.Equal(70, t.At("HR", -5));
            Assert.Equal(80, t.At("HR", 999));
            Assert.Equal(90, t.At("HR", 20));
        }

        [Fact]
        public void The_patient_stays_at_the_cases_baseline_until_the_starting_effect_is_applied()
        {
            var e = Engine(Tiny());
            e.Advance(500);
            Assert.False(e.Started);
            Assert.Equal(80, e.Read().Values["HR"]);
            Assert.False(e.Active);
        }

        [Fact]
        public void After_the_starting_effect_the_case_moves_by_what_pulse_moved_and_nothing_else()
        {
            var e = Engine(Tiny());
            e.Advance(100);
            e.Apply(Effect);                                   // the recording's first action is at its 10 s mark, so recording time is 10 s now
            Assert.True(e.Started);
            e.Advance(10);                                     // recording time 20 s: Pulse is +20 on the heart rate and -20 on the systolic pressure against its own start
            var v = e.Read().Values;
            Assert.Equal(100, v["HR"], 9);
            Assert.Equal(110, v["SBP"], 9);
            Assert.Equal(80, v["DBP"]);                        // Pulse's diastolic did not move
            Assert.Equal(97, v["SpO2"]);
            Assert.Equal(v["MAP"], (v["SBP"] + 2 * v["DBP"]) / 3, 9);
            Assert.True(e.Active);
        }

        [Fact]
        public void An_effect_that_is_not_the_starting_one_is_counted_not_replayed()
        {
            var e = Engine(Tiny());
            e.Apply(Content.Physio.FindEffect("EFF-OXYGEN")!);
            Assert.False(e.Started);
            Assert.Equal(new[] { "EFF-OXYGEN" }, e.Ignored);
            e.Apply(Effect);
            e.Apply(Effect);                                   // a second dose does not restart the recording
            Assert.Equal(new[] { "EFF-OXYGEN", "EFF-ADRENALINE" }, e.Ignored);
        }

        [Fact]
        public void The_flags_follow_the_replayed_values()
        {
            var low = new PulseTrace("L", "1", "abc", 10, new[] { new PulseTraceAction(0, "EFF-ADRENALINE") },
                new[] { new PulseTraceSample(0, new Dictionary<string, double> { ["HR"] = 70, ["SBP"] = 120, ["DBP"] = 70, ["RR"] = 12, ["SpO2"] = 98, ["Temp"] = 36.5 }), new PulseTraceSample(10, new Dictionary<string, double> { ["HR"] = 70, ["SBP"] = 120, ["DBP"] = 70, ["RR"] = 12, ["SpO2"] = 80, ["Temp"] = 36.5 }) });
            var e = Engine(low);
            e.Apply(Effect);
            e.Advance(10);
            Assert.Equal(97 - 18, e.Read().Values["SpO2"], 9);
            Assert.NotEmpty(e.Read().Flags);                   // the physiology flag rules read the replayed oxygen level
        }

        private static Session Start(string trace, ulong seed = 42) => new Session(Content.Cases["VAR-S1-A"], seed, SessionMode.Practice, Subsystems.Default(Content), new SessionOptions(TrainingLevel.Beginner, "", false, trace));

        [Fact]
        public void A_session_with_a_trace_is_deterministic_and_its_vitals_follow_the_recording()
        {
            var a = Start("PS-ADRENALINE");
            var b = Start("PS-ADRENALINE");
            double baseline = a.Patient.Physiological.Vitals.HeartRate;
            foreach (var s in new[] { a, b }) { s.Physiology!.Apply(Effect); s.Tick(300); }
            Assert.True(a.Patient.Physiological.Vitals.HeartRate > baseline + 10);
            Assert.Equal(a.Patient.Physiological.Vitals, b.Patient.Physiological.Vitals);
            Assert.Equal(a.Log.Hash(), b.Log.Hash());
        }

        [Fact]
        public void An_unknown_trace_is_refused_when_the_session_starts()
        {
            var e = Assert.Throws<ContentException>(() => Start("PS-NO-SUCH"));
            Assert.Contains("PS-NO-SUCH", e.Message);
        }

        [Fact]
        public void The_default_session_still_uses_litephysio()
        {
            var s = Start("");
            Assert.IsType<LitePhysio>(s.Physiology!.Engine);
            Assert.IsType<PulseTraceEngine>(Start("PS-ADRENALINE").Physiology!.Engine);
        }

        [Fact]
        public void A_saved_session_names_its_trace_and_replays_to_the_same_hash()
        {
            var s = Start("PS-ADRENALINE");
            s.Tick(60);
            var text = SessionFile.Write(s);
            Assert.Contains("\"pulse_trace\":\"PS-ADRENALINE\"", text.Split('\n')[0]);
            Assert.DoesNotContain("pulse_trace", SessionFile.Write(Start("")).Split('\n')[0]);          // an older log reads the same
            Assert.True(SessionFile.Replay(text, Content, () => Subsystems.Default(Content)).Match);
        }

        [Fact]
        public void Every_golden_run_completes_with_a_trace_selected_and_is_unchanged_unless_the_starting_effect_was_given()
        {
            var dir = Path.Combine(TestCase.Repo, "tests", "golden");
            var ids = Directory.GetFiles(Path.Combine(TestCase.ContentRoot, "case_files"), "*.json").Select(Path.GetFileNameWithoutExtension).OrderBy(x => x, StringComparer.Ordinal).Select(x => x!).ToArray();
            var all = ContentLoader.Load(new CompositeContentReader(new FileContentReader(TestCase.ContentRoot), new FileContentReader(Path.Combine(TestCase.Repo, "tests", "fixtures"))), ids);
            int ran = 0, started = 0;
            foreach (var script in Directory.GetFiles(dir, "*.actions.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                string expectedPath = script.Replace(".actions.json", ".expected.json");
                var exp = (Dictionary<string, object?>)MiniJson.Parse(File.ReadAllText(expectedPath))!;
                string caseId = (string)exp["case"]!;
                if (!all.Cases.ContainsKey(caseId)) continue;
                ulong seed = ulong.Parse((string)exp["seed"]!, CultureInfo.InvariantCulture);
                var mode = (SessionMode)Enum.Parse(typeof(SessionMode), (string)exp["mode"]!, true);
                var options = GoldenRun.Options(File.ReadAllText(expectedPath));
                var actions = ActionScript.Parse(File.ReadAllText(script)).ToList();
                var lite = new Session(all.Cases[caseId], seed, mode, Subsystems.Default(all), options);
                SessionFile.Play(lite, actions);
                var pulse = new Session(all.Cases[caseId], seed, mode, Subsystems.Default(all), options with { PulseTrace = "PS-ADRENALINE" });
                SessionFile.Play(pulse, actions);
                ran++;
                Assert.Equal(lite.EndStates?.Resolved ?? "", pulse.EndStates?.Resolved ?? "");                  // scoring is by actions: the end state does not depend on the engine
                bool began = ((PulseTraceEngine)pulse.Physiology!.Engine).Started;
                if (began) started++;
                else if (lite.Observable.Hash() != pulse.Observable.Hash())
                    // nothing started the recording, so the patient is exactly as before, except where the deterioration clock moves the engine's TARGETS (DT-083): the lite engine carries the patient there with its time
                    // constants and the recorded one has no such lag, so a deteriorating emergency (scenario S2) may differ in its vital signs until a recording starts
                    Assert.Equal("S2", all.Cases[caseId].Scenario);
            }
            Assert.True(ran >= 100, "ran " + ran);
        }
    }
}
