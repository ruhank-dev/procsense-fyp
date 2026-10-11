using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProcSense.Core.Clinical;
using ProcSense.Core.Content;

namespace ProcSense.Core.Tests
{
    /// <summary>The treatment library and its required checks (p0-clinical-logic-s10): tests T-TR1, T-TR4 and T-TR5 of the Implementation Guide, plus the library's own rules.</summary>
    public class TreatmentsTests
    {
        private static readonly string Json = File.ReadAllText(Path.Combine(TestCase.ContentRoot, "treatments.json")).Replace("\r\n", "\n");
        private static readonly TreatmentTables Tables = TestCase.Package().Treatments;

        private static TreatmentDef Get(string id) => Tables.ById[id];

        /// <summary>A patient for whom everything is in place, so a test breaks exactly one thing.</summary>
        private static TreatmentContext Ready(string treatment)
        {
            var c = new TreatmentContext { IdentityConfirmed = true, AllergiesKnown = true };
            c.Indicated.Add(treatment); c.TimingChecked.Add(treatment); c.Ordered.Add(treatment); c.Documented.Add(treatment); c.Done.Add("iv_access");
            return c;
        }

        private static CheckReport Before(string id, TreatmentContext c) => RequiredChecks.Evaluate(Tables, Get(id), c, "before");

        private static string Mutate(string from, string to, int occurrence = 0)
        {
            int at = -1;
            for (int i = 0; i <= occurrence; i++)
            {
                at = Json.IndexOf(from, at + 1, StringComparison.Ordinal);
                Assert.True(at >= 0, "the text to change was not found: " + from);
            }
            return Json.Substring(0, at) + to + Json.Substring(at + from.Length);
        }

        /// <summary>Changes the first occurrence of <paramref name="from"/> after the treatment <paramref name="treatment"/> starts.</summary>
        private static string MutateIn(string treatment, string from, string to)
        {
            int start = Json.IndexOf("\"id\": \"" + treatment + "\"", StringComparison.Ordinal);
            Assert.True(start >= 0, treatment);
            int at = Json.IndexOf(from, start, StringComparison.Ordinal);
            Assert.True(at >= 0, "the text to change was not found: " + from);
            return Json.Substring(0, at) + to + Json.Substring(at + from.Length);
        }

        private static string ErrorOf(string json) => Assert.Throws<ContentException>(() => TreatmentTables.Parse(json)).Message;

        // ---------------------------------------------------------------- the library

        [Fact]
        public void The_library_loads_with_the_package_and_has_the_treatments_the_book_uses()
        {
            Assert.Equal(17, Tables.Treatments.Count);
            Assert.Equal(Tables.Treatments.Count, Tables.Treatments.Select(t => t.Id).Distinct().Count());
            foreach (var id in new[] { "TR-PARACETAMOL", "TR-NSAID", "TR-OPIOID", "TR-IV-FLUIDS", "TR-ABX-PROPHYLAXIS", "TR-ADRENALINE", "TR-OXYGEN", "TR-IV-ACCESS" })
                Assert.True(Tables.ById.ContainsKey(id), id);
        }

        [Fact]
        public void Every_medicine_treatment_names_a_row_of_the_medicine_table_and_every_treatment_has_a_source()
        {
            var medicineIds = File.ReadAllLines(Path.Combine(TestCase.ContentRoot, "medicines.csv")).Skip(1).Select(l => l.Substring(0, l.IndexOf(',', StringComparison.Ordinal))).ToHashSet();
            foreach (var t in Tables.Treatments)
            {
                if (t.Kind == "medicine" || t.Kind == "fluid") Assert.Contains(t.MedicineId!, medicineIds);
                else Assert.Null(t.MedicineId);
                Assert.NotEmpty(t.Refs);
            }
        }

