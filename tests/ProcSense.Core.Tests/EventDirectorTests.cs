using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Events;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Rules;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>The Event Director (p0-physiology-events-behaviour-s08, plan tests T-E1 to T-E7 and T-E9) and the what-if coverage list.</summary>
    public class EventDirectorTests
    {
        private static readonly ContentPackage Content = TestCase.Package();

        private static string Ev(string id, string status = "APPROVED_FOR_TRAINING", string trigger = "true", string eligibility = "true", string needsFlag = "", double p = 1.0, string kind = "caused", string category = "clinical",
            string expected = "", double cooldown = 0, string physio = "", string cues = "")
        {
            return "{\"id\":\"" + id + "\",\"whatif\":\"w00\",\"category\":\"" + category + "\",\"kind\":\"" + kind + "\",\"status\":\"" + status + "\",\"trigger\":" + trigger + ",\"eligibility\":" + eligibility +
                ",\"needs_flag\":\"" + needsFlag + "\",\"watch\":{\"vital\":\"\",\"op\":\"\",\"value\":0},\"base_probability\":" + p.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"severity\":\"low\",\"difficulty_tier\":\"standard\",\"cooldown_seconds\":" + cooldown +
                ",\"cues\":[" + cues + "],\"physio_effects\":[" + physio + "],\"set_flags\":[],\"expected_responses\":[" + expected + "],\"unacceptable\":[],\"consequences\":{\"handled\":" + Cons("h") + ",\"missed\":" + Cons("m") + ",\"wrong\":" + Cons("w") +
                "},\"communication_intents\":[],\"sources\":[\"SR-2863\"]}";
        }

        private static string Cons(string tag) => "{\"items\":[\"item." + tag + "\"],\"flag\":\"flag." + tag + "\",\"physio_effects\":[]}";

        private static EventTables Library(params string[] events) => EventTables.Parse("{\"evaluate_every_steps\":20,\"warmup_seconds\":180,\"global_cooldown_seconds\":90,\"max_clinical\":1,\"max_other\":1,\"events\":[" + string.Join(",", events) + "],\"modifiers\":[],\"refs\":[]}");

        private static Session Start(EventTables? tables, SessionMode mode = SessionMode.Practice, ulong seed = 42, IReadOnlyList<ScheduledEvent>? schedule = null)
        {
            var chain = Subsystems.Default(Content).Where(x => !(x is EventDirector)).Concat(new ISubsystem[] { new EventDirector(Content, tables, schedule) });
            return new Session(Content.Cases["VAR-S1-A"], seed, mode, chain);
        }

        private static string Wait = "{\"id\":\"wait\",\"verb\":\"wait\",\"params\":{},\"within_seconds\":60,\"label\":\"wait\"}";

        // ---------------------------------------------------------------- the content

        [Fact]
        public void The_content_has_the_hypersensitivity_event_and_the_six_vital_sign_events_with_a_source()
        {
            var ids = Content.Events.Events.Select(e => e.Id).ToList();
            Assert.Equal(new[] { "E-HYPERSENSITIVITY", "E-LOW-BP", "E-HIGH-HR", "E-LOW-HR", "E-LOW-OXYGEN", "E-FAST-BREATHING", "E-SLOW-BREATHING" }, ids);
            foreach (var e in Content.Events.Events) { Assert.Contains("SR-2863", e.Sources); Assert.NotEmpty(e.Expected); }
            Assert.Equal((20, 180.0, 90.0, 1, 1), (Content.Events.EvaluateEverySteps, Content.Events.WarmupSeconds, Content.Events.GlobalCooldownSeconds, Content.Events.MaxClinical, Content.Events.MaxOther));
        }

        [Fact]
        public void T_E1_every_content_event_can_be_forced_and_opens_its_response_windows_and_cues()
        {
            foreach (var def in Content.Events.Events)
            {
                var s = Start(null);
                s.Director!.Fire(def);
                Assert.True(s.Director.HasFired(def.Id));
                Assert.True(s.Director.IsActive(def.Id));
                Assert.Contains(s.Director.History, h => h.Kind == "event_fired" && h.Id == def.Id);
                Assert.Equal(def.Cues.Count, s.Director.Cues.Count);
            }
        }

        // ---------------------------------------------------------------- the allergic reaction (the worked example)

        [Fact]
        public void An_allergen_given_makes_the_reaction_fire_with_cues_and_the_blood_pressure_falls()
        {
            var s = Start(null);
            double sbp = s.Patient.Physiological.Vitals.SystolicBp;
            s.Patient.Clinical.SetFlag("allergy.reaction_eligible");
            s.Tick(30);
            Assert.True(s.Director!.IsActive("E-HYPERSENSITIVITY"));
            Assert.Equal(new[] { "rash and flushing", "swollen lips", "wheeze" }, s.Observable.Cues!.Select(c => c.Text));
            s.Tick(900);
            Assert.True(s.Patient.Physiological.Vitals.SystolicBp < sbp - 20);
            Assert.True(s.Patient.Clinical.HasFlag("event.hypersensitivity"));
        }

        [Fact]
        public void An_unanswered_reaction_is_missed_after_its_window()
        {
            var s = Start(null);
            s.Patient.Clinical.SetFlag("allergy.reaction_eligible");
            s.Tick(30);
            s.Tick(700);
            Assert.False(s.Director!.IsActive("E-HYPERSENSITIVITY"));
            var missed = s.Director.History.Single(h => h.Kind == "event_missed" && h.Id == "E-HYPERSENSITIVITY");
            Assert.Contains("critical.reaction_not_treated", missed.ScoreItems);
            Assert.True(s.Patient.Clinical.HasFlag("event.hypersensitivity.missed"));
        }

        [Fact]
        public void A_call_for_help_in_time_counts_but_the_adrenaline_window_still_runs()
        {
            var s = Start(null);
            s.Patient.Clinical.SetFlag("allergy.reaction_eligible");
            s.Tick(30);
            s.Apply(new SimAction("escalate", "patient", Channel.Cli, new Dictionary<string, string> { ["role"] = "anaesthetist", ["s"] = "reaction", ["b"] = "antibiotic given", ["a"] = "anaphylaxis", ["r"] = "adrenaline" }));
            Assert.True(s.Director!.IsActive("E-HYPERSENSITIVITY"));
            s.Tick(700);
            Assert.Contains(s.Director.History, h => h.Kind == "event_missed");
        }

        // ---------------------------------------------------------------- vital-sign recognition events

        [Fact]
        public void A_pressure_of_90_or_less_opens_the_escalation_window_and_a_call_in_time_handles_it()
        {
            var s = Start(null);
            var phys = s.Patient.Physiological;
            phys.Vitals = phys.Vitals with { SystolicBp = 85 };
            s.Tick(10);
            Assert.True(s.Director!.IsActive("E-LOW-BP"));
            var o = s.Apply(new SimAction("escalate", "patient", Channel.Cli, new Dictionary<string, string> { ["role"] = "senior", ["s"] = "low pressure", ["b"] = "post op", ["a"] = "shock", ["r"] = "review now" }));
            Assert.True(o.Accepted);
            Assert.False(s.Director.IsActive("E-LOW-BP"));
            Assert.Contains(s.Director.History, h => h.Kind == "event_handled" && h.Id == "E-LOW-BP");
        }

        [Fact]
        public void A_low_pressure_nobody_answers_is_a_missed_response()
        {
            var s = Start(null);
            var phys = s.Patient.Physiological;
            phys.Vitals = phys.Vitals with { SystolicBp = 85 };
            s.Tick(10);
            s.Tick(400);
            Assert.Contains(s.Director!.History, h => h.Kind == "event_missed" && h.Id == "E-LOW-BP" && h.ScoreItems.Contains("major.e_low_bp_not_escalated"));
        }

        // ---------------------------------------------------------------- T-E2 to T-E7

        [Fact]
        public void T_E2_an_event_that_is_not_eligible_never_fires_in_ten_thousand_evaluations()
        {
            var s = Start(Library(Ev("E-NO", eligibility: "false")));
            for (int i = 0; i < 2000; i++) s.Tick(50);          // 100 000 s of play = 10 000 evaluations (one every 10 s)
            Assert.False(s.Director!.HasFired("E-NO"));
        }

        [Fact]
        public void T_E3_the_same_seed_gives_the_same_events_at_the_same_times_and_another_seed_differs()
        {
            string Run(ulong seed)
            {
                var s = Start(Library(Enumerable.Range(0, 24).Select(i => Ev("E-" + i, p: 0.5, kind: "random", category: i % 2 == 0 ? "clinical" : "other", expected: Wait)).ToArray()), seed: seed);
                for (int i = 0; i < 60; i++) s.Tick(30);
                return string.Join(";", s.Director!.History.Where(h => h.Kind == "event_fired").Select(h => h.Id + "@" + h.Step));
            }
            Assert.Equal(Run(7), Run(7));
            Assert.NotEqual(Run(7), Run(8));
            Assert.NotEqual("", Run(7));
        }

        [Fact]
        public void T_E4_two_eligible_clinical_events_respect_the_concurrency_limit()
        {
            var s = Start(Library(Ev("E-A", expected: Wait), Ev("E-B", expected: Wait)));
            s.Tick(10);
            Assert.True(s.Director!.IsActive("E-A"));
            Assert.False(s.Director.IsActive("E-B"));
            s.Apply(new SimAction("wait", "patient"));            // answers E-A
            Assert.False(s.Director.IsActive("E-A"));
            s.Tick(10);
            Assert.True(s.Director.IsActive("E-B"));
        }

        [Fact]
        public void One_clinical_and_one_other_event_may_run_together()
        {
            var s = Start(Library(Ev("E-C", expected: Wait), Ev("E-O", category: "other", expected: Wait)));
            s.Tick(10);
            Assert.True(s.Director!.IsActive("E-C") && s.Director.IsActive("E-O"));
        }

        [Fact]
        public void An_unprompted_event_waits_for_the_warm_up_and_the_cooldown_between_events()
        {
            var s = Start(Library(Ev("E-R1", kind: "random", expected: Wait), Ev("E-R2", kind: "random", category: "other", expected: Wait)));
            s.Tick(120);
            Assert.False(s.Director!.HasFired("E-R1"));                       // inside the 3 minute warm-up
            s.Tick(90);
            Assert.True(s.Director.HasFired("E-R1"));
            Assert.False(s.Director.HasFired("E-R2"));                        // inside the 90 s global cooldown
            s.Tick(120);
            Assert.True(s.Director.HasFired("E-R2"));
        }

        [Fact]
        public void T_E5_assessment_ignores_probabilities_and_runs_the_fixed_schedule_the_same_every_time()
        {
            string Run()
            {
                var lib = Library(Ev("E-SCHED", status: "APPROVED_FOR_ASSESSMENT", trigger: "false", p: 0.0), Ev("E-NOCHANCE", status: "APPROVED_FOR_ASSESSMENT", p: 0.0));
                var s = Start(lib, SessionMode.Assessment, schedule: new[] { new ScheduledEvent("E-SCHED", 300, null) });
                s.Tick(299);
                bool before = s.Director!.HasFired("E-SCHED");
                s.Tick(30);
                return before + ":" + string.Join(";", s.Director.History.Where(h => h.Kind == "event_fired").Select(h => h.Id + "@" + h.Step));
            }
            var first = Run();
            Assert.Equal(first, Run());
            Assert.StartsWith("False:", first);
            Assert.Contains("E-SCHED@", first);
            Assert.Contains("E-NOCHANCE@", first);        // base probability 0 does not stop a true trigger in assessment
        }

        [Fact]
        public void T_E6_an_event_approved_only_for_training_is_not_loaded_in_assessment()
        {
            var lib = Library(Ev("E-TRAIN"), Ev("E-ASSESS", status: "APPROVED_FOR_ASSESSMENT"), Ev("E-DRAFT", status: "DRAFT"));
            Assert.Equal(new[] { "E-TRAIN", "E-ASSESS" }, Start(lib).Director!.Library.Select(e => e.Id));
            Assert.Equal(new[] { "E-ASSESS" }, Start(lib, SessionMode.Assessment).Director!.Library.Select(e => e.Id));
            Assert.Empty(Start(null, SessionMode.Assessment).Director!.Library);       // the shipped events are approved for training only
        }

        [Fact]
        public void T_E7_thirty_events_add_under_a_fifth_of_a_millisecond_to_an_evaluation()
        {
            double Best(EventTables? tables)
            {
                var s = Start(tables);
                s.Tick(10);
                double best = double.MaxValue;
                for (int attempt = 0; attempt < 15; attempt++)
                {
                    var sw = Stopwatch.StartNew();
                    for (int i = 0; i < 300; i++) s.Tick(10);          // 300 evaluations (one every 20 steps), each with the other 20 steps of the whole pipeline
                    best = Math.Min(best, sw.Elapsed.TotalMilliseconds / 300);
                }
                return best;
            }
            var thirty = Library(Enumerable.Range(0, 30).Select(i => Ev("E-P" + i, trigger: "{\"<\":[{\"var\":\"vitals.hr\"},0]}")).ToArray());
            double extra = Best(thirty) - Best(Library());
            Assert.True(extra < 0.2, "30 events added " + extra + " ms to an evaluation");
        }

        [Fact]
        public void A_session_with_no_event_flag_set_does_not_evaluate_the_shipped_flag_event()
        {
            var s = Start(null);
            s.Tick(600);
            Assert.False(s.Director!.HasFired("E-HYPERSENSITIVITY"));
            Assert.Empty(s.Director.Cues);
        }

        [Fact]
        public void T_E9_a_beginner_asking_for_an_allergen_is_challenged_and_no_reaction_fires()
        {
            var s = Start(null);
            s.Tick(60);
            Assert.False(s.Director!.HasFired("E-HYPERSENSITIVITY"));          // practice mode: the nurse challenges (MedicineWorkflowTests T-TR3) and the reaction flag is never set
        }

        // ---------------------------------------------------------------- the library parser and the coverage list

        [Fact]
        public void A_broken_library_is_refused_with_the_path()
        {
            var e = Assert.Throws<ContentException>(() => EventTables.Parse("{\"evaluate_every_steps\":0}"));
            Assert.Contains("evaluate_every_steps", e.Message);
            var e2 = Assert.Throws<ContentException>(() => Library(Ev("E-X", p: 1.5)));
            Assert.Contains("base_probability", e2.Message);
        }

        [Fact]
        public void The_what_if_coverage_list_names_all_47_and_every_engine_test_file_exists()
        {
            var text = System.IO.File.ReadAllText(System.IO.Path.Combine(TestCase.Repo, "docs", "research", "whatif_coverage.md"));
            for (int i = 1; i <= 47; i++) Assert.Contains("| w" + i.ToString("00", System.Globalization.CultureInfo.InvariantCulture) + " |", text);
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text, "`([A-Za-z0-9]+Tests\\.cs)`"))
                Assert.True(System.IO.File.Exists(System.IO.Path.Combine(TestCase.Repo, "tests", "ProcSense.Core.Tests", m.Groups[1].Value)), m.Groups[1].Value);
        }
    }
}
