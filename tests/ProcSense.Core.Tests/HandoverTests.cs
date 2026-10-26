using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Clinical;
using ProcSense.Core.Content;
using ProcSense.Core.Workflow;
using ProcSense.Core.Workflow.Steps;

namespace ProcSense.Core.Tests
{
    /// <summary>Workflow Step 12: the SBAR handover at theatre reception (p0-simulation-workflow-s15, plan tests T12-1 to T12-3).</summary>
    public class HandoverTests
    {
        private static readonly ContentPackage Content = TestCase.Package();
        private static HandoverTables Tables => Content.Handover;

        private static SimAction Act(string verb, params (string k, string v)[] p) => new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        private static ActionOutcome Do(Session s, string verb, params (string k, string v)[] p) => s.Apply(Act(verb, p));

        private static string Reject(ActionOutcome o) { Assert.False(o.Accepted); return o.RejectReason; }

        // A session at the holding area with everything the planned case knows: the facts the menu rules read are set the way the earlier steps set them.
        private static Session Holding(string scenario = "S1", SessionMode mode = SessionMode.Practice, bool knowAll = true)
        {
            var card = Content.Cases["VAR-S1-A"] with { Scenario = scenario };
            var s = new Session(card, 42, mode, Subsystems.Default(Content));
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.True(Do(s, verb).Accepted, verb);
            if (knowAll)
            {
                Do(s, "ask_allergies"); Do(s, "ask", ("topic", "complaint")); Do(s, "ask", ("topic", "past_history")); Do(s, "ask", ("topic", "medicines")); Do(s, "ask", ("topic", "pregnancy"));
                Do(s, "order_investigation", ("test", "lft")); Do(s, "take_sample", ("test", "lft"), ("tube", "serum_gold"));
                s.Tick(3600);
                Do(s, "view_result", ("test", "lft"));                                           // a result the trainee has seen can be handed over
                s.Patient.Clinical.Fasting = new FastingResult("meets", new List<FastingItem>(), 0, "");
                foreach (var flag in new[] { "news2.computed", "consent.signed", "treatment.antibiotic_given", "treatment.fluids_given" }) s.Patient.Clinical.SetFlag(flag);
            }
            s.Patient.Clinical.TraineeLocation = "theatre_reception"; s.Patient.Clinical.Location = "theatre_reception";     // the patient and the trainee are both at the holding area
            return s;
        }

        private static void Hand(Session s, string section, string items) => Assert.True(Do(s, "handover", ("section", section), ("items", items)).Accepted, section + ":" + items);

        private static void GiveRequired(Session s) { foreach (var g in s.Handover!.Required().GroupBy(i => i.Section)) Hand(s, g.Key, string.Join(",", g.Select(i => i.Id))); }

        // ---------------------------------------------------------------- the content

        [Fact]
        public void The_content_has_the_SBAR_menu_and_the_two_required_sets()
        {
            Assert.Equal(new[] { "s", "b", "a", "r" }, Tables.Sections);
            Assert.Equal(14, Tables.Items.Count);
            var s1 = Tables.Items.Where(i => i.RequiredIn.Contains("S1")).Select(i => i.Id);
            var s2 = Tables.Items.Where(i => i.RequiredIn.Contains("S2")).Select(i => i.Id);
            Assert.Equal(new[] { "identity", "allergies", "history", "observations", "fasting", "results", "pregnancy", "consent", "recommendation" }, s1);
            Assert.Equal(new[] { "identity", "allergies", "observations", "fasting", "results", "pregnancy", "aspiration_risk", "antibiotic", "fluids", "recommendation" }, s2);
            Assert.Equal(new[] { "allergies", "fasting", "aspiration_risk" }, Tables.Items.Where(i => i.Severity == "critical").Select(i => i.Id));
        }

        // ---------------------------------------------------------------- giving it

        [Fact]
        public void The_handover_is_given_at_theatre_reception()
        {
            var s = Holding();
            s.Patient.Clinical.TraineeLocation = "patient_bay"; s.Patient.Clinical.Location = "patient_bay";
            Assert.Equal(HandoverSubsystem.NotAtHolding, Reject(Do(s, "handover", ("section", "s"), ("items", "identity"))));
            Assert.Equal(HandoverSubsystem.NotAtHolding, Reject(Do(s, "finish_handover")));
        }

