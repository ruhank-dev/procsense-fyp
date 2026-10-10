using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProcSense.Core.Clinical.Scores;
using ProcSense.Core.Content;

namespace ProcSense.Core.Tests
{
    /// <summary>The clinical calculators (p0-clinical-logic-s05): the walkthrough's worked examples, the edges of every range, missing inputs and broken tables.</summary>
    public class ScoresTests
    {
        private readonly Xunit.Abstractions.ITestOutputHelper _out;

        public ScoresTests(Xunit.Abstractions.ITestOutputHelper output) => _out = output;

        private static readonly ScoreTables Tables = ScoreTables.Parse(File.ReadAllText(Path.Combine(TestCase.Repo, "content", "scores", "scores.json")), "scores");
        private static readonly ScoreCalculator Calc = new ScoreCalculator(Tables);

        private static Dictionary<string, object?> F(params (string name, object? value)[] facts) => facts.ToDictionary(x => x.name, x => x.value);

        private static Dictionary<string, object?> With(Dictionary<string, object?> facts, params (string name, object? value)[] change)
        {
            var copy = new Dictionary<string, object?>(facts);
            foreach (var c in change) { if (c.value == null) copy.Remove(c.name); else copy[c.name] = c.value; }
            return copy;
        }

        // Patient S2-A in the walkthrough (Part 7): a 19-year-old man, pain moved to the lower right 20 hours ago, vomited once, mild guarding, medium rebound,
        // temperature 37.9, WBC 13.6, neutrophils 82%, CRP 38.
        private static readonly Dictionary<string, object?> AirS2A = F(
            ("pain_rlq", true), ("vomiting", true), ("rebound_light", false), ("rebound_medium", true), ("rebound_strong", false),
            ("temperature_c", 37.9), ("wbc", 13.6), ("neutrophils_pct", 82.0), ("crp", 38.0));

        private static readonly Dictionary<string, object?> AasS2A = F(
            ("pain_moved", true), ("pain_rlq", true), ("rlq_tenderness", "everyone_else"), ("guarding_mild", true), ("guarding_moderate_or_severe", false),
            ("wbc", 13.6), ("neutrophils_pct", 82.0), ("crp_pain_under_24h", 38.0));

        // ---------------------------------------------------------------- the worked examples

        [Fact]
        public void Patient_S2_A_scores_AIR_7_unsure_item_by_item()
        {
            var r = Calc.Point("air", AirS2A);
            Assert.True(r.Complete);
            Assert.Equal(7, r.Total);
            Assert.Equal("unsure", r.Band);
            // score_examples.csv: air.vomiting 1, air.rlq 1, air.rebound 2, air.temp 0, air.wbc 1, air.neut 1, air.crp 1
            Assert.Equal(new[] { ("vomiting", 1), ("pain_rlq", 1), ("rebound_medium", 2), ("wbc", 1), ("neutrophils_pct", 1), ("crp", 1) }.OrderBy(x => x.Item1),
                r.Items.Select(i => (i.Item, i.Points)).OrderBy(x => x.Item));
        }

        [Fact]
        public void Patient_S2_A_scores_AAS_19_high_item_by_item()
        {
            var r = Calc.Point("aas", AasS2A);
            Assert.Equal(19, r.Total);
            Assert.Equal("high", r.Band);
            // score_examples.csv: aas.rlq 2, aas.moved 2, aas.tender 3, aas.rebound 2, aas.wbc 2, aas.neut 3, aas.crp 5
            Assert.Equal(new Dictionary<string, int> { ["pain_rlq"] = 2, ["pain_moved"] = 2, ["rlq_tenderness"] = 3, ["guarding_mild"] = 2, ["wbc"] = 2, ["neutrophils_pct"] = 3, ["crp_pain_under_24h"] = 5 },
                r.Items.ToDictionary(i => i.Item, i => i.Points));
        }

        [Fact]
        public void Every_scored_item_names_the_register_row_it_came_from()
        {
            var r = Calc.Point("aas", AasS2A);
            Assert.All(r.Items, i => { Assert.StartsWith("aas.", i.RowId); Assert.Matches("^SR-[0-9]{4}$", i.Ref); });
        }