        [Fact]
        public void Every_treatment_lists_its_checks_and_keeps_the_order_of_the_check_list()
        {
            var order = Tables.Checks.Select(c => c.Id).ToList();
            foreach (var t in Tables.Treatments)
            {
                Assert.NotEmpty(t.RequiredChecks);
                Assert.Equal(t.RequiredChecks.OrderBy(c => order.IndexOf(c)).ToList(), t.RequiredChecks);
                Assert.Equal(t.RequiredChecks.Contains("restriction"), t.RestrictedBy.Count > 0);   // a restriction check with nothing to restrict (or the reverse) is a mistake
                Assert.Contains("identity", t.RequiredChecks);
                Assert.Contains("documentation", t.RequiredChecks);
            }
        }

        [Fact]
        public void The_treatments_that_need_an_IV_are_the_ones_the_medicine_table_words_that_way()
        {
            var iv = Tables.Treatments.Where(t => t.Requires.Contains("iv_access")).Select(t => t.Id).OrderBy(x => x, StringComparer.Ordinal).ToList();

            Assert.Equal(new[] { "TR-ABX-PROPHYLAXIS", "TR-HYPERKALAEMIA", "TR-IV-FLUIDS", "TR-IV-GLUCOSE", "TR-OPIOID", "TR-SEPSIS-BUNDLE", "TR-STEROID-COVER" }, iv);
        }

        [Fact]
        public void The_effects_a_treatment_names_are_all_in_the_effect_list()
        {
            foreach (var t in Tables.Treatments)
                foreach (var e in t.Effects) Assert.Contains(e, Tables.Effects);
            Assert.Contains("EFF-FLUID-BOLUS", Get("TR-IV-FLUIDS").Effects);
            Assert.Contains("EFF-OXYGEN", Get("TR-OXYGEN").Effects);
        }

        // ---------------------------------------------------------------- dose policy: T-TR1

        [Fact] // T-TR1
        public void A_dose_without_a_reviewer_is_a_content_error()
        {
            string reviewer = Get("TR-PARACETAMOL").Dose!.Reviewer;
            string bad = Json.Replace("\"reviewer\": \"" + reviewer + "\"", "\"reviewer\": \"\"");

            Assert.Contains("DOSE_REQUIRES_REVIEWER", ErrorOf(bad));
        }

        [Fact]
        public void A_dose_without_a_source_is_a_content_error()
        {
            string source = Get("TR-PARACETAMOL").Dose!.Source;

            Assert.Contains("DOSE_REQUIRES_REVIEWER", ErrorOf(Mutate("\"source\": \"" + source + "\"", "\"source\": \"\"")));
        }

        [Fact]
        public void A_dose_on_a_patient_specific_treatment_is_refused()
        {
            // TR-NSAID is PATIENT_SPECIFIC: the simulator shows the decision, never a number.
            int nsaid = Json.IndexOf("\"id\": \"TR-NSAID\"", StringComparison.Ordinal);
            int nullDose = Json.IndexOf("\"dose_display\": null", nsaid, StringComparison.Ordinal);
            string bad = Json.Substring(0, nullDose) + "\"dose_display\": { \"text\": \"a number\", \"source\": \"x\", \"reviewer\": \"y\" }" + Json.Substring(nullDose + "\"dose_display\": null".Length);

            Assert.Contains("only an EDUCATIONAL_EXAMPLE treatment may show a dose", ErrorOf(bad));
        }

        [Fact]
        public void An_educational_example_must_carry_its_dose_text()
        {
            int fluids = Json.IndexOf("\"id\": \"TR-IV-FLUIDS\"", StringComparison.Ordinal);
            int open = Json.IndexOf("\"dose_display\": {", fluids, StringComparison.Ordinal);
            int close = Json.IndexOf("},", open, StringComparison.Ordinal) + 1;
            string bad = Json.Substring(0, open) + "\"dose_display\": null" + Json.Substring(close);

            Assert.Contains("must carry its dose text", ErrorOf(bad));
        }

        [Fact]
        public void In_the_real_library_only_educational_examples_carry_a_dose_and_each_names_source_and_reviewer()
        {
            foreach (var t in Tables.Treatments)
            {
                if (t.DosePolicy == "EDUCATIONAL_EXAMPLE")
                {
                    Assert.NotNull(t.Dose);
                    Assert.NotEmpty(t.Dose!.Source);
                    Assert.NotEmpty(t.Dose.Reviewer);
                }
                else Assert.Null(t.Dose);
            }
            Assert.Null(Get("TR-OPIOID").Dose);
            Assert.Null(Get("TR-NALOXONE").Dose);
        }

