using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Rules;

namespace ProcSense.Core.Tests
{
    /// <summary>Rule records, provenance and the engine (p0-clinical-logic-s01). Uses tests/fixtures/rules: test-only rules, not clinical content.</summary>
    public class RuleEngineTests
    {
        private static string FixtureText() => File.ReadAllText(Path.Combine(TestCase.Repo, "tests", "fixtures", "rules", "test_rules.json"));

        private static List<RuleRecord> Fixture() => RuleLoader.Parse(FixtureText(), "test_rules");

        private static string Edit(string from, string to) => FixtureText().Replace(from, to);

        [Fact]
        public void The_fixture_rules_load_with_their_provenance()
        {
            var rules = Fixture();

            Assert.Equal(new[] { "TEST-R-RANGE", "TEST-R-BLOCK" }, rules.Select(r => r.Id));
            Assert.Equal(RuleAction.Block, rules[1].Action);
            Assert.Equal(new[] { "SR-0001", "SR-0002" }, rules[1].Refs);
            Assert.Equal("REVIEWED", rules[1].Status);
            Assert.Equal("high", rules[1].Severity);
        }

        [Fact]
        public void Every_rule_passes_its_own_test_cases()
        {
            Assert.Empty(RuleLoader.RunOwnTests(Fixture()));
        }

        [Fact]
        public void A_rule_whose_test_disagrees_is_reported_by_name()
        {
            var rules = RuleLoader.Parse(Edit("\"state\": { \"n\": 3 }, \"expect\": true", "\"state\": { \"n\": 3 }, \"expect\": false"));

            var failures = RuleLoader.RunOwnTests(rules);

            Assert.Single(failures);
            Assert.Contains("TEST-R-RANGE / inside", failures[0]);
        }

        [Fact]
        public void The_engine_reports_only_the_rules_that_fire_in_id_order()
        {
            var engine = new RuleEngine(Fixture());
            var state = MiniJson.Parse("{\"n\": 3, \"items\": [ {\"done\": true} ]}");

            var hits = engine.Evaluate(state);

            Assert.Equal(new[] { "TEST-R-BLOCK", "TEST-R-RANGE" }, hits.Select(h => h.RuleId));   // ordinal order, not file order
        }

        [Fact]
        public void No_rule_fires_when_nothing_matches()
        {
            Assert.Empty(new RuleEngine(Fixture()).Evaluate(MiniJson.Parse("{}")));
        }

        // The plan's check: a session log line shows "rule: R-xxx, ref: SR-xxxx"
        [Fact]
        public void A_hit_is_written_into_the_log_effects_with_its_rule_and_source()
        {
            var engine = new RuleEngine(Fixture());
            var outcome = new ActionOutcome();

            engine.Apply(MiniJson.Parse("{\"n\": 3}"), outcome);

            Assert.Equal(new[] { "rule:TEST-R-RANGE ref:SR-0001" }, outcome.Effects);
        }

        [Fact]
        public void A_rule_can_only_raise_block_review_concern_or_a_score_item()
        {
            foreach (var kind in new[] { "concern", "require_review", "block", "score_item" })
                Assert.Single(RuleLoader.Parse(Edit("\"kind\": \"concern\"", "\"kind\": \"" + kind + "\"")), r => r.Id == "TEST-R-RANGE");

            var e = Assert.Throws<ContentException>(() => RuleLoader.Parse(Edit("\"kind\": \"concern\"", "\"kind\": \"cancel_surgery\"")));
            Assert.Contains("cancel_surgery", e.Message);
        }

        [Theory]
        [InlineData("\"refs\": [ \"SR-0001\" ]", "\"refs\": []", "at least one source-register id")]
        [InlineData("\"refs\": [ \"SR-0001\" ]", "\"refs\": [ \"nope\" ]", "register id like SR-0001")]
        [InlineData("\"id\": \"TEST-R-RANGE\"", "\"id\": \"TEST-R-BLOCK\"", "duplicate rule id")]
        [InlineData("\"id\": \"TEST-R-RANGE\"", "\"id\": \"lower\"", "upper-case")]
        [InlineData("\"severity\": \"low\"", "\"severity\": \"urgent\"", "urgent")]
        [InlineData("\"status\": \"DRAFT\"", "\"status\": \"MAYBE\"", "MAYBE")]
        [InlineData("\"text\": \"Test fixture: n is inside the range.\"", "\"text\": \"  \"", "must not be empty")]
        public void A_rule_missing_its_provenance_or_with_a_bad_value_is_refused(string from, string to, string message)
        {
            var e = Assert.Throws<ContentException>(() => RuleLoader.Parse(Edit(from, to)));

            Assert.Contains(message, e.Message);
        }

        [Fact]
        public void A_rule_without_any_test_case_is_refused()
        {
            var json = "{\"rules\":[{\"id\":\"R-X\",\"when\":true,\"then\":{\"kind\":\"concern\",\"text\":\"t\"},\"severity\":\"low\",\"refs\":[\"SR-0001\"],\"status\":\"DRAFT\",\"tests\":[]}]}";

            var e = Assert.Throws<ContentException>(() => RuleLoader.Parse(json, "rules.json"));

            Assert.Contains("rules.json.rules[0].tests", e.Message);
            Assert.Contains("at least one test case", e.Message);
        }

        [Fact]
        public void A_misspelled_field_is_an_error_not_ignored()
        {
            var e = Assert.Throws<ContentException>(() => RuleLoader.Parse(Edit("\"severity\": \"low\"", "\"severity\": \"low\", \"sevrity\": \"x\"")));

            Assert.Contains("sevrity", e.Message);
            Assert.Contains("unknown field", e.Message);
        }

        [Fact]
        public void A_bad_operator_inside_a_rule_names_the_rule_file_and_position()
        {
            var e = Assert.Throws<ContentException>(() => RuleLoader.Parse(Edit("\"and\"", "\"andd\""), "test_rules"));

            Assert.Contains("test_rules.rules[0].when", e.Message);
            Assert.Contains("andd", e.Message);
        }
    }
}
