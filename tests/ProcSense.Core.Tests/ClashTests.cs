using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Clinical;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Workflow;
using ProcSense.Core.Workflow.Steps;

namespace ProcSense.Core.Tests
{
    /// <summary>The three drug checks, the treatment clash table and the medicine plans (p0-clinical-logic-s12, plan test T-DE2).</summary>
    public class ClashTests
    {
        private static readonly ContentPackage Content = TestCase.Package();
        private static ClashTables Tables => Content.Clashes;

        private static CaseFile File_(string id) => ContentLoader.LoadCaseFile(new FileContentReader(TestCase.ContentRoot), id);

        private static SimAction Act(string verb, params (string k, string v)[] p) => new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        private static ActionOutcome Do(Session s, string verb, params (string k, string v)[] p) => s.Apply(Act(verb, p));

        /// <summary>A session for this case file with identity confirmed, allergies asked and pregnancy asked (so the pregnancy gate is open).</summary>
        private static Session Start(string caseId)
        {
            var file = File_(caseId);
            var chain = Subsystems.Default(Content).Where(x => !(x is ClashSubsystem) && !(x is ProblemsSubsystem)).Concat(new ISubsystem[] { new ClashSubsystem(Content, file), new ProblemsSubsystem(Content, file) });
            var s = new Session(Content.Cases["VAR-S1-A"], 42, SessionMode.Practice, chain);
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity", "ask_allergies" }) Assert.True(Do(s, verb).Accepted, verb);
            Do(s, "ask", ("topic", "pregnancy"));
            return s;
        }

        // ---------------------------------------------------------------- the content

        [Fact]
        public void The_content_has_every_clash_row_the_factors_and_the_plans()
        {
            Assert.Equal(18, Tables.Clashes.Count);
            Assert.Equal(18, Tables.Factors.Count);
            Assert.Equal(19, Tables.Plans.Count);
            Assert.All(Tables.Clashes, c => Assert.NotEmpty(c.When));
            Assert.Contains("pain_kidney", Tables.Clashes.Select(c => c.Id));
            Assert.Equal(new[] { "need_pain_relief" }, Tables.Needs["TR-NSAID"]);
        }

        [Fact]
        public void Five_clashes_need_facts_the_engine_does_not_know_yet_and_cannot_be_raised()
        {
            var unreachable = new[] { "insulin_nbm", "high_potassium_suxamethonium", "full_stomach_emergency", "low_oxygen_copd", "confused_consent_emergency" };
            var everything = new HashSet<string>(Tables.Factors.Select(f => f.Tag).Concat(Tables.Needs.Values.SelectMany(x => x)).Concat(Tables.Context).Append("pregnancy"));
            var raisable = Tables.Clashes.Where(c => c.When.Any(a => a.All(everything.Contains))).Select(c => c.Id);
            Assert.DoesNotContain(raisable, id => unreachable.Contains(id));
            Assert.Equal(13, raisable.Count());
        }

        // ---------------------------------------------------------------- the checker

        [Fact]
        public void T_DE2_pain_relief_for_a_patient_with_kidney_injury_raises_the_clash()
        {
            var tags = ClashChecker.TagsFor(Tables, "TR-NSAID", new[] { "kidney_injury" }, new string[0]);
            var hit = ClashChecker.Check(Tables, tags).Single();
            Assert.Equal("pain_kidney", hit.Clash.Id);
            Assert.Contains("NSAIDs", hit.Clash.Why);
            Assert.Contains("no NSAIDs", hit.Clash.Safer);
        }

        [Fact]
        public void No_factor_or_no_request_means_no_clash()
        {
            Assert.Empty(ClashChecker.Check(Tables, ClashChecker.TagsFor(Tables, "TR-NSAID", new string[0], new string[0])));
            Assert.Empty(ClashChecker.Check(Tables, ClashChecker.TagsFor(Tables, "TR-OXYGEN", new[] { "kidney_injury" }, new string[0])));
        }

        [Fact]
        public void A_clash_with_two_ways_of_arising_is_raised_by_either()
        {
            Assert.Equal("fluid_heart_kidney", ClashChecker.Check(Tables, ClashChecker.TagsFor(Tables, "TR-IV-FLUIDS", new[] { "heart_failure" }, new string[0])).Single().Clash.Id);
            Assert.Equal("fluid_heart_kidney", ClashChecker.Check(Tables, ClashChecker.TagsFor(Tables, "TR-IV-FLUIDS", new[] { "kidney_failure" }, new string[0])).Single().Clash.Id);
        }

        [Fact]
        public void The_context_matters_for_a_blood_thinner_and_urgent_surgery()
        {
            Assert.Empty(ClashChecker.Check(Tables, new[] { "blood_thinner" }));
            Assert.Equal("blood_thinner_urgent_surgery", ClashChecker.Check(Tables, new[] { "blood_thinner", "urgent_surgery" }).Single().Clash.Id);
        }

        [Fact]
        public void A_word_is_found_only_at_the_start_of_a_word()
        {
            Assert.True(PatientFactors.ContainsWord("mild dehydration today", "dehydrat"));
            Assert.False(PatientFactors.ContainsWord("a consistent finding", "stent"));
            Assert.True(PatientFactors.ContainsWord("heart stent in 2022", "stent"));
        }

        // ---------------------------------------------------------------- the factors of the real cases

