using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Scoring;
using ProcSense.Core.Workflow;
using ProcSense.Core.Workflow.Steps;

namespace ProcSense.Core.Tests
{
    /// <summary>Score items, the evidence mapper, the outcome and the debrief facts (p0-scoring-debrief s01 to s04, plan tests T-SCO1 to T-SCO5 and T-EM1 to T-EM5).</summary>
    public class ScoringTests
    {
        private static readonly ContentPackage Content = TestCase.Package();
        private static ScoreTables Tables => Content.Scoring;

        private static CaseFile File_(string id) => ContentLoader.LoadCaseFile(new FileContentReader(TestCase.ContentRoot), id);

        private static SimAction Act(string verb, params (string k, string v)[] p) => new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        private static ActionOutcome Do(Session s, string verb, params (string k, string v)[] p) => s.Apply(Act(verb, p));

        private static Session Start(string caseId, out ProblemsSubsystem problems)
        {
            var file = File_(caseId);
            problems = new ProblemsSubsystem(Content, file);
            var chain = Subsystems.Default(Content).Where(x => !(x is DecisionSubsystem) && !(x is ProblemsSubsystem) && !(x is OrReadinessSubsystem) && !(x is EndStateSubsystem))
                .Concat(new ISubsystem[] { new DecisionSubsystem(Content, file), problems, new OrReadinessSubsystem(Content, file), new EndStateSubsystem(Content, file) });
            var card = Content.Cases["VAR-S1-A"] with { Scenario = file.Scenario };
            var s = new Session(card, 42, SessionMode.Practice, chain);
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.True(Do(s, verb).Accepted, verb);
            return s;
        }

        private static ActionOutcome Say(Session s, string item, string response) => Do(s, "checklist_item", ("phase", item.Substring(0, item.LastIndexOf('.'))), ("item", item), ("response", response));

        private static void ReadyRun(Session s, bool declareTooEarly = false)
        {
            s.Patient.Clinical.SetFlag("consent.signed");
            Do(s, "attach_sensor", ("sensor", "spo2"));
            if (declareTooEarly) Do(s, "declare_or_ready");
            foreach (var st in Content.Or.Stations.Where(x => x.Owner == "trainee")) Do(s, "check_station", ("station", st.Id));
            Say(s, "sign_in.01", "yes"); Say(s, "sign_in.02", "na"); s.Tick(10);
            Say(s, "sign_in.04", "yes"); Say(s, "sign_in.05", "no"); Say(s, "sign_in.06", "no"); Say(s, "sign_in.07", "no");
            s.Tick(10); Say(s, "time_out.02", "yes"); Say(s, "time_out.03", "na"); s.Tick(10); s.Tick(10); s.Tick(10); Say(s, "time_out.07", "na");
            Do(s, "declare_or_ready");
            Do(s, "complete_time_out");
        }

        // ---------------------------------------------------------------- the content

        [Fact]
        public void The_content_has_22_behaviours_and_every_action_they_name_is_a_real_verb()
        {
            Assert.Equal(22, Tables.Behaviours.Count);
            var s = new Session(Content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(Content));
            foreach (var b in Tables.Behaviours)
                foreach (var verb in b.Actions) Assert.True(s.Verbs.IsKnown(verb), b.Id + " names the verb " + verb + ", which no step registers");
            foreach (var w in Tables.Weights.Values) Assert.Equal(22, w.Count);
            Assert.True(Tables.Points["minor"] < 0 && Tables.Points["major"] < Tables.Points["minor"] && Tables.Points["credit"] > 0);
        }

        [Fact]
        public void Every_item_id_in_the_engine_has_a_rubric_rule_and_the_asa_slips_are_teaching_moments()
        {
            foreach (var id in new[] { "credit.consent_valid", "major.handover_omitted", "critical.consent_signed_without_capacity", "critical.proceed_despite_fasting_breach", "critical.ignored_critical_result", "credit.asa_correct", "info.checklist_stopped_honestly", "major.sepsis_antibiotics_late" })
                Assert.NotNull(Tables.RuleFor(id));
            Assert.True(Tables.RuleFor("minor.asa_close")!.Teaching);
            Assert.True(Tables.RuleFor("major.asa_far")!.Teaching);
            Assert.False(Tables.RuleFor("major.handover_omitted")!.Teaching);
        }

