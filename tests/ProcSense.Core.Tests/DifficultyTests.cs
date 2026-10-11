using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Content;
using ProcSense.Core.Difficulty;
using ProcSense.Core.Replay;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>Adaptive difficulty, ratings and the beginner or advanced level (p0-case-library s06, plan tests T-DF1 to T-DF4). A rating and a lookup, not a trained model.</summary>
    public class DifficultyTests
    {
        private static readonly string[] Ids = System.IO.Directory.GetFiles(System.IO.Path.Combine(TestCase.ContentRoot, "case_files"), "*.json").Select(System.IO.Path.GetFileNameWithoutExtension).OrderBy(x => x, System.StringComparer.Ordinal).ToArray()!;
        private static readonly ContentPackage All = ContentLoader.Load(new CompositeContentReader(new FileContentReader(TestCase.ContentRoot), new FileContentReader(System.IO.Path.Combine(TestCase.Repo, "tests", "fixtures"))), Ids, Ids);
        private static DifficultyTables T => All.Difficulty;

        private static SessionRecord Rec(string caseId, params (string b, int credits, int penalties)[] by) => new SessionRecord(caseId, by.ToDictionary(x => x.b, x => (x.credits, x.penalties)));

        private static Ratings Rate(params SessionRecord[] sessions) => Ratings.From(sessions, T, id => All.CaseFiles[id].Tier);

        [Fact]
        public void The_content_has_8_dimensions_that_between_them_cover_all_22_behaviours_once()
        {
            Assert.Equal(8, T.Dimensions.Count);
            Assert.Equal((16.0, 3), (T.K, T.MinSessions));
            var covered = T.Dimensions.SelectMany(d => d.WeakBehaviours).OrderBy(x => x, System.StringComparer.Ordinal).ToList();
            Assert.Equal(All.Scoring.Behaviours.Select(b => b.Id).OrderBy(x => x, System.StringComparer.Ordinal), covered);
        }

        [Fact]
        public void An_even_match_and_a_perfect_session_move_the_rating_by_half_of_K()
        {
            var r = Rate(Rec("VAR-S1-A", ("b05", 8, 0)));
            Assert.Equal(1008.0, r.Of("b05"), 6);                       // P0 case = start rating, expected 0.5, score 1: 1000 + 16 * 0.5
            Assert.Equal(1, r.TimesRated("b05"));
            Assert.Equal(1000.0, r.Of("b01"), 6);                       // not shown, not rated
        }

        [Fact]
        public void A_poor_session_lowers_the_rating_and_a_harder_case_is_worth_more()
        {
            var easy = Rate(Rec("VAR-S1-A", ("b11", 0, 3)));
            Assert.Equal(992.0, easy.Of("b11"), 6);
            var hard = Rate(Rec("SC-1", ("b11", 3, 0)));                // a P2 case: rating 1200, so a pass earns more
            Assert.True(hard.Of("b11") > Rate(Rec("VAR-S1-A", ("b11", 3, 0))).Of("b11"));
        }

        // T-DF2
        [Fact]
        public void A_new_trainee_gets_the_default_curriculum_with_the_example_patient_first()
        {
            var pick = Selector.Next(Rate(), new string[0], All.CaseFiles, SessionMode.Practice);
            Assert.Equal("VAR-S1-A", pick.CaseId);
            Assert.Contains("standard order", pick.Reason);
            var second = Selector.Next(Rate(Rec("VAR-S1-A", ("b05", 1, 0))), new[] { "VAR-S1-A" }, All.CaseFiles, SessionMode.Practice);
            Assert.NotEqual("VAR-S1-A", second.CaseId);
            Assert.Equal("P0", second.Tier);
        }

        [Fact]
        public void The_selector_does_not_adapt_before_three_sessions()
        {
            var two = Rate(Rec("VAR-S1-A", ("b11", 0, 3)), Rec("VAR-S1-C", ("b11", 0, 3)));
            var pick = Selector.Next(two, new[] { "VAR-S1-A", "VAR-S1-C" }, All.CaseFiles, SessionMode.Practice);
            Assert.Contains("fewer than 3", pick.Reason);
        }

        // T-DF4
        [Fact]
        public void T_DF4_a_weak_handover_gets_a_handover_heavy_case()
        {
            var weak = Rate(Rec("VAR-S1-A", ("b11", 0, 3), ("b01", 4, 0)), Rec("VAR-S1-C", ("b11", 0, 3), ("b01", 4, 0)), Rec("VAR-S1-D", ("b11", 0, 3), ("b01", 4, 0)));
            var pick = Selector.Next(weak, new[] { "VAR-S1-A", "VAR-S1-C", "VAR-S1-D" }, All.CaseFiles, SessionMode.Practice, id => All.Scoring.Find(id)!.Label);
            Assert.Equal("complex_handover", pick.Dimension);
            Assert.Equal("b11", pick.Behaviour);
            Assert.Contains("complex_handover", CaseTags.Of(All.CaseFiles[pick.CaseId]));
            Assert.Contains("Communicates during handover and transfer", pick.Explanation);
        }

        // T-DF1
        [Fact]
        public void T_DF1_only_approved_cases_are_ever_selected_even_when_a_draft_has_the_right_tag()
        {
            var pool = All.CaseFiles.ToDictionary(kv => kv.Key, kv => kv.Value.Id == "VAR-S1-A" ? kv.Value : kv.Value with { Status = kv.Value.Tier == "P0" ? "DRAFT" : kv.Value.Status });
            var weak = Rate(Rec("VAR-S1-A", ("b11", 0, 3)), Rec("VAR-S1-A", ("b11", 0, 3)), Rec("VAR-S1-A", ("b11", 0, 3)));
            var pick = Selector.Next(weak, new[] { "VAR-S1-A" }, pool, SessionMode.Practice);
            Assert.Equal("APPROVED_FOR_TRAINING", pool[pick.CaseId].Status);
            Assert.Equal("VAR-S1-A", Selector.Next(Rate(), new string[0], pool, SessionMode.Practice).CaseId);
            foreach (var draft in All.CaseFiles.Values.Where(f => f.Status == "DRAFT")) Assert.False(ContentGate.Allows(draft.Status, SessionMode.Practice));
            var every = new HashSet<string>();
            for (int n = 0; n < All.CaseFiles.Count; n++)
            {
                var p = Selector.Next(Rate(), Ids.Take(n).ToArray(), All.CaseFiles, SessionMode.Practice);
                Assert.NotEqual("DRAFT", All.CaseFiles[p.CaseId].Status);
                every.Add(p.CaseId);
            }
            Assert.NotEmpty(every);
        }

        // T-DF3
        [Fact]
        public void T_DF3_assessment_mode_never_adapts_and_returns_the_fixed_order_or_nothing_when_nothing_is_approved_for_it()
        {
            var weak = Rate(Rec("VAR-S1-A", ("b11", 0, 3)), Rec("VAR-S1-A", ("b11", 0, 3)), Rec("VAR-S1-A", ("b11", 0, 3)));
            var none = Selector.Next(weak, new[] { "VAR-S1-A" }, All.CaseFiles, SessionMode.Assessment);
            Assert.Equal("", none.CaseId);                                       // nothing is approved for assessment yet
            var pool = All.CaseFiles.ToDictionary(kv => kv.Key, kv => kv.Value.Tier == "P0" ? kv.Value with { Status = "APPROVED_FOR_ASSESSMENT" } : kv.Value);
            var a = Selector.Next(weak, new[] { "VAR-S1-A" }, pool, SessionMode.Assessment);
            var b = Selector.Next(Rate(), new[] { "VAR-S1-A" }, pool, SessionMode.Assessment);
            Assert.Equal(a.CaseId, b.CaseId);                                    // the ratings make no difference
            Assert.Contains("fixed order", a.Reason);
            Assert.Equal("", a.Dimension);
        }

        [Fact]
        public void The_tags_a_case_earns_are_stable_and_come_from_its_own_content()
        {
            Assert.Contains("emergency_pace", CaseTags.Of(All.CaseFiles["VAR-S2-PD"]));
            Assert.DoesNotContain("emergency_pace", CaseTags.Of(All.CaseFiles["VAR-S1-A"]));
            Assert.Contains("unexpected_events", CaseTags.Of(All.CaseFiles["VAR-S1-I"]));        // a penicillin anaphylaxis allergy
            Assert.Equal(CaseTags.Of(All.CaseFiles["SC-1"]), CaseTags.Of(All.CaseFiles["SC-1"]));
            foreach (var dim in T.Dimensions.Where(d => d.Tag != "communication_challenge" && d.Tag != "environment"))
                Assert.Contains(All.CaseFiles.Values, f => f.Status != "DRAFT" && CaseTags.Of(f).Contains(dim.Tag));      // every realisable dimension has an approved case
        }

        // ---------------------------------------------------------------- the level and the variant are part of what is replayed

        [Fact]
        public void The_level_and_the_variant_are_saved_in_the_log_header_and_come_back_on_replay()
        {
            var content = TestCase.Package();
            var s = new Session(content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(content), new SessionOptions(TrainingLevel.Advanced, "B2"));
            Assert.True(s.Patient.Clinical.HasFlag("variant.B2"));
            s.Apply(new Actions.SimAction("ask_identity", "patient"));
            var text = SessionFile.Write(s);
            Assert.Contains("\"level\":\"advanced\"", text);
            Assert.Contains("\"variant\":\"B2\"", text);
            var r = SessionFile.Replay(text, content, () => Subsystems.Default(content));
            Assert.True(r.Match);
            Assert.Equal(TrainingLevel.Advanced, r.Session.Level);
            Assert.Equal("B2", r.Session.Variant);
            var plain = new Session(content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(content));
            Assert.DoesNotContain("level", SessionFile.Write(plain));             // the default leaves older logs unchanged
        }
    }
}
