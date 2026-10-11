using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ProcSense.Core.Actions;
using ProcSense.Core.AIContext;
using ProcSense.Core.Content;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>The read-only context an AI-voiced character is given, and who speaks next (p1-ai-language s03, plan tests T-RL1 to T-RL6).</summary>
    public class ContextBuilderTests
    {
        private static readonly string[] Ids = Directory.GetFiles(Path.Combine(TestCase.ContentRoot, "case_files"), "*.json").Select(Path.GetFileNameWithoutExtension).OrderBy(x => x, System.StringComparer.Ordinal).Select(x => x!).ToArray();
        private static readonly ContentPackage All = ContentLoader.Load(new CompositeContentReader(new FileContentReader(TestCase.ContentRoot), new FileContentReader(Path.Combine(TestCase.Repo, "tests", "fixtures"))), Ids, Ids);

        private static Session Start(string caseId = "VAR-S1-A")
        {
            var s = new Session(All.Cases[caseId], 42, SessionMode.Practice, Subsystems.Default(All));
            foreach (var v in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) s.Apply(new SimAction(v, "patient"));
            return s;
        }

        private static BuiltContext Patient(Session s, string asked = "", params ContextTurn[] turns) => ContextBuilder.Build(s, All, "patient", asked, turns);

        [Fact]
        public void The_roles_and_their_intents_come_from_content_and_the_lexicon_has_every_category()
        {
            Assert.Equal(8, All.Ai.Roles.Count);
            Assert.Contains("answer_question", All.Ai.Roles["patient"]);
            Assert.Contains("confirm_order", All.Ai.Roles["nurse"]);
            Assert.NotEmpty(All.Ai.Lexicon.Categories["drug"]);
            Assert.Throws<System.ArgumentException>(() => ContextBuilder.Build(Start(), All, "porter_pigeon", "", new ContextTurn[0]));
        }

        // T-RL1: the context has no way back into the session
        [Fact]
        public void T_RL1_the_context_types_are_plain_read_only_records_that_cannot_reach_the_session()
        {
            foreach (var t in new[] { typeof(ContextDto), typeof(ContextFact), typeof(ContextTurn), typeof(Reaction) })
            {
                foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    var setter = p.SetMethod;
                    Assert.True(setter == null || setter.ReturnParameter.GetRequiredCustomModifiers().Any(m => m.Name == "IsExternalInit"), t.Name + "." + p.Name + " has a public setter");
                    Assert.DoesNotContain("Session", p.PropertyType.Name);
                }
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Where(m => !m.IsSpecialName))
                    Assert.DoesNotContain(m.GetParameters(), pr => pr.ParameterType == typeof(Session));
            }
        }

        // T-RL2: a hidden fact is not in what the model is sent
        [Fact]
        public void T_RL2_the_patient_context_holds_the_volunteered_complaint_and_nothing_hidden_until_it_is_asked()
        {
            int checkedCases = 0;
            foreach (var id in Ids.Where(i => All.CaseFiles[i].Tier == "P0"))
            {
                var s = Start(id);
                var dto = Patient(s).Dto;
                var file = All.CaseFiles[id];
                var hidden = file.History.Where(h => !h.Volunteered && h.Variant == null && h.Answer.Length > 12).ToList();
                string json = dto.ToJson();
                foreach (var h in hidden) Assert.DoesNotContain(h.Answer, json);
                Assert.Contains(dto.Facts, f => f.Topic == "complaint" && f.DisclosableNow);
                checkedCases++;
            }
            Assert.Equal(17, checkedCases);
        }

        [Fact]
        public void The_fact_asked_about_this_turn_is_earned_and_after_it_is_told_it_is_disclosed()
        {
            var s = Start("VAR-S1-C");
            var asked = Patient(s, "medicines");
            var fact = asked.Dto.Facts.Single(f => f.Topic == "medicines");
            Assert.True(fact.DisclosableNow);
            Assert.False(fact.Disclosed);
            Assert.DoesNotContain(asked.Validation.Facts, f => f.Id == "h.medicines" && !f.DisclosableNow && !f.Disclosed);      // not hidden any more this turn
            s.Apply(new SimAction("ask", "patient", Channel.Cli, new Dictionary<string, string> { ["topic"] = "medicines" }));
            var after = Patient(s).Dto.Facts.Single(f => f.Topic == "medicines");
            Assert.True(after.Disclosed);
        }

        [Fact]
        public void The_validators_view_still_holds_the_hidden_facts_so_a_leak_is_caught()
        {
            var s = Start("VAR-S1-C");
            var built = Patient(s);
            Assert.Contains(built.Validation.Facts, f => f.Id == "h.medicines" && !f.Disclosed && !f.DisclosableNow);
            string leak = "{\"kind\":\"utterance\",\"role\":\"patient\",\"intent\":\"answer_question\",\"text\":\"Metformin and gliclazide. She has not taken them this morning.\",\"fact_ids\":[]}";
            var codes = Validator.Validate(MiniJson.Parse(leak), built.Validation, All.Ai.Lexicon);
            Assert.Contains("DISCLOSURE_LEAK", codes);
            string fine = "{\"kind\":\"utterance\",\"role\":\"patient\",\"intent\":\"answer_question\",\"text\":\"I get attacks of pain after oily food.\",\"fact_ids\":[\"h.complaint\"]}";
            Assert.DoesNotContain("DISCLOSURE_LEAK", Validator.Validate(MiniJson.Parse(fine), built.Validation, All.Ai.Lexicon));
        }

        [Fact]
        public void Staff_are_never_given_a_hidden_or_earned_patient_fact_only_what_the_trainee_has_learned()
        {
            var s = Start("VAR-S1-C");
            var nurse = ContextBuilder.Build(s, All, "nurse", "medicines", new ContextTurn[0]);
            Assert.DoesNotContain(nurse.Dto.Facts, f => !f.Disclosed);
            s.Apply(new SimAction("ask", "patient", Channel.Cli, new Dictionary<string, string> { ["topic"] = "complaint" }));
            var later = ContextBuilder.Build(s, All, "nurse", "", new ContextTurn[0]);
            Assert.Contains(later.Dto.Facts, f => f.Topic == "complaint" && f.Disclosed);
            Assert.DoesNotContain(later.Dto.Facts, f => f.Topic == "medicines");
        }

        [Fact]
        public void The_context_carries_no_score_no_expected_answer_and_no_hint()
        {
            var s = Start("VAR-S1-H");
            string json = Patient(s).Dto.ToJson().ToLowerInvariant();
            foreach (var word in new[] { "\"expected", "\"score", "\"hint", "\"critical", "\"points", "\"outcome" }) Assert.DoesNotContain(word, json);
        }

        // T-RL3: the token budget
        [Fact]
        public void T_RL3_an_over_long_conversation_is_trimmed_oldest_turns_first_to_the_budget()
        {
            var s = Start();
            var turns = Enumerable.Range(0, 200).Select(i => new ContextTurn(i % 2 == 0 ? "trainee" : "patient", "turn number " + i + " with some words in it to take up room in the context")).ToArray();
            var dto = Patient(s, "", turns).Dto;
            Assert.True(dto.ApproxTokens <= ContextBuilder.TokenBudget, dto.ApproxTokens.ToString());
            Assert.NotEmpty(dto.LastTurns);
            Assert.Equal("turn number 199 with some words in it to take up room in the context", dto.LastTurns.Last().Text);       // the newest survives
            Assert.DoesNotContain(dto.LastTurns, t => t.Text.StartsWith("turn number 0 "));                                       // the oldest went first
        }

        [Fact]
        public void The_same_session_gives_the_same_context()
        {
            Assert.Equal(Patient(Start()).Dto.ToJson(), Patient(Start()).Dto.ToJson());
        }

        // T-RL4 to T-RL6: who speaks next
        [Fact]
        public void T_RL4_an_event_outranks_a_question_which_outranks_a_spontaneous_remark()
        {
            var q = new ReactionQueue();
            Assert.True(q.Enqueue(new Reaction(ReactionKind.Spontaneous, "worry", 10)));
            Assert.True(q.Enqueue(new Reaction(ReactionKind.Question, "asked:allergies", 11)));
            Assert.True(q.Enqueue(new Reaction(ReactionKind.Event, "E-HYPERSENSITIVITY", 12)));
            Assert.Equal(new[] { "E-HYPERSENSITIVITY", "asked:allergies", "worry" }, new[] { q.Next()!.Key, q.Next()!.Key, q.Next()!.Key });
            Assert.Null(q.Next());
        }

        [Fact]
        public void T_RL5_the_same_trigger_inside_the_debounce_window_counts_once()
        {
            var q = new ReactionQueue();
            Assert.True(q.Enqueue(new Reaction(ReactionKind.Question, "asked:allergies", 10)));
            Assert.False(q.Enqueue(new Reaction(ReactionKind.Question, "asked:allergies", 12)));
            Assert.True(q.Enqueue(new Reaction(ReactionKind.Question, "asked:allergies", 15)));
            Assert.Equal(2, q.Count);
        }

        [Fact]
        public void T_RL6_a_patient_makes_at_most_one_spontaneous_remark_every_8_seconds()
        {
            var q = new ReactionQueue();
            Assert.True(q.Enqueue(new Reaction(ReactionKind.Spontaneous, "a", 0)));
            Assert.NotNull(q.Next());
            Assert.False(q.Enqueue(new Reaction(ReactionKind.Spontaneous, "b", 5)));
            Assert.True(q.Enqueue(new Reaction(ReactionKind.Spontaneous, "c", 9)));
            Assert.True(q.Enqueue(new Reaction(ReactionKind.Question, "q", 5)));          // a question is not limited
        }
    }
}