        [Fact]
        public void A_section_needs_known_items_from_its_own_menu()
        {
            var s = Holding();
            Assert.Equal(HandoverSubsystem.MissingSection, Reject(Do(s, "handover", ("items", "identity"))));
            Assert.Equal(HandoverSubsystem.UnknownSection, Reject(Do(s, "handover", ("section", "x"), ("items", "identity"))));
            Assert.Equal(HandoverSubsystem.MissingItems, Reject(Do(s, "handover", ("section", "s"))));
            Assert.Equal(HandoverSubsystem.UnknownItem, Reject(Do(s, "handover", ("section", "s"), ("items", "shoe_size"))));
            Assert.Equal(HandoverSubsystem.WrongSection, Reject(Do(s, "handover", ("section", "s"), ("items", "allergies"))));
            Assert.Equal(HandoverSubsystem.NothingHandedOver, Reject(Do(s, "finish_handover")));
        }

        [Fact]
        public void A_fact_the_trainee_does_not_know_yet_cannot_be_handed_over()
        {
            var s = Holding(knowAll: false);
            var o = Do(s, "handover", ("section", "b"), ("items", "allergies"));
            Assert.Equal(HandoverSubsystem.UnknownFact, Reject(o));
            Assert.Contains("not_known:allergies", o.Effects);
            Do(s, "ask_allergies");
            Hand(s, "b", "allergies");
        }

        // ---------------------------------------------------------------- scoring (T12-1, T12-2)

        // The walkthrough's planned example: S: Ayesha Khan ... B: no allergies ... A: obs, fasting, LFTs, pregnancy, consent ... R: ready
        [Fact]
        public void T12_1_a_complete_planned_SBAR_gets_full_credit()
        {
            var s = Holding();
            GiveRequired(s);
            var o = Do(s, "finish_handover");
            Assert.Contains("handover_complete", o.Effects);
            Assert.DoesNotContain(o.Effects, e => e.StartsWith("missing:"));
            Assert.Contains("credit.handover_complete", s.Handover!.History.Last().ScoreItems);
            Assert.True(s.Handover.Results.Single().Complete);
        }

        [Fact]
        public void DT080_denying_a_fact_the_state_holds_is_worse_than_leaving_it_out()
        {
            var s = Holding();
            var item = s.Handover!.Required().First(i => i.Id == "allergies");
            var o = Do(s, "handover", ("section", item.Section), ("not", "allergies"));
            Assert.True(o.Accepted, o.RejectReason);
            Assert.Contains("contradicted:allergies", o.Effects);
            Assert.Contains("score:critical.handover_contradicted", o.Effects);                 // allergies are an item whose omission is critical, so a false denial is too
            Assert.Contains(s.Handover.History, h => h.Kind == "handover_contradicted" && h.ScoreItems.Contains("critical.handover_contradicted"));
        }

        [Fact]
        public void DT080_denying_something_the_patient_does_not_have_is_a_true_statement_and_scores_nothing()
        {
            var s = Holding(knowAll: false);
            var unknown = s.Handover!.Required().First(i => !Do(Holding(knowAll: false), "handover", ("section", i.Section), ("items", i.Id)).Accepted);      // an item the trainee cannot hand over yet is one the state does not hold
            var o = Do(s, "handover", ("section", unknown.Section), ("not", unknown.Id));
            Assert.True(o.Accepted, o.RejectReason);
            Assert.DoesNotContain(o.Effects, e => e.StartsWith("score:"));
        }

        [Fact]
        public void T12_2_leaving_out_the_allergy_is_critical_and_in_practice_the_anaesthetist_asks_for_it()
        {
            var s = Holding();
            foreach (var g in s.Handover!.Required().Where(i => i.Id != "allergies").GroupBy(i => i.Section)) Hand(s, g.Key, string.Join(",", g.Select(i => i.Id)));
            var o = Do(s, "finish_handover");
            Assert.Contains("missing:allergies", o.Effects);
            Assert.Contains("anaesthetist_asks:allergies", o.Effects);
            var h = s.Handover.History.Last();
            Assert.Equal("handover_incomplete", h.Kind);
            Assert.Contains("critical.handover_omitted", h.ScoreItems);
            Assert.DoesNotContain("credit.handover_complete", h.ScoreItems);
        }

