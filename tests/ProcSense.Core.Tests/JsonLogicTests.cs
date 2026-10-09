using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ProcSense.Core.Content;
using ProcSense.Core.Rules;

namespace ProcSense.Core.Tests
{
    /// <summary>The rule evaluator against content/rules/vectors.json (written by tools/rules/make_vectors.py) plus the specific guarantees of p0-clinical-logic-s01.</summary>
    public class JsonLogicTests
    {
        private static Dictionary<string, object?> Vectors() =>
            (Dictionary<string, object?>)MiniJson.Parse(File.ReadAllText(Path.Combine(TestCase.ContentRoot, "rules", "vectors.json")))!;

        private static List<Dictionary<string, object?>> Section(string key) =>
            ((List<object?>)Vectors()[key]!).Cast<Dictionary<string, object?>>().ToList();

        private static bool Equal(object? expected, object? actual)
        {
            if (expected == null || actual == null) return expected == null && actual == null;
            if (IsNumber(expected) && IsNumber(actual)) return Convert.ToDouble(expected) == Convert.ToDouble(actual);
            if (expected is List<object?> e && actual is List<object?> a) return e.Count == a.Count && e.Zip(a, Equal).All(x => x);
            return expected.Equals(actual);
        }

        private static bool IsNumber(object o) => o is double || o is long || o is int;

        private static string Show(object? v) => v switch
        {
            null => "null",
            List<object?> l => "[" + string.Join(",", l.Select(Show)) + "]",
            string s => "\"" + s + "\"",
            bool b => b ? "true" : "false",
            _ => Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture)!,
        };

        [Fact]
        public void There_are_at_least_150_vectors()
        {
            Assert.True(Section("vectors").Count >= 150, Section("vectors").Count.ToString());
            Assert.True(Section("errors").Count >= 10);
        }

        [Fact]
        public void Every_vector_gives_the_expected_result()
        {
            var failures = new List<string>();
            foreach (var vec in Section("vectors"))
            {
                string name = (string)vec["name"]!;
                try
                {
                    object? actual = Logic.Compile(vec["rule"], name).Evaluate(vec["data"]);
                    if (!Equal(vec["expect"], actual)) failures.Add(name + ": expected " + Show(vec["expect"]) + ", got " + Show(actual));
                }
                catch (Exception e) { failures.Add(name + ": threw " + e.GetType().Name + " " + e.Message); }
            }

            Assert.True(failures.Count == 0, string.Join("\n", failures));
        }

        // T-JL2 and friends: a malformed rule fails when loaded; wrong types fail when run
        [Fact]
        public void Every_error_vector_fails_the_way_it_says()
        {
            var failures = new List<string>();
            foreach (var vec in Section("errors"))
            {
                string name = (string)vec["name"]!, kind = (string)vec["error"]!;
                Logic? logic = null;
                try { logic = Logic.Compile(vec["rule"], name); }
                catch (ContentException) when (kind == "compile") { continue; }
                catch (Exception e) { failures.Add(name + ": expected " + kind + " error, got " + e.GetType().Name); continue; }

                if (kind == "compile") { failures.Add(name + ": compiled but should not have"); continue; }
                try { logic.Evaluate(new Dictionary<string, object?>()); failures.Add(name + ": ran but should have thrown"); }
                catch (LogicException) { }
            }

            Assert.True(failures.Count == 0, string.Join("\n", failures));
        }

        [Fact]
        public void An_unknown_operator_names_the_place_and_the_allowed_operators()
        {
            var e = Assert.Throws<ContentException>(() => Logic.Compile(MiniJson.Parse("{\"and\":[true,{\"foo\":[1]}]}"), "rules[2].when"));

            Assert.Contains("rules[2].when.and[1]", e.Message);
            Assert.Contains("unknown operator 'foo'", e.Message);
        }

        // T-JL4
        [Fact]
        public void Nesting_deeper_than_32_levels_is_a_load_error()
        {
            string Nest(int n) => string.Concat(Enumerable.Repeat("{\"!\":[", n)) + "true" + string.Concat(Enumerable.Repeat("]}", n));

            Logic.Compile(MiniJson.Parse(Nest(31)));   // 31 operations + the literal = 32 levels: allowed
            var e = Assert.Throws<ContentException>(() => Logic.Compile(MiniJson.Parse(Nest(32))));

            Assert.Contains("nested more than 32 levels", e.Message);
        }

        [Fact]
        public void Numbers_read_from_json_as_whole_numbers_still_compare_with_decimals()
        {
            var data = MiniJson.Parse("{\"spo2\": 98, \"temp\": 36.7}");

            Assert.True(Logic.Compile(MiniJson.Parse("{\"and\":[{\">=\":[{\"var\":\"spo2\"},94]},{\"<\":[{\"var\":\"temp\"},38]}]}")).Test(data));
            Assert.True(Logic.Compile(MiniJson.Parse("{\"==\":[{\"var\":\"spo2\"},98.0]}")).Test(data));
        }

        [Fact]
        public void The_same_rule_gives_the_same_answer_every_time()
        {
            var rule = Logic.Compile(MiniJson.Parse("{\"some\":[{\"var\":\"xs\"},{\">\":[{\"var\":\"\"},2]}]}"));
            var data = MiniJson.Parse("{\"xs\":[1,2,3]}");

            Assert.All(Enumerable.Range(0, 100), _ => Assert.True(rule.Test(data)));
        }

        // T-JL3: 10,000 evaluations. The plan's 20 ms is for a Release build on a quiet machine; the guard is looser so a busy CI runner cannot fail it.
        [Fact]
        public void Ten_thousand_evaluations_are_fast()
        {
            var rule = Logic.Compile(MiniJson.Parse("{\"and\":[{\">=\":[{\"var\":\"v.hr\"},50]},{\"<\":[{\"var\":\"v.hr\"},130]},{\"in\":[{\"var\":\"s\"},[\"a\",\"b\"]]}]}"));
            var data = MiniJson.Parse("{\"v\":{\"hr\":76},\"s\":\"b\"}");
            for (int i = 0; i < 1000; i++) rule.Test(data);

            // Best of five runs: other tests run in parallel, and one noisy run says nothing about the evaluator.
            double best = double.MaxValue;
            for (int run = 0; run < 5; run++)
            {
                var clock = Stopwatch.StartNew();
                for (int i = 0; i < 10000; i++) rule.Test(data);
                best = Math.Min(best, clock.Elapsed.TotalMilliseconds);
            }

            Assert.True(best < 200, best.ToString("F1") + " ms for 10,000 evaluations");
        }
    }
}
