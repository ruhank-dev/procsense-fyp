using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.Core.Replay;
using ProcSense.Core.Workflow;
using ProcSense.VR.Bridge;
using ProcSense.VR.Fallback;
using ProcSense.VR.UI.Checklist;
using UnityEngine;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// The WHO board and the ward checklist panel (p0-vr-interaction-ui-s08): VAR-S1-A's ward checklist, Sign In and Time Out are driven through the panels with the same log as Core alone; a deliberately wrong
    /// allergy answer shows Sign In STOPPED in red with its reason, and the right answer resumes it (T-SC6).
    /// </summary>
    public class ChecklistBoardTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();
        private SimulationHost _host;

        private static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        private T Make<T>(string name) where T : Component { var go = new GameObject(name); _made.Add(go); return go.AddComponent<T>(); }

        private void Host()
        {
            TabletFallbackRegistry.Clear();
            if (SimulationHost.Instance != null) Object.DestroyImmediate(SimulationHost.Instance.gameObject);
            _host = Make<SimulationHost>("host");
            Assert.AreSame(_host, SimulationHost.Instance);
            _host.AutoTick = false;
            _host.SaveLogOnDestroy = false;
            _host.StartSession("VAR-S1-A", 42, SessionMode.Practice);
            ModeGate.SessionProvider = () => _host.Session;
        }

        private static List<TimedAction> Script() => ActionScript.Parse(File.ReadAllText(Path.Combine(Repo, "tests", "golden", "VAR-S1-A_correct.actions.json")));

        [TearDown]
        public void TearDown()
        {
            ModeGate.SessionProvider = () => SimulationHost.Instance != null ? SimulationHost.Instance.Session : null;
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
        }

        [Test]
        public void Sign_In_and_Time_Out_are_driven_through_the_board_with_the_same_log_as_core_alone()
        {
            Host();
            var board = Make<ChecklistBoard>("board");
            var ward = Make<WardChecklistPanel>("ward");
            ward.transform.position = new Vector3(3f, 0f, 0f);                      // beside the board, so their pictures do not overlap
            var content = _host.Content;
            var reference = new Session(content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(content));
            foreach (var step in Script())
            {
                _host.Tick(step.T - _host.Session.Clock.SimSeconds);
                reference.Tick(step.T - reference.Clock.SimSeconds);
                var p = step.Action.Params.ToDictionary(kv => kv.Key, kv => kv.Value);
                reference.Apply(new SimAction(step.Action.Verb, "patient", Channel.Vr, p));
                switch (step.Action.Verb)
                {
                    case "tick_ward_item": Assert.IsTrue(ward.Verify(p["item"]).Accepted, p["item"]); break;
                    case "sign_ward_checklist": Assert.IsTrue(ward.Sign().Accepted); break;
                    case "checklist_item":
                        if (board.Phase != p["phase"]) board.ShowPhase(p["phase"]);
                        Assert.IsTrue(board.Answer(p["item"], p["response"]).Accepted, p["item"]);
                        break;
                    case "complete_time_out": Assert.IsTrue(board.CompleteTimeOut().Accepted); break;
                    default: _host.Apply(new SimAction(step.Action.Verb, "patient", Channel.Vr, p)); break;
                }
                if (step.Action.Verb == "complete_time_out") break;
            }
            Assert.AreEqual(reference.Log.Hash(), _host.Session.Log.Hash(), "the panels made a different log");
            board.ShowPhase("sign_in");
            Assert.AreEqual("COMPLETE", board.PhaseText);
            Assert.IsTrue(board.Rows.All(r => r.State == "confirmed"));
            Assert.Greater(board.RowColour("sign_in.01").g, board.RowColour("sign_in.01").r, "confirmed is green");
            board.ShowPhase("time_out");
            Assert.AreEqual("COMPLETE", board.PhaseText);
            CollectionAssert.AreEqual(content.Checklist.InPhase("time_out").Select(i => i.Id).ToList(), board.Rows.Select(r => r.Id).ToList(), "in the content's order");
            Assert.IsTrue(board.Rows.Any(r => r.Npc), "the board shows the items the team answers");

            ward.RenderNow();
            Assert.AreEqual(content.Ward.Items.Count, ward.Lines.Count, "every ward item has a row");
            Assert.IsTrue(ward.Lines.Where(l => !l.Contains("only if it applies")).All(l => l.StartsWith("[x]")), "every required ward item ticked");
            Snapshots.Photograph(ward.Canvas, Snapshots.Out("ward-checklist.png"));
            Snapshots.Photograph(board.Canvas, Snapshots.Out("who-board.png"));
            Assert.IsTrue(ward.Lines.Any(l => l.Contains("consent form signed")), "verify shows the evidence");
            Assert.IsTrue(ward.Lines.Any(l => l.Contains("wristband read")));
        }

        [Test]
        public void A_wrong_allergy_answer_stops_Sign_In_in_red_and_the_right_one_resumes_it()
        {
            Host();
            var board = Make<ChecklistBoard>("board");
            foreach (var step in Script().TakeWhile(s => !(s.Action.Verb == "checklist_item" && s.Action.Params["item"] == "sign_in.05")))
            {
                _host.Tick(step.T - _host.Session.Clock.SimSeconds);
                _host.Apply(new SimAction(step.Action.Verb, "patient", Channel.Vr, step.Action.Params.ToDictionary(kv => kv.Key, kv => kv.Value)));
            }
            board.ShowPhase("sign_in");
            Assert.IsTrue(board.Select("sign_in.05"));
            var wrong = board.Answer("sign_in.05", "yes");                     // VAR-S1-A has no known allergy
            CollectionAssert.Contains(wrong.Effects, "contradiction:sign_in.05");
            Assert.AreEqual("STOPPED", board.PhaseText);
            var red = board.RowColour("sign_in.05");
            Assert.Greater(red.r, red.g, "stopped is red");
            StringAssert.Contains("STOPPED", board.StopText);
            Snapshots.Photograph(board.Canvas, Snapshots.Out("who-board-stopped.png"));
            StringAssert.Contains(_host.Content.Checklist.Find("sign_in.05").IfNo, board.StopText, "the reason and what fixes it, from content");

            Assert.IsTrue(board.Answer("sign_in.05", "no").Accepted, "the correction");
            Assert.AreEqual("IN PROGRESS", board.PhaseText, "Sign In resumes");
            Assert.AreEqual("", board.StopText);
        }
    }
}