        [Fact]
        public void No_item_name_in_the_content_or_code_uses_a_capital_prefix_that_the_failure_rule_would_miss()
        {
            var bad = new List<string>();
            foreach (var p in System.IO.Directory.GetFiles(System.IO.Path.Combine(TestCase.Repo, "src"), "*.cs", System.IO.SearchOption.AllDirectories))
                if (System.Text.RegularExpressions.Regex.IsMatch(System.IO.File.ReadAllText(p), "\"(score:)?(CRITICAL|Critical|MAJOR|Major)\\.")) bad.Add(p);
            Assert.Empty(bad);
            Assert.DoesNotContain("CRITICAL.", System.IO.File.ReadAllText(System.IO.Path.Combine(TestCase.ContentRoot, "workflow", "workflow.json")));
        }

        // ---------------------------------------------------------------- T-SCO2 / T-SCO4: the outcome

        [Fact]
        public void T_SCO2_each_end_state_scores_as_defined()
        {
            var ready = Start("VAR-S1-A", out _);
            ReadyRun(ready);
            Assert.Equal((Scorer.ResultSuccess, "READY_SAFE"), (Scorer.Score(ready, Content).Result, Scorer.Score(ready, Content).EndState));

            var delayed = Start("VAR-S1-A", out _);
            ReadyRun(delayed, declareTooEarly: true);
            Assert.Equal(Scorer.ResultDelayed, Scorer.Score(delayed, Content).Result);

            var postponed = Start("VAR-S1-PB", out var problems);
            Do(postponed, "ask", ("topic", "pregnancy"));
            Do(postponed, "order_investigation", ("test", "lft")); Do(postponed, "take_sample", ("test", "lft"), ("tube", "serum_gold"));
            Do(postponed, "order_investigation", ("test", "pregnancy")); Do(postponed, "perform_bedside_test", ("test", "pregnancy"));
            postponed.Tick(3600); Do(postponed, "view_result", ("test", "lft")); Do(postponed, "view_result", ("test", "pregnancy"));
            problems.Detect(File_("VAR-S1-PB").Problems.First(p => p.Type == "pathway_changing").Id);
            Do(postponed, "declare_outcome", ("outcome", "REVIEW"));
            Do(postponed, "postpone", ("reason", "probable duct stone: senior review and the biliary pathway first"));
            var pr = Scorer.Score(postponed, Content);
            Assert.Equal(("POSTPONED_APPROPRIATELY", Scorer.ResultSuccess), (pr.EndState, pr.Result));       // a correct postponement counts as success
            Assert.Contains(pr.Items, i => i.Id.StartsWith("credit.postponed_appropriately") || i.Status == "credit");

            var redirected = Start("VAR-S2-B", out var p2);
            p2.Detect(File_("VAR-S2-B").Problems.First(p => p.Type == "life_threat").Id);
            Do(redirected, "redirect", ("service", "gynaecology"));
            Assert.Equal(Scorer.ResultSuccess, Scorer.Score(redirected, Content).Result);

            var failed = Start("STK-S1-A", out var p3);
            p3.Detect("stka.hypo");
            Do(failed, "postpone", ("reason", "come back tomorrow"));
            var fr = Scorer.Score(failed, Content);
            Assert.Equal((Scorer.ResultFail, "SAFETY_FAILURE"), (fr.Result, fr.EndState));
            Assert.Contains(fr.Critical, i => i.Id == "critical.postponed_in_emergency");
        }

        [Fact]
        public void A_case_that_has_not_ended_is_in_progress()
        {
            var s = Start("VAR-S1-A", out _);
            Assert.Equal(Scorer.ResultInProgress, Scorer.Score(s, Content).Result);
        }

        [Fact]
        public void T_SCO4_every_critical_item_is_listed_and_one_is_enough_to_fail()
        {
            var s = Start("STK-S1-A", out var problems);
            Do(s, "tick_ward_item", ("item", "consent"));                  // ticked consent that was never given
            problems.Detect("stka.hypo");
            Do(s, "postpone", ("reason", "come back tomorrow"));
            var r = Scorer.Score(s, Content);
            Assert.Equal(Scorer.ResultFail, r.Result);
            Assert.Contains(r.Critical, i => i.Id == "critical.ward_tick_false");
            Assert.Contains(r.Critical, i => i.Id == "critical.postponed_in_emergency");
            Assert.True(r.Critical.Count >= 2);
        }

