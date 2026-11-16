using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Scoring;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>
    /// P3-VOX-06, the engine half: an examination run where every conversational step (introduction, identity questions, allergies, the whole consent talk, capacity, teach-back) is an action of the spoken catalogue
    /// (content/ai/spoken_actions.json), and the only other actions are the physical gestures speech may never start (reading the wristband, opening the notes, confirming identity, signing). No menu-only action is
    /// needed: identity is confirmed, the allergy is disclosed and the consent is signed and valid. The score and the state are deterministic: two runs, and the same run through the menu, give the same hashes and total.
    /// The Unity half (the router and the real tablet) is SpokenExamVoiceOnlyTests in the Unity PlayMode tests.
    /// </summary>
    public class VoiceOnlyExaminationTests
    {
        private const string Case = "VAR-S1-A";
        private static readonly string[] Gestures = { "read_wristband", "open_notes", "confirm_identity", "sign_consent" };

        private static readonly string[] SpokenScript =
        {
            "introduce_self", "ask_identity", "ask_allergies", "ask_allergy_reaction", "ask_reason_for_admission",
            "consent_discussion:diagnosis", "consent_discussion:operation", "consent_discussion:benefits", "consent_discussion:risks", "consent_discussion:alternatives",
            "consent_discussion:no_treatment", "consent_discussion:matters", "check_capacity", "teach_back",
        };

        private static Dictionary<string, SimAction> Catalogue()
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(TestCase.ContentRoot, "ai", "spoken_actions.json")));
            var map = new Dictionary<string, SimAction>();
            foreach (var a in doc.RootElement.GetProperty("actions").EnumerateArray())
            {
                var p = new Dictionary<string, string>();
                if (a.TryGetProperty("key", out var k)) p[k.GetString()!] = a.GetProperty("value").GetString()!;
                map[a.GetProperty("id").GetString()!] = new SimAction(a.GetProperty("verb").GetString()!, "", Channel.Vr, p);
            }
            return map;
        }

        private static (Session session, ContentPackage content) New()
        {
            var c = ContentLoader.Load(new FileContentReader(TestCase.ContentRoot), new[] { Case }, new[] { Case });
            return (new Session(c.Cases[Case], 42, SessionMode.Assessment, Subsystems.Default(c)), c);
        }

        /// <summary>The voice-only run: spoken steps from the catalogue, gestures as gestures. Returns the outcomes by step.</summary>
        private static List<(string step, ActionOutcome outcome)> Play(Session s, IReadOnlyDictionary<string, SimAction> catalogue)
        {
            var done = new List<(string, ActionOutcome)>();
            void Gesture(string verb) => done.Add((verb, s.Apply(new SimAction(verb, "", Channel.Vr))));
            void Say(string id) => done.Add((id, s.Apply(catalogue[id])));
            Say("introduce_self");
            Say("ask_identity");
            Gesture("read_wristband");
            Gesture("open_notes");
            Gesture("confirm_identity");
            foreach (var id in SpokenScript.Skip(2)) Say(id);
            Gesture("sign_consent");
            return done;
        }

        [Fact]
        public void Every_spoken_step_of_the_script_is_in_the_catalogue_and_no_gesture_is()
        {
            var catalogue = Catalogue();
            Assert.All(SpokenScript, id => Assert.True(catalogue.ContainsKey(id), id));
            Assert.All(Gestures, g => Assert.DoesNotContain(catalogue.Values, a => a.Verb == g));
        }

        [Fact]
        public void Identity_allergy_and_consent_are_completed_by_speech_and_gestures_alone_in_the_examination()
        {
            var (s, _) = New();
            Assert.False(s.PracticeAids);
            var run = Play(s, Catalogue());
            foreach (var (step, o) in run) Assert.True(o.Accepted, step + ": " + o.RejectReason);
            Assert.True(s.Observable.IdentityConfirmed);
            Assert.Contains(s.Observable.DisclosedFacts, f => f.Topic == "allergies");
            Assert.Contains("consent_signed", run.Single(r => r.step == "sign_consent").outcome.Effects);
            Assert.DoesNotContain(run.SelectMany(r => r.outcome.Effects), e => e.StartsWith("missing:") || e.StartsWith("consent_invalid"));
        }

        [Fact]
        public void The_voice_only_score_is_deterministic_and_equals_the_menu_run()
        {
            var catalogue = Catalogue();
            var (a, ca) = New();
            var (b, cb) = New();
            Play(a, catalogue);
            Play(b, catalogue);
            Assert.Equal(a.Observable.Hash(), b.Observable.Hash());
            Assert.Equal(a.Log.Hash(), b.Log.Hash());
            Assert.Equal(Scorer.Score(a, ca).Total, Scorer.Score(b, cb).Total);
            // The menu run: the tablet buttons make the same SimActions (verb, params, channel) as the catalogue entries (SpokenEquivalenceTests), so the engine cannot tell them apart.
            var (m, cm) = New();
            var tablet = new Dictionary<string, SimAction>(catalogue.ToDictionary(kv => kv.Key, kv => new SimAction(kv.Value.Verb, "", Channel.Vr, new Dictionary<string, string>(kv.Value.Params))));
            Play(m, tablet);
            Assert.Equal(a.Observable.Hash(), m.Observable.Hash());
            Assert.Equal(Scorer.Score(a, ca).Total, Scorer.Score(m, cm).Total);
        }
    }
}
