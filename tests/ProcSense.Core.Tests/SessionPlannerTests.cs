using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProcSense.Core.Content;
using ProcSense.Core.Flow;
using ProcSense.Core.Replay;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>P3-SES-06: a session plan comes only from a request; refusals have codes; the request is written to the log header and a replay rebuilds it. P3-SCN-09: a retried plan replays a golden script to the same hash.</summary>
    public class SessionPlannerTests
    {
        private static readonly CaseCatalogue Catalogue = CaseCatalogue.Parse(File.ReadAllText(Path.Combine(TestCase.ContentRoot, "ui", "case_catalogue.json")));
        private static ulong Draw() => 99;

        [Fact]
        public void A_request_is_planned_with_its_seed_and_header()
        {
            var plan = SessionPlanner.Plan(Catalogue, new SessionRequest(SessionMode.Practice, CaseCatalogue.Cholecystectomy, TrainingLevel.Beginner, Seed: 5), null, Draw).Plan!;
            Assert.Equal(5UL, plan.Seed);
            Assert.Equal("cholecystectomy", plan.Procedure);
            Assert.Equal("5", plan.Header()["seed"]);
            Assert.Equal(plan.CaseId, plan.Header()["case"]);
            Assert.Equal(TrainingLevel.Beginner, plan.Options.Level);
        }

        [Fact]
        public void A_missing_seed_is_drawn_and_recorded()
        {
            var plan = SessionPlanner.Plan(Catalogue, new SessionRequest(SessionMode.Practice, CaseCatalogue.Appendicectomy, TrainingLevel.Advanced), null, Draw).Plan!;
            Assert.Equal(99UL, plan.Seed);
        }

        [Fact]
        public void Refusals_have_codes()
        {
            Assert.Equal(SessionPlanner.UnknownProcedure, SessionPlanner.Plan(Catalogue, new SessionRequest(SessionMode.Practice, "x", TrainingLevel.Beginner), null, Draw).Refusal);
            Assert.Equal(SessionPlanner.GeneratedNotInAssessment, SessionPlanner.Plan(Catalogue, new SessionRequest(SessionMode.Assessment, CaseCatalogue.Appendicectomy, TrainingLevel.Advanced, CaseSource.Generated), null, Draw).Refusal);
            Assert.Equal(SessionPlanner.CaseNotAllowed, SessionPlanner.Plan(Catalogue, new SessionRequest(SessionMode.Practice, CaseCatalogue.Cholecystectomy, TrainingLevel.Beginner, CaseId: "SC-1"), null, Draw).Refusal);   // SC-1 is an appendicectomy case
        }

        [Fact]
        public void The_examination_has_no_case_until_one_is_approved_for_assessment_and_plays_at_the_advanced_level()
        {
            var outcome = SessionPlanner.Plan(Catalogue, new SessionRequest(SessionMode.Assessment, CaseCatalogue.Cholecystectomy, TrainingLevel.Beginner), null, Draw);
            if (outcome.Plan == null) Assert.Equal(SessionPlanner.NoCaseAvailable, outcome.Refusal);
            else Assert.Equal(TrainingLevel.Advanced, outcome.Plan.Options.Level);
        }

        [Fact]
        public void Retry_reproduces_the_plan()
        {
            var first = SessionPlanner.Plan(Catalogue, new SessionRequest(SessionMode.Practice, CaseCatalogue.Cholecystectomy, TrainingLevel.Advanced, Seed: 7), null, Draw).Plan!;
            var again = SessionPlanner.Plan(Catalogue, SessionPlanner.Retry(first, TrainingLevel.Advanced), null, Draw).Plan!;
            Assert.Equal(first, again);
        }

        private static ContentPackage Content(string id) => ContentLoader.Load(new FileContentReader(TestCase.ContentRoot), new[] { id }, new[] { id });

        private static Session Play(SessionPlan plan, ContentPackage content)
        {
            var s = new Session(content.Cases[plan.CaseId], plan.Seed, plan.Mode, Subsystems.Default(content), plan.Options);
            SessionFile.Play(s, ActionScript.Parse(File.ReadAllText(Path.Combine(TestCase.Repo, "tests", "golden", plan.CaseId + "_correct.actions.json"))));
            return s;
        }

        [Fact]
        public void The_request_is_written_to_the_log_header_and_a_replay_rebuilds_the_same_request_and_plan()
        {
            var request = new SessionRequest(SessionMode.Practice, CaseCatalogue.Cholecystectomy, TrainingLevel.Advanced, Seed: 42, CaseId: "VAR-S1-A");
            var plan = SessionPlanner.Plan(Catalogue, request, null, Draw).Plan!;
            var content = Content(plan.CaseId);
            var s = Play(plan, content);
            string text = SessionFile.Write(s);
            var header = SessionFile.HeaderOf(text);
            Assert.Equal(plan.Header()["procedure"], header.Procedure);
            Assert.Equal("", header.Source);                                                                  // authored is the default and is left out
            Assert.DoesNotContain("hint_limit", text.Split('\n')[0]);                                         // the default limit is left out too
            Assert.Equal(request, SessionFile.RequestOf(text));
            Assert.Equal(plan, SessionPlanner.Plan(Catalogue, SessionFile.RequestOf(text)!, null, Draw).Plan);

            var replay = SessionFile.Replay(text, content, () => Subsystems.Default(content));
            Assert.True(replay.Match);
            Assert.Equal(text, SessionFile.Write(replay.Session));                                            // the replay keeps the request in its own header
        }

        [Fact]
        public void A_session_not_started_from_a_plan_keeps_the_header_it_always_had()
        {
            var content = Content("VAR-S1-A");
            var s = new Session(content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(content));
            SessionFile.Play(s, ActionScript.Parse(File.ReadAllText(Path.Combine(TestCase.Repo, "tests", "golden", "VAR-S1-A_correct.actions.json"))));
            string text = SessionFile.Write(s);
            var head = text.Split('\n')[0];
            Assert.Null(SessionFile.RequestOf(text));
            Assert.Equal("{\"header\":{\"case\":\"VAR-S1-A\",\"seed\":\"42\",\"mode\":\"practice\",\"log_hash\":\"" + s.Log.Hash() + "\"}}", head);

            var plan = SessionPlanner.Plan(Catalogue, new SessionRequest(SessionMode.Practice, CaseCatalogue.Cholecystectomy, TrainingLevel.Beginner, Seed: 42, CaseId: "VAR-S1-A"), null, Draw).Plan!;
            Assert.Equal(s.Log.Hash(), Play(plan, content).Log.Hash());                                       // the request in the header does not change the log hash
        }

        [Fact]
        public void A_generated_source_is_written_and_read_back()
        {
            var plan = SessionPlanner.Plan(Catalogue, new SessionRequest(SessionMode.Practice, CaseCatalogue.Cholecystectomy, TrainingLevel.Beginner, CaseSource.Generated, 3, "VAR-S1-A"), null, Draw).Plan!;
            var content = Content(plan.CaseId);
            string text = SessionFile.Write(new Session(content.Cases[plan.CaseId], plan.Seed, plan.Mode, Subsystems.Default(content), plan.Options));
            Assert.Equal("generated", SessionFile.HeaderOf(text).Source);
            Assert.Equal(CaseSource.Generated, SessionFile.RequestOf(text)!.Source);
        }

        [Fact]
        public void A_retried_plan_replays_the_golden_script_to_the_same_hash()
        {
            var first = SessionPlanner.Plan(Catalogue, new SessionRequest(SessionMode.Practice, CaseCatalogue.Cholecystectomy, TrainingLevel.Beginner, Seed: 42, CaseId: "VAR-S1-A"), null, Draw).Plan!;
            var content = Content(first.CaseId);
            var original = Play(first, content);
            var retried = SessionPlanner.Plan(Catalogue, SessionPlanner.Retry(first, first.Options.Level), null, () => 12345).Plan!;   // a different draw must not matter: retry keeps the seed
            Assert.Equal(first, retried);
            var again = Play(retried, content);
            Assert.Equal(original.Log.Hash(), again.Log.Hash());
            Assert.Equal(original.Observable.Hash(), again.Observable.Hash());
            string expected = File.ReadAllText(Path.Combine(TestCase.Repo, "tests", "golden", "VAR-S1-A_correct.expected.json"));
            Assert.Contains("\"log_hash\": \"" + again.Log.Hash() + "\"", expected);                         // the same hash the golden file records

            var drawn = SessionPlanner.Plan(Catalogue, new SessionRequest(SessionMode.Practice, CaseCatalogue.Appendicectomy, TrainingLevel.Advanced), null, () => 77).Plan!;
            Assert.Equal(drawn with { Relaxed = null }, SessionPlanner.Plan(Catalogue, SessionPlanner.Retry(drawn, drawn.Options.Level), null, () => 1).Plan);
        }

        /// <summary>P3-MNU-05: the intermediate level reaches the session header and replays; it draws from the whole pool (stacked cases too) like advanced, and gives no automatic practice hint.</summary>
        [Fact]
        public void The_intermediate_level_reaches_the_header_and_draws_from_the_whole_pool()
        {
            var request = new SessionRequest(SessionMode.Practice, CaseCatalogue.Cholecystectomy, TrainingLevel.Intermediate, Seed: 42, CaseId: "VAR-S1-A");
            var plan = SessionPlanner.Plan(Catalogue, request, null, Draw).Plan!;
            Assert.Equal(TrainingLevel.Intermediate, plan.Options.Level);
            Assert.Equal("intermediate", plan.Header()["level"]);
            var content = Content(plan.CaseId);
            var s = Play(plan, content);
            string text = SessionFile.Write(s);
            Assert.Contains("\"level\":\"intermediate\"", text.Split('\n')[0]);
            Assert.Equal(request, SessionFile.RequestOf(text));
            Assert.True(SessionFile.Replay(text, content, () => Subsystems.Default(content)).Match);
            Assert.Equal("", s.Observable.Hint);                                                               // no automatic practice hint above beginner

            var stacked = new HashSet<string>(Catalogue.Entries.Where(e => e.Stacked && e.Procedure == CaseCatalogue.Cholecystectomy).Select(e => e.Id));
            bool beginnerDrew = false, intermediateDrew = false;
            for (ulong seed = 1; seed <= 400; seed++)
            {
                beginnerDrew |= stacked.Contains(ScenarioPicker.Pick(Catalogue, SessionMode.Practice, CaseCatalogue.Cholecystectomy, TrainingLevel.Beginner, seed)!.CaseId);
                intermediateDrew |= stacked.Contains(ScenarioPicker.Pick(Catalogue, SessionMode.Practice, CaseCatalogue.Cholecystectomy, TrainingLevel.Intermediate, seed)!.CaseId);
            }
            if (stacked.Count > 0) { Assert.False(beginnerDrew, "beginner keeps the easier pool"); Assert.True(intermediateDrew, "intermediate draws stacked cases too"); }
        }
    }
}
