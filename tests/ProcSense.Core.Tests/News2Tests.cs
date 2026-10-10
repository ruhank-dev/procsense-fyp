using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Clinical;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Workflow;
using ProcSense.Core.Workflow.Steps;

namespace ProcSense.Core.Tests
{
    /// <summary>NEWS2 (p0-clinical-logic-s03): the walkthrough's worked examples, the edge of every cell, both scales, the tiers, missing readings and pregnancy.</summary>
    public class News2Tests
    {
        private readonly Xunit.Abstractions.ITestOutputHelper _out;

        public News2Tests(Xunit.Abstractions.ITestOutputHelper output) => _out = output;

        private static readonly ContentPackage Content = TestCase.Package();
        private static readonly News2Table Table = News2Table.Parse(File.ReadAllText(Path.Combine(TestCase.Repo, "content", "news2", "news2.json")), "news2");
        private static readonly News2 Calc = new News2(Table);

        private static News2Input Normal(double? rr = 16, double? spo2 = 98, double? sbp = 120, double? hr = 70, double? temp = 36.8, string? acvpu = "A", bool? oxygen = false) =>
            new News2Input(rr, spo2, sbp, hr, temp, acvpu, oxygen);

        // ---------------------------------------------------------------- the worked examples

        // Walkthrough Part 4: breathing 26 (3) + oxygen 94% (1) + room air (0) + BP 92 (2) + heart rate 128 (2) + alert (0) + temperature 39.2 (2) = 10, emergency response.
        [Fact]
        public void The_walkthrough_worked_example_scores_10_and_needs_an_emergency_response()
        {
            var r = Calc.Score(Normal(rr: 26, spo2: 94, sbp: 92, hr: 128, temp: 39.2));
            Assert.True(r.Complete);
            Assert.Equal(10, r.Total);
            Assert.Equal("7+", r.Tier);
            Assert.Equal("Continuous monitoring", r.Frequency);
            Assert.Equal(new[] { 3, 1, 0, 2, 2, 0, 2 }, r.Parts.Select(p => p.Points));
            Assert.Equal(new[] { "respiratory_rate", "spo2", "supplemental_oxygen", "systolic_bp", "heart_rate", "acvpu", "temperature_c" }, r.Parts.Select(p => p.Parameter));
        }

        [Fact]
        public void A_healthy_baseline_scores_0_and_is_routine()
        {
            var r = Calc.Score(Normal());
            Assert.Equal(0, r.Total);
            Assert.Equal("0", r.Tier);
            Assert.Equal("At least every 12 hours", r.Frequency);
            Assert.False(r.Single3);
        }

        // EXP-CL-1 (NEWS2): every observation in the 53 case files was scored by the Python table reader when the files were built; the engine must give the same total
        [Fact]
        public void The_engine_gives_the_same_total_as_every_observation_in_the_53_case_files()
        {
            int n = 0;
            foreach (var path in Directory.GetFiles(Path.Combine(TestCase.ContentRoot, "case_files"), "*.json"))
            {
                var f = ContentLoader.LoadCaseFile(new FileContentReader(TestCase.ContentRoot), Path.GetFileNameWithoutExtension(path));
                foreach (var o in f.Observations)
                {
                    var r = Calc.Score(new News2Input(o.RespiratoryRate, o.Spo2, o.SystolicBp, o.HeartRate, o.TemperatureC, o.Acvpu, o.Oxygen != "room_air"));
                    Assert.Equal(o.News2, r.Total);
                    n++;
                }
            }
            _out.WriteLine($"EXP-CL-1 (NEWS2): {n} observations in 53 case files, all {n} totals reproduced; the walkthrough's worked example gives 10.");
            Assert.True(n >= 53);
        }

        // ---------------------------------------------------------------- the edge of every cell

