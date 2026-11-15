using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Workflow;

namespace ProcSense.Core.Tests
{
    /// <summary>
    /// P3-VOX-13: every action the spoken catalogue offers is the tablet's action (same verb, target, channel, parameters), and the engine gives the same outcome, effects and state for it in practice and in the examination
    /// (the examination only adds the hint cost); no critical physical verb is in the catalogue, so speech never starts one. Mismatches are fixed in the catalogue, never by retuning the frozen evaluator.
    /// </summary>
    public class SpokenEquivalenceTests
    {
        private const string Case = "VAR-S1-A";
        private static readonly string[] CriticalPhysical =
        {
            "confirm_identity", "report_mismatch", "read_wristband", "open_notes", "attach_sensor", "detach_sensor", "measure_nibp", "measure_temperature", "count_rr",
            "take_sample", "examine", "murphy_test", "sign_consent", "complete_time_out", "sign_ward_checklist", "tick_ward_item", "checklist_item", "use_emergency_route",
        };

        private static Session New(SessionMode mode)
        {
            var c = ContentLoader.Load(new FileContentReader(TestCase.ContentRoot), new[] { Case }, new[] { Case });
            return new Session(c.Cases[Case], 42, mode, Subsystems.Default(c));
        }

        private static List<SimAction> Catalogue()
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(TestCase.ContentRoot, "ai", "spoken_actions.json")));
            var list = new List<SimAction>();
            foreach (var a in doc.RootElement.GetProperty("actions").EnumerateArray())
            {
                var p = new Dictionary<string, string>();
                if (a.TryGetProperty("key", out var k)) p[k.GetString()!] = a.GetProperty("value").GetString()!;
                list.Add(new SimAction(a.GetProperty("verb").GetString()!, "patient", Channel.Vr, p));
            }
            return list;
        }

        [Fact]
        public void The_spoken_catalogue_has_no_critical_physical_verb()
        {
            var actions = Catalogue();
            Assert.True(actions.Count > 10);
            Assert.DoesNotContain(actions, a => CriticalPhysical.Contains(a.Verb));
        }

        [Fact]
        public void Every_catalogue_action_gets_the_same_engine_outcome_in_practice_and_in_the_examination()
        {
            foreach (var action in Catalogue())
            {
                var practice = New(SessionMode.Practice);
                var exam = New(SessionMode.Assessment);
                var a = practice.Apply(action);
                var b = exam.Apply(action);
                var name = action.Verb + string.Concat(action.Params.Select(kv => ":" + kv.Value));
                Assert.True(a.Accepted == b.Accepted, name);
                Assert.True(a.RejectReason == b.RejectReason, name);
                Assert.True(a.Effects.SequenceEqual(b.Effects), name);
            }
        }

        [Fact]
        public void Hint_and_submit_by_voice_or_menu_are_one_action_with_the_same_outcome_and_the_hint_costs_only_in_the_examination()
        {
            foreach (var mode in new[] { SessionMode.Practice, SessionMode.Assessment })
            {
                var voice = New(mode);
                var menu = New(mode);
                var hint = new SimAction(ExamControl.RequestHint, "", Channel.Vr);
                var v = voice.Apply(hint);
                var m = menu.Apply(hint);
                Assert.True(v.Accepted && m.Accepted);
                Assert.Equal(m.Effects, v.Effects);
                Assert.Equal(mode == SessionMode.Assessment, v.Effects.Contains("score:" + ExamControl.HintScoreItem));
                for (int i = 0; i < 3; i++) { voice.Apply(hint); menu.Apply(hint); }
                Assert.Equal(ExamControl.HintLimitReached, voice.Apply(hint).RejectReason);          // the same limit as the glass button
                var submit = new SimAction(ExamControl.SubmitPatient, "", Channel.Vr, new Dictionary<string, string>());
                Assert.True(voice.Apply(submit).Accepted && menu.Apply(submit).Accepted);
                Assert.Equal(menu.Observable.Hash(), voice.Observable.Hash());
            }
        }
    }
}
