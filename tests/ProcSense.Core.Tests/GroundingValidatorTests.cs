using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProcSense.Core.AIContext;
using ProcSense.Core.Content;

namespace ProcSense.Core.Tests
{
    /// <summary>The C# twin of the grounding validator gives exactly the Python validator's codes on the shared vectors (p1-ai-language s01, plan test T-GV5).</summary>
    public class GroundingValidatorTests
    {
        private static readonly Lexicon Lex = Lexicon.Parse(File.ReadAllText(Path.Combine(TestCase.ContentRoot, "ai", "lexicon.json")));
        private static readonly Dictionary<string, object?> Vectors = (Dictionary<string, object?>)MiniJson.Parse(File.ReadAllText(Path.Combine(TestCase.Repo, "tests", "vectors", "grounding_vectors.json")))!;

        [Fact]
        public void T_GV5_every_shared_vector_gets_exactly_the_expected_codes()
        {
            var contexts = (Dictionary<string, object?>)Vectors["contexts"]!;
            var wrong = new List<string>();
            var vectors = (List<object?>)Vectors["vectors"]!;
            foreach (var v in vectors.Select(x => (Dictionary<string, object?>)x!))
            {
                var ctx = ValidationContext.Parse(contexts[(string)v["context"]!]);
                var got = Validator.Validate(v["output"], ctx, Lex);
                var want = ((List<object?>)v["expect"]!).Select(x => (string)x!).ToList();
                if (!got.SequenceEqual(want)) wrong.Add((string)v["name"]! + ": got [" + string.Join(",", got) + "] want [" + string.Join(",", want) + "]");
            }
            Assert.Empty(wrong);
            Assert.True(vectors.Count >= 40);
        }

        [Fact]
        public void The_lexicon_has_the_five_categories_and_one_word_list_per_event_the_director_can_fire()
        {
            Assert.Equal(new[] { "allergen", "condition", "drug", "equipment", "test" }, Lex.Categories.Keys.OrderBy(k => k, System.StringComparer.Ordinal));
            Assert.Equal(Content.Events.Events.Select(e => e.Id).OrderBy(x => x, System.StringComparer.Ordinal), Lex.Events.Keys.OrderBy(x => x, System.StringComparer.Ordinal));
        }

        private static readonly ContentPackage Content = TestCase.Package();

        [Fact]
        public void Normalisation_matches_the_python_twin_on_awkward_text()
        {
            Assert.Equal("potassium 6.4 sugar 13.4", Validator.Norm("Potassium 6.4, sugar 13.4!"));
            Assert.Equal("co-amoxiclav tablets", Validator.Norm("Co-amoxiclav tablets"));
            Assert.Equal("didn t", Validator.Norm("didn't"));
        }
    }
}
