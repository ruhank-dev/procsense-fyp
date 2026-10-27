using System.Linq;
using NUnit.Framework;
using ProcSense.Core.State;
using ProcSense.VR.UI.Results;

namespace ProcSense.Tests.EditMode
{
    /// <summary>The results viewer's words and the phone's read-back choices (p0-vr-interaction-ui-s05): flags are Core's, only shortened (T-UI4); the trend compares numbers; units are never touched.</summary>
    public class ResultsTextTests
    {
        [Test]
        public void Flags_are_shortened_never_worked_out()
        {
            Assert.AreEqual(("H", "L", "HH", ""), (ResultsText.Mark("high"), ResultsText.Mark("low"), ResultsText.Mark("critical_high"), ResultsText.Mark("normal")));
        }

        [Test]
        public void The_trend_compares_the_value_with_the_previous_one_as_numbers()
        {
            Assert.AreEqual(ResultsText.Up, ResultsText.TrendOf("72", "14"));
            Assert.AreEqual(ResultsText.Down, ResultsText.TrendOf("4.6", "6.40"));
            Assert.AreEqual(ResultsText.Same, ResultsText.TrendOf("4.6", "4.60"));
            Assert.AreEqual("", ResultsText.TrendOf("Tall peaked T waves", "4.6"));
        }

        [Test]
        public void A_line_shows_the_unit_and_range_exactly_as_content_wrote_them()
        {
            var line = ResultsText.Line(new ResultLine { Analyte = "Bilirubin", Value = "72", Unit = "umol/L", Range = "3-21", Mark = "H", Previous = "14", PreviousWhen = "pre-assessment 6 weeks ago", Trend = ResultsText.Up, At = "09:40" });
            StringAssert.Contains("72 umol/L  H   (3-21)", line);
            StringAssert.Contains("previously 14 (pre-assessment 6 weeks ago) " + ResultsText.Up, line);
        }

        [Test]
        public void Modes_follow_the_kind_of_test()
        {
            Assert.AreEqual(ResultsText.Image, ResultsText.ModeOf("ultrasound", "imaging"));
            Assert.AreEqual(ResultsText.Strip, ResultsText.ModeOf("ecg", "bedside"));
            Assert.AreEqual(ResultsText.Table, ResultsText.ModeOf("u_and_e", "blood"));
        }

        [Test]
        public void The_read_back_offers_the_values_as_phoned_and_two_mishearings_in_the_same_words()
        {
            var call = new CriticalCall { Id = 1 };
            call.Values.Add(new CriticalValue("potassium", 6.6, "Potassium 6.6 mmol/L"));
            var choices = PhoneView.ChoicesFor(call);
            Assert.AreEqual(3, choices.Count);
            Assert.AreEqual(1, choices.Count(c => c.Exact));
            Assert.AreEqual("potassium=6.6", choices.Single(c => c.Exact).Values);
            Assert.AreEqual(1, choices.ToList().IndexOf(choices.Single(c => c.Exact)), "the exact one's place follows the call");
            CollectionAssert.AreEquivalent(new[] { "Potassium 6.6 mmol/L", "Potassium 6.9 mmol/L", "Potassium 5.6 mmol/L" }, choices.Select(c => c.Label).ToList());
        }
    }
}