        [Theory]
        [InlineData("SC-13", "blood_thinner")]          // warfarin not stopped
        [InlineData("STK-S1-B", "blood_thinner")]       // apixaban this morning
        [InlineData("STK-S1-A", "sglt2_user")]          // dapagliflozin
        [InlineData("VAR-S2-H", "pregnancy")]
        [InlineData("SC-1", "penicillin_allergy")]
        [InlineData("SC-1", "kidney_injury")]
        public void The_factors_of_the_real_cases_are_read_from_their_wording(string caseId, string tag)
        {
            Assert.Contains(tag, PatientFactors.From(File_(caseId), Tables));
        }

        [Fact]
        public void A_fit_patient_has_no_factors()
        {
            Assert.Empty(PatientFactors.From(File_("VAR-S1-A"), Tables));
        }

        // ---------------------------------------------------------------- the subsystem in a session

        [Fact]
        public void The_patients_factors_become_condition_flags_the_restrictions_read()
        {
            var s = Start("SC-1");
            Assert.True(s.Patient.Clinical.HasFlag("condition.kidney_injury"));
            Assert.True(s.Patient.Clinical.HasFlag("condition.penicillin_allergy"));
            Assert.Contains("kidney_injury", s.Clashes!.Factors);
        }

        [Fact]
        public void T_DE2_in_a_session_an_NSAID_for_AKI_raises_the_clash_once_and_a_safety_blocker_problem()
        {
            var s = Start("SC-1");
            var raised = new List<ClashRaised>();
            s.Events.Subscribe<ClashRaised>(raised.Add);
            var o = Do(s, "request_medication", ("class", "TR-NSAID"), ("indication", "pain"));
            Assert.True(o.Accepted);
            Assert.Contains("clash:pain_kidney", o.Effects);
            Assert.Single(raised);
            Assert.Equal(("pain_kidney", "TR-NSAID"), (raised[0].Clash, raised[0].Request));
            var p = s.Problems!.Ordered.Single(x => x.Id == "clash.pain_kidney");
            Assert.Equal("safety_blocker", p.Type);
            Do(s, "request_medication", ("class", "TR-NSAID"), ("indication", "pain again"));
            Assert.Single(s.Clashes!.History, h => h.Kind == "clash:pain_kidney");        // raised once
            Assert.Single(raised);
        }

        [Fact]
        public void A_request_the_workflow_refused_raises_nothing()
        {
            var s = Start("SC-1");
            var o = Do(s, "request_medication", ("class", "TR-NOT-A-TREATMENT"));
            Assert.False(o.Accepted);
            Assert.Empty(s.Clashes!.History);
        }

        [Fact]
        public void A_penicillin_allergy_with_an_antibiotic_request_raises_the_allergy_clash()
        {
            var s = Start("VAR-S1-I");
            Do(s, "request_iv_access");
            s.Tick(150);                                                                 // the antibiotic is an IV treatment: the line goes in first
            var o = Do(s, "request_medication", ("class", "TR-ABX-PROPHYLAXIS"), ("indication", "prevent infection"));
            Assert.True(o.Accepted, o.RejectReason);
            Assert.Contains("clash:antibiotic_penicillin_allergy", o.Effects);
        }

        [Fact]
        public void A_CT_for_a_pregnant_patient_raises_the_pregnancy_clash()
        {
            var s = Start("VAR-S2-H");
            var o = Do(s, "order_investigation", ("test", "ct"), ("mode", "trolley"), ("escort", "porter,nurse"), ("accompany", "true"));
            Assert.True(o.Accepted, o.RejectReason);
            Assert.Contains("clash:ct_pregnancy", o.Effects);
        }

        [Fact]
        public void An_emergency_case_with_a_blood_thinner_raises_the_urgent_surgery_clash_when_a_painkiller_is_requested()
        {
            var file = File_("SC-13") with { Scenario = "S2" };
            var chain = Subsystems.Default(Content).Where(x => !(x is ClashSubsystem)).Concat(new ISubsystem[] { new ClashSubsystem(Content, file) });
            var card = Content.Cases["VAR-S1-A"] with { Scenario = "S2" };
            var s = new Session(card, 42, SessionMode.Practice, chain);
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity", "ask_allergies" }) Do(s, verb);
            var o = Do(s, "request_medication", ("class", "TR-PARACETAMOL"), ("indication", "pain"));
            Assert.Contains("clash:blood_thinner_urgent_surgery", o.Effects);
        }

        // ---------------------------------------------------------------- the medicine plans

        [Fact]
        public void VAR_S1_C_diabetes_medicines_get_the_metformin_and_sulfonylurea_plans()
        {
            var plans = MedicinePlans.For(File_("VAR-S1-C"), Tables);
            Assert.Equal(new[] { "metformin", "sulfonylureas" }, plans.Select(p => p.Id));
            Assert.Equal("Omit", plans.Single(p => p.Id == "sulfonylureas").Plan);
            Assert.Contains("low sugar", plans.Single(p => p.Id == "sulfonylureas").Care);
        }

        [Fact]
        public void An_apixaban_patient_gets_the_DOAC_plan_and_a_warfarin_patient_the_warfarin_plan()
        {
            Assert.Contains(MedicinePlans.For(File_("STK-S1-B"), Tables), p => p.Id == "doacs" && p.Plan.Contains("1 day"));
            Assert.Contains(MedicinePlans.For(File_("SC-13"), Tables), p => p.Id == "warfarin" && p.Plan.Contains("5 days"));
        }

        [Fact]
        public void The_session_keeps_the_plans_for_its_case()
        {
            Assert.Contains(Start("STK-S1-A").Clashes!.Plans, p => p.Id == "sglt2");
        }
    }
}
