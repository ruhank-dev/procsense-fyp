using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Clinical;
using ProcSense.Core.Content;
using ProcSense.Core.Workflow;
using ProcSense.Core.Workflow.Steps;

namespace ProcSense.Core.Tests
{
    /// <summary>Workflow Step 10: the ward / ED checklist (p0-simulation-workflow-s12, plan tests T10-1 and T10-2).</summary>
    public class WardChecklistTests
    {
        private static readonly ContentPackage Content = TestCase.Package();
        private static WardTables Tables => Content.Ward;

        private static SimAction Act(string verb, params (string k, string v)[] p) => new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        private static ActionOutcome Do(Session s, string verb, params (string k, string v)[] p) => s.Apply(Act(verb, p));

        private static string Reject(ActionOutcome o) { Assert.False(o.Accepted); return o.RejectReason; }

        private static Session Start()
        {
            var s = new Session(Content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(Content));
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.True(Do(s, verb).Accepted, verb);
            return s;
        }

        private static void Prepare(Session s, string item, string detail) => Assert.True(Do(s, "prepare", ("item", item), ("detail", detail)).Accepted, item);

        private static void Tick(Session s, string item) => Assert.True(Do(s, "tick_ward_item", ("item", item)).Accepted, item);

        // Everything the engine can verify is true: fasting known and met, temperature measured, pregnancy known, consent signed, bands, and the physical things done.
        private static Session ReadyWard()
        {
            var s = Start();
            Do(s, "ask", ("topic", "last_food")); Do(s, "ask", ("topic", "last_drink")); Do(s, "ask", ("topic", "pregnancy"));
            s.Patient.Clinical.Fasting = new FastingResult("meets", new List<FastingItem>(), 0, "");
            Do(s, "attach_sensor", ("sensor", "spo2")); Do(s, "measure_temperature");
            s.Patient.Clinical.SetFlag("consent.signed");                                      // the consent step sets this
            foreach (var (item, detail) in new[] { ("gown", "worn"), ("jewellery", "removed"), ("aids", "noted"), ("site_marking", "not_applicable"), ("documents", "together") }) Prepare(s, item, detail);
            return s;
        }

        private static void TickAllNeeded(Session s) { foreach (var i in s.Ward!.Needed()) Tick(s, i.Id); }

        // ---------------------------------------------------------------- the content

        [Fact]
        public void The_content_has_the_thirteen_lines_of_Step_10()
        {
            Assert.Equal(new[] { "fasting", "gown", "jewellery", "aids", "hair", "site_marking", "stockings", "pregnancy", "consent", "bands", "cbg", "temperature", "documents" }, Tables.Items.Select(i => i.Id));
            Assert.Equal(new[] { "pregnancy", "consent", "bands" }, Tables.Items.Where(i => i.Critical).Select(i => i.Id));
            Assert.Equal(new[] { "hair", "stockings", "cbg" }, Tables.Items.Where(i => !i.Required).Select(i => i.Id));
            Assert.Equal("ward.checklist_signed", Tables.SignedFlag);
        }

        // ---------------------------------------------------------------- prepare

        [Fact]
        public void A_preparation_needs_a_known_item_and_a_known_way_of_doing_it()
        {
            var s = Start();
            Assert.Equal(WardChecklistSubsystem.MissingItem, Reject(Do(s, "prepare", ("detail", "worn"))));
            Assert.Equal(WardChecklistSubsystem.UnknownItem, Reject(Do(s, "prepare", ("item", "tie"), ("detail", "worn"))));
            Assert.Equal(WardChecklistSubsystem.NothingToDo, Reject(Do(s, "prepare", ("item", "consent"), ("detail", "signed"))));      // consent is a fact, not something the trainee does here
            Assert.Equal(WardChecklistSubsystem.MissingDetail, Reject(Do(s, "prepare", ("item", "gown"))));
            Assert.Equal(WardChecklistSubsystem.UnknownDetail, Reject(Do(s, "prepare", ("item", "gown"), ("detail", "inside_out"))));
        }

        [Fact]
        public void Jewellery_left_on_then_removed_is_fine_in_the_end_and_a_razor_is_an_infection_risk_item()
        {
            var s = Start();
            Prepare(s, "jewellery", "left_on");
            Assert.False(s.Ward!.Verified("jewellery"));
            Prepare(s, "jewellery", "taped");
            Assert.True(s.Ward.Verified("jewellery"));
            var o = Do(s, "prepare", ("item", "hair"), ("detail", "razor"));
            Assert.Contains("infection_risk:razor", o.Effects);
            Assert.Contains(s.Ward.History, h => h.Kind == "razor_used" && h.ScoreItems.Contains("minor.razor_used"));
            Prepare(s, "hair", "clippers");
            Assert.False(s.Ward.Verified("hair"));                                              // the razor is not forgotten
        }

        // ---------------------------------------------------------------- ticks are verified against the state (T10-1)

        [Fact]
        public void T10_1_a_true_tick_is_accepted_quietly()
        {
            var s = ReadyWard();
            var o = Do(s, "tick_ward_item", ("item", "gown"));
            Assert.Contains("ticked:gown", o.Effects);
            Assert.DoesNotContain(o.Effects, e => e.StartsWith("discrepancy"));
            Assert.Empty(s.Ward!.History);
        }

        [Fact]
        public void T10_1_a_tick_that_contradicts_the_state_is_an_item_and_a_discrepancy_event()
        {
            var s = Start();
            var events = new List<WorkflowEvent>();
            s.Events.Subscribe<WorkflowEvent>(events.Add);
            var o = Do(s, "tick_ward_item", ("item", "gown"));                                  // no gown has been put on
            Assert.Contains("discrepancy:gown", o.Effects);
            Assert.Contains(events, e => e.Name == "E-WARD-DISCREPANCY" && e.Source == "gown");
            Assert.Contains("minor.ward_tick_unverified", s.Ward!.History.Single().ScoreItems);
        }