        // ---------------------------------------------------------------- required checks

        [Fact]
        public void With_everything_in_place_a_treatment_has_no_items_before_and_after()
        {
            foreach (var t in Tables.Treatments)
            {
                var c = Ready(t.Id);
                Assert.Empty(RequiredChecks.Evaluate(Tables, t, c, "before").Items);
                Assert.Empty(RequiredChecks.Evaluate(Tables, t, c, "after").Items);
            }
        }

        [Fact] // T-TR4
        public void A_missing_timing_check_is_an_item()
        {
            var c = Ready("TR-ABX-PROPHYLAXIS");
            c.TimingChecked.Clear();

            var report = Before("TR-ABX-PROPHYLAXIS", c);

            Assert.True(report.Allowed);
            var item = Assert.Single(report.Items);
            Assert.Equal(new CheckItem("TR-ABX-PROPHYLAXIS", "timing", "major", "missing"), item);
        }

        [Fact]
        public void A_treatment_that_does_not_list_the_timing_check_is_not_judged_on_it()
        {
            var c = Ready("TR-PARACETAMOL");
            c.TimingChecked.Clear();

            Assert.Empty(Before("TR-PARACETAMOL", c).Items);
        }

        [Fact] // T-TR5
        public void An_IV_treatment_without_IV_access_is_rejected()
        {
            var c = Ready("TR-IV-FLUIDS");
            c.Done.Clear();

            var report = Before("TR-IV-FLUIDS", c);

            Assert.False(report.Allowed);
            Assert.Equal("IV_ACCESS_REQUIRED", report.RejectReason);
            Assert.Empty(report.Items);   // refused outright: nothing else is judged
        }

        [Fact]
        public void The_same_treatment_is_allowed_once_IV_access_is_in_place()
        {
            var c = Ready("TR-IV-FLUIDS");

            Assert.True(Before("TR-IV-FLUIDS", c).Allowed);
        }

        [Fact]
        public void A_treatment_that_needs_no_IV_is_allowed_without_one()
        {
            var c = Ready("TR-PARACETAMOL");
            c.Done.Clear();

            Assert.True(Before("TR-PARACETAMOL", c).Allowed);
        }

        [Fact]
        public void Identity_and_allergy_checks_missing_are_critical_and_reported_in_check_order()
        {
            var c = Ready("TR-PARACETAMOL");
            c.IdentityConfirmed = false; c.AllergiesKnown = false; c.Ordered.Clear();

            var report = Before("TR-PARACETAMOL", c);

            Assert.Equal(new[] { "identity", "allergy_known", "order" }, report.Items.Select(i => i.Check));
            Assert.Equal(new[] { "critical", "critical", "major" }, report.Items.Select(i => i.Severity));
            Assert.True(report.HasCritical);
        }

        [Fact]
        public void A_restriction_that_applies_is_a_critical_item_naming_the_condition()
        {
            var c = Ready("TR-NSAID");
            c.Restrictions.Add("kidney_injury");

            var item = Assert.Single(Before("TR-NSAID", c).Items);

            Assert.Equal(new CheckItem("TR-NSAID", "restriction", "critical", "kidney_injury"), item);
        }

        [Fact]
        public void A_condition_that_does_not_restrict_this_treatment_is_ignored_and_each_one_that_does_is_an_item()
        {
            var c = Ready("TR-NSAID");
            c.Restrictions.Add("long_qtc");          // restricts ondansetron, not NSAIDs
            Assert.Empty(Before("TR-NSAID", c).Items);

            c.Restrictions.Add("asthma"); c.Restrictions.Add("pregnancy");
            Assert.Equal(new[] { "asthma", "pregnancy" }, Before("TR-NSAID", c).Items.Select(i => i.Reason).OrderBy(x => x, StringComparer.Ordinal));
        }

