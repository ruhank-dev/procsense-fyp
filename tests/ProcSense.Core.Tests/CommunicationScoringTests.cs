using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Content;
using ProcSense.Core.Scoring;
using ProcSense.Core.Speech;

namespace ProcSense.Core.Tests
{
    /// <summary>The graded communication credit (p1-ai-language-s22, ADR-0025): the formula, the gates, the weights and the rule that speech never earns a physical part. EXPERIMENTAL weights: they live in content/scoring/rubric.json and these tests read them from there.</summary>
    public class CommunicationScoringTests
    {
        private static readonly ContentPackage Content = TestCase.Package();
        private static ScoreTables Tables => Content.Scoring;
        private static CommunicationRubric R => Tables.Communication;

        private static SpeechJudgement J(int r, int c, int k, string assertion = "performed", string actor = "trainee") => new SpeechJudgement(assertion, actor, "statement", r, c, k, false);

        [Fact]
        public void The_formula_separates_zero_partial_substantial_and_full_credit()
        {
            Assert.Equal(0, R.Credit(0, 3, 3));                                    // irrelevant
            Assert.Equal(0, R.Credit(3, 0, 3));                                    // wrong
            Assert.Equal(1.0, R.Credit(3, 3, 3), 6);                               // complete, correct, relevant
            Assert.InRange(R.Credit(3, 3, 1), 0.5, 0.9);                           // correct but incomplete: substantial partial credit
            Assert.InRange(R.Credit(3, 2, 2), 0.3, 0.9);                           // partially correct: proportional
            Assert.Equal(0, R.Credit(1, 1, 0));                                    // so weak it falls under the credit floor
            Assert.True(R.Credit(3, 3, 3) > R.Credit(3, 3, 2) && R.Credit(3, 3, 2) > R.Credit(3, 3, 1) && R.Credit(3, 3, 1) > R.Credit(3, 3, 0));   // completeness only ever adds
            Assert.True(R.Credit(3, 3, 3) > R.Credit(3, 2, 3) && R.Credit(3, 2, 3) > R.Credit(3, 1, 3));                                             // so does correctness
        }

        [Fact]
        public void A_malformed_judgement_is_refused_and_credits_nothing()
        {
            var b = Tables.Behaviours.First(x => x.Id == "b01");
            var store = new EvidenceStore(Tables);
            var bad = new SpeechJudgement("performed", "trainee", "statement", 9, 3, 3, false);
            var r = store.SubmitUtterance("u1", "ASK_TOPIC", "do you smoke", bad, b.Steps, new[] { 1 });
            Assert.False(r.Accepted);
            Assert.Empty(CommunicationScorer.Collect(store, Tables, new Dictionary<string, double>()));
        }

        [Fact]
        public void The_parameters_come_from_the_rubric_and_a_critical_objective_weighs_less_than_an_ordinary_one()
        {
            Assert.Equal(3, R.GradeLevels);
            Assert.True(R.CriticalWeight < R.OrdinaryWeight);
            Assert.Equal(1.0, R.OrdinaryWeight);                                   // the one credit point a behaviour already carries
        }

        [Fact]
        public void An_ordinary_objective_earns_its_grade_times_the_weight_once_per_behaviour()
        {
            var b = Tables.Behaviours.First(x => x.Id == "b01");
            var store = new EvidenceStore(Tables);
            store.SubmitUtterance("u1", "ASK_TOPIC", "do you smoke", J(3, 3, 2), b.Steps, new[] { 1 });
            store.SubmitUtterance("u2", "ASK_TOPIC", "do you smoke at all", J(3, 3, 3), b.Steps, new[] { 2 });
            var item = Assert.Single(CommunicationScorer.Collect(store, Tables, new Dictionary<string, double>()));
            Assert.Equal("b01", item.Behaviour);
            Assert.Equal(1.0, item.Credit, 6);                                     // the better of the two, not their sum
            Assert.False(item.Critical);
        }