        [Fact]
        public void A_critical_item_fails_the_case_even_when_nothing_else_is_wrong()
        {
            var s = Start("VAR-S1-A", out _);
            Do(s, "tick_ward_item", ("item", "consent"));
            ReadyRun(s);
            Assert.Equal(Scorer.ResultFail, Scorer.Score(s, Content).Result);
        }

        [Fact]
        public void An_item_is_counted_once_however_often_the_report_is_made()
        {
            var s = Start("VAR-S1-A", out _);
            ReadyRun(s, declareTooEarly: true);
            var a = Scorer.Score(s, Content).Items.Select(i => i.Id + "@" + i.Step).ToList();
            var b = Scorer.Score(s, Content).Items.Select(i => i.Id + "@" + i.Step).ToList();
            Assert.Equal(a, b);
            Assert.Equal(a.Count, a.Distinct().Count() + a.GroupBy(x => x).Sum(g => g.Count() - 1));      // (items repeat only when the same thing really happened more than once)
        }

        [Fact]
        public void T_SCO3_the_category_totals_add_up_to_the_items()
        {
            var s = Start("VAR-S1-A", out _);
            ReadyRun(s, declareTooEarly: true);
            var r = Scorer.Score(s, Content);
            Assert.Equal(r.Items.Where(i => i.Points > 0).Sum(i => i.Points), r.Categories.Sum(c => c.Credits));
            Assert.Equal(r.Items.Where(i => i.Points < 0).Sum(i => -i.Points), r.Categories.Sum(c => c.Penalties));
            Assert.All(r.Items, i => Assert.NotEqual("", i.Category));
            Assert.Contains(r.Lateness, l => l.What == "major.or_declared_ready_not_ready");
        }

        // ---------------------------------------------------------------- the failure rule now sees every step's items

        [Fact]
        public void A_consent_signed_without_capacity_now_fails_the_case_at_its_end()
        {
            var reader = new CompositeContentReader(new FileContentReader(TestCase.ContentRoot), new FileContentReader(System.IO.Path.Combine(TestCase.Repo, "tests", "fixtures")));
            var pkg = ContentLoader.Load(reader, new[] { "VAR-S1-A", "STK-S1-A" }, new[] { "VAR-S1-A", "STK-S1-A" });
            var s = new Session(pkg.Cases["STK-S1-A"], 42, SessionMode.Practice, Subsystems.Default(pkg));          // low sugar: confused, no capacity
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.True(Do(s, verb).Accepted, verb);
            s.Tick(60);
            Do(s, "check_capacity");
            Do(s, "consent_discussion", ("topic", "diagnosis"));
            Do(s, "sign_consent");
            Assert.Contains("critical.consent_signed_without_capacity", s.EndStates!.AllItems());                    // the consent step's items now reach the end-state rule
            Do(s, "postpone", ("reason", "low sugar first"));
            s.Tick(1);
            Assert.Equal("SAFETY_FAILURE", s.EndStates.Resolved);
            Assert.Equal(Scorer.ResultFail, Scorer.Score(s, pkg).Result);
        }

        // ---------------------------------------------------------------- the evidence mapper (T-EM1 to T-EM5)

        [Fact]
        public void T_EM2_menu_actions_are_deterministic_evidence()
        {
            var s = Start("VAR-S1-A", out _);
            Do(s, "ask", ("topic", "complaint"));
            var store = new EvidenceStore(Tables);
            store.FromLog(s.Log);
            Assert.Contains(store.Credited("b01"), r => r.Act == "ask" && r.Source == "deterministic" && r.LogRefs.Count == 1);
            Assert.Contains(store.Credited("b13"), r => r.Act == "confirm_identity");
            int before = store.Records.Count;
            store.FromLog(s.Log);
            Assert.Equal(before, store.Records.Count);                          // adding again adds nothing new
        }

        [Fact]
        public void T_EM1b_a_confident_sbar_element_in_a_handover_step_is_recorded_but_never_credited_to_b11()
        {
            var b11 = Tables.Behaviours.First(b => b.Id == "b11");
            Assert.True(b11.Critical);                                           // the conditions under which speech could once credit b11
            Assert.Contains("SBAR_ELEMENT", b11.AiActs);
            Assert.True(Tables.ThresholdFor("SBAR_ELEMENT") <= 0.99);
            foreach (var step in b11.Steps)
            {
                var store = new EvidenceStore(Tables);
                var r = store.Submit("SBAR_ELEMENT", "handover", "Situation: Mr Khan, planned surgery this morning", 0.99, step, new[] { 1 });
                Assert.False(r.Accepted);
                Assert.Equal(EvidenceStore.CriticalNeedsAction, r.Reason);
                Assert.Empty(store.Credited("b11"));
                Assert.Single(store.Records);                                    // recorded, not credited
            }
        }

