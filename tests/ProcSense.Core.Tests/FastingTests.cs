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
    /// <summary>The fasting rule R-FAST-01 (p0-clinical-logic-s15): judged at the time of anaesthesia, with the three walkthrough cases and the edge of every limit.</summary>
    public class FastingTests
    {
        private static readonly ContentPackage Content = TestCase.Package();
        private static readonly FastingRules Rules = Content.Fasting;

        private static CaseFile File_(string id) => ContentLoader.LoadCaseFile(new FileContentReader(TestCase.ContentRoot), id);

        private static Intake Take(string kind, double? hours) => new Intake(kind, hours);

        private static (Intake food, Intake drink, double toAnaesthesia, bool emergency) FromCase(string id)
        {
            var f = File_(id);
            var food = f.History.Single(h => h.Topic == "last_food").Intake!;
            var drink = f.History.Single(h => h.Topic == "last_drink").Intake!;
            bool emergency = !f.Presentation.SurgeryTime.HasValue;
            return (new Intake(food.Kind, food.HoursBeforeNow), new Intake(drink.Kind, drink.HoursBeforeNow), emergency ? 0 : (f.Presentation.SurgeryTime!.Value - f.Presentation.TimeNow).TotalHours, emergency);
        }

        private static FastingResult Assess(string id)
        {
            var c = FromCase(id);
            return Fasting.Assess(Rules, c.food, c.drink, c.toAnaesthesia, c.emergency);
        }

        // ---------------------------------------------------------------- the three cases in the plan

        [Fact]
        public void VAR_S1_A_water_at_06_30_and_dinner_the_night_before_meet_the_rule_at_anaesthesia_time()
        {
            var r = Assess("VAR-S1-A");
            Assert.Equal("meets", r.Status);
            Assert.Equal(0, r.HoursUntilSafe);
            Assert.Equal("", r.Plan);
            // dinner 11 h before 08:00 and surgery at 09:00: 12 h at anaesthesia (needs 8); water 1.5 h before 08:00: 2.5 h (needs 2)
            Assert.Equal(new[] { 12.0, 2.5 }, r.Items.Select(i => i.HoursAtAnaesthesia!.Value));
            Assert.Equal(new[] { 8.0, 2.0 }, r.Items.Select(i => i.RequiredHours));
        }

        [Fact]
        public void VAR_S1_G_toast_and_milky_tea_at_07_00_are_not_safe_until_13_00()
        {
            var r = Assess("VAR-S1-G");
            Assert.Equal("breach", r.Status);
            Assert.All(r.Items, i => Assert.Equal("breach", i.Status));
            Assert.Equal(5.0, r.HoursUntilSafe);                    // 07:00 + 6 hours = 13:00; it is 08:00 now
            Assert.Equal(new[] { "light_meal", "milk" }, r.Items.Select(i => i.Kind));
        }

        [Fact]
        public void VAR_S2_F_biryani_two_hours_ago_is_a_full_stomach_so_the_plan_is_RSI_not_cancellation()
        {
            var r = Assess("VAR-S2-F");
            Assert.Equal("breach", r.Status);
            Assert.Equal("rsi", r.Plan);
            Assert.Equal(6.0, r.HoursUntilSafe);                    // heavy meal needs 8 hours, 2 have passed
        }

        // ---------------------------------------------------------------- the edge of every limit (inclusive)

        [Theory]
        [InlineData("clear_liquids", 2.0, "meets")]
        [InlineData("clear_liquids", 1.99, "breach")]
        [InlineData("milk", 6.0, "meets")]
        [InlineData("milk", 5.99, "breach")]
        [InlineData("light_meal", 6.0, "meets")]
        [InlineData("light_meal", 5.99, "breach")]
        [InlineData("heavy_meal", 8.0, "meets")]
        [InlineData("heavy_meal", 7.99, "breach")]
        public void Each_kind_needs_its_exact_hours_at_anaesthesia_and_the_limit_itself_meets(string kind, double hoursAtAnaesthesia, string status)
        {
            // the other intake is long past, so only this one decides; anaesthesia is 1 hour away
            var food = kind == "clear_liquids" ? Take("none", null) : Take(kind, hoursAtAnaesthesia - 1);
            var drink = kind == "clear_liquids" ? Take(kind, hoursAtAnaesthesia - 1) : Take("none", null);
            Assert.Equal(status, Fasting.Assess(Rules, food, drink, 1, false).Status);
        }

        [Fact]
        public void The_hours_come_from_the_verified_table()
        {
            Assert.Equal(new Dictionary<string, double> { ["clear_liquids"] = 2, ["heavy_meal"] = 8, ["light_meal"] = 6, ["milk"] = 6 }, Rules.Hours);
            Assert.Equal(8, Rules.UnknownKindHours);
        }

        // ---------------------------------------------------------------- what is not known

        [Fact]
        public void Until_both_the_food_and_the_drink_are_known_the_status_is_unknown()
        {
            Assert.Equal("unknown", Fasting.Assess(Rules, Take("heavy_meal", 12), null, 1, false).Status);
            Assert.Equal("unknown", Fasting.Assess(Rules, null, Take("clear_liquids", 3), 1, false).Status);
        }

        [Theory]
        [InlineData(7.9, "uncertain")]    // what it was is not known, so the longest fast is asked for; falling short is not called a breach
        [InlineData(8.0, "meets")]
        public void An_unknown_kind_is_held_to_the_longest_fast(double hoursAtAnaesthesia, string status)
        {
            Assert.Equal(status, Fasting.Assess(Rules, Take("unknown", hoursAtAnaesthesia - 1), Take("none", null), 1, false).Status);
        }

        [Fact]
        public void A_meal_with_no_time_is_uncertain_and_nothing_eaten_meets()
        {
            Assert.Equal("uncertain", Fasting.Assess(Rules, Take("unknown", null), Take("none", null), 1, false).Status);
            Assert.Equal("uncertain", Fasting.Assess(Rules, Take("heavy_meal", null), Take("none", null), 1, false).Status);
            Assert.Equal("meets", Fasting.Assess(Rules, Take("none", null), Take("none", null), 1, false).Status);
        }

        [Fact]
        public void A_breach_wins_over_an_uncertain_item_and_an_emergency_with_an_uncertain_stomach_still_gets_the_RSI_plan()
        {
            var r = Fasting.Assess(Rules, Take("unknown", null), Take("clear_liquids", 0.5), 0, true);
            Assert.Equal("breach", r.Status);
            Assert.Equal("rsi", r.Plan);
            Assert.Equal("rsi", Fasting.Assess(Rules, Take("unknown", null), Take("none", null), 0, true).Plan);
            Assert.Equal("", Fasting.Assess(Rules, Take("none", null), Take("none", null), 0, true).Plan);
        }

        [Fact]
        public void Anaesthesia_cannot_be_before_now()
        {
            Assert.Throws<ArgumentException>(() => Fasting.Assess(Rules, Take("none", null), Take("none", null), -1, false));
        }

        // ---------------------------------------------------------------- the rule inside a session

        private static Session NewSession(CaseFile? file = null)
        {
            var chain = file == null ? Subsystems.Default(Content)
                : Subsystems.Default(Content).Where(x => !(x is FastingSubsystem)).Concat(new ISubsystem[] { new FastingSubsystem(Content, file) });
            var s = new Session(Content.Cases["VAR-S1-A"], 42, SessionMode.Practice, chain);
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.True(s.Apply(Act(verb)).Accepted, verb);
            return s;
        }

        private static SimAction Act(string verb, params (string k, string v)[] p) => new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        private static ActionOutcome Ask(Session s, string topic) => s.Apply(Act("ask", ("topic", topic)));

        [Fact]
        public void The_chip_stays_unknown_until_both_questions_are_asked_then_shows_the_result()
        {
            var s = NewSession();
            Assert.Equal("unknown", s.Observable.Fasting.Status);
            Ask(s, "last_food");
            Assert.Equal("unknown", s.Observable.Fasting.Status);
            Assert.Equal("pending", s.Observable.Goals.Single(g => g.Id == "G-FASTING").State);

            var o = Ask(s, "last_drink");
            Assert.Contains("fasting:meets", o.Effects);
            Assert.Equal("meets", s.Observable.Fasting.Status);
            Assert.Equal("", s.Observable.Fasting.SafeFrom);
            Assert.Equal("complete", s.Observable.Goals.Single(g => g.Id == "G-FASTING").State);
            Assert.False(s.Patient.Clinical.HasFlag(FastingSubsystem.BreachFlag));
            Assert.Empty(s.Observable.Decisions);
        }

        [Fact]
        public void A_breach_for_a_planned_patient_shows_the_safe_time_and_opens_the_decision_DP_S1_FAST()
        {
            var s = NewSession(File_("VAR-S1-G"));
            Ask(s, "last_food"); Ask(s, "last_drink");
            Assert.Equal("breach", s.Observable.Fasting.Status);
            Assert.StartsWith("13:0", s.Observable.Fasting.SafeFrom);
            Assert.True(s.Patient.Clinical.HasFlag(FastingSubsystem.BreachFlag));
            Assert.Equal("DP-S1-FAST", s.Observable.Decisions.Single().Id);
            Assert.False(s.Patient.Clinical.HasFlag(FastingSubsystem.RsiFlag));
        }

        [Fact]
        public void Carrying_on_despite_the_breach_is_scored_critical_and_informing_the_anaesthetist_is_the_expected_choice()
        {
            var proceed = NewSession(File_("VAR-S1-G"));
            Ask(proceed, "last_food"); Ask(proceed, "last_drink");
            var bad = proceed.Apply(Act("decide", ("dp", "DP-S1-FAST"), ("option", "proceed")));
            Assert.Contains("score:critical.proceed_despite_fasting_breach", bad.Effects);
            Assert.Contains(bad.Effects, e => e.EndsWith("not_expected"));
            Assert.True(proceed.Patient.Clinical.HasFlag("fasting.proceeded_despite_breach"));

            var good = NewSession(File_("VAR-S1-G"));
            var events = new List<WorkflowEvent>();
            good.Events.Subscribe<WorkflowEvent>(events.Add);
            Ask(good, "last_food"); Ask(good, "last_drink");
            var ok = good.Apply(Act("decide", ("dp", "DP-S1-FAST"), ("option", "inform_and_delay")));
            Assert.Contains(ok.Effects, e => e.EndsWith("as_expected"));
            Assert.Contains(events, e => e.Name == "E-OT-DELAY");
            Assert.True(good.Patient.Clinical.HasFlag("fasting.anaesthetist_informed"));
        }

        [Fact]
        public void An_emergency_patient_with_a_full_stomach_gets_the_RSI_flag_and_no_decision_to_cancel()
        {
            var s = NewSession(File_("VAR-S2-F"));
            Ask(s, "last_food"); Ask(s, "last_drink");
            Assert.Equal("rsi", s.Observable.Fasting.Plan);
            Assert.True(s.Patient.Clinical.HasFlag(FastingSubsystem.RsiFlag));
            Assert.False(s.Patient.Clinical.HasFlag(FastingSubsystem.BreachFlag));
            Assert.Empty(s.Observable.Decisions);
        }

        [Fact]
        public void The_fasting_status_is_worked_out_once_and_does_not_change_when_more_is_asked()
        {
            var s = NewSession(File_("VAR-S1-G"));
            Ask(s, "last_food"); Ask(s, "last_drink");
            string before = s.Observable.Fasting.SafeFrom;
            s.Apply(Act("wait"));
            Ask(s, "medicines");
            Assert.Equal(before, s.Observable.Fasting.SafeFrom);
        }

        [Fact]
        public void A_case_without_a_case_file_stays_unknown()
        {
            var content = ContentLoader.Load(new FileContentReader(TestCase.ContentRoot), new[] { "VAR-S1-A" }, new string[0]);
            var s = new Session(content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(content));
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) s.Apply(Act(verb));
            Ask(s, "last_food"); Ask(s, "last_drink");
            Assert.Equal("unknown", s.Observable.Fasting.Status);
        }

        // ---------------------------------------------------------------- the 53 case files and the table

        [Fact]
        public void Every_case_file_records_what_was_last_eaten_and_drunk()
        {
            foreach (var path in Directory.GetFiles(Path.Combine(TestCase.ContentRoot, "case_files"), "*.json"))
            {
                var f = File_(Path.GetFileNameWithoutExtension(path));
                foreach (var topic in new[] { "last_food", "last_drink" })
                {
                    var h = f.History.Single(x => x.Topic == topic);
                    Assert.NotNull(h.Intake);
                    if (h.Intake!.Kind != "none" && h.Intake.Kind != "unknown") Assert.True(h.Intake.HoursBeforeNow.HasValue, f.Id + " " + topic);
                }
            }
        }

        private static string Edit(Action<System.Text.Json.Nodes.JsonNode> change)
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(TestCase.Repo, "content", "fasting", "fasting.json")))!;
            change(node);
            return node.ToJsonString();
        }

        [Fact]
        public void A_fasting_table_with_zero_hours_or_a_wrong_unknown_limit_is_refused()
        {
            Assert.Contains("more than 0", Assert.Throws<ContentException>(() => FastingRules.Parse(Edit(n => n["classes"]!["milk"]!["hours"] = 0))).Message);
            Assert.Contains("longest fast", Assert.Throws<ContentException>(() => FastingRules.Parse(Edit(n => n["unknown_kind_hours"] = 6))).Message);
            Assert.Contains("unknown field", Assert.Throws<ContentException>(() => FastingRules.Parse(Edit(n => n["colour"] = 1))).Message);
        }
    }
}