        [Fact]
        public void Rabia_has_definite_acute_cholecystitis_grade_I()
        {
            var r = Calc.Tg18Cholecystitis(RabiaFull());
            Assert.Equal("definite", r.Diagnosis);
            Assert.Equal("I", r.Grade);
            Assert.Empty(r.Missing);
        }

        private static Dictionary<string, object?> RabiaFull() => F(
            ("murphy", true), ("ruq_lump_pain_tenderness", true), ("fever", true), ("raised_crp", true), ("raised_wbc", true), ("imaging_inflamed", true),
            ("vasopressor", false), ("reduced_consciousness", false), ("poor_oxygen", false), ("low_urine", false), ("creatinine_umol", 70.0), ("inr", 1.0), ("platelets", 250.0),
            ("wbc", 15.2), ("tender_lump", false), ("symptom_hours", 48.0), ("severe_local_inflammation", false));

        [Fact]
        public void The_duct_stone_risk_is_high_for_bilirubin_above_68_with_a_duct_wider_than_6_mm()
        {
            // VAR-S1-PB: "Bilirubin above 68 micromol/L plus a duct wider than 6 mm (ASGE 2019)"
            var r = Calc.Asge(F(("fever", false), ("jaundice", true), ("ruq_pain", true), ("stone_on_scan", false), ("bilirubin_umol", 80.0), ("duct_mm", 8.0), ("liver_tests_abnormal", true), ("age", 44.0)));
            Assert.Equal("high", r.Tier);
        }

        [Fact]
        public void Mrs_Rukhsana_creatinine_160_against_85_is_AKI_stage_1()
        {
            var r = Calc.Aki(F(("creatinine_umol", 160.0), ("baseline_umol", 85.0)));
            Assert.True(r.Complete);
            Assert.Equal(1, r.Stage);
            Assert.Equal(1.88, r.Ratio);
        }

        [Fact]
        public void The_STK_S1_D_cholangitis_case_is_definite_and_an_emergency()
        {
            var r = Calc.Tg18Cholangitis(F(("temperature_c", 39.2), ("chills", true), ("wbc", 13.4), ("crp", 120.0), ("bilirubin_umol", 70.0), ("liver_tests_x_uln", 3.0), ("duct_widened", true), ("cause_seen", false)));
            Assert.Equal("definite", r.Diagnosis);
            Assert.True(r.Emergency);
            var asge = Calc.Asge(F(("fever", true), ("jaundice", true), ("ruq_pain", true)));
            Assert.Equal("cholangitis", asge.Tier);
            Assert.True(asge.Emergency);
        }

        // EXP-CL-1 progress: every worked example the walkthrough gives for these calculators is reproduced
        [Fact]
        public void All_four_worked_examples_of_the_walkthrough_are_reproduced()
        {
            int ok = 0;
            if (Calc.Point("air", AirS2A).Total == 7) ok++;
            if (Calc.Point("aas", AasS2A).Total == 19) ok++;
            if (Calc.Tg18Cholecystitis(RabiaFull()).Grade == "I") ok++;
            if (Calc.Aki(F(("creatinine_umol", 160.0), ("baseline_umol", 85.0))).Stage == 1) ok++;
            _out.WriteLine($"EXP-CL-1 (calculators): {ok} of 4 worked examples reproduced (Hamza AIR 7 and AAS 19, Rabia TG18 grade I, Rukhsana AKI stage 1) plus the duct-stone high tier.");
            Assert.Equal(4, ok);
        }

        // ---------------------------------------------------------------- the edges of every range

        [Theory]
        [InlineData("wbc", 9.99, 0)]
        [InlineData("wbc", 10.0, 1)]
        [InlineData("wbc", 14.9, 1)]
        [InlineData("wbc", 14.95, 1)]      // between the table's 14.9 and 15.0: still the lower row
        [InlineData("wbc", 15.0, 2)]
        [InlineData("crp", 9.9, 0)]
        [InlineData("crp", 10.0, 1)]
        [InlineData("crp", 49.5, 1)]
        [InlineData("crp", 50.0, 2)]
        [InlineData("neutrophils_pct", 69.9, 0)]
        [InlineData("neutrophils_pct", 70.0, 1)]
        [InlineData("neutrophils_pct", 84.5, 1)]
        [InlineData("neutrophils_pct", 85.0, 2)]
        [InlineData("temperature_c", 38.49, 0)]
        [InlineData("temperature_c", 38.5, 1)]
        public void AIR_numbers_score_the_row_whose_range_holds_them(string item, double value, int points)
        {
            var r = Calc.Point("air", With(AirS2A, (item, value)));
            int got = r.Items.Where(i => i.Item == item).Sum(i => i.Points);
            Assert.Equal(points, got);
        }