        [Theory]
        [InlineData("respiratory_rate", 8, 3)]
        [InlineData("respiratory_rate", 9, 1)]
        [InlineData("respiratory_rate", 11, 1)]
        [InlineData("respiratory_rate", 12, 0)]
        [InlineData("respiratory_rate", 20, 0)]
        [InlineData("respiratory_rate", 21, 2)]
        [InlineData("respiratory_rate", 24, 2)]
        [InlineData("respiratory_rate", 25, 3)]
        [InlineData("spo2", 91, 3)]
        [InlineData("spo2", 92, 2)]
        [InlineData("spo2", 93, 2)]
        [InlineData("spo2", 94, 1)]
        [InlineData("spo2", 95, 1)]
        [InlineData("spo2", 96, 0)]
        [InlineData("systolic_bp", 90, 3)]
        [InlineData("systolic_bp", 91, 2)]
        [InlineData("systolic_bp", 100, 2)]
        [InlineData("systolic_bp", 101, 1)]
        [InlineData("systolic_bp", 110, 1)]
        [InlineData("systolic_bp", 111, 0)]
        [InlineData("systolic_bp", 219, 0)]
        [InlineData("systolic_bp", 220, 3)]
        [InlineData("heart_rate", 40, 3)]
        [InlineData("heart_rate", 41, 1)]
        [InlineData("heart_rate", 50, 1)]
        [InlineData("heart_rate", 51, 0)]
        [InlineData("heart_rate", 90, 0)]
        [InlineData("heart_rate", 91, 1)]
        [InlineData("heart_rate", 110, 1)]
        [InlineData("heart_rate", 111, 2)]
        [InlineData("heart_rate", 130, 2)]
        [InlineData("heart_rate", 131, 3)]
        [InlineData("temperature_c", 35.0, 3)]
        [InlineData("temperature_c", 35.1, 1)]
        [InlineData("temperature_c", 36.0, 1)]
        [InlineData("temperature_c", 36.1, 0)]
        [InlineData("temperature_c", 38.0, 0)]
        [InlineData("temperature_c", 38.1, 1)]
        [InlineData("temperature_c", 39.0, 1)]
        [InlineData("temperature_c", 39.1, 2)]
        public void Each_reading_scores_the_cell_it_falls_in_and_the_limits_belong_to_the_cell_the_chart_gives_them(string parameter, double value, int points)
        {
            var obs = parameter switch
            {
                "respiratory_rate" => Normal(rr: value),
                "spo2" => Normal(spo2: value),
                "systolic_bp" => Normal(sbp: value),
                "heart_rate" => Normal(hr: value),
                _ => Normal(temp: value),
            };
            Assert.Equal(points, Calc.Score(obs).Parts.Single(p => p.Parameter == parameter).Points);
        }

        [Fact]
        public void A_reading_is_rounded_to_the_precision_of_the_chart_before_it_is_scored()
        {
            Assert.Equal(2, Calc.Score(Normal(spo2: 91.5)).Parts.Single(p => p.Parameter == "spo2").Points);          // 91.5 reads as 92
            Assert.Equal(3, Calc.Score(Normal(spo2: 91.4)).Parts.Single(p => p.Parameter == "spo2").Points);
            Assert.Equal(0, Calc.Score(Normal(temp: 36.05)).Parts.Single(p => p.Parameter == "temperature_c").Points);   // 36.05 reads as 36.1
            Assert.Equal(1, Calc.Score(Normal(temp: 36.04)).Parts.Single(p => p.Parameter == "temperature_c").Points);   // 36.0
            Assert.Equal(2, Calc.Score(Normal(rr: 20.5)).Parts.Single(p => p.Parameter == "respiratory_rate").Points);          // 20.5 reads as 21
        }

        [Fact]
        public void Extra_oxygen_adds_2_and_anything_but_alert_adds_3()
        {
            Assert.Equal(2, Calc.Score(Normal(oxygen: true)).Total);
            Assert.Equal(0, Calc.Score(Normal(oxygen: false)).Total);
            Assert.Equal(0, Calc.Score(Normal(acvpu: "A")).Total);
            foreach (var level in new[] { "C", "V", "P", "U" }) Assert.Equal(3, Calc.Score(Normal(acvpu: level)).Total);
        }

        // ---------------------------------------------------------------- scale 2 (target 88 to 92% in hypercapnic failure; set by the clinician)

