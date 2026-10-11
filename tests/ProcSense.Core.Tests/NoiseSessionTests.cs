using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Replay;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>Noise on the live vital signs as a session option (DT-083): off by default, seeded, bounded and replayed.</summary>
    public class NoiseSessionTests
    {
        private static readonly Content.ContentPackage Content = TestCase.Package();

        private static Session Start(bool noise, ulong seed = 42) => new Session(Content.Cases["VAR-S1-A"], seed, SessionMode.Practice, Subsystems.Default(Content), new SessionOptions(TrainingLevel.Beginner, "", noise));

        private static List<double> Heart(Session s, int readings)
        {
            var list = new List<double>();
            for (int i = 0; i < readings; i++) { s.Tick(30); list.Add(s.Patient.Physiological.Vitals.HeartRate); }
            return list;
        }

        [Fact]
        public void By_default_a_quiet_patient_shows_exactly_the_cases_numbers()
        {
            Assert.All(Heart(Start(false), 40), h => Assert.Equal(Content.Cases["VAR-S1-A"].BaselineVitals.HeartRate, h));
        }

        [Fact]
        public void With_noise_the_heart_rate_flickers_inside_its_cap_and_is_the_same_for_the_same_seed()
        {
            var baseline = Content.Cases["VAR-S1-A"].BaselineVitals.HeartRate;
            var a = Heart(Start(true), 80);
            Assert.True(a.Distinct().Count() > 1, "the heart rate never moved");
            var cap = Content.Physio.Noise.Single(n => n.Variable == "HR").Cap;
            Assert.All(a, h => Assert.InRange(h, baseline - cap - 1, baseline + cap + 1));                     // rounded readings
            Assert.Equal(a, Heart(Start(true), 80));
            Assert.NotEqual(a, Heart(Start(true, 7), 80));
        }

        [Fact]
        public void The_noise_option_is_saved_in_the_log_header_and_replays_to_the_same_hash()
        {
            var s = Start(true);
            s.Apply(new Actions.SimAction("ask_identity", "patient"));
            s.Tick(120);
            var text = SessionFile.Write(s);
            Assert.Contains("\"noise\":true", text);
            Assert.True(SessionFile.Replay(text, Content, () => Subsystems.Default(Content)).Match);
            Assert.DoesNotContain("noise", SessionFile.Write(Start(false)));
        }
    }
}