        [Theory]
        [InlineData(10.0, 0)]     // "above 10.0": 10.0 itself does not score
        [InlineData(10.1, 2)]
        public void Alvarado_WBC_must_be_above_10(double wbc, int points)
        {
            var facts = AllAlvarado(false, wbc, 60.0, 36.8);
            Assert.Equal(points, Calc.Point("alvarado", facts).Total);
        }

        private static Dictionary<string, object?> AllAlvarado(bool yes, double wbc, double neut, double temp) => F(
            ("pain_moved", yes), ("loss_of_appetite", yes), ("nausea_or_vomiting", yes), ("rlq_tenderness", yes), ("rebound", yes), ("temperature_c", temp), ("wbc", wbc), ("neutrophils_pct", neut));

        [Fact]
        public void Alvarado_adds_to_10_and_reads_its_bands()
        {
            var all = Calc.Point("alvarado", AllAlvarado(true, 12.0, 80.0, 37.5));
            Assert.Equal(10, all.Total);
            Assert.Equal("very_probable", all.Band);
            var none = Calc.Point("alvarado", AllAlvarado(false, 8.0, 50.0, 36.8));
            Assert.Equal(0, none.Total);
            Assert.Equal("unlikely", none.Band);
            Assert.Equal("unsure", Calc.Point("alvarado", AllAlvarado(true, 8.0, 50.0, 36.8)).Band);     // 1+1+1+2+1 = 6 is "unsure"; 7 needs one more point
            Assert.Equal("probable", Calc.Point("alvarado", AllAlvarado(true, 8.0, 50.0, 37.5)).Band);   // 7
        }

        [Theory]
        [InlineData(0, "low")]
        [InlineData(4, "low")]
        [InlineData(5, "unsure")]
        [InlineData(8, "unsure")]
        [InlineData(9, "high")]
        public void AIR_bands_follow_the_table(int total, string band)
        {
            var sb = Tables.Points["air"].Bands.First(b => b.Covers(total));
            Assert.Equal(band, sb.Band);
        }

        [Fact]
        public void AAS_CRP_depends_on_how_long_the_pain_has_lasted()
        {
            var under = Calc.Point("aas", With(AasS2A, ("crp_pain_under_24h", 38.0)));
            Assert.Equal(5, under.Items.Single(i => i.Item == "crp_pain_under_24h").Points);
            var over = Calc.Point("aas", With(AasS2A, ("crp_pain_under_24h", null), ("crp_pain_24h_or_more", 38.0)));
            Assert.Equal(2, over.Items.Single(i => i.Item == "crp_pain_24h_or_more").Points);
            Assert.Equal(over.Total, under.Total - 3);
        }

        [Fact]
        public void AAS_RLQ_tenderness_for_a_woman_16_to_49_is_1_not_3()
        {
            var r = Calc.Point("aas", With(AasS2A, ("rlq_tenderness", "women16-49")));
            Assert.Equal(1, r.Items.Single(i => i.Item == "rlq_tenderness").Points);
            Assert.Equal(17, r.Total);
            Assert.Equal(0, Calc.Point("aas", With(AasS2A, ("rlq_tenderness", "none"))).Items.Count(i => i.Item == "rlq_tenderness"));
        }

        // ---------------------------------------------------------------- missing inputs (T05-3)

        [Fact]
        public void A_score_without_the_WBC_is_incomplete_names_the_missing_test_and_gives_no_band()
        {
            var r = Calc.Point("air", With(AirS2A, ("wbc", null)));
            Assert.False(r.Complete);
            Assert.Equal(new[] { "wbc" }, r.Missing);
            Assert.Null(r.Band);
            Assert.Equal(6, r.Total);        // the partial total is shown but never interpreted
        }