        [Theory]
        [InlineData(83, false, 3)]
        [InlineData(84, false, 2)]
        [InlineData(85, false, 2)]
        [InlineData(86, false, 1)]
        [InlineData(87, false, 1)]
        [InlineData(88, false, 0)]
        [InlineData(92, false, 0)]
        [InlineData(93, false, 0)]
        [InlineData(99, false, 0)]
        [InlineData(88, true, 0)]
        [InlineData(92, true, 0)]
        [InlineData(93, true, 1)]
        [InlineData(94, true, 1)]
        [InlineData(95, true, 2)]
        [InlineData(96, true, 2)]
        [InlineData(97, true, 3)]
        [InlineData(100, true, 3)]
        public void Scale_2_scores_SpO2_against_the_88_to_92_target_and_on_oxygen_above_it_counts_against_the_patient(double spo2, bool oxygen, int points)
        {
            var r = Calc.Score(Normal(spo2: spo2, oxygen: oxygen), scale: 2);
            Assert.Equal(points, r.Parts.Single(p => p.Parameter == "spo2").Points);
        }

        [Fact]
        public void The_same_SpO2_can_score_differently_on_the_two_scales()
        {
            Assert.Equal(2, Calc.Score(Normal(spo2: 92), scale: 1).Parts.Single(p => p.Parameter == "spo2").Points);
            Assert.Equal(0, Calc.Score(Normal(spo2: 92), scale: 2).Parts.Single(p => p.Parameter == "spo2").Points);
        }

        // ---------------------------------------------------------------- the response tier

        [Fact]
        public void Totals_0_and_1_to_4_select_their_own_rows()
        {
            var zero = Calc.Score(Normal());
            Assert.Equal("0", zero.Tier);
            var one = Calc.Score(Normal(hr: 95));                              // pulse 91 to 110 scores 1
            Assert.Equal(1, one.Total); Assert.Equal("1-4", one.Tier); Assert.Equal("At least every 4–6 hours", one.Frequency); Assert.Equal("Nurse decides whether to call a doctor", one.Responder);
            var four = Calc.Score(Normal(hr: 95, rr: 22, temp: 38.5));          // 1 + 2 + 1
            Assert.Equal(4, four.Total); Assert.Equal("1-4", four.Tier);
        }

        [Fact]
        public void Totals_5_6_and_7_or_more_select_their_own_rows_even_when_a_single_reading_scores_3()
        {
            // 5: oxygen 2 + breathing 22 (2) + pulse 95 (1) = 5
            var five = Calc.Score(Normal(oxygen: true, rr: 22, hr: 95));
            Assert.Equal(5, five.Total); Assert.Equal("5-6", five.Tier); Assert.Equal("Urgent review by a doctor skilled in acutely ill patients", five.Responder);
            // 6: add temperature 38.5 (1)
            var six = Calc.Score(Normal(oxygen: true, rr: 22, hr: 95, temp: 38.5));
            Assert.Equal(6, six.Total); Assert.Equal("5-6", six.Tier);
            // 7: add blood pressure 105 (1)
            var seven = Calc.Score(Normal(oxygen: true, rr: 22, hr: 95, temp: 38.5, sbp: 105));
            Assert.Equal(7, seven.Total); Assert.Equal("7+", seven.Tier);
            // 5 with a single 3 (confusion): the total decides
            var five3 = Calc.Score(Normal(acvpu: "C", rr: 22));
            Assert.Equal(5, five3.Total); Assert.True(five3.Single3); Assert.Equal("5-6", five3.Tier);
        }

        [Fact]
        public void A_low_total_with_one_reading_scoring_3_still_needs_an_urgent_ward_review()
        {
            var r = Calc.Score(Normal(sbp: 88));
            Assert.Equal(3, r.Total);
            Assert.True(r.Single3);
            Assert.Equal("single3", r.Tier);
            Assert.Equal("Urgent review by the ward doctor", r.Responder);
            Assert.Equal("At least hourly", r.Frequency);
            Assert.Equal("single3", Calc.Score(Normal(acvpu: "V")).Tier);       // new confusion scores 3 on its own
            Assert.Equal("1-4", Calc.Score(Normal(hr: 95, rr: 22)).Tier);        // 3 points from separate readings is not a single 3
            Assert.False(Calc.Score(Normal(hr: 95, rr: 22)).Single3);
        }