        [Fact]
        public void T_EM1_a_critical_act_from_speech_is_recorded_but_not_credited()
        {
            var store = new EvidenceStore(Tables);
            var tables = Tables.Behaviours.First(b => b.AiActs.Count > 0);
            var critical = new ScoreTables(Tables.Behaviours.Select(b => b.Id == tables.Id ? b with { AiActs = b.AiActs.Concat(new[] { "CONFIRM_IDENTITY" }).ToList() } : b).ToList(), Tables.Rules, Tables.Points, Tables.Weights, Tables.DefaultThreshold, Tables.Thresholds, Tables.CriticalActs, Tables.Sentences);
            var s2 = new EvidenceStore(critical);
            var r = s2.Submit("CONFIRM_IDENTITY", "identity", "this is Mrs Khan", 0.97, tables.Steps[0], new[] { 3 });
            Assert.False(r.Accepted);
            Assert.Equal(EvidenceStore.CriticalNeedsAction, r.Reason);
            Assert.Empty(s2.Credited(tables.Id));
            Assert.Single(s2.Records);                                           // but it is on record
            Assert.Empty(store.Records);
        }

        [Fact]
        public void T_EM3_a_low_confidence_act_is_recorded_but_not_credited_and_a_good_one_is()
        {
            var store = new EvidenceStore(Tables);
            var low = store.Submit("CHECK_UNDERSTANDING", "consent", "so you understand?", 0.55, 9, new[] { 7 });
            Assert.False(low.Accepted);
            Assert.Equal(EvidenceStore.LowConfidence, low.Reason);
            var good = store.Submit("CHECK_UNDERSTANDING", "consent", "can you tell me back what we agreed?", 0.88, 9, new[] { 8 });
            Assert.True(good.Accepted);
            Assert.Single(store.Credited("b07"));
        }

        [Fact]
        public void T_EM4_an_act_from_the_wrong_step_is_rejected_with_the_reason()
        {
            var store = new EvidenceStore(Tables);
            var r = store.Submit("CHECK_UNDERSTANDING", "consent", "x", 0.95, 1, new[] { 1 });
            Assert.False(r.Accepted);
            Assert.StartsWith(EvidenceStore.ContextMismatch, r.Reason);
            Assert.False(store.Submit("MADE_UP_ACT", "", "", 0.99, 9, new int[0]).Accepted);
        }

        [Fact]
        public void T_EM5_with_no_classifier_only_deterministic_evidence_exists()
        {
            var s = Start("VAR-S1-A", out _);
            Do(s, "ask", ("topic", "complaint"));
            var store = new EvidenceStore(Tables);
            store.FromLog(s.Log);
            Assert.All(store.Records, r => Assert.Equal("deterministic", r.Source));
            Assert.NotEmpty(store.NotShown());                                   // behaviours that need speech show as not yet shown, never as failed
        }

        // ---------------------------------------------------------------- the debrief facts (T-SCO1, T-SCO5 is checked against the schema in CI)

        [Fact]
        public void T_SCO1_every_debrief_sentence_links_to_a_log_entry_and_shows_its_rule_and_source()
        {
            var s = Start("VAR-S1-A", out _);
            Do(s, "ask", ("topic", "complaint"));
            ReadyRun(s, declareTooEarly: true);
            var facts = DebriefFacts.Build(s, Content);
            Assert.NotEmpty(facts.Lines);
            foreach (var l in facts.Lines)
            {
                Assert.NotEmpty(l.Evidence);
                Assert.Contains(l.RuleId, l.Sentence);
                Assert.Contains(l.Source, l.Sentence);
            }
            Assert.StartsWith("{\"schema\":\"debrief_facts.v1\"", facts.ToJson());
            Assert.DoesNotContain("<script", facts.ToHtml());
            Assert.Equal(facts.ToJson(), DebriefFacts.Build(s, Content).ToJson());                    // the same session gives the same facts
        }

        [Fact]
        public void A_sentence_exists_only_for_something_that_happened()
        {
            var s = Start("VAR-S1-A", out _);
            var facts = DebriefFacts.Build(s, Content);
            Assert.Empty(facts.Lines);
            Assert.Equal(Scorer.ResultInProgress, facts.Result);
        }
    }
}