        [Fact]
        public void In_assessment_the_anaesthetist_does_not_ask()
        {
            var s = Holding(mode: SessionMode.Assessment);
            Hand(s, "s", "identity");
            var o = Do(s, "finish_handover");
            Assert.Contains("missing:allergies", o.Effects);
            Assert.DoesNotContain(o.Effects, e => e.StartsWith("anaesthetist_asks"));
        }

        [Fact]
        public void Leaving_the_same_item_out_again_after_being_asked_is_a_further_major_item()
        {
            var s = Holding();
            Hand(s, "s", "identity");
            Do(s, "finish_handover");                                                           // asked for the rest
            var again = Do(s, "finish_handover");
            Assert.Contains("major.handover_repeat_omission", s.Handover!.History.Last().ScoreItems);
            Assert.Equal(2, s.Handover.Results.Count);
        }

        [Fact]
        public void Severity_follows_the_item_a_blood_thinner_or_antibiotic_time_is_major_and_the_rest_minor()
        {
            var s = Holding("S2");
            Hand(s, "s", "identity"); Hand(s, "b", "allergies");
            Do(s, "finish_handover");
            var items = s.Handover!.History.Last().ScoreItems;
            Assert.Contains("critical.handover_omitted", items);                                // fasting and aspiration risk
            Assert.Contains("major.handover_omitted", items);                                   // the antibiotic
            Assert.Contains("minor.handover_omitted", items);                                   // observations, results and the rest
        }

        // ---------------------------------------------------------------- the emergency case (E12-B)

        [Fact]
        public void An_emergency_case_must_include_the_aspiration_risk_antibiotic_fluids_and_the_rest_of_its_list()
        {
            var s = Holding("S2");
            Assert.Equal(new[] { "identity", "allergies", "observations", "fasting", "results", "pregnancy", "aspiration_risk", "antibiotic", "fluids", "recommendation" }, s.Handover!.Required().Select(i => i.Id));
            GiveRequired(s);
            Assert.Contains("handover_complete", Do(s, "finish_handover").Effects);
        }

        [Fact]
        public void An_antibiotic_or_fluids_not_yet_given_cannot_be_handed_over()
        {
            var s = Holding("S2");
            s.Patient.Clinical.ClearFlag("treatment.antibiotic_given");
            Assert.Equal(HandoverSubsystem.UnknownFact, Reject(Do(s, "handover", ("section", "a"), ("items", "antibiotic"))));
        }

        // ---------------------------------------------------------------- the transfer into the holding area (L12-01)

        [Fact]
        public void The_handover_can_be_given_once_the_patient_has_been_transferred_with_the_ward_checklist_signed()
        {
            var s = Holding(knowAll: false);
            s.Patient.Clinical.TraineeLocation = "patient_bay"; s.Patient.Clinical.Location = "patient_bay";
            var transfer = new[] { ("mode", "trolley"), ("escort", "porter,nurse"), ("accompany", "true") };
            Do(s, "ask", ("topic", "pregnancy"));
            Assert.Equal("BLOCKED:B-WARD", Reject(Do(s, "transfer_to_theatre", transfer)));
            s.Patient.Clinical.SetFlag("ward.checklist_signed"); s.Patient.Clinical.SetFlag("consent.signed");
            Assert.True(Do(s, "transfer_to_theatre", transfer).Accepted);
            for (int i = 0; i < 600 && s.Patient.Clinical.TraineeLocation != "theatre_reception"; i++) s.Tick(30);
            Assert.Equal("theatre_reception", s.Patient.Clinical.TraineeLocation);
            Hand(s, "s", "identity");
        }

        // T12-3: the form path works without any voice or AI
        [Fact]
        public void T12_3_the_handover_is_a_plain_form_so_it_works_when_speech_recognition_is_down()
        {
            var s = Holding();
            Hand(s, "s", "identity,reason");
            Assert.Equal(new[] { "identity", "reason" }, s.Handover!.Given);
        }
    }
}
