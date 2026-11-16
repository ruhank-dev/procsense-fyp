using System.Collections.Generic;
using NUnit.Framework;
using ProcSense.Core.Flow;
using ProcSense.VR.Flow;
using ProcSense.VR.UI.Theme;

namespace ProcSense.Tests.EditMode
{
    /// <summary>P3-RES-04, P3-RES-08, P3-TRN-07: the words of the results card from the engine's summary (criticals first, no goal list in the examination), the verification status read from the
    /// service's answer, and the comparison with the previous attempt only for the same case and seed.</summary>
    public class ResultsCardTextTests
    {
        private static ResultSummary Summary(string mode = "practice", ResultGrade grade = null, int criticals = 0, bool abandoned = false, ulong seed = 7, double minutes = 12.5, params string[] missed) =>
            new ResultSummary("VAR-S1-E", seed, mode, "SUCCESS", "THEATRE", true, "accepted", abandoned, grade, 40, 5, criticals, new List<string> { "identity" }, new List<string>(missed), 1, mode == "assessment" ? 1 : 0,
                              minutes, new List<StageTime>(), new List<string> { "Confirm the identity first.", "Check the consent." });

        [Test]
        public void The_verdict_follows_the_grade_then_the_result()
        {
            Assert.AreEqual("PASSED", ResultsCardText.Verdict(Summary(grade: new ResultGrade(82, "B", true, false, 0))));
            Assert.AreEqual("NOT PASSED", ResultsCardText.Verdict(Summary(grade: new ResultGrade(40, "F", false, true, 0))));
            Assert.AreEqual("SUCCESS", ResultsCardText.Verdict(Summary()));
            Assert.AreEqual("NOT COMPLETED", ResultsCardText.Verdict(Summary(abandoned: true)));
            Assert.AreEqual(UiTheme.Current.Done, ResultsCardText.VerdictColour(Summary(grade: new ResultGrade(82, "B", true, false, 0))));
            Assert.AreEqual(UiTheme.Current.Danger, ResultsCardText.VerdictColour(Summary(grade: new ResultGrade(40, "F", false, true, 0))));
        }

        [Test]
        public void Critical_items_come_first_and_the_examination_shows_no_goal_list()
        {
            var practice = ResultsCardText.Sections(Summary(criticals: 2, missed: new[] { "consent" }));
            Assert.IsTrue(practice[0].Critical);
            Assert.AreEqual("2 critical items", practice[0].Heading);
            Assert.AreEqual("Missed", practice[1].Heading);
            Assert.AreEqual("Recommendations", practice[2].Heading);
            CollectionAssert.Contains(practice[3].Lines, "Time 12.5 min");
            var exam = ResultsCardText.Sections(Summary(mode: "assessment", missed: new[] { "consent" }));
            Assert.IsFalse(exam.Exists(x => x.Heading == "Missed"), "no goal names in the examination (P3-EXM-09)");
            CollectionAssert.Contains(exam[exam.Count - 1].Lines, "Hints 1 (charged)");
            Assert.AreEqual("Case VAR-S1-E · seed 7", ResultsCardText.CaseLine(Summary()));
        }

        [Test]
        public void Only_this_sessions_verified_row_gives_the_badge()
        {
            const string json = "{\"total\":2,\"items\":[{\"session_id\":\"a\",\"status\":\"pending\"},{\"session_id\":\"b\",\"status\":\"verified\"}]}";
            Assert.AreEqual("verified", ServerVerification.StatusOf(json, "b"));
            Assert.AreEqual("pending", ServerVerification.StatusOf(json, "a"));
            Assert.AreEqual("", ServerVerification.StatusOf(json, "c"), "not uploaded yet");
            Assert.AreEqual("", ServerVerification.StatusOf("not json", "b"));
            Assert.AreEqual("", ServerVerification.StatusOf(json, ""));
        }

        [Test]
        public void A_retry_is_compared_with_the_previous_attempt_and_a_new_scenario_is_not()
        {
            var first = Summary(minutes: 20, missed: new[] { "consent", "allergy" });
            var retry = Summary(grade: new ResultGrade(90, "A", true, false, 0), minutes: 14);
            Assert.AreEqual("Previous attempt: SUCCESS, 20 min, 2 missed steps\nThis attempt: PASSED, 14 min, 0 missed steps", ResultsCardText.Comparison(first, retry));
            Assert.AreEqual("", ResultsCardText.Comparison(first, Summary(seed: 8)), "another seed is another scenario");
            Assert.AreEqual("", ResultsCardText.Comparison(null, retry));
        }
    }
}
