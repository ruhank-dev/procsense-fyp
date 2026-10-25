using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.VR.Bridge;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>The follow-up encounter with the same patient (plan p2-ai-advanced-simulation-s02, Unity half): the host captures the twin, starts the next encounter from it in practice only, saves the finished one, and keeps the follow-up
    /// on the device with its twin file. The headset check (DC-07) is Sannan's.</summary>
    public class FollowUpEncounterTests
    {
        private static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        private string _dir;

        [SetUp] public void Setup() { _dir = Path.Combine(Path.GetTempPath(), "procsense-followup-" + System.Guid.NewGuid().ToString("N")); SimulationHost.SessionsDirectoryOverride = _dir; }
        [TearDown] public void Teardown() { SimulationHost.SessionsDirectoryOverride = null; if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        /// <summary>The real content with the case raised to APPROVED_FOR_ASSESSMENT in memory only, as ModeGateTests does (no case is approved for assessment).</summary>
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

        private static SimulationHost Host(SessionMode mode)
        {
            var host = new GameObject("host").AddComponent<SimulationHost>();
            host.AutoTick = false;
            host.SaveLogOnDestroy = false;
            IContentReader real = new FileContentReader(Path.Combine(Repo, "content"));
            host.StartSession("VAR-S1-A", 42, mode, mode == SessionMode.Assessment ? new Raised(real) : real);
            return host;
        }

        [Test]
        public void The_follow_up_starts_from_the_carried_patient_and_the_first_encounter_is_saved_and_queued()
        {
            var host = Host(SessionMode.Practice);
            try
            {
                foreach (var verb in new[] { "introduce_self", "ask_identity", "open_notes" }) host.Apply(new SimAction(verb));
                host.Tick(30);
                var twin = host.CaptureTwin();
                string firstId = host.SessionId;
                string hash = host.StartFollowUp(twin, 43);
                Assert.AreEqual(twin.Hash(), hash);
                Assert.AreEqual(hash, host.TwinHash);
                Assert.AreNotEqual(firstId, host.SessionId);
                Assert.AreEqual(0, host.Session.Log.Count);                                           // a new encounter, nothing done in it yet
                Assert.AreEqual(twin.ClockTime, host.Session.Clock.ClockTime.ToString(@"hh\:mm\:ss"));
                CollectionAssert.IsSubsetOf(twin.Flags, host.Session.Patient.Clinical.Flags);
                Assert.IsTrue(Directory.GetFiles(_dir, "*" + firstId + ".jsonl").Length == 1, "the first encounter was saved");
            }
            finally { Object.DestroyImmediate(host.gameObject); }
        }

        [Test]
        public void A_follow_up_is_saved_beside_its_twin_file_and_not_uploaded_and_a_new_session_clears_the_twin()
        {
            var host = Host(SessionMode.Practice);
            try
            {
                host.Apply(new SimAction("introduce_self"));
                host.StartFollowUp(host.CaptureTwin(), 43);
                host.Apply(new SimAction("ask_identity"));
                string file = host.SaveSessionLog();
                StringAssert.Contains("_followup_", file);
                Assert.IsTrue(File.Exists(Path.ChangeExtension(file, ".twin.json")));
                Assert.AreEqual(host.TwinHash, TwinHashOf(file));
                host.StartSession("VAR-S1-A", 44, SessionMode.Practice, new FileContentReader(Path.Combine(Repo, "content")));
                Assert.AreEqual("", host.TwinHash);
            }
            finally { Object.DestroyImmediate(host.gameObject); }
        }

        private static string TwinHashOf(string sessionFile) => ProcSense.Core.Twin.TwinSnapshot.FromJson(File.ReadAllText(Path.ChangeExtension(sessionFile, ".twin.json"))).Hash();

        [Test]
        public void Assessment_refuses_a_follow_up_and_keeps_its_session()
        {
            var host = Host(SessionMode.Assessment);
            try
            {
                var e = Assert.Throws<ContentException>(() => host.StartFollowUp(host.CaptureTwin(), 43));
                StringAssert.Contains("practice only", e.Message);
                Assert.AreEqual("", host.TwinHash);
                Assert.AreEqual(SessionMode.Assessment, host.Session.Mode);
            }
            finally { Object.DestroyImmediate(host.gameObject); }
        }
    }
}
