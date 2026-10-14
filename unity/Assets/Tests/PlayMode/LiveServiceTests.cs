using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.VR.Auth;
using ProcSense.VR.Bridge;
using ProcSense.VR.Sync;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// The whole chain over a real network connection, with the real UnityWebRequest (p1-backend-s03): sign in, play, end the session, upload it, wait for the service's engine to verify it and read the result back.
    /// It needs a running service and an account, given by environment variables, and is skipped without them (it is not part of the normal run):
    ///   PROCSENSE_E2E_URL (for example http://127.0.0.1:8767), PROCSENSE_E2E_EMAIL, PROCSENSE_E2E_PASSWORD.
    /// A local copy of the service with sample people is started by `python -m server.tests.browser_stack`; the live service works too with a real account that has agreed to the privacy choices.
    /// </summary>
    [Category("Live")]
    public class LiveServiceTests
    {
        [UnityTest]
        public IEnumerator A_session_played_here_is_uploaded_verified_by_the_service_and_read_back()
        {
            string url = Environment.GetEnvironmentVariable("PROCSENSE_E2E_URL");
            string email = Environment.GetEnvironmentVariable("PROCSENSE_E2E_EMAIL");
            string password = Environment.GetEnvironmentVariable("PROCSENSE_E2E_PASSWORD");
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password)) Assert.Ignore("set PROCSENSE_E2E_URL, PROCSENSE_E2E_EMAIL and PROCSENSE_E2E_PASSWORD to run this against a real service");

            string dir = Path.Combine(Path.GetTempPath(), "procsense-live-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            SimulationHost.SessionsDirectoryOverride = Path.Combine(dir, "sessions");
            var originalSync = SessionUploader.Instance != null ? SessionUploader.Instance.Sync : null;
            if (SimulationHost.Instance != null) { SimulationHost.Instance.SaveLogOnDestroy = false; UnityEngine.Object.DestroyImmediate(SimulationHost.Instance.gameObject); }
            var host = new GameObject("host").AddComponent<SimulationHost>();
            try
            {
                // 1. sign in through the real service and the real account system
                var auth = new AuthService(new UnityHttp(), url, 100);
                SignInResult signedIn = null;
                yield return auth.SignIn(email, password, r => signedIn = r);
                Assert.IsTrue(signedIn.Ok, signedIn.Message);
                Assert.AreSame(auth, AuthService.Current);
                SessionUploader.Instance.Sync = new SessionSync(new UploadQueue(Path.Combine(dir, "outbox"), SessionUploader.UnixNow), () => AuthService.Current, SessionUploader.UnixNow);

                // 2. play a little and end the session
                host.AutoTick = false;
                host.SaveLogOnDestroy = false;
                host.StartSession("VAR-S1-A", 42, SessionMode.Practice);
                foreach (var verb in new[] { "introduce_self", "ask_identity", "read_wristband", "open_notes" })
                    host.Apply(new SimAction(verb, "patient", Channel.Vr, new Dictionary<string, string>()));
                Assert.IsNotNull(host.EndSession());

                // 3. the uploader sends it and the service confirms; 4. the service's engine verifies it; 5. the person's results show it
                string sessionId = host.SessionId;
                float limit = Time.realtimeSinceStartup + 120f;
                ResultsView view = null;
                while (Time.realtimeSinceStartup < limit)
                {
                    yield return SessionUploader.Instance.Sync.Process();
                    yield return ResultsClient.Fetch(auth, 10, v => view = v);
                    if (SessionUploader.Instance.Sync.Queue.Entries.Count == 0 && view != null && view.Ok && !view.AnyPending && view.Lines.Count > 0) break;
                    yield return new WaitForSecondsRealtime(2f);
                }
                Assert.AreEqual(0, SessionUploader.Instance.Sync.Queue.Entries.Count, "the service confirmed the session, so the queue is empty");
                Assert.IsTrue(view != null && view.Ok, view != null ? view.Error : "no answer");
                Assert.IsFalse(view.AnyPending, "the service's engine did not finish checking the session in time: " + view.Text);
                StringAssert.Contains("VAR-S1-A", view.Text);
                StringAssert.Contains("Not finished", view.Lines[0], "the newest session is the one just played: verified, and the case was not finished");
                StringAssert.Contains("(provisional)", view.Lines[0]);
                Debug.Log("[live] session " + sessionId + " verified by the service: " + view.Lines[0]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host.gameObject);
                SimulationHost.SessionsDirectoryOverride = null;
                if (SessionUploader.Instance != null && originalSync != null) SessionUploader.Instance.Sync = originalSync;
                AuthService.Clear();
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }
}