        [Fact]
        public void Every_missing_input_is_listed_once_in_the_order_the_score_asks_for_them()
        {
            var r = Calc.Point("air", With(AirS2A, ("crp", null), ("wbc", null), ("rebound_light", null), ("rebound_medium", null), ("rebound_strong", null)));
            Assert.Equal(new[] { "rebound_light", "wbc", "crp" }, r.Missing);
        }

        [Fact]
        public void False_is_an_answer_but_absent_is_not()
        {
            Assert.True(Calc.Point("alvarado", AllAlvarado(false, 8.0, 50.0, 36.8)).Complete);
            Assert.False(Calc.Point("alvarado", With(AllAlvarado(false, 8.0, 50.0, 36.8), ("rebound", null))).Complete);
        }

        [Fact]
        public void Wrong_kinds_of_answer_are_refused_with_the_item_named()
        {
            Assert.Contains("only one of", Assert.Throws<ArgumentException>(() => Calc.Point("air", With(AirS2A, ("rebound_light", true)))).Message);
            Assert.Contains("rlq_tenderness", Assert.Throws<ArgumentException>(() => Calc.Point("aas", With(AasS2A, ("rlq_tenderness", true)))).Message);
            Assert.Contains("must be one of", Assert.Throws<ArgumentException>(() => Calc.Point("aas", With(AasS2A, ("rlq_tenderness", "sometimes")))).Message);
            Assert.Contains("wbc", Assert.Throws<ArgumentException>(() => Calc.Point("air", With(AirS2A, ("wbc", "high")))).Message);
            Assert.Contains("unknown score", Assert.Throws<ArgumentException>(() => Calc.Point("curb65", F())).Message);
        }

        // ---------------------------------------------------------------- TG18

        [Fact]
        public void WBC_above_18_or_a_long_illness_makes_it_grade_II_and_organ_failure_grade_III()
        {
            Assert.Equal("II", Calc.Tg18Cholecystitis(With(RabiaFull(), ("wbc", 18.5))).Grade);
            Assert.Equal("I", Calc.Tg18Cholecystitis(With(RabiaFull(), ("wbc", 18.0))).Grade);          // above 18, not 18 itself
            Assert.Equal("II", Calc.Tg18Cholecystitis(With(RabiaFull(), ("symptom_hours", 73.0))).Grade);
            Assert.Equal("I", Calc.Tg18Cholecystitis(With(RabiaFull(), ("symptom_hours", 72.0))).Grade);
            Assert.Equal("II", Calc.Tg18Cholecystitis(With(RabiaFull(), ("tender_lump", true))).Grade);
            Assert.Equal("III", Calc.Tg18Cholecystitis(With(RabiaFull(), ("creatinine_umol", 178.0))).Grade);
            Assert.Equal("I", Calc.Tg18Cholecystitis(With(RabiaFull(), ("creatinine_umol", 177.0))).Grade);
            Assert.Equal("III", Calc.Tg18Cholecystitis(With(RabiaFull(), ("inr", 1.6))).Grade);
            Assert.Equal("III", Calc.Tg18Cholecystitis(With(RabiaFull(), ("platelets", 99.0))).Grade);
            Assert.Equal("I", Calc.Tg18Cholecystitis(With(RabiaFull(), ("platelets", 100.0))).Grade);
            Assert.Equal("III", Calc.Tg18Cholecystitis(With(RabiaFull(), ("vasopressor", true), ("wbc", 20.0))).Grade);   // the worst grade wins
        }

        [Fact]
        public void Without_the_scan_the_diagnosis_is_suspected_and_with_no_signs_there_is_none()
        {
            var suspected = Calc.Tg18Cholecystitis(With(RabiaFull(), ("imaging_inflamed", false)));
            Assert.Equal("suspected", suspected.Diagnosis);
            Assert.Equal("I", suspected.Grade);
            var none = Calc.Tg18Cholecystitis(F(("murphy", false), ("ruq_lump_pain_tenderness", false), ("fever", false), ("raised_crp", false), ("raised_wbc", false), ("imaging_inflamed", false)));
            Assert.Equal("none", none.Diagnosis);
            Assert.Equal("", none.Grade);
        }

