using System.IO;
using System.Linq;
using ProcSense.Core.Content;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>Any of the 53 case files can be the patient of a session (DT-070, plan test T-VB4 idle stability).</summary>
    public class CaseFileCardTests
    {
        private static readonly string[] Ids = Directory.GetFiles(Path.Combine(TestCase.ContentRoot, "case_files"), "*.json").Select(Path.GetFileNameWithoutExtension).OrderBy(x => x, System.StringComparer.Ordinal).ToArray()!;
        private static readonly ContentPackage All = ContentLoader.Load(new CompositeContentReader(new FileContentReader(TestCase.ContentRoot), new FileContentReader(Path.Combine(TestCase.Repo, "tests", "fixtures"))), Ids, Ids);

        [Fact]
        public void Every_case_file_gives_a_card_and_a_session_starts_from_it()
        {
            Assert.Equal(53, Ids.Length);
            foreach (var id in Ids)
            {
                Assert.True(All.Cases.ContainsKey(id), id);
                var s = new Session(All.Cases[id], 42, SessionMode.Practice, Subsystems.Default(All));
                Assert.Equal(All.CaseFiles[id].Identity.FullName, s.Case.Identity.FullName);
            }
        }

        [Fact]
        public void The_responsible_adult_answer_comes_from_the_file_and_is_not_stated_only_when_the_file_says_nothing()
        {
            // DT-178: the 16 P0 files that stated none now do; a planned patient is taken home, an emergency patient is admitted
            var planned = All.Cases["VAR-S1-B"].ResponsibleAdult;
            Assert.True(planned.TakesHome && planned.StaysOvernight);
            Assert.Equal("quote", planned.Wording);
            var admitted = All.Cases["VAR-S2-PD"].ResponsibleAdult;
            Assert.False(admitted.TakesHome);
            Assert.All(new[] { "VAR-S1-B", "VAR-S1-C", "VAR-S1-D", "VAR-S1-E", "VAR-S1-G", "VAR-S1-H", "VAR-S1-J", "VAR-S1-PB", "STK-S1-D", "VAR-S2-B", "VAR-S2-D", "VAR-S2-F", "VAR-S2-G", "VAR-S2-PC", "VAR-S2-PD", "STK-S2-B" },
                id => Assert.NotEqual("Not stated in this case.", All.Cases[id].ResponsibleAdult.Text));
            Assert.Equal("Not stated in this case.", All.Cases["SC-1"].ResponsibleAdult.Text);   // a file that says nothing still answers "not stated"
        }

        [Fact]
        public void A_card_made_from_the_file_agrees_with_the_hand_made_card_where_both_exist()
        {
            var hand = TestCase.Package().Cases["VAR-S1-A"];
            var made = CaseFileCard.From(All.CaseFiles["VAR-S1-A"], All.HistoryTopics.Ids);
            Assert.Equal(hand.Identity, made.Identity);
            Assert.Equal(hand.Wristband, made.Wristband);
            Assert.Equal(hand.Notes, made.Notes);
            Assert.Equal(hand.AllergyAnswer, made.AllergyAnswer);
            Assert.Equal(hand.ReasonAnswer, made.ReasonAnswer);
            Assert.Equal(hand.SpokenIdentityAnswer, made.SpokenIdentityAnswer);
            Assert.Equal(hand.Timing, made.Timing);
            Assert.Equal(hand.BaselineVitals, made.BaselineVitals);
            Assert.Equal(hand.History.Select(h => (h.Topic, h.Answer, h.Volunteered)), made.History.Select(h => (h.Topic, h.Answer, h.Volunteered)));
        }

        [Fact]
        public void The_five_cases_with_allergies_carry_them_into_the_session()
        {
            var withAllergies = Ids.Where(id => All.CaseFiles[id].Allergies.Count > 0).ToList();
            Assert.Equal(new[] { "SC-1", "SC-15", "STK-S2-B", "VAR-S1-I", "VAR-S2-K" }, withAllergies);
            foreach (var id in withAllergies) Assert.Equal(All.CaseFiles[id].Allergies, All.Cases[id].Allergies);
        }

        [Fact]
        public void A_five_minute_idle_session_of_every_case_keeps_the_vital_signs_inside_the_models_guard_bounds()
        {
            foreach (var id in Ids)
            {
                var s = new Session(All.Cases[id], 42, SessionMode.Practice, Subsystems.Default(All));
                s.Tick(300);
                var v = s.Patient.Physiological.Vitals;
                Assert.InRange(v.HeartRate, 30, 200);
                Assert.InRange(v.SystolicBp, 50, 220);
                Assert.InRange(v.RespiratoryRate, 4, 45);
                Assert.InRange(v.Spo2, 60, 100);
                Assert.InRange(v.TemperatureC, 34, 41);
            }
        }
    }
}
