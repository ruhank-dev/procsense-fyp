using System;
using System.IO;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Difficulty;
using ProcSense.Core.Replay;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>
    /// The assessment-mode lock, end to end (p0-evaluation-performance-s04): only content approved for assessment is loaded (cases T-CL-2, events T-E6), the next case is the fixed order (T-DF3), the trainee
    /// sees no goal, hint, blocker message, flow or NEWS2 helper, no practice safety net acts for them, critical items are logged strictly, the mode is in the session file and nothing can change it mid-session.
    /// No shipped case or event is approved for assessment yet (that is the clinician's sign-off), so the test approves VAR-S1-A and one event in the content it reads.
    /// </summary>
    public class AssessmentModeTests
    {
        private static SimAction Act(string verb, params (string k, string v)[] p) => new SimAction(verb, "patient", Channel.Cli, p.ToDictionary(x => x.k, x => x.v));

        private static ActionOutcome Do(Session s, string verb, params (string k, string v)[] p) => s.Apply(Act(verb, p));

        // The real content, with the case files of VAR-S1-A and VAR-S1-H and the hypersensitivity event raised to APPROVED_FOR_ASSESSMENT (what a clinician's sign-off would do).
        private sealed class ApprovedReader : IContentReader
        {
            private readonly IContentReader _inner = new CompositeContentReader(new FileContentReader(TestCase.ContentRoot), new FileContentReader(Path.Combine(TestCase.Repo, "tests", "fixtures")));

            public string ReadText(string path)
            {
                string text = _inner.ReadText(path);
                if (path == "case_files/VAR-S1-A.json" || path == "case_files/VAR-S1-H.json") return text.Replace("\"status\": \"APPROVED_FOR_TRAINING\"", "\"status\": \"APPROVED_FOR_ASSESSMENT\"");
                if (path == "events/events.json")
                {
                    int at = text.IndexOf("\"E-HYPERSENSITIVITY\"", StringComparison.Ordinal);
                    int status = text.IndexOf("\"status\": \"APPROVED_FOR_TRAINING\"", at, StringComparison.Ordinal);
                    return text.Substring(0, status) + "\"status\": \"APPROVED_FOR_ASSESSMENT\"" + text.Substring(status + "\"status\": \"APPROVED_FOR_TRAINING\"".Length);
                }
                return text;
            }
        }

        private static ModeLoad Load(SessionMode mode, params string[] cases) => ContentMode.Load(new ApprovedReader(), mode, false, cases);

        private static Session Play(SessionMode mode, string caseId = "VAR-S1-A")
        {
            var content = Load(mode, caseId).Package;
            var s = new Session(content.Cases[caseId], 42, mode, Subsystems.Default(content));
            SessionFile.Play(s, ActionScript.Parse(File.ReadAllText(Path.Combine(TestCase.Repo, "tests", "golden", caseId + "_correct.actions.json"))));
            return s;
        }

        [Fact]
        public void Only_content_approved_for_assessment_is_loaded()
        {
            var load = Load(SessionMode.Assessment, "VAR-S1-A", "VAR-S1-B", "VAR-S1-C");
            Assert.Equal(new[] { "VAR-S1-A" }, load.Package.CaseFiles.Keys);
            Assert.Equal(new[] { "VAR-S1-B", "VAR-S1-C" }, load.Excluded.Select(e => e.Id).OrderBy(x => x));       // the draft and the training-only case, each named
            Assert.All(load.Excluded, e => Assert.Contains("APPROVED_FOR_ASSESSMENT", e.Reason));
            var s = new Session(load.Package.Cases["VAR-S1-A"], 1, SessionMode.Assessment, Subsystems.Default(load.Package));
            Assert.Equal(new[] { "E-HYPERSENSITIVITY" }, s.Director!.Library.Select(e => e.Id));                 // the other six events are approved for training only
        }

        [Fact]
        public void With_nothing_signed_off_assessment_loads_no_shipped_case_and_no_event()
        {
            var real = ContentMode.Load(new CompositeContentReader(new FileContentReader(TestCase.ContentRoot)), SessionMode.Assessment, false, "VAR-S1-A");
            Assert.Empty(real.Package.CaseFiles);                                                                  // the lock holds until a clinician approves cases for assessment
        }

        [Fact]
        public void The_next_case_is_the_fixed_order_whatever_the_ratings()
        {
            var pool = Load(SessionMode.Assessment, "VAR-S1-A").Package.CaseFiles;
            var tables = TestCase.Package().Difficulty;
            var fresh = Ratings.From(Array.Empty<SessionRecord>(), tables, _ => "P0");
            var pick = Selector.Next(fresh, Array.Empty<string>(), pool, SessionMode.Assessment);
            Assert.Equal("VAR-S1-A", pick.CaseId);
            Assert.Contains("fixed order", pick.Reason);
            Assert.Equal("", pick.Dimension);
        }

        [Fact]
        public void The_trainee_sees_no_practice_help_in_assessment()
        {
            var practice = Play(SessionMode.Practice).Observable;
            var exam = Play(SessionMode.Assessment).Observable;
            Assert.NotEmpty(practice.Goals);                       // the same play shows help in practice ...
            Assert.Empty(exam.Goals);                              // ... and none in assessment
            Assert.Equal("", exam.Hint);
            Assert.Equal("", exam.BlockMessage);
            Assert.Empty(exam.Flow);
            Assert.Empty(exam.News2);
        }

        [Fact]
        public void No_practice_safety_net_acts_for_the_trainee_and_the_decision_is_not_given_away()
        {
            var practice = Play(SessionMode.Practice, "VAR-S1-H");                 // the pregnant patient: the trainee declares the outcome
            var exam = Play(SessionMode.Assessment, "VAR-S1-H");
            Assert.Contains("\"expected:", practice.Log.ToJsonl());                // practice tells the trainee what the master decision should have been
            Assert.DoesNotContain("\"expected:", exam.Log.ToJsonl());
            Assert.True(practice.PracticeAids);
            Assert.False(exam.PracticeAids);                                       // the one check the views use
        }

        [Fact]
        public void A_medicine_given_without_the_allergy_check_is_a_critical_item_in_assessment()
        {
            var content = Load(SessionMode.Assessment, "VAR-S1-A").Package;
            var s = new Session(content.Cases["VAR-S1-A"], 42, SessionMode.Assessment, Subsystems.Default(content));
            foreach (var verb in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Do(s, verb);
            Do(s, "request_medication", ("class", "TR-PARACETAMOL"), ("indication", "pain"));
            s.Tick(150);
            var given = s.MedicineHistory.Single(e => e.Kind == "administered");    // no nurse challenge: it is given, and logged as critical
            Assert.Contains("critical.treatment_allergy_known", given.ScoreItems);
        }

        [Fact]
        public void The_mode_is_in_the_session_file_and_nothing_changes_it_mid_session()
        {
            var s = Play(SessionMode.Assessment);
            Assert.Equal("assessment", SessionFile.HeaderOf(SessionFile.Write(s)).Mode);
            Assert.DoesNotContain(s.Verbs.Known, v => v.Contains("mode", StringComparison.Ordinal));                     // no verb switches the mode
            Assert.Equal(SessionMode.Assessment, s.Mode);
        }
    }
}