        // ---------------------------------------------------------------- missing readings and pregnancy

        [Fact]
        public void A_missing_reading_makes_the_score_incomplete_with_no_tier_and_names_what_is_missing_in_chart_order()
        {
            var r = Calc.Score(new News2Input(null, 98, null, 70, null, null, false));
            Assert.False(r.Complete);
            Assert.Equal("", r.Tier);
            Assert.Equal(new[] { "respiratory_rate", "systolic_bp", "acvpu", "temperature_c" }, r.Missing);
            Assert.Equal(0, r.Total);                                          // what has been scored so far
            Assert.Equal(new[] { "supplemental_oxygen" }, Calc.Score(Normal(oxygen: null)).Missing);
        }

        [Fact]
        public void A_pregnant_patient_is_not_scored_with_NEWS2()
        {
            var r = Calc.Score(Normal(rr: 26), pregnant: true);
            Assert.True(r.Disabled);
            Assert.False(r.Complete);
            Assert.Equal("", r.Tier);
            Assert.Empty(r.Parts);
        }

        [Fact]
        public void Impossible_requests_are_refused()
        {
            Assert.Throws<ArgumentException>(() => Calc.Score(Normal(), scale: 3));
            Assert.Throws<ArgumentException>(() => Calc.Score(Normal(acvpu: "X")));
        }

        // ---------------------------------------------------------------- the engine: scores kept as the trainee gathers observations

        private static SimAction Act(string verb, params (string k, string v)[] p) => new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        private static ActionOutcome Do(Session s, string verb, params (string k, string v)[] p) => s.Apply(Act(verb, p));

        private static Session NewSession(SessionMode mode = SessionMode.Practice, CaseFile? file = null)
        {
            var chain = file == null ? Subsystems.Default(Content)
                : Subsystems.Default(Content).Where(x => !(x is News2Subsystem)).Concat(new ISubsystem[] { new News2Subsystem(Content, file) });
            return new Session(Content.Cases["VAR-S1-A"], 42, mode, chain);
        }

        private static void TakeAll(Session s)
        {
            Do(s, "attach_sensor", ("sensor", "spo2")); Do(s, "attach_sensor", ("sensor", "nibp_cuff")); Do(s, "measure_nibp");
            Do(s, "measure_temperature"); Do(s, "count_rr"); Do(s, "assess_acvpu");
            s.Tick(Content.Nibp.CycleSecondsMax + 1);
        }

        [Fact]
        public void The_baseline_of_VAR_S1_A_gives_NEWS2_0_once_every_reading_is_known_and_not_before()
        {
            var s = NewSession();
            Do(s, "attach_sensor", ("sensor", "spo2")); Do(s, "measure_temperature"); Do(s, "count_rr"); Do(s, "assess_acvpu");
            Assert.Empty(s.Observable.News2);                                   // no blood pressure yet
            TakeAll(s);
            var n = s.Observable.News2.Single();
            Assert.Equal(0, n.Total);
            Assert.Equal("0", n.Tier);
            Assert.Equal("At least every 12 hours", n.Frequency);
            Assert.True(s.Patient.Clinical.HasFlag(News2Subsystem.ComputedFlag));
        }

        [Fact]
        public void Taking_a_reading_again_adds_a_new_score_and_nothing_else_does()
        {
            var s = NewSession();
            TakeAll(s);
            Do(s, "wait");
            Do(s, "ask_pain_score");
            Assert.Single(s.Observable.News2);
            Do(s, "measure_temperature");
            Assert.Equal(2, s.Observable.News2.Count);
        }

        [Fact]
        public void The_blood_pressure_that_arrives_on_a_tick_completes_the_score()
        {
            var s = NewSession();
            Do(s, "attach_sensor", ("sensor", "spo2")); Do(s, "attach_sensor", ("sensor", "nibp_cuff")); Do(s, "measure_temperature"); Do(s, "count_rr"); Do(s, "assess_acvpu"); Do(s, "measure_nibp");
            Assert.Empty(s.Observable.News2);
            s.Tick(Content.Nibp.CycleSecondsMax + 1);
            Assert.Single(s.Observable.News2);
        }

