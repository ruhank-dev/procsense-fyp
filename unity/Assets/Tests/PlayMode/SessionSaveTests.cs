using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.Core.Replay;
using ProcSense.VR.Auth;
using ProcSense.VR.Bridge;
using ProcSense.VR.Content;
using ProcSense.VR.Sync;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>What the headset saves at the end of a session and what it queues for the service (p1-backend-s03): the header the service needs, once per session, for the person who played it.</summary>
    public class SessionSaveTests
    {
        private const string Base = "https://service.test";
        private const string Sb = "https://project.supabase.co";
        private const string User = "11111111-1111-1111-1111-111111111111";
        private const string Org = "22222222-2222-2222-2222-222222222222";

        private sealed class ScriptedHttp : IHttp
        {
            private readonly Dictionary<string, HttpResult> _answers = new Dictionary<string, HttpResult>();
            public ScriptedHttp Answer(string method, string url, long status, string body = "") { _answers[method + " " + url] = new HttpResult(status, body); return this; }
            public IEnumerator Send(string method, string url, string jsonBody, IDictionary<string, string> headers, int timeoutSeconds, Action<HttpResult> done)
            {
                done(_answers.TryGetValue(method + " " + url, out var r) ? r : new HttpResult(0, "", true));
                yield break;
            }
        }

        private readonly List<GameObject> _made = new List<GameObject>();
        private string _dir;
        private SessionSync _original;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "procsense-save-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            SimulationHost.SessionsDirectoryOverride = Path.Combine(_dir, "sessions");
            _original = SessionUploader.Instance != null ? SessionUploader.Instance.Sync : null;
            if (SessionUploader.Instance != null)
                SessionUploader.Instance.Sync = new SessionSync(new UploadQueue(Path.Combine(_dir, "outbox"), SessionUploader.UnixNow), () => AuthService.Current, SessionUploader.UnixNow);
            AuthService.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _made.Clear();
            SimulationHost.SessionsDirectoryOverride = null;
            if (SessionUploader.Instance != null && _original != null) SessionUploader.Instance.Sync = _original;
            AuthService.Clear();
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private SimulationHost PlayedSession()
        {
            var go = new GameObject("host");
            _made.Add(go);
            var host = go.AddComponent<SimulationHost>();
            host.AutoTick = false;
            host.SaveLogOnDestroy = false;
            host.StartSession("VAR-S1-A", 42, SessionMode.Practice);
            host.Apply(new SimAction("introduce_self", "patient", Channel.Vr, new Dictionary<string, string>()));
            return host;
        }

        private static IEnumerator SignIn()
        {
            var http = new ScriptedHttp()
                .Answer("GET", Base + "/config.json", 200, "{\"supabase_url\":\"" + Sb + "\",\"supabase_publishable_key\":\"k\"}")
                .Answer("POST", Sb + "/auth/v1/token?grant_type=password", 200, "{\"access_token\":\"AT\",\"refresh_token\":\"RT\",\"expires_in\":3600}")
                .Answer("GET", Base + "/me", 200, "{\"user_id\":\"" + User + "\",\"pseudonym\":\"T-ab12cd34\",\"display_name\":\"\",\"memberships\":[{\"organisation_id\":\"" + Org + "\",\"organisation_name\":\"Demo\",\"slug\":\"demo\",\"role\":\"trainee\",\"status\":\"active\"}]}")
                .Answer("GET", Base + "/privacy/consent", 200, "{\"needs_consent\":false}");
            SignInResult result = null;
            yield return new AuthService(http, Base).SignIn("jane@example.org", "pw", r => result = r);
            Assert.IsTrue(result.Ok, result.Message);
        }

        [Test]
        public void The_saved_file_carries_the_session_id_the_content_version_and_the_engine_version_and_replays_to_its_own_hash()
        {
            var host = PlayedSession();
            string file = host.SaveSessionLog();
            Assert.IsTrue(File.Exists(file));
            StringAssert.Contains(host.SessionId, Path.GetFileName(file));
            string text = File.ReadAllText(file);
            var header = SessionFile.HeaderOf(text);
            Assert.IsTrue(header.IsVersion2);
            Assert.AreEqual(host.SessionId, header.SessionId);
            Assert.IsTrue(Guid.TryParse(header.SessionId, out _));
            Assert.AreEqual(ProcSense.Core.Content.ContentManifestCheck.Version(UnityContentReader.ForThisPlatform()), header.ContentVersion);
            Assert.AreEqual(host.ContentVersion, header.ContentVersion);
            Assert.AreEqual(CoreInfo.Version, header.EngineVersion);
            var verdict = SessionVerifier.Verify(text, UnityContentReader.ForThisPlatform());
            Assert.IsTrue(verdict.Ok, verdict.Reason + ": " + verdict.Detail);   // what the service will do with it
        }

        [Test]
        public void Saving_again_replaces_the_same_file_and_a_new_session_gets_a_new_id()
        {
            var host = PlayedSession();
            string first = host.SaveSessionLog();
            host.Apply(new SimAction("ask_identity", "patient", Channel.Vr, new Dictionary<string, string>()));
            string second = host.SaveSessionLog();
            Assert.AreEqual(first, second);
            Assert.AreEqual(1, Directory.GetFiles(Path.GetDirectoryName(first)).Length);
            string oldId = host.SessionId;
            host.StartSession("VAR-S1-A", 42, SessionMode.Practice);
            Assert.AreNotEqual(oldId, host.SessionId);
        }

        [UnityTest]
        public IEnumerator Ending_a_session_queues_it_once_for_the_person_who_is_signed_in()
        {
            Assert.IsNotNull(SessionUploader.Instance, "the uploader starts with the app");
            yield return SignIn();
            var host = PlayedSession();
            string raised = null;
            host.SessionEnded += id => raised = id;
            string file = host.EndSession();
            Assert.IsNotNull(file);
            Assert.IsNull(host.EndSession(), "ending twice does nothing the second time");
            Assert.AreEqual(host.SessionId, raised);
            var entries = SessionUploader.Instance.Sync.Queue.Entries;
            Assert.AreEqual(1, entries.Count);
            Assert.AreEqual(host.SessionId, entries[0].sessionId);
            Assert.AreEqual(User, entries[0].userId);
            Assert.AreEqual(Org, entries[0].organisationId);
            Assert.AreEqual(file, entries[0].path);
        }

        [Test]
        public void A_session_ended_without_a_signed_in_person_is_saved_on_the_device_and_not_queued()
        {
            var host = PlayedSession();
            string file = host.EndSession();
            Assert.IsTrue(File.Exists(file));
            Assert.AreEqual(0, SessionUploader.Instance.Sync.Queue.Entries.Count);
        }

        [Test]
        public void A_session_with_no_actions_is_not_saved_or_queued()
        {
            var go = new GameObject("host");
            _made.Add(go);
            var host = go.AddComponent<SimulationHost>();
            host.AutoTick = false;
            host.SaveLogOnDestroy = false;
            host.StartSession("VAR-S1-A", 42, SessionMode.Practice);
            Assert.IsNull(host.EndSession());
        }
    }
}