        [Fact]
        public void A_grade_is_not_given_until_it_is_certain_and_the_missing_facts_are_named()
        {
            var r = Calc.Tg18Cholecystitis(With(RabiaFull(), ("inr", null)));
            Assert.Equal("definite", r.Diagnosis);
            Assert.Equal("", r.Grade);
            Assert.Equal(new[] { "inr" }, r.Missing);
            Assert.Equal("unknown", Calc.Tg18Cholecystitis(F(("murphy", false))).Diagnosis);
        }

        [Fact]
        public void Cholangitis_needs_inflammation_plus_blocked_bile_or_a_scan_finding()
        {
            var a = F(("temperature_c", 38.5), ("chills", false), ("wbc", 7.0), ("crp", 5.0));
            Assert.Equal("suspected", Calc.Tg18Cholangitis(With(a, ("bilirubin_umol", 40.0), ("liver_tests_x_uln", 1.0), ("duct_widened", false), ("cause_seen", false))).Diagnosis);
            Assert.Equal("suspected", Calc.Tg18Cholangitis(With(a, ("bilirubin_umol", 10.0), ("liver_tests_x_uln", 1.0), ("duct_widened", true), ("cause_seen", false))).Diagnosis);
            Assert.Equal("none", Calc.Tg18Cholangitis(With(a, ("bilirubin_umol", 10.0), ("liver_tests_x_uln", 1.0), ("duct_widened", false), ("cause_seen", false))).Diagnosis);
            var cold = F(("temperature_c", 37.0), ("chills", false), ("wbc", 7.0), ("crp", 5.0), ("bilirubin_umol", 80.0), ("duct_widened", true));
            Assert.Equal("none", Calc.Tg18Cholangitis(cold).Diagnosis);
            Assert.False(Calc.Tg18Cholangitis(cold).Emergency);
        }

        [Theory]
        [InlineData(38.0, false)]     // fever is above 38, not 38 itself
        [InlineData(38.1, true)]
        public void Cholangitis_fever_is_above_38(double temperature, bool inflamed)
        {
            var f = F(("temperature_c", temperature), ("chills", false), ("wbc", 7.0), ("crp", 5.0), ("bilirubin_umol", 10.0), ("liver_tests_x_uln", 1.0), ("duct_widened", true), ("cause_seen", false));
            Assert.Equal(inflamed ? "suspected" : "none", Calc.Tg18Cholangitis(f).Diagnosis);
        }

        [Theory]
        [InlineData(3.9, true)]
        [InlineData(4.0, false)]
        [InlineData(10.0, false)]
        [InlineData(10.1, true)]
        public void Cholangitis_WBC_below_4_or_above_10_counts_as_inflammation(double wbc, bool inflamed)
        {
            var f = F(("temperature_c", 37.0), ("chills", false), ("wbc", wbc), ("crp", 5.0), ("bilirubin_umol", 10.0), ("liver_tests_x_uln", 1.0), ("duct_widened", true), ("cause_seen", false));
            Assert.Equal(inflamed ? "suspected" : "none", Calc.Tg18Cholangitis(f).Diagnosis);
        }

        // ---------------------------------------------------------------- ASGE

        [Fact]
        public void A_stone_seen_on_the_scan_is_high_risk_whatever_the_bilirubin()
        {
            Assert.Equal("high", Calc.Asge(F(("fever", false), ("jaundice", false), ("ruq_pain", false), ("stone_on_scan", true))).Tier);
        }

        [Fact]
        public void Bilirubin_must_be_above_68_and_the_duct_above_6_mm_for_high_risk()
        {
            var baseFacts = F(("fever", false), ("jaundice", false), ("ruq_pain", false), ("stone_on_scan", false), ("liver_tests_abnormal", false), ("age", 40.0));
            Assert.Equal("intermediate", Calc.Asge(With(baseFacts, ("bilirubin_umol", 68.0), ("duct_mm", 8.0))).Tier);     // 68 itself is not above 68; a wide duct alone is intermediate
            Assert.Equal("high", Calc.Asge(With(baseFacts, ("bilirubin_umol", 69.0), ("duct_mm", 6.1))).Tier);
            Assert.Equal("low", Calc.Asge(With(baseFacts, ("bilirubin_umol", 69.0), ("duct_mm", 6.0))).Tier);                 // 6.0 is not wider than 6
        }

