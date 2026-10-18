using System.Linq;
using ProcSense.Core.Content;
using ProcSense.Core.Events;
using ProcSense.Core.Replay;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>The events a generated case plans (features A5 and A8, DT-224): practice considers only the planned ids, assessment ignores the plan, the plan is in the session header and a replay plays the same events.</summary>
    public class PlannedEventsTests
    {
        private static readonly ContentPackage Content = TestCase.Package();

        private static Session Start(string planned, SessionMode mode = SessionMode.Practice) =>
            new Session(Content.Cases["VAR-S1-A"], 42, mode, Subsystems.Default(Content), new SessionOptions(PlannedEvents: planned));

        [Fact]
        public void With_no_plan_every_approved_event_stays_in_the_library_as_before()
        {
            Assert.Equal(Content.Events.Events.Count, Start("").Director!.Library.Count);
        }

        [Fact]
        public void A_plan_keeps_only_the_planned_events_in_practice_and_an_unknown_id_is_never_met()
        {
            var d = Start("E-LOW-BP,E-HIGH-HR").Director!;
            Assert.Equal(new[] { "E-LOW-BP", "E-HIGH-HR" }.OrderBy(x => x), d.Library.Select(e => e.Id).OrderBy(x => x));
            Assert.Empty(Start("E-NOT-AN-EVENT").Director!.Library);
        }

        [Fact]
        public void An_event_that_is_not_planned_cannot_fire_and_a_planned_one_can()
        {
            var s = Start("E-LOW-OXYGEN");
            s.Patient.Clinical.SetFlag("allergy.reaction_eligible");                 // the reaction would fire in an unplanned session (see EventDirectorTests)
            s.Tick(900);
            Assert.False(s.Director!.HasFired("E-HYPERSENSITIVITY"));
            Assert.DoesNotContain(s.Director.History, h => h.Id == "E-HYPERSENSITIVITY");
        }

        [Fact]
        public void Assessment_ignores_the_plan_because_its_schedule_is_fixed()
        {
            var tables = Content.Events.For(SessionMode.Assessment).Select(e => e.Id).OrderBy(x => x);
            var s = new Session(Content.Cases["VAR-S1-A"], 42, SessionMode.Assessment, Subsystems.Default(Content), new SessionOptions(PlannedEvents: "E-LOW-BP"));
            Assert.Equal(tables, s.Director!.Library.Select(e => e.Id).OrderBy(x => x));
        }

        [Fact]
        public void The_plan_is_in_the_header_and_a_replay_reads_it_back()
        {
            var s = Start("E-LOW-BP");
            string text = SessionFile.Write(s);
            Assert.Contains("\"planned_events\":\"E-LOW-BP\"", text.Split('\n')[0]);
            Assert.DoesNotContain("planned_events", SessionFile.Write(Start("")).Split('\n')[0]);       // an older log reads the same
            var replayed = SessionFile.Replay(text, Content, () => Subsystems.Default(Content)).Session;
            Assert.Equal("E-LOW-BP", replayed.PlannedEvents);
        }

        [Fact]
        public void A_package_names_its_planned_events_from_its_provenance()
        {
            string pkg = System.IO.File.ReadAllText(System.IO.Path.Combine(TestCase.Repo, "tests", "golden_generated", "generated_VAR-S1-A.package.json"));
            Assert.Equal("", new ScenarioPackageReader(pkg).PlannedEvents);                           // the packages made before the plan had events plan none: every event may fire
            string withPlan = pkg.Replace("\"plan\": {", "\"plan\": {\"events\": [{\"event_id\": \"E-LOW-BP\", \"rationale\": \"x\"}, {\"event_id\": \"E-HIGH-HR\", \"rationale\": \"y\"}], ");
            Assert.Equal("E-LOW-BP,E-HIGH-HR", new ScenarioPackageReader(withPlan).PlannedEvents);
        }
    }
}