        [Fact]
        public void Oxygen_is_restricted_for_a_patient_who_retains_carbon_dioxide()
        {
            var c = Ready("TR-OXYGEN");
            c.Restrictions.Add("co2_retainer");

            Assert.Equal("co2_retainer", Assert.Single(Before("TR-OXYGEN", c).Items).Reason);
        }

        [Fact]
        public void A_check_the_treatment_does_not_list_is_never_judged()
        {
            var c = new TreatmentContext { IdentityConfirmed = true };   // allergies unknown, nothing ordered
            c.Ordered.Add("TR-IV-ACCESS");

            Assert.Empty(Before("TR-IV-ACCESS", c).Items);   // IV access lists identity and order only
        }

        [Fact]
        public void Documentation_is_judged_after_the_treatment_and_not_before()
        {
            var c = Ready("TR-PARACETAMOL");
            c.Documented.Clear();

            Assert.Empty(Before("TR-PARACETAMOL", c).Items);
            var item = Assert.Single(RequiredChecks.Evaluate(Tables, Get("TR-PARACETAMOL"), c, "after").Items);
            Assert.Equal(new CheckItem("TR-PARACETAMOL", "documentation", "minor", "missing"), item);
        }

        [Fact]
        public void The_same_context_always_gives_the_same_report()
        {
            var c = Ready("TR-NSAID");
            c.IdentityConfirmed = false; c.Restrictions.Add("heart_failure"); c.Restrictions.Add("asthma");

            Assert.Equal(Before("TR-NSAID", c).Items, Before("TR-NSAID", c).Items);
            Assert.Equal(new[] { "identity", "restriction", "restriction" }, Before("TR-NSAID", c).Items.Select(i => i.Check));
        }

        [Fact]
        public void An_unknown_phase_is_refused()
        {
            Assert.Throws<ArgumentException>(() => RequiredChecks.Evaluate(Tables, Get("TR-OXYGEN"), new TreatmentContext(), "during"));
        }

        // ---------------------------------------------------------------- the loader refuses a broken file

        [Fact]
        public void The_loader_refuses_a_check_that_is_not_in_the_check_list()
        {
            Assert.Contains("is not in the check list", ErrorOf(MutateIn("TR-PARACETAMOL", "\"allergy_known\",", "\"vibes\",")));
        }

        [Fact]
        public void The_loader_refuses_an_effect_nobody_defined()
        {
            Assert.Contains("is not in the effect list", ErrorOf(MutateIn("TR-PARACETAMOL", "\"EFF-ANALGESIA\"", "\"EFF-MAGIC\"")));
        }

        [Fact]
        public void The_loader_refuses_an_unknown_prerequisite()
        {
            Assert.Contains("not a known prerequisite", ErrorOf(MutateIn("TR-OPIOID", "\"iv_access\"", "\"central_line\"")));
        }

        [Fact]
        public void The_loader_refuses_a_restriction_check_with_nothing_to_restrict()
        {
            // TR-ABX-S2 lists no restriction; adding the check without a condition is a mistake.
            int at = Json.IndexOf("\"id\": \"TR-ABX-S2\"", StringComparison.Ordinal);
            int checks = Json.IndexOf("\"indication\",", at, StringComparison.Ordinal);
            string bad = Json.Substring(0, checks) + "\"indication\",\n    \"restriction\"," + Json.Substring(checks + "\"indication\",".Length);

            Assert.Contains("names no restriction", ErrorOf(bad));
        }

        [Fact]
        public void The_loader_refuses_a_misspelled_field_and_a_bad_register_id()
        {
            Assert.Contains("unknown field", ErrorOf(MutateIn("TR-NSAID", "\"kind\": \"medicine\",", "\"kind\": \"medicine\", \"extra\": 1,")));
            Assert.Contains("is not a register id", ErrorOf(Mutate("\"SR-4100\"", "\"4000\"")));
        }

        [Fact]
        public void A_package_without_a_treatment_file_cannot_load()
        {
            var reader = new CompositeContentReader(new FileContentReader(Path.Combine(TestCase.Repo, "tests", "fixtures")));
            Assert.ThrowsAny<Exception>(() => ContentLoader.Load(reader, "VAR-S1-A"));
        }
    }
}