        [Fact]
        public void A_critical_objective_earns_only_the_small_communication_weight_and_its_physical_part_needs_a_verified_action()
        {
            var ev = Tables.CriticalEvidence["STATE_ALLERGY"];
            var steps = Tables.Find(ev.Behaviour)!.Steps;
            var spoken = new EvidenceStore(Tables);
            var r = spoken.SubmitUtterance("u1", "STATE_ALLERGY", "are you allergic to anything", J(3, 3, 3) with { ClaimsPhysical = true }, steps, new[] { 1 });
            Assert.False(r.Accepted);
            var item = Assert.Single(CommunicationScorer.Collect(spoken, Tables, new Dictionary<string, double>()));
            Assert.True(item.Critical);
            Assert.Equal(R.CriticalWeight, item.Credit, 6);
            Assert.True(item.ClaimsPhysical);
            Assert.False(item.PhysicalVerified);                                   // claimed, never verified by speech

            var verified = new EvidenceStore(Tables);
            verified.AddDeterministic(ev.Actions[0], new[] { 5 });                 // the simulator accepted the real action
            verified.SubmitUtterance("u1", "STATE_ALLERGY", "are you allergic to anything", J(3, 3, 3), steps, new[] { 1 });
            Assert.True(Assert.Single(CommunicationScorer.Collect(verified, Tables, new Dictionary<string, double>())).PhysicalVerified);
            Assert.Contains(verified.Records, x => x.Source == "deterministic" && x.Accepted);
        }

        [Fact]
        public void Every_critical_objective_names_a_behaviour_and_real_actions_of_that_behaviour()
        {
            foreach (var act in Tables.CriticalActs)
            {
                var e = Tables.CriticalEvidence[act];
                var b = Tables.Find(e.Behaviour);
                Assert.NotNull(b);
                Assert.NotEmpty(e.Actions);
                Assert.All(e.Actions, a => Assert.Contains(a, b!.Actions));
            }
        }

        [Fact]
        public void The_offline_check_applies_the_engines_own_rules_to_judged_speech()
        {
            string json = "[{\"id\":\"a\",\"steps\":[2],\"acts\":[{\"act\":\"ASK_TOPIC\",\"assertion\":\"performed\",\"actor\":\"trainee\",\"form\":\"question\",\"relevance\":3,\"correctness\":3,\"completeness\":3,\"span\":\"x\"}]},"
                          + "{\"id\":\"b\",\"steps\":[2],\"acts\":[{\"act\":\"ASK_TOPIC\",\"assertion\":\"negated\",\"actor\":\"trainee\",\"form\":\"statement\",\"relevance\":3,\"correctness\":3,\"completeness\":3,\"span\":\"x\"}]},"
                          + "{\"id\":\"c\",\"steps\":[2],\"acts\":[{\"act\":\"ANSWER_QUESTION\",\"assertion\":\"performed\",\"actor\":\"trainee\",\"form\":\"statement\",\"relevance\":3,\"correctness\":3,\"completeness\":3,\"span\":\"x\"}]},"
                          + "{\"id\":\"d\",\"steps\":[2],\"patient_question_pending\":true,\"acts\":[{\"act\":\"ANSWER_QUESTION\",\"assertion\":\"performed\",\"actor\":\"trainee\",\"form\":\"statement\",\"relevance\":3,\"correctness\":3,\"completeness\":3,\"span\":\"x\"}]}]";
            string output = SpeechScoreCheck.Run(json, Tables);
            var rows = ((List<object?>)MiniJson.Parse(output)!).Cast<Dictionary<string, object?>>().ToDictionary(d => (string)d["id"]!);
            Assert.Equal(1.0, System.Convert.ToDouble(rows["a"]["communication_total"], System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(0.0, System.Convert.ToDouble(rows["b"]["communication_total"], System.Globalization.CultureInfo.InvariantCulture));        // denied
            Assert.Equal(0.0, System.Convert.ToDouble(rows["c"]["communication_total"], System.Globalization.CultureInfo.InvariantCulture));        // no waiting question
            Assert.Equal(1.0, System.Convert.ToDouble(rows["d"]["communication_total"], System.Globalization.CultureInfo.InvariantCulture));        // the same words with a waiting question
        }
    }
}
