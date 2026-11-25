using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.VR.Bridge;
using ProcSense.VR.UI.Training;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>
    /// The training board (P3-TRN-03) and the engine's "not needed" reasons (P3-TRN-02, DT-298): the board is built from the running session and the guide only, follows the engine,
    /// explains a step in the chosen input mode and is absent in the examination.
    /// </summary>
    public class TrainingBoardTests
    {
        private static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        private static IContentReader Real() =>
            new CompositeContentReader(new FileContentReader(Path.Combine(Repo, "content")), new FileContentReader(Path.Combine(Repo, "tests", "fixtures")));

        private static TrainingGuide Guide() => TrainingGuide.Load(new FileContentReader(Path.Combine(Repo, "content")));

        private sealed class Raised : IContentReader
        {
            private readonly IContentReader _inner;
            public Raised(IContentReader inner) { _inner = inner; }
            public string ReadText(string path)
            {
                var text = _inner.ReadText(path);
                return path.StartsWith("case_files/") ? text.Replace("\"status\": \"APPROVED_FOR_TRAINING\"", "\"status\": \"APPROVED_FOR_ASSESSMENT\"") : text;
            }
        }

        private SimulationHost _host;

        private Session Start(SessionMode mode)
        {
            _host = new GameObject("host").AddComponent<SimulationHost>();
            _host.AutoTick = false;
            _host.SaveLogOnDestroy = false;
            _host.StartSession("VAR-S1-A", 42, mode, mode == SessionMode.Assessment ? new Raised(Real()) : Real());
            ModeGate.SessionProvider = () => _host.Session;
            return _host.Session;
        }

        [TearDown]
        public void Reset()
        {
            if (_host != null) Object.DestroyImmediate(_host.gameObject);
            _host = null;
            ModeGate.SessionProvider = () => SimulationHost.Instance != null ? SimulationHost.Instance.Session : null;
        }

        private static SimAction Act(string verb) => new SimAction(verb, "patient", Channel.Vr, new Dictionary<string, string>());

        [Test]
        public void The_board_lists_the_steps_of_the_case_from_the_engine_with_an_objective_and_progress()
        {
            var m = TrainingBoardText.Build(Start(SessionMode.Practice), Guide());

            Assert.IsTrue(m.Visible);
            Assert.IsNotEmpty(m.Sections);
            Assert.AreEqual("identity", m.Sections[0].Id);
            Assert.IsNotEmpty(m.Objective);
            Assert.AreEqual("G-S1-IDENTITY", m.ObjectiveId);
            StringAssert.StartsWith("Goals 0 of ", m.Progress);
            Assert.IsFalse(m.AllSteps.Any(r => r.Id.StartsWith("S2.")), "an S1 case shows S1 stages only");
            Assert.IsFalse(m.AllSteps.Any(r => r.Id == "G-SENIOR-NOW"), "a goal the engine has not triggered is never shown");
        }

        [Test]
        public void A_line_the_engine_lists_as_not_needed_shows_the_guide_reason()
        {
            var m = TrainingBoardText.Build(Start(SessionMode.Practice), Guide());

            var cbg = m.AllSteps.Single(r => r.Id == "ward.cbg");
            Assert.AreEqual("not_needed", cbg.State);
            StringAssert.Contains("not diabetic", cbg.Note);
        }

        [Test]
        public void The_board_follows_the_engine_when_a_goal_is_done()
        {
            var s = Start(SessionMode.Practice);
            var guide = Guide();
            Assert.AreEqual("open", TrainingBoardText.Build(s, guide).AllSteps.Single(r => r.Id == "G-S1-IDENTITY").State);

            foreach (var v in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.IsTrue(_host.Apply(Act(v)).Accepted, v);

            var m = TrainingBoardText.Build(s, guide);
            Assert.AreEqual("done", m.AllSteps.Single(r => r.Id == "G-S1-IDENTITY").State);
            Assert.AreNotEqual("G-S1-IDENTITY", m.ObjectiveId);
        }

        /// <summary>P3-TRN-08 "hint-no-action": a hint tells, it never does: no goal moves, no step changes on the board, and the patient's state is the same after it.</summary>
        [Test]
        public void A_hint_changes_no_step_and_no_goal()
        {
            var s = Start(SessionMode.Practice);
            var guide = Guide();
            var before = TrainingBoardText.Build(s, guide).AllSteps.Select(r => r.Id + "=" + r.State).ToList();
            var graph = s.GraphHistory.Count;
            Assert.IsTrue(_host.Apply(Act("request_hint")).Accepted);
            Assert.IsNotNull(s.LastHint, "the hint is given");
            CollectionAssert.AreEqual(before, TrainingBoardText.Build(s, guide).AllSteps.Select(r => r.Id + "=" + r.State).ToList(), "no step done or undone by a hint");
            Assert.AreEqual(graph, s.GraphHistory.Count, "no goal completed or missed by a hint");
        }

        [Test]
        public void A_refused_action_shows_why_on_the_board()
        {
            var s = Start(SessionMode.Practice);
            var outcome = _host.Apply(Act("sign_ward_checklist"));      // before identity: refused by the engine
            Assume.That(!outcome.Accepted);

            Assert.IsNotEmpty(TrainingBoardText.Build(s, Guide()).Feedback);
        }

        [Test]
        public void The_view_explains_the_selected_step_in_the_chosen_input_mode()
        {
            Start(SessionMode.Practice);
            var view = new GameObject("board").AddComponent<TrainingBoardView>();
            try
            {
                view.Guide = Guide();
                view.Build();
                view.Refresh();
                Assert.IsTrue(view.Canvas.gameObject.activeSelf);
                var content = (RectTransform)view.Canvas.transform.Find("Content");
                Assert.AreEqual(new Vector2(0, 1), content.anchorMin, "the content starts at the panel's top-left corner, not its centre");
                Assert.AreEqual(Vector2.zero, content.anchoredPosition);
                StringAssert.Contains("Identity confirmed", view.ObjectiveText);
                StringAssert.Contains("Admission tab", view.HowText);
                view.ToggleInputMode();
                StringAssert.Contains("Say:", view.HowText);
                string first = view.SelectedId;
                view.Select(+1);
                Assert.AreNotEqual(first, view.SelectedId);
            }
            finally { Object.DestroyImmediate(view.gameObject); }
        }

        [Test]
        public void The_examination_shows_no_board_no_goals_and_no_reasons()
        {
            var s = Start(SessionMode.Assessment);
            var m = TrainingBoardText.Build(s, Guide());
            Assert.IsFalse(m.Visible);
            Assert.IsEmpty(m.Sections);
            Assert.AreEqual("", m.Objective);

            var view = new GameObject("board").AddComponent<TrainingBoardView>();
            try
            {
                view.Guide = Guide();
                view.Build();
                view.Refresh();
                Assert.IsFalse(view.Canvas.gameObject.activeSelf);
            }
            finally { Object.DestroyImmediate(view.gameObject); }
        }
    }
}
