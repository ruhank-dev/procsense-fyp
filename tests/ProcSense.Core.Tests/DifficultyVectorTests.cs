using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProcSense.Core.Content;
using ProcSense.Core.Difficulty;

namespace ProcSense.Core.Tests
{
    /// <summary>The engine's rating rules give the ratings the Python mirror (server/generation/objectives.py) wrote into the shared vectors (p1-live-scenario-generation-s01).</summary>
    public class DifficultyVectorTests
    {
        private static readonly DifficultyTables Tables = DifficultyTables.Parse(File.ReadAllText(Path.Combine(TestCase.ContentRoot, "difficulty", "dimensions.json")));
        private static readonly Dictionary<string, object?> Vectors = (Dictionary<string, object?>)MiniJson.Parse(File.ReadAllText(Path.Combine(TestCase.Repo, "tests", "vectors", "difficulty_vectors.json")))!;

        private static double Num(object? o) => o is long l ? l : (double)o!;

        [Fact]
        public void The_constants_in_the_vectors_are_the_engines()
        {
            Assert.Equal(Tables.K, Num(Vectors["k"]));
            Assert.Equal(Tables.Start, Num(Vectors["start"]));
            Assert.Equal(Tables.MinSessions, (int)(long)Vectors["min_sessions"]!);
            foreach (var kv in (Dictionary<string, object?>)Vectors["tier_offsets"]!) Assert.Equal(Tables.CaseRating(kv.Key), Tables.Start + Num(kv.Value));
        }

        [Fact]
        public void Every_shared_history_gives_the_same_ratings_in_the_engine()
        {
            var wrong = new List<string>();
            foreach (var c in ((List<object?>)Vectors["cases"]!).Select(x => (Dictionary<string, object?>)x!))
            {
                var ratings = new Ratings(Tables);
                foreach (var s in ((List<object?>)c["sessions"]!).Select(x => (Dictionary<string, object?>)x!))
                {
                    var behaviours = ((Dictionary<string, object?>)s["behaviours"]!).ToDictionary(kv => kv.Key, kv => { var p = (List<object?>)kv.Value!; return ((int)(long)p[0]!, (int)(long)p[1]!); });
                    ratings.Add(new SessionRecord((string)s["case"]!, behaviours), (string)s["tier"]!);
                }
                var want = (Dictionary<string, object?>)c["expected"]!;
                if ((long)want["sessions"]! != ratings.Sessions) wrong.Add((string)c["name"]! + ": sessions");
                foreach (var kv in (Dictionary<string, object?>)want["ratings"]!)
                    if (System.Math.Abs(ratings.Of(kv.Key) - Num(kv.Value)) > 1e-6) wrong.Add($"{c["name"]}: {kv.Key} engine {ratings.Of(kv.Key):R} vector {Num(kv.Value):R}");
                foreach (var kv in (Dictionary<string, object?>)want["times_rated"]!)
                    if (ratings.TimesRated(kv.Key) != (long)kv.Value!) wrong.Add($"{c["name"]}: {kv.Key} times rated");
                var weakest = ratings.RatedBehaviours.OrderBy(b => ratings.Of(b)).ThenBy(b => b, System.StringComparer.Ordinal).Take(3).ToList();
                if (!weakest.SequenceEqual(((List<object?>)want["weakest"]!).Select(x => (string)x!))) wrong.Add($"{c["name"]}: weakest {string.Join(",", weakest)}");
            }
            Assert.Empty(wrong);
        }
    }
}
