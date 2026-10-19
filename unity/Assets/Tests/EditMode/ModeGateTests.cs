using System.IO;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Content;
using ProcSense.VR.Bridge;
using ProcSense.VR.Views;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>The one central check for practice-only presentation (plan p0-evaluation-performance-s04): on in practice (and with no session), off in assessment; the label is what the debrief shows. The on-headset check that no hint appears is Sannan's (UX-P0-ASSESS).</summary>
    public class ModeGateTests
    {
        private static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        private static IContentReader Real() =>
            new CompositeContentReader(new FileContentReader(Path.Combine(Repo, "content")), new FileContentReader(Path.Combine(Repo, "tests", "fixtures")));

        /// <summary>The real content, with the case file raised to APPROVED_FOR_ASSESSMENT as a clinician's sign-off would do (no case is approved for assessment yet), as tests/ProcSense.Core.Tests/AssessmentModeTests.cs does.</summary>
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

        private static SimulationHost Host(SessionMode mode, string caseId)
        {
            var host = new GameObject("host").AddComponent<SimulationHost>();
            host.AutoTick = false;
            host.SaveLogOnDestroy = false;
            host.StartSession(caseId, 42, mode, mode == SessionMode.Assessment ? new Raised(Real()) : Real());
            ModeGate.SessionProvider = () => host.Session;
            return host;
        }

        [TearDown]
        public void Reset() => ModeGate.SessionProvider = () => SimulationHost.Instance != null ? SimulationHost.Instance.Session : null;

        [Test]
        public void With_no_session_the_aids_are_on()
        {
            Assert.IsTrue(ModeGate.PracticeAids);
            Assert.AreEqual("a hint", ModeGate.Practice("a hint"));
            Assert.AreEqual("", ModeGate.Label);
        }

        [Test]
        public void Practice_shows_the_aids_and_labels_the_mode()
        {
            var host = Host(SessionMode.Practice, "VAR-S1-A");
            try
            {
                Assert.IsTrue(ModeGate.PracticeAids);
                Assert.AreEqual("a hint", ModeGate.Practice("a hint"));
                Assert.AreEqual("PRACTICE", ModeGate.Label);
            }
            finally { Object.DestroyImmediate(host.gameObject); }
        }

        [Test]
        public void Assessment_hides_every_practice_only_text_and_object()
        {
            var host = Host(SessionMode.Assessment, "VAR-S1-A");
            try
            {
                Assert.IsFalse(ModeGate.PracticeAids);
                Assert.AreEqual("", ModeGate.Practice("a hint"));
                Assert.AreEqual("ASSESSMENT", ModeGate.Label);
                var holder = new GameObject("holder").AddComponent<PracticeOnly>();
                var child = new GameObject("chip");
                child.transform.SetParent(holder.transform);
                holder.Apply();
                Assert.IsFalse(child.activeSelf);
                Object.DestroyImmediate(holder.gameObject);
            }
            finally { Object.DestroyImmediate(host.gameObject); }
        }

        [Test]
        public void The_mode_can_be_chosen_before_a_session_and_not_during_it()
        {
            ModeChoice.Reset();
            try
            {
                Assert.AreEqual(SessionMode.Practice, ModeChoice.For(SessionMode.Practice));              // nothing chosen: the configuration decides
                Assert.IsTrue(ModeChoice.Choose(SessionMode.Assessment));
                Assert.AreEqual(SessionMode.Assessment, ModeChoice.For(SessionMode.Practice));
                var host = Host(SessionMode.Practice, "VAR-S1-A");                                          // starting a session locks the choice
                try
                {
                    Assert.IsTrue(ModeChoice.Locked);
                    Assert.IsFalse(ModeChoice.Choose(SessionMode.Practice));
                    Assert.AreEqual(SessionMode.Assessment, ModeChoice.Chosen);
                }
                finally { Object.DestroyImmediate(host.gameObject); }
                ModeChoice.Unlock();                                                                        // what EndSession does
                Assert.IsTrue(ModeChoice.Choose(SessionMode.Practice));
            }
            finally { ModeChoice.Reset(); }
        }

        [Test]
        public void A_refused_start_does_not_lock_the_choice()
        {
            ModeChoice.Reset();
            var host = new GameObject("host").AddComponent<SimulationHost>();
            try
            {
                Assert.Throws<ContentException>(() => host.StartSession("VAR-S1-A", 42, SessionMode.Assessment, Real()));        // no case is approved for assessment yet
                Assert.IsFalse(ModeChoice.Locked);
            }
            finally { Object.DestroyImmediate(host.gameObject); ModeChoice.Reset(); }
        }

        [Test]
        public void The_chooser_sets_the_mode_before_a_session_and_is_disabled_during_one()
        {
            ModeChoice.Reset();
            var go = new GameObject("chooser");
            var view = go.AddComponent<ModeChooserView>();
            SimulationHost host = null;
            try
            {
                view.Build();
                view.Refresh();
                Assert.IsTrue(view.PracticeButton.interactable && view.AssessmentButton.interactable);
                view.AssessmentButton.onClick.Invoke();
                Assert.AreEqual(SessionMode.Assessment, ModeChoice.Chosen);
                view.PracticeButton.onClick.Invoke();
                Assert.AreEqual(SessionMode.Practice, ModeChoice.Chosen);
                host = Host(SessionMode.Practice, "VAR-S1-A");
                view.Refresh();
                Assert.IsFalse(view.PracticeButton.interactable);
                Assert.IsFalse(view.AssessmentButton.interactable);
                StringAssert.Contains("cannot be changed", view.StatusText);
                view.AssessmentButton.onClick.Invoke();                                                     // a click on a disabled button's handler still changes nothing
                Assert.AreEqual(SessionMode.Practice, ModeChoice.Chosen);
            }
            finally { if (host != null) Object.DestroyImmediate(host.gameObject); Object.DestroyImmediate(go); ModeChoice.Reset(); }
        }
    }
}
