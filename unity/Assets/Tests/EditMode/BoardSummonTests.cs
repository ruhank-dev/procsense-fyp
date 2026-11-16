using System.IO;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Content;
using ProcSense.VR.Bridge;
using ProcSense.VR.UI.Accessibility;
using ProcSense.VR.UI.Training;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>P3-TRN-04: the icon sits at the edge of the view inside the head-locked limits, the press brings the board world-locked 0.8 m ahead and the next press sends it home, and there is no icon in the examination.</summary>
    public class BoardSummonTests
    {
        private static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        private SimulationHost _host;
        private TrainingBoardView _board;
        private BoardSummon _summon;
        private Transform _head;

        private sealed class Raised : IContentReader
        {
            private readonly IContentReader _inner;
            public Raised(IContentReader inner) { _inner = inner; }
            public string ReadText(string path) { var t = _inner.ReadText(path); return path.StartsWith("case_files/") ? t.Replace("\"status\": \"APPROVED_FOR_TRAINING\"", "\"status\": \"APPROVED_FOR_ASSESSMENT\"") : t; }
        }

        private void Make(SessionMode mode)
        {
            IContentReader real = new CompositeContentReader(new FileContentReader(Path.Combine(Repo, "content")), new FileContentReader(Path.Combine(Repo, "tests", "fixtures")));
            _host = new GameObject("summon host").AddComponent<SimulationHost>();
            _host.AutoTick = false;
            _host.SaveLogOnDestroy = false;
            _host.StartSession("VAR-S1-A", 42, mode, mode == SessionMode.Assessment ? new Raised(real) : real);
            ModeGate.SessionProvider = () => _host.Session;
            var boardGo = new GameObject("board");
            boardGo.transform.SetPositionAndRotation(new Vector3(2f, 1.4f, 3f), Quaternion.Euler(0, 30, 0));
            _board = boardGo.AddComponent<TrainingBoardView>();
            _board.Guide = TrainingGuide.Load(new FileContentReader(Path.Combine(Repo, "content")));
            _board.SessionProvider = () => _host.Session;
            _board.Build();
            _head = new GameObject("head").transform;
            _head.SetPositionAndRotation(new Vector3(0f, 1.6f, 0f), Quaternion.Euler(10f, 40f, 0f));
            _summon = new GameObject("summon").AddComponent<BoardSummon>();
            _summon.Head = _head;
            _summon.Init(_board);
            _summon.Follow(10f);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in new Object[] { _summon != null ? _summon.gameObject : null, _board != null ? _board.gameObject : null, _head != null ? _head.gameObject : null, _host != null ? _host.gameObject : null })
                if (o != null) Object.DestroyImmediate(o);
            ModeGate.SessionProvider = () => SimulationHost.Instance != null ? SimulationHost.Instance.Session : null;
        }

        [Test]
        public void The_icon_is_inside_the_head_locked_limits()
        {
            Assert.IsEmpty(Readability.HeadLockedProblems(BoardSummon.IconOffset, .1f));
        }

        [Test]
        public void A_press_brings_the_board_in_front_and_the_next_sends_it_home()
        {
            Make(SessionMode.Practice);
            Assert.IsTrue(_summon.Icon.gameObject.activeSelf, "icon in training");
            Assert.Less(Vector3.Distance(_head.TransformPoint(BoardSummon.IconOffset), _summon.Icon.transform.parent.position), .01f, "at the edge of the view");
            var home = (_board.transform.position, _board.transform.rotation);
            _summon.IconButton.onClick.Invoke();
            Assert.IsTrue(_summon.Summoned);
            var flat = Vector3.ProjectOnPlane(_head.forward, Vector3.up).normalized;
            Assert.Less(Vector3.Distance(_head.position + flat * BoardSummon.BoardDistance, _board.transform.position), .001f, "0.8 m ahead, level");
            Assert.Less(Vector3.Angle(Vector3.up, _board.transform.up), .01f, "upright");
            _head.Rotate(0, 60, 0);
            _summon.Follow(1f);
            Assert.Less(Vector3.Distance(_head.position + flat * BoardSummon.BoardDistance, _board.transform.position), .001f, "world-locked: it stays when the head turns");
            _summon.IconButton.onClick.Invoke();
            Assert.AreEqual(home, (_board.transform.position, _board.transform.rotation), "back where it stands");
        }

        [Test]
        public void No_icon_in_the_examination()
        {
            Make(SessionMode.Assessment);
            Assert.IsFalse(_summon.Icon.gameObject.activeSelf);
        }
    }
}
