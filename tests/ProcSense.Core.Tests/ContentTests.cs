using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ProcSense.Core.Content;
using ProcSense.Core.State;

namespace ProcSense.Core.Tests
{
    public class ContentTests
    {
        private static readonly string Repo = TestCase.Repo;
        private static readonly string ContentRoot = TestCase.ContentRoot;

        private static string CaseJson() => File.ReadAllText(Path.Combine(ContentRoot, "cases", "VAR-S1-A.json"));

        private static CaseCard Card() => TestCase.Card();

        [Fact]
        public void Loaded_case_matches_the_file()
        {
            var card = Card();

            Assert.Equal("Ayesha Khan", card.Identity.FullName);
            Assert.Equal(42, card.Identity.Age);
            Assert.Equal("AYESHA KHAN", card.Wristband.NameText);
            Assert.Equal("MR 102938", card.Wristband.MrnText);
            Assert.Empty(card.Allergies);
            Assert.Equal(new TimeSpan(8, 0, 0), card.Timing.TimeNow);
            Assert.Equal(12, card.History.Count);
            Assert.Equal(124, card.BaselineVitals.SystolicBp);
            Assert.Equal(36.7, card.BaselineVitals.TemperatureC);
            Assert.Equal("SR-0027", card.Refs["baseline_vitals.heart_rate"]);
        }

        [Fact]
        public void Package_includes_nibp_timing()
        {
            var nibp = ContentLoader.Load(new FileContentReader(ContentRoot), "VAR-S1-A").Nibp;

            Assert.Equal(30, nibp.CycleSecondsMin);
            Assert.Equal(40, nibp.CycleSecondsMax);
        }

        [Fact]
        public void Extra_field_is_rejected_with_its_path()
        {
            var json = CaseJson().Replace("\"age\": 42,", "\"age\": 42, \"shoe_size\": 5,");

            var e = Assert.Throws<ContentException>(() => ContentLoader.LoadCase(json, "VAR-S1-A"));

            Assert.Contains("identity.shoe_size: unknown field", e.Message);
        }

        [Fact]
        public void Clinical_value_without_a_source_ref_is_rejected()
        {
            var json = CaseJson().Replace("\"baseline_vitals.spo2\": \"SR-0030\",", "");

            var e = Assert.Throws<ContentException>(() => ContentLoader.LoadCase(json, "VAR-S1-A"));

            Assert.Contains("no source reference for 'baseline_vitals.spo2'", e.Message);
        }

        [Fact]
        public void Bad_value_and_broken_json_give_clear_errors()
        {
            var badEnum = CaseJson().Replace("\"acvpu\": \"A\"", "\"acvpu\": \"X\"");
            var e1 = Assert.Throws<ContentException>(() => ContentLoader.LoadCase(badEnum, "VAR-S1-A"));
            Assert.Contains("baseline_vitals.acvpu: 'X' is not one of", e1.Message);

            var e2 = Assert.Throws<ContentException>(() => ContentLoader.LoadCase("{ \"id\": ", "VAR-S1-A"));
            Assert.Contains("invalid JSON", e2.Message);
        }

        [Fact]
        public void Patient_factory_builds_the_four_layers_from_the_case()
        {
            var card = Card();
            var p = PatientFactory.Create(card);

            Assert.Equal(card.Identity, p.Profile.Identity);
            Assert.False(p.Clinical.IdentityVerified);
            Assert.Equal(PatientFactory.PatientBay, p.Clinical.Location);
            Assert.Equal(card.BaselineVitals, p.Physiological.Vitals);
            Assert.Equal(0, p.Behavioural.Trust);
        }

        [Fact]
        public void History_facts_start_hidden_until_revealed()
        {
            var p = PatientFactory.Create(Card());

            Assert.Empty(p.Clinical.RevealedTopics);
            Assert.True(p.Clinical.Reveal("last_food"));
            Assert.False(p.Clinical.Reveal("last_food"));
            Assert.True(p.Clinical.IsRevealed("last_food"));
            Assert.False(p.Clinical.IsRevealed("smoking"));
        }

        [Fact]
        public void No_vital_sign_value_is_typed_into_Core_code()
        {
            var v = Card().BaselineVitals;
            var numbers = new[] { v.HeartRate, v.SystolicBp, v.DiastolicBp, v.RespiratoryRate, v.Spo2, v.TemperatureC }
                .Select(x => x.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToList();
            var code = Directory.GetFiles(Path.Combine(Repo, "src", "ProcSense.Core"), "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
                .Select(File.ReadAllText).ToList();

            foreach (var n in numbers)
                Assert.DoesNotContain(code, text => Regex.IsMatch(text, @"(?<![\w.])" + Regex.Escape(n) + @"(?![\w.])"));
        }
    }
}