        [Fact]
        public void In_assessment_mode_the_chart_is_hidden_but_the_score_is_still_kept()
        {
            var s = NewSession(SessionMode.Assessment);
            TakeAll(s);
            Assert.Empty(s.Observable.News2);
            Assert.Single(s.Patient.Clinical.News2);
            Assert.Equal(0, s.Patient.Clinical.News2[0].Total);
        }

        [Fact]
        public void Once_the_trainee_knows_the_patient_is_pregnant_the_chart_says_NEWS2_is_not_used()
        {
            var file = ContentLoader.LoadCaseFile(new FileContentReader(TestCase.ContentRoot), "VAR-S1-H");
            Assert.True(file.Pregnant);
            var s = NewSession(file: file);
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Do(s, verb);
            TakeAll(s);
            Assert.Equal("0", s.Observable.News2.Single().Tier);                 // before the trainee knows, a normal NEWS2
            Do(s, "ask", ("topic", "pregnancy"));
            Do(s, "measure_temperature");
            var last = s.Observable.News2.Last();
            Assert.Equal("not_used", last.Tier);
            Assert.Equal(News2Subsystem.NotUsedNote, last.Note);
            Do(s, "measure_temperature");
            Assert.Equal(2, s.Observable.News2.Count);                           // the note is not repeated for every reading
        }

        [Fact]
        public void Only_cases_with_a_pregnant_patient_are_marked_pregnant()
        {
            var pregnant = new List<string>();
            foreach (var path in Directory.GetFiles(Path.Combine(TestCase.ContentRoot, "case_files"), "*.json"))
            {
                var f = ContentLoader.LoadCaseFile(new FileContentReader(TestCase.ContentRoot), Path.GetFileNameWithoutExtension(path));
                if (f.Pregnant) pregnant.Add(f.Id);
            }
            Assert.Equal(new[] { "SC-2", "SC-3", "STK-S2-D", "VAR-S1-H", "VAR-S2-B", "VAR-S2-H" }, pregnant.OrderBy(x => x));
        }

        [Fact]
        public void The_scale_2_flag_switches_the_engine_to_the_oxygen_target_scale()
        {
            var s = NewSession();
            s.Patient.Clinical.SetFlag(News2Subsystem.Scale2Flag);
            var v = s.Patient.Physiological.Vitals;
            s.Patient.Physiological.Vitals = v with { Spo2 = 90 };               // 90% scores 2 on scale 1 and 0 on scale 2
            TakeAll(s);
            Assert.Equal(0, s.Observable.News2.Single().Total);
        }

        // ---------------------------------------------------------------- the table itself

        private static string Edit(Action<System.Text.Json.Nodes.JsonNode> change)
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(TestCase.Repo, "content", "news2", "news2.json")))!;
            change(node);
            return node.ToJsonString();
        }

        [Fact]
        public void The_real_package_loads_the_chart_with_a_register_id_on_every_cell()
        {
            Assert.Equal(new[] { "heart_rate", "respiratory_rate", "spo2", "systolic_bp", "temperature_c" }, Content.News2.Cells.Keys.OrderBy(k => k));
            Assert.All(Content.News2.Cells.Values.SelectMany(c => c), c => Assert.Matches("^SR-[0-9]{4}$", c.Ref));
            Assert.Equal(1, Content.News2.Precision["temperature_c"]);
            Assert.Equal(0, Content.News2.Precision["spo2"]);
        }

        [Fact]
        public void A_broken_chart_is_refused_with_its_place()
        {
            Assert.Contains("0 to 3", Assert.Throws<ContentException>(() => News2Table.Parse(Edit(n => n["parameters"]!["spo2"]![0]!["points"] = 4))).Message);
            Assert.Contains("above max", Assert.Throws<ContentException>(() => News2Table.Parse(Edit(n => n["parameters"]!["heart_rate"]![1]!["min"] = 500))).Message);
            Assert.Contains("unknown field", Assert.Throws<ContentException>(() => News2Table.Parse(Edit(n => n["colour"] = 1))).Message);
            Assert.Contains("missing", Assert.Throws<ContentException>(() => News2Table.Parse(Edit(n => n["response"]!.AsArray().RemoveAt(2)))).Message);
        }
    }
}