        [Fact]
        public void Liver_tests_or_age_over_55_make_it_intermediate_and_otherwise_low()
        {
            var baseFacts = F(("fever", false), ("jaundice", false), ("ruq_pain", false), ("stone_on_scan", false), ("bilirubin_umol", 20.0), ("duct_mm", 4.0), ("liver_tests_abnormal", false), ("age", 55.0));
            Assert.Equal("low", Calc.Asge(baseFacts).Tier);
            Assert.Equal("intermediate", Calc.Asge(With(baseFacts, ("age", 56.0))).Tier);
            Assert.Equal("intermediate", Calc.Asge(With(baseFacts, ("liver_tests_abnormal", true))).Tier);
        }

        [Fact]
        public void An_incomplete_duct_stone_assessment_gives_no_tier_and_names_what_is_missing()
        {
            var r = Calc.Asge(F(("fever", false), ("jaundice", false), ("ruq_pain", false), ("stone_on_scan", false)));
            Assert.Equal("", r.Tier);
            Assert.Contains("bilirubin_umol", r.Missing);
            Assert.Equal(new[] { "fever", "jaundice", "ruq_pain" }, Calc.Asge(F()).Missing);
        }

        // ---------------------------------------------------------------- STOP-Bang, AKI, ASA and the plain formulas

        [Theory]
        [InlineData(0, "low")]
        [InlineData(1, "low")]
        [InlineData(2, "low")]
        [InlineData(3, "intermediate")]
        [InlineData(4, "intermediate")]
        [InlineData(5, "high")]
        [InlineData(8, "high")]
        public void STOP_Bang_counts_yes_answers_and_reads_its_bands(int yes, string band)
        {
            var facts = Tables.StopBangItems.Select((item, i) => (item, (object?)(i < yes))).ToArray();
            var r = Calc.StopBang(F(facts));
            Assert.True(r.Complete);
            Assert.Equal(yes, r.Total);
            Assert.Equal(band, r.Band);
        }

        [Fact]
        public void STOP_Bang_with_a_question_unanswered_has_no_band()
        {
            var r = Calc.StopBang(F(("snoring", true)));
            Assert.False(r.Complete);
            Assert.Null(r.Band);
            Assert.Equal(7, r.Missing.Count);
        }

        [Theory]
        [InlineData(100.0, 85.0, 0)]
        [InlineData(127.5, 85.0, 1)]       // exactly 1.5 times
        [InlineData(161.0, 85.0, 1)]       // 1.89 times: still stage 1 (1.5 to 1.9)
        [InlineData(170.0, 85.0, 2)]       // exactly 2 times
        [InlineData(254.0, 85.0, 2)]
        [InlineData(255.0, 85.0, 3)]       // exactly 3 times
        [InlineData(354.0, 300.0, 3)]      // 354 or more is stage 3 whatever the ratio
        [InlineData(353.0, 300.0, 0)]       // 1.18 times and under 354: no injury by these criteria
        public void AKI_stage_follows_the_ratio_to_the_usual_creatinine(double now, double usual, int stage)
        {
            Assert.Equal(stage, Calc.Aki(F(("creatinine_umol", now), ("baseline_umol", usual))).Stage);
        }

        [Theory]
        [InlineData(100.0, 73.5, 1)]       // up 26.5 in 48 hours: stage 1 although only 1.36 times the usual
        [InlineData(100.0, 73.6, 0)]       // up 26.4: not enough
        [InlineData(100.0, 100.0, 0)]
        public void AKI_stage_1_also_comes_from_a_rise_of_26_5_or_more_within_48_hours(double now, double fortyEightHoursAgo, int stage)
        {
            Assert.Equal(stage, Calc.Aki(F(("creatinine_umol", now), ("baseline_umol", 85.0), ("creatinine_48h_ago_umol", fortyEightHoursAgo))).Stage);
        }