        [Fact]
        public void A_false_tick_of_consent_pregnancy_or_identity_is_critical()
        {
            var s = Start();
            Tick(s, "consent");
            Tick(s, "pregnancy");
            Assert.Equal(new[] { "critical.ward_tick_false", "critical.ward_tick_false" }, s.Ward!.History.SelectMany(h => h.ScoreItems));
        }

        [Fact]
        public void E10_A_fasted_ticked_during_a_breach_is_a_discrepancy_until_the_anaesthetist_is_told()
        {
            var s = ReadyWard();
            s.Patient.Clinical.Fasting = new FastingResult("breach", new List<FastingItem>(), 4, "");
            Assert.Contains("discrepancy:fasting", Do(s, "tick_ward_item", ("item", "fasting")).Effects);
            Assert.True(Do(s, "tell_anaesthetist", ("topic", "fasting")).Accepted);
            Assert.True(s.Ward!.Verified("fasting"));
            Assert.Contains(s.Ward.History, h => h.Kind == "late_fasting_passed" && h.ScoreItems.Contains("credit.late_fasting_passed"));
        }

        [Fact]
        public void The_temperature_must_be_measured_and_at_least_36()
        {
            var s = Start();
            Assert.False(s.Ward!.Verified("temperature"));                                      // never measured
            Do(s, "attach_sensor", ("sensor", "spo2")); Do(s, "measure_temperature");
            Assert.True(s.Ward.Verified("temperature"));                                        // VAR-S1-A is 36.7
        }

        [Fact]
        public void A_diabetic_needs_the_sugar_recorded()
        {
            var s = Start();
            Assert.DoesNotContain(s.Ward!.Needed(), i => i.Id == "cbg");
            s.Patient.Clinical.SetFlag("condition.diabetes");
            Assert.Contains(s.Ward.Needed(), i => i.Id == "cbg");
            Prepare(s, "cbg", "recorded");
            Assert.True(s.Ward.Verified("cbg"));
        }

        // ---------------------------------------------------------------- signing and transfer (T10-2)

        [Fact]
        public void T10_2_the_checklist_cannot_be_signed_until_every_needed_line_is_ticked_and_the_nurse_says_which_are_missing()
        {
            var s = ReadyWard();
            Tick(s, "gown");
            var o = Do(s, "sign_ward_checklist");
            Assert.Equal(WardChecklistSubsystem.NotAllTicked, Reject(o));
            Assert.Contains(o.Effects, e => e.StartsWith("not_ticked:") && e.Contains("consent") && !e.Contains("gown"));
            Assert.False(s.Ward!.Signed);
        }

        [Fact]
        public void T10_2_a_complete_checklist_is_signed_credited_and_lets_the_transfer_through()
        {
            var s = ReadyWard();
            var transfer = new[] { ("mode", "trolley"), ("escort", "porter,nurse") };
            Assert.Equal("BLOCKED:B-WARD", Reject(Do(s, "transfer_to_theatre", transfer)));
            TickAllNeeded(s);
            var o = Do(s, "sign_ward_checklist");
            Assert.True(o.Accepted, o.RejectReason);
            Assert.True(s.Ward!.Signed);
            Assert.Contains(s.Ward.History, h => h.Kind == "ward_checklist_signed" && h.ScoreItems.Contains("credit.ward_checklist_complete"));
            Assert.NotEqual("BLOCKED:B-WARD", Do(s, "transfer_to_theatre", transfer).RejectReason);
            Assert.Equal(WardChecklistSubsystem.AlreadySigned, Reject(Do(s, "sign_ward_checklist")));
        }

        [Fact]
        public void Signing_with_a_tick_that_is_not_true_is_a_major_item()
        {
            var s = ReadyWard();
            s.Patient.Clinical.ClearFlag("consent.signed");
            TickAllNeeded(s);
            var o = Do(s, "sign_ward_checklist");
            Assert.True(o.Accepted);
            Assert.Contains("gaps:consent", o.Effects);
            Assert.Contains("major.ward_signed_with_gaps", s.Ward!.History.Last().ScoreItems);
        }

        [Fact]
        public void A_late_fasting_breach_not_passed_to_the_anaesthetist_is_a_major_item_at_signing()
        {
            var s = ReadyWard();
            s.Patient.Clinical.Fasting = new FastingResult("breach", new List<FastingItem>(), 4, "");
            TickAllNeeded(s);
            Do(s, "sign_ward_checklist");
            Assert.Contains("major.late_fasting_not_passed", s.Ward!.History.Last().ScoreItems);
        }

        [Fact]
        public void A_tell_needs_a_known_topic()
        {
            var s = Start();
            Assert.Equal(WardChecklistSubsystem.MissingTopic, Reject(Do(s, "tell_anaesthetist")));
            Assert.Equal(WardChecklistSubsystem.UnknownTopic, Reject(Do(s, "tell_anaesthetist", ("topic", "gossip"))));
        }

        [Fact]
        public void Identity_must_be_confirmed_before_the_checklist_is_touched()
        {
            var s = new Session(Content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(Content));
            Assert.Equal("IDENTITY_NOT_CONFIRMED", Reject(Do(s, "prepare", ("item", "gown"), ("detail", "worn"))));
            Assert.Equal("IDENTITY_NOT_CONFIRMED", Reject(Do(s, "sign_ward_checklist")));
        }
    }
}
