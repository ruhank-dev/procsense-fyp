using System.IO;
using System.Linq;
using ProcSense.Core.Content;

namespace ProcSense.Core.Tests
{
    /// <summary>The playable case files (content/case_files) load through Core's strict loader, agree with the card Core already reads, and broken files are refused with the path.</summary>
    public class CaseFileTests
    {
        private static readonly string Dir = Path.Combine(TestCase.ContentRoot, "case_files");

        private static string[] Ids() => Directory.GetFiles(Dir, "*.json").Select(Path.GetFileNameWithoutExtension).OrderBy(x => x, System.StringComparer.Ordinal).ToArray()!;

        private static ContentPackage Package() => ContentLoader.Load(new FileContentReader(TestCase.ContentRoot), new[] { "VAR-S1-A" }, Ids());

        private static string Json(string id) => File.ReadAllText(Path.Combine(Dir, id + ".json"));

        private static ContentException Refused(string id, string from, string to)
        {
            var json = Json(id);
            Assert.Contains(from, json);
            return Assert.Throws<ContentException>(() => CaseFileLoader.Load(json.Replace(from, to), id));
        }

        [Fact]
        public void Every_case_file_loads_and_there_are_the_books_53_plus_the_combination_cases_in_three_tiers()
        {
            var files = Package().CaseFiles;

            Assert.Equal(TestCase.AllCases, files.Count);
            Assert.Equal(12, TestCase.CombinationIds.Count);                                                   // DT-220: six planned and six emergency combinations
            Assert.Equal(17, files.Values.Count(f => f.Tier == "P0"));
            Assert.Equal(16, files.Values.Count(f => f.Tier == "P1"));
            Assert.Equal(20 + TestCase.CombinationIds.Count, files.Values.Count(f => f.Tier == "P2"));
            Assert.All(files, kv => Assert.Equal(kv.Key, kv.Value.Id));
            var pending = System.Text.Json.Nodes.JsonNode.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(TestCase.ContentRoot, "case_signoff_pending.json")))!["pending"]!.AsObject().Select(kv => kv.Key).ToList();
            Assert.All(files.Values.Where(f => !pending.Contains(f.Id) && !TestCase.CombinationIds.Contains(f.Id)), f => Assert.Equal("APPROVED_FOR_TRAINING", f.Status));
            Assert.All(TestCase.CombinationIds, id =>
            {
                Assert.Equal("APPROVED_FOR_TRAINING", files[id].Status);                                       // the owner authorized educational practice use on 25 Oct 2026 (--grant-cases)
                Assert.Contains("educational practice", files[id].Approval!.By);
                Assert.Contains("No clinician reviewed", files[id].Approval!.Note);                           // the approval never claims clinical review
            });
            Assert.All(files.Values.Where(f => pending.Contains(f.Id)), f => Assert.Equal("DRAFT", f.Status));       // changed after the last attestation: awaiting clinician sign-off
            Assert.Equal(new[] { "SC-4", "VAR-S1-B" }, pending.OrderBy(x => x, System.StringComparer.Ordinal));      // the six P1 stacked cases were signed off by the owner on 17 Oct 2026
        }

        [Fact]
        public void The_twenty_scenario_cards_all_have_a_case_file()
        {
            var files = Package().CaseFiles;

            for (int n = 1; n <= 20; n++) Assert.True(files.ContainsKey("SC-" + n), "SC-" + n);
        }

        [Fact]
        public void The_case_file_agrees_with_the_card_core_already_reads()
        {
            var package = Package();
            var card = package.Cases["VAR-S1-A"];
            var file = package.CaseFiles["VAR-S1-A"];

            Assert.Equal(card.Identity.FullName, file.Identity.FullName);
            Assert.Equal(card.Identity.Age, file.Identity.Age);
            Assert.Equal(card.Identity.Dob, file.Identity.Dob);
            Assert.Equal(card.Identity.Mrn, file.Identity.Mrn);
            Assert.Equal(card.Timing.SurgeryTime, file.Presentation.SurgeryTime);
            Assert.Equal(card.BaselineVitals.HeartRate, file.Observations[0].HeartRate);
            Assert.Equal(card.BaselineVitals.SystolicBp, file.Observations[0].SystolicBp);
            Assert.Equal(card.History.Select(h => h.Topic).OrderBy(x => x), file.History.Select(h => h.Topic).OrderBy(x => x));
        }

        [Fact]
        public void The_usman_timeline_reaches_news2_17_untreated()
        {
            var usman = Package().CaseFiles["VAR-S2-PD"];

            var untreated = usman.Observations.Where(o => o.Condition != "after_correct_action").Select(o => o.News2).ToArray();

            Assert.Equal(new long[] { 2, 5, 10, 17 }, untreated);
            Assert.Equal(3, usman.Problems.Count);
        }

        [Fact]
        public void Every_case_has_exactly_one_volunteered_fact_and_a_right_and_wrong_option_in_each_decision()
        {
            foreach (var f in Package().CaseFiles.Values)
            {
                Assert.Single(f.History, h => h.Volunteered);
                Assert.All(f.Decisions, d => Assert.Contains(d.Options, o => o.Correct));
                Assert.All(f.Decisions, d => Assert.Contains(d.Options, o => !o.Correct));
                Assert.Equal(Enumerable.Range(1, f.Problems.Count).Select(i => (long)i), f.Problems.Select(p => p.Rank).OrderBy(x => x));
            }
        }

        [Fact]
        public void A_critical_potassium_keeps_its_flag_and_quality_note()
        {
            var jk = Package().CaseFiles["VAR-S1-J"];

            var k = jk.Results.First(r => r.Test == "Potassium");

            Assert.True(k.Flag == "critical_high" || k.Quality == "haemolysed" || k.Flag == "high");
            Assert.Contains(Package().CaseFiles["SC-12"].Results, r => r.Quality == "haemolysed");
        }

        [Fact]
        public void Extra_field_is_rejected_with_its_path()
        {
            var e = Refused("VAR-S1-C", "\"occupation\":", "\"shoe_size\": 5, \"occupation\":");

            Assert.Contains("identity.shoe_size: unknown field", e.Message);
        }

        [Fact]
        public void A_value_outside_its_list_is_rejected()
        {
            var e = Refused("VAR-S1-C", "\"expected_end_state\": \"ready\"", "\"expected_end_state\": \"fine\"");

            Assert.Contains("expected_end_state", e.Message);
            Assert.Contains("is not one of", e.Message);
        }

        [Fact]
        public void An_age_that_does_not_match_the_date_of_birth_is_rejected()
        {
            var json = Json("VAR-S1-A");
            var age = System.Text.RegularExpressions.Regex.Match(json, "\"age\": (\\d+)").Groups[1].Value;

            var e = Assert.Throws<ContentException>(() => CaseFileLoader.Load(json.Replace("\"age\": " + age, "\"age\": 60"), "VAR-S1-A"));

            Assert.Contains("identity.age", e.Message);
        }

        [Fact]
        public void A_second_volunteered_fact_is_rejected()
        {
            var json = Json("VAR-S1-A");
            var i = json.IndexOf("\"topic\": \"red_flags\"", System.StringComparison.Ordinal);
            var j = json.IndexOf("\"volunteered\": false", i, System.StringComparison.Ordinal);

            var e = Assert.Throws<ContentException>(() => CaseFileLoader.Load(json.Substring(0, j) + "\"volunteered\": true" + json.Substring(j + "\"volunteered\": false".Length), "VAR-S1-A"));

            Assert.Contains("exactly one fact is volunteered", e.Message);
        }

        [Fact]
        public void A_non_synthetic_patient_is_rejected()
        {
            var e = Refused("VAR-S1-C", "\"synthetic\": true", "\"synthetic\": false");

            Assert.Contains("synthetic", e.Message);
        }

        [Fact]
        public void An_approved_case_without_an_approval_record_is_rejected()
        {
            var json = Json("VAR-S1-C");

            var e = Assert.Throws<ContentException>(() => CaseFileLoader.Load(json.Replace("\"approval\": {", "\"approved_by\": {"), "VAR-S1-C"));

            Assert.Contains("approval", e.Message);
        }

        [Fact]
        public void A_file_whose_id_differs_from_its_name_is_rejected()
        {
            var reader = new FileContentReader(TestCase.ContentRoot);
            Assert.Equal("VAR-S1-C", ContentLoader.LoadCaseFile(reader, "VAR-S1-C").Id);

            var tmp = Path.Combine(Path.GetTempPath(), "procsense-casefile-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(tmp, "case_files"));
            try
            {
                File.WriteAllText(Path.Combine(tmp, "case_files", "VAR-S1-D.json"), Json("VAR-S1-C"));

                var e = Assert.Throws<ContentException>(() => ContentLoader.LoadCaseFile(new FileContentReader(tmp), "VAR-S1-D"));

                Assert.Contains("holds the case VAR-S1-C", e.Message);
            }
            finally { Directory.Delete(tmp, true); }
        }

        [Fact]
        public void A_decision_with_no_correct_option_is_rejected()
        {
            var json = Json("VAR-S1-C");
            var broken = json.Replace("\"correct\": true", "\"correct\": false");

            var e = Assert.Throws<ContentException>(() => CaseFileLoader.Load(broken, "VAR-S1-C"));

            Assert.Contains("needs at least one correct and one wrong option", e.Message);
        }

        [Fact]
        public void Problem_ranks_with_a_gap_are_rejected()
        {
            var json = Json("VAR-S2-PD");
            var e = Assert.Throws<ContentException>(() => CaseFileLoader.Load(json.Replace("\"rank\": 2", "\"rank\": 7"), "VAR-S2-PD"));

            Assert.Contains("ranks must be 1 to", e.Message);
        }
    }
}