        [Fact]
        public void The_48_hour_rise_never_lowers_a_stage_the_ratio_already_gave()
        {
            Assert.Equal(2, Calc.Aki(F(("creatinine_umol", 170.0), ("baseline_umol", 85.0), ("creatinine_48h_ago_umol", 169.0))).Stage);
        }

        [Fact]
        public void AKI_without_a_usual_value_is_incomplete_and_a_zero_baseline_is_refused()
        {
            var r = Calc.Aki(F(("creatinine_umol", 160.0)));
            Assert.False(r.Complete);
            Assert.Equal(new[] { "baseline_umol" }, r.Missing);
            Assert.Throws<ArgumentException>(() => Calc.Aki(F(("creatinine_umol", 160.0), ("baseline_umol", 0.0))));
        }

        [Fact]
        public void The_ASA_check_reports_how_far_off_the_proposal_is_and_whether_the_emergency_letter_matches()
        {
            var exact = ScoreCalculator.AsaCheck(1, false, 1, false);
            Assert.True(exact.Exact); Assert.Equal(0, exact.Difference); Assert.True(exact.EmergencyMatches);
            var off = ScoreCalculator.AsaCheck(3, true, 2, false);
            Assert.False(off.Exact); Assert.Equal(1, off.Difference); Assert.False(off.EmergencyMatches);
            Assert.Throws<ArgumentException>(() => ScoreCalculator.AsaCheck(7, false, 1, false));
        }

        [Fact]
        public void The_plain_formulas_give_the_standard_numbers()
        {
            Assert.Equal(93.3, ScoreCalculator.Map(120, 80));
            Assert.Equal(1.0, ScoreCalculator.ShockIndex(100, 100));
            Assert.Equal(1.16, ScoreCalculator.ShockIndex(116, 100));
            Assert.Equal(2.6, ScoreCalculator.RRatio(120, 40, 150, 130));
            Assert.Throws<ArgumentException>(() => ScoreCalculator.ShockIndex(100, 0));
        }

        // ---------------------------------------------------------------- the tables themselves

        [Fact]
        public void The_real_package_loads_the_three_point_scores_and_their_register_ids()
        {
            var package = TestCase.Package();
            Assert.Equal(new[] { "aas", "air", "alvarado" }, package.Scores.Points.Keys.OrderBy(k => k));
            Assert.All(package.Scores.Points.Values.SelectMany(p => p.Items), r => Assert.Matches("^SR-[0-9]{4}$", r.Ref));
            Assert.Equal(12, package.Scores.Points["air"].Max);
        }

        private static string Doc(Func<string, string> change) => change(File.ReadAllText(Path.Combine(TestCase.Repo, "content", "scores", "scores.json")));

        private static string Edit(Action<System.Text.Json.Nodes.JsonNode> change)
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(TestCase.Repo, "content", "scores", "scores.json")))!;
            change(node);
            return node.ToJsonString();
        }

        [Fact]
        public void A_table_with_an_unknown_field_is_refused_with_its_place()
        {
            var e = Assert.Throws<ContentException>(() => ScoreTables.Parse(Doc(j => j.Replace("\"stage3_ratio\"", "\"colour\": 1,\n  \"stage3_ratio\"")), "scores"));
            Assert.Contains("unknown field", e.Message);
        }

        [Fact]
        public void A_required_item_with_no_row_is_refused()
        {
            var e = Assert.Throws<ContentException>(() => ScoreTables.Parse(Edit(n => n["point_scores"]![0]!["required"]![0]![0] = "pain_nowhere"), "scores"));
            Assert.Contains("has no row", e.Message);
        }

        [Fact]
        public void Bands_with_a_gap_are_refused()
        {
            var e = Assert.Throws<ContentException>(() => ScoreTables.Parse(Edit(n => n["point_scores"]![0]!["bands"]![1]!["min"] = 6), "scores"));
            Assert.Contains("without a gap or overlap", e.Message);
        }

        [Fact]
        public void A_missing_number_is_refused_with_its_place()
        {
            var e = Assert.Throws<ContentException>(() => ScoreTables.Parse(Doc(j => j.Replace("\"stage3_absolute_umol\": 354,", "")), "scores"));
            Assert.Contains("stage3_absolute_umol", e.Message);
        }
    }
}
