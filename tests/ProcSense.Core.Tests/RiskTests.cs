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
    /// <summary>Step 5: ASA proposal, clot form, scores, the anaesthetist, and the readiness decision (p0-simulation-workflow-s08, plan tests T05-1 to T05-3).</summary>
    public class RiskTests
    {
        private static readonly ContentPackage Content = TestCase.Package();

        private static CaseFile File_(string id) => ContentLoader.LoadCaseFile(new FileContentReader(TestCase.ContentRoot), id);

        private static SimAction Act(string verb, params (string k, string v)[] p) => new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        private static ActionOutcome Do(Session s, string verb, params (string k, string v)[] p) => s.Apply(Act(verb, p));

        private static string Reject(ActionOutcome o) { Assert.False(o.Accepted); return o.RejectReason; }

        // A session whose Step 4 findings come from VAR-S1-A and whose risk data come from `riskCase`; `fastingCase` gives the fasting intake. Identity is confirmed, history and examination are done.
        private static Session Ready(string riskCase = "VAR-S1-A", string? fastingCase = null, SessionMode mode = SessionMode.Practice, bool examined = true)
        {
            var chain = Subsystems.Default(Content).Where(x => !(x is RiskSubsystem) && !(x is FastingSubsystem)).ToList();
            chain.Add(new RiskSubsystem(Content, File_(riskCase)));
            chain.Add(fastingCase == null ? new FastingSubsystem(Content) : new FastingSubsystem(Content, File_(fastingCase)));
            var s = new Session(Content.Cases["VAR-S1-A"], 42, mode, chain);
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.True(s.Apply(Act(verb)).Accepted, verb);
            if (examined)
            {
                Do(s, "ask", ("topic", "medicines"));
                Do(s, "ask_exam_permission");
                foreach (var region in new[] { "eyes", "chest", "abdomen" }) Do(s, "examine", ("region", region));
            }
            return s;
        }

        private static readonly string AllNo = string.Join(",", new[]
        {
            "age_over_60", "bmi_over_30", "cancer", "previous_clot", "family_history_clots", "oestrogen_or_hrt", "pregnancy", "dehydration", "reduced_mobility", "surgery_over_90_minutes", "acute_tummy_inflammation",
            "active_bleeding", "blood_thinners", "platelets_below_75", "very_high_bp",
        }.Select(i => i + ":no"));

        private static string Vte(params string[] yes) => string.Join(",", AllNo.Split(',').Select(p => yes.Contains(p.Split(':')[0]) ? p.Split(':')[0] + ":yes" : p));

        private static string State(Session s, string goal) => s.Observable.Goals.Single(g => g.Id == goal).State;

        // ---------------------------------------------------------------- T05-1 the correct path (walkthrough Scene 5)

        [Fact]
        public void The_correct_path_for_VAR_S1_A_is_ASA_1_agreed_and_a_standard_general_anaesthetic()
        {
            var s = Ready();
            var asa = Do(s, "assess_risk", ("asa", "1"));
            Assert.Contains("asa:credit", asa.Effects);
            Assert.Equal("credit", s.Observable.Risk.AsaChip);

            Assert.Contains("vte:no_plan", Do(s, "assess_vte", ("items", AllNo)).Effects);
            var review = Do(s, "request_anaesthetic_review");
            Assert.Contains("npc:asa_agreed", review.Effects);
            Assert.Contains("plan:standard_ga", review.Effects);
            Assert.Equal(new[] { "I agree: ASA 1.", "The plan is a standard general anaesthetic." }, s.Observable.Risk.AnaesthetistLines);

            var ready = Do(s, "decide_ready", ("option", "proceed"));
            Assert.DoesNotContain(ready.Effects, e => e.StartsWith("major:") || e.StartsWith("critical:"));
            Assert.False(s.Observable.Risk.Blocked);
            Assert.Equal("proceed", s.Observable.Risk.Decision);
            foreach (var g in new[] { "G-RISK-ASA", "G-RISK-VTE", "G-ANAES-REVIEW", "G-ANAES-PLAN", "G-READY" }) Assert.Equal("complete", State(s, g));
        }

        // ---------------------------------------------------------------- ASA (L5-01)

        [Theory]
        [InlineData("1", "false", "credit")]
        [InlineData("2", "false", "minor")]       // one grade outside
        [InlineData("3", "false", "major")]
        [InlineData("1", "true", "minor")]        // the E is part of the answer
        public void The_proposal_is_compared_with_the_range_the_case_states(string grade, string emergency, string cls)
        {
            var s = Ready();
            Assert.Contains("asa:" + cls, Do(s, "assess_risk", ("asa", grade), ("emergency", emergency)).Effects);
        }

        [Fact]
        public void A_range_credits_every_grade_inside_it()
        {
            foreach (var g in new[] { "2", "3" })
                Assert.Contains("asa:credit", Do(Ready("VAR-S1-B"), "assess_risk", ("asa", g)).Effects);
            Assert.Contains("asa:minor", Do(Ready("VAR-S1-B"), "assess_risk", ("asa", "1")).Effects);
            Assert.Contains("asa:minor", Do(Ready("VAR-S1-B"), "assess_risk", ("asa", "4")).Effects);
        }

        [Fact]
        public void A_wrong_grade_is_corrected_by_the_anaesthetist_as_a_teaching_point_with_the_cases_reason()
        {
            var s = Ready();
            Do(s, "assess_risk", ("asa", "3"));
            var review = Do(s, "request_anaesthetic_review");
            Assert.Contains("npc:asa_corrected", review.Effects);
            var line = s.Observable.Risk.AnaesthetistLines[0];
            Assert.StartsWith("I would call this ASA 1, because She is fit", line);
            Assert.Contains("teaching point, not a mark against you", line);
        }

        [Fact]
        public void The_grade_cannot_be_proposed_before_history_and_examination()
        {
            Assert.Equal(RiskSubsystem.HistoryAndExamFirst, Reject(Do(Ready(examined: false), "assess_risk", ("asa", "1"))));
            var half = Ready(examined: false);
            Do(half, "ask", ("topic", "medicines"));
            Assert.Equal(RiskSubsystem.HistoryAndExamFirst, Reject(Do(half, "assess_risk", ("asa", "1"))));     // history without an examination is not enough
        }

        [Fact]
        public void A_missing_or_impossible_grade_is_refused_and_records_nothing()
        {
            var s = Ready();
            Assert.Equal(RiskSubsystem.MissingAsa, Reject(Do(s, "assess_risk")));
            foreach (var bad in new[] { "0", "7", "x", "1.5", "-1" }) Assert.Equal(RiskSubsystem.BadAsa, Reject(Do(s, "assess_risk", ("asa", bad))));
            Assert.Equal("", s.Observable.Risk.Asa);
            Assert.False(s.Patient.Clinical.HasFlag(RiskSubsystem.AsaProposedFlag));
        }

        [Fact]
        public void A_case_that_states_no_grade_is_not_marked_and_the_anaesthetist_says_so()
        {
            var s = Ready("VAR-S1-I");        // a P1 case: no expected grade or plan authored yet
            Assert.Contains("asa:no_expected", Do(s, "assess_risk", ("asa", "2")).Effects);
            Assert.Equal("", s.Observable.Risk.AsaChip);
            Do(s, "request_anaesthetic_review");
            Assert.Equal(new[] { "I have heard your grade; I will confirm it myself before the list.", "The clot and bleeding risk form is not finished." }, s.Observable.Risk.AnaesthetistLines);
            Assert.Equal("", s.Observable.Risk.Plan);
        }

        [Fact]
        public void The_anaesthetist_asks_for_a_grade_when_none_was_proposed()
        {
            var s = Ready();
            var review = Do(s, "request_anaesthetic_review");
            Assert.Contains("npc:ask_asa", review.Effects);
            Assert.Equal(new[] { "What ASA grade would you give this patient, and why?" }, s.Observable.Risk.AnaesthetistLines);
            Assert.True(s.Patient.Clinical.HasFlag(RiskSubsystem.AnaesthetistCalledFlag));
            Assert.False(s.Patient.Clinical.HasFlag(RiskSubsystem.PlanAgreedFlag));
        }

        [Fact]
        public void An_emergency_case_is_planned_as_a_rapid_sequence_induction()
        {
            var s = Ready("VAR-S2-PC");
            Do(s, "assess_risk", ("asa", "1"), ("emergency", "true"));
            var review = Do(s, "request_anaesthetic_review");
            Assert.Contains("plan:rsi", review.Effects);
            Assert.Equal("rsi", s.Observable.Risk.Plan);
            Assert.Contains(s.Observable.Risk.AnaesthetistLines, l => l.Contains("rapid sequence induction"));
        }

        // ---------------------------------------------------------------- the clot and bleeding form (L5-02)

        [Fact]
        public void The_form_must_be_fully_answered_and_any_clot_factor_means_a_plan_is_required()
        {
            var s = Ready();
            Assert.Equal(RiskSubsystem.VteItemsMissing, Reject(Do(s, "assess_vte", ("items", "age_over_60:no"))));
            Assert.Equal(RiskSubsystem.VteItemsMissing, Reject(Do(s, "assess_vte")));
            Assert.False(s.Patient.Clinical.HasFlag(RiskSubsystem.VteDoneFlag));

            var done = Do(s, "assess_vte", ("items", Vte("pregnancy")));
            Assert.Contains("vte:plan_required", done.Effects);
            Assert.True(s.Patient.Clinical.HasFlag(RiskSubsystem.VtePlanFlag));
            Assert.Equal("plan_required", s.Observable.Risk.Vte);
            Assert.False(s.Patient.Clinical.HasFlag("vte.bleeding_risk"));
        }

        [Fact]
        public void A_bleeding_factor_is_recorded_separately()
        {
            var s = Ready();
            var done = Do(s, "assess_vte", ("items", Vte("blood_thinners")));
            Assert.Contains("vte:no_plan", done.Effects);
            Assert.Contains("vte_bleeding:yes", done.Effects);
            Assert.True(s.Patient.Clinical.HasFlag("vte.bleeding_risk"));
        }

        [Fact]
        public void An_unknown_or_malformed_item_is_refused()
        {
            var s = Ready();
            Assert.Equal(RiskSubsystem.VteUnknownItem, Reject(Do(s, "assess_vte", ("items", "age_over_60:maybe"))));
            Assert.Equal(RiskSubsystem.VteUnknownItem, Reject(Do(s, "assess_vte", ("items", AllNo + ",shoe_size:yes"))));
        }

        // ---------------------------------------------------------------- scores (L5-03, T05-3)

        private static (string, string)[] AirS2A => new[]
        {
            ("score", "air"), ("pain_rlq", "true"), ("vomiting", "true"), ("rebound_light", "false"), ("rebound_medium", "true"), ("rebound_strong", "false"),
            ("temperature_c", "37.9"), ("wbc", "13.6"), ("neutrophils_pct", "82"), ("crp", "38"),
        };

        [Fact]
        public void The_AIR_score_is_calculated_from_the_components_the_trainee_enters()
        {
            var s = Ready();
            var o = Do(s, "compute_score", AirS2A);
            Assert.Contains("score_calc:air 7 unsure", o.Effects);
            Assert.True(s.Patient.Clinical.HasFlag("score.air.computed"));
            Assert.Contains("air: 7 unsure", s.Observable.Risk.Scores);
        }

        [Fact]
        public void A_score_without_the_WBC_prompts_the_trainee_to_order_the_test_and_gives_no_band()
        {
            var s = Ready();
            var o = Do(s, "compute_score", AirS2A.Where(p => p.Item1 != "wbc").ToArray());
            Assert.Contains("missing:wbc", o.Effects);
            Assert.Contains("prompt_order:FBC", o.Effects);
            Assert.Contains("air: incomplete: 6 so far", s.Observable.Risk.Scores);
            Assert.Equal(new[] { "Order a full blood count: the white cell count is missing." }, s.Observable.Risk.Prompts);
            Assert.False(s.Patient.Clinical.HasFlag("score.air.computed"));

            Do(s, "compute_score", AirS2A);          // the result arrives and the trainee tries again
            Assert.True(s.Patient.Clinical.HasFlag("score.air.computed"));
        }

        [Fact]
        public void The_other_scores_the_tablet_offers_are_calculated()
        {
            var s = Ready();
            Assert.Contains("score_calc:stopbang 1 low", Do(s, "compute_score", new[] { ("score", "stopbang"), ("snoring", "true"), ("tired", "false"), ("observed", "false"), ("pressure", "false"), ("bmi_over_35", "false"), ("age_over_50", "false"), ("neck_over_40", "false"), ("male", "false") }).Effects);
            Assert.Contains("score_calc:tg18_cholecystitis diagnosis definite grade I", Do(s, "compute_score", new[]
            {
                ("score", "tg18_cholecystitis"), ("murphy", "true"), ("ruq_lump_pain_tenderness", "true"), ("fever", "true"), ("raised_crp", "true"), ("raised_wbc", "true"), ("imaging_inflamed", "true"),
                ("vasopressor", "false"), ("reduced_consciousness", "false"), ("poor_oxygen", "false"), ("low_urine", "false"), ("creatinine_umol", "70"), ("inr", "1"), ("platelets", "250"),
                ("wbc", "15.2"), ("tender_lump", "false"), ("symptom_hours", "48"), ("severe_local_inflammation", "false"),
            }).Effects);
            Assert.Contains("score_calc:asge tier high", Do(s, "compute_score", new[] { ("score", "asge"), ("fever", "false"), ("jaundice", "true"), ("ruq_pain", "true"), ("stone_on_scan", "true") }).Effects);
        }

        [Fact]
        public void Bad_score_requests_are_refused()
        {
            var s = Ready();
            Assert.Equal(RiskSubsystem.MissingScore, Reject(Do(s, "compute_score")));
            Assert.Equal(RiskSubsystem.UnknownScore, Reject(Do(s, "compute_score", ("score", "curb65"))));
            Assert.Equal(RiskSubsystem.BadScoreInput, Reject(Do(s, "compute_score", AirS2A.Concat(new[] { ("rebound_light", "true") }).GroupBy(p => p.Item1).Select(g => g.Last()).ToArray())));
            Assert.Equal(RiskSubsystem.BadScoreInput, Reject(Do(s, "compute_score", AirS2A.Select(p => p.Item1 == "wbc" ? ("wbc", "lots") : p).ToArray())));
            Assert.Empty(s.Observable.Risk.Scores);
        }

        // ---------------------------------------------------------------- the readiness decision (L5-04, E5-A, E5-B)

        [Fact]
        public void Proceeding_with_an_unresolved_fasting_breach_is_allowed_on_the_tablet_but_critical_and_blocks_readiness()
        {
            var s = Ready("VAR-S1-G", fastingCase: "VAR-S1-G");
            Do(s, "ask", ("topic", "last_food")); Do(s, "ask", ("topic", "last_drink"));
            Assert.True(s.Patient.Clinical.HasFlag(FastingSubsystem.BreachFlag));

            var o = Do(s, "decide_ready", ("option", "proceed"));
            Assert.True(o.Accepted);
            Assert.Contains("critical:proceed_despite_fasting_breach", o.Effects);
            Assert.Contains("blocked:fasting_breach_unresolved", o.Effects);
            Assert.Contains("score:CRITICAL.proceed_despite_fasting_breach", o.Effects);
            Assert.True(s.Observable.Risk.Blocked);
            Assert.True(s.Patient.Clinical.HasFlag(RiskSubsystem.BlockedFlag));
            Assert.Equal(new[] { "I have not agreed to go ahead while the fasting breach is unresolved." }, s.Observable.Risk.AnaesthetistLines);
        }

        [Fact]
        public void Re_deciding_after_informing_the_anaesthetist_unblocks_readiness_and_the_critical_item_is_not_scored_twice()
        {
            var s = Ready("VAR-S1-G", fastingCase: "VAR-S1-G");
            Do(s, "ask", ("topic", "last_food")); Do(s, "ask", ("topic", "last_drink"));
            Do(s, "decide_ready", ("option", "proceed"));
            Assert.True(s.Observable.Risk.Blocked);

            Do(s, "decide", ("dp", "DP-S1-FAST"), ("option", "inform_and_delay"));
            var again = Do(s, "decide_ready", ("option", "proceed"));
            Assert.False(s.Observable.Risk.Blocked);
            Assert.False(s.Patient.Clinical.HasFlag(RiskSubsystem.BlockedFlag));
            Assert.DoesNotContain(again.Effects, e => e.StartsWith("critical:"));
        }

        [Fact]
        public void Choosing_to_delay_clears_a_block_and_the_anaesthetist_names_the_time_the_fast_is_met()
        {
            var s = Ready("VAR-S1-G", fastingCase: "VAR-S1-G");
            Do(s, "ask", ("topic", "last_food")); Do(s, "ask", ("topic", "last_drink"));
            Do(s, "assess_risk", ("asa", "1"));
            Do(s, "request_anaesthetic_review");
            Assert.Contains(s.Observable.Risk.AnaesthetistLines, l => l.StartsWith("The fasting time has not been reached. We should wait until 13:0"));
            Do(s, "decide_ready", ("option", "proceed"));
            Assert.True(s.Observable.Risk.Blocked);
            Do(s, "decide_ready", ("option", "postpone"));
            Assert.False(s.Observable.Risk.Blocked);
        }

        [Fact]
        public void Deciding_without_the_clot_form_or_the_anaesthetist_is_a_major_item_but_is_allowed()
        {
            var s = Ready();
            var o = Do(s, "decide_ready", ("option", "proceed"));
            Assert.True(o.Accepted);
            Assert.Contains("major:vte_not_done", o.Effects);
            Assert.Contains("score:major.vte_not_assessed", o.Effects);
            Assert.Contains("major:anaesthetist_not_called", o.Effects);
        }

        [Fact]
        public void A_delay_nobody_explained_raises_anxiety_once_and_an_explained_one_does_not()
        {
            var silent = Ready();
            int before = silent.Patient.Behavioural.Anxiety;
            var o = Do(silent, "decide_ready", ("option", "postpone"));
            Assert.Contains("anxiety:delay_not_explained", o.Effects);
            Assert.Equal(before + 1, silent.Patient.Behavioural.Anxiety);
            Do(silent, "decide_ready", ("option", "more_tests"));
            Assert.Equal(before + 1, silent.Patient.Behavioural.Anxiety);

            var told = Ready();
            int b2 = told.Patient.Behavioural.Anxiety;
            Do(told, "explain_delay");
            Do(told, "decide_ready", ("option", "optimise"));
            Assert.Equal(b2, told.Patient.Behavioural.Anxiety);

            var proceed = Ready();
            int b3 = proceed.Patient.Behavioural.Anxiety;
            Do(proceed, "decide_ready", ("option", "proceed"));
            Assert.Equal(b3, proceed.Patient.Behavioural.Anxiety);
        }

        [Fact]
        public void Bad_decisions_are_refused()
        {
            var s = Ready();
            Assert.Equal(RiskSubsystem.MissingOption, Reject(Do(s, "decide_ready")));
            Assert.Equal(RiskSubsystem.UnknownOption, Reject(Do(s, "decide_ready", ("option", "cancel"))));
            Assert.Equal("", s.Observable.Risk.Decision);
        }

        // ---------------------------------------------------------------- visibility, gating and the content

        [Fact]
        public void In_assessment_mode_the_chip_is_hidden_but_the_anaesthetist_still_speaks()
        {
            var s = Ready(mode: SessionMode.Assessment);
            Do(s, "assess_risk", ("asa", "3"));
            Do(s, "request_anaesthetic_review");
            Assert.Equal("", s.Observable.Risk.AsaChip);
            Assert.Equal("3", s.Observable.Risk.Asa);
            Assert.NotEmpty(s.Observable.Risk.AnaesthetistLines);
        }

        [Fact]
        public void Nothing_at_Step_5_works_before_the_identity_is_confirmed()
        {
            var chain = Subsystems.Default(Content);
            var s = new Session(Content.Cases["VAR-S1-A"], 42, SessionMode.Practice, chain);
            foreach (var verb in new[] { "assess_risk", "assess_vte", "compute_score", "request_anaesthetic_review", "decide_ready", "explain_delay" })
                Assert.Equal(IdentitySubsystem.IdentityNotConfirmed, Reject(Do(s, verb)));
        }

        [Fact]
        public void Every_P0_case_file_states_its_grade_and_plan_and_an_emergency_is_never_planned_as_standard()
        {
            int p0 = 0;
            foreach (var path in Directory.GetFiles(Path.Combine(TestCase.ContentRoot, "case_files"), "*.json"))
            {
                var f = File_(Path.GetFileNameWithoutExtension(path));
                if (f.Tier == "P0") { p0++; Assert.NotNull(f.Risk); }
                if (f.Risk != null)
                {
                    Assert.InRange(f.Risk.Asa.Min, 1, f.Risk.Asa.Max);
                    if (f.Scenario == "S2") Assert.Equal("rsi", f.Risk.Plan);
                }
            }
            Assert.Equal(17, p0);
        }

        [Fact]
        public void The_clot_form_items_are_the_verified_lists()
        {
            Assert.Equal(11, Content.Risk.VteRiskFactors.Count);
            Assert.Equal(new[] { "active_bleeding", "blood_thinners", "platelets_below_75", "very_high_bp" }, Content.Risk.VteBleedingFactors);
            Assert.Equal(new[] { "proceed", "optimise", "more_tests", "review", "postpone" }, Content.Risk.Options.Select(o => o.Id));
        }

        private static string Edit(string file, Action<System.Text.Json.Nodes.JsonNode> change)
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(TestCase.ContentRoot, file)))!;
            change(node);
            return node.ToJsonString();
        }

        private static string Risk() => File.ReadAllText(Path.Combine(TestCase.ContentRoot, "risk", "risk.json"));
        private static string Intents() => File.ReadAllText(Path.Combine(TestCase.ContentRoot, "npc", "anaesthetist_intents.json"));

        [Fact]
        public void Broken_risk_or_intent_content_is_refused_with_its_place()
        {
            Assert.Contains("minor", Assert.Throws<ContentException>(() => RiskTables.Parse(Edit("risk/risk.json", n => n["asa_difference"]!["minor"] = -1), Intents())).Message);
            Assert.Contains("proceed", Assert.Throws<ContentException>(() => RiskTables.Parse(Edit("risk/risk.json", n => n["readiness_options"]![0]!["delay"] = true), Intents())).Message);
            Assert.Contains("duplicate", Assert.Throws<ContentException>(() => RiskTables.Parse(Edit("risk/risk.json", n => n["vte"]!["risk_factors"]![1]!["id"] = "age_over_60"), Intents())).Message);
            Assert.Contains("both a clot and a bleeding", Assert.Throws<ContentException>(() => RiskTables.Parse(Edit("risk/risk.json", n => n["vte"]!["bleeding_factors"]![0]!["id"] = "cancer"), Intents())).Message);
            Assert.Contains("empty", Assert.Throws<ContentException>(() => RiskTables.Parse(Risk(), Edit("npc/anaesthetist_intents.json", n => n["intents"]![0]!["line"] = " "))).Message);
            Assert.Contains("unknown field", Assert.Throws<ContentException>(() => RiskTables.Parse(Edit("risk/risk.json", n => n["colour"] = 1), Intents())).Message);
        }
    }
}
