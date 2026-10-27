using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.VR.Auth;
using ProcSense.VR.Bridge;
using ProcSense.VR.Views;
using UnityEngine;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// Draws the tablet's tabs to picture files so a person can look at the layout without a headset: artifacts/unity/tablet-*.png (git-ignored).
    /// It only checks that the pictures were made and are not blank; the point is for someone to open them.
    /// </summary>
    public class TabletSnapshotTests
    {
        private const string Base = "https://service.test";
        private const string Sb = "https://project.supabase.co";
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

        [UnityTest]
        public IEnumerator The_tablet_tabs_are_drawn_to_pictures_for_a_person_to_look_at()
        {
            var http = new ScriptedHttp()
                .Answer("GET", Base + "/config.json", 200, "{\"supabase_url\":\"" + Sb + "\",\"supabase_publishable_key\":\"k\"}")
                .Answer("POST", Sb + "/auth/v1/token?grant_type=password", 200, "{\"access_token\":\"AT\",\"refresh_token\":\"RT\",\"expires_in\":3600}")
                .Answer("GET", Base + "/me", 200, "{\"user_id\":\"u1\",\"pseudonym\":\"T-ab12cd34\",\"display_name\":\"\",\"memberships\":[{\"organisation_id\":\"" + Org + "\",\"organisation_name\":\"ProcSense Demo\",\"slug\":\"demo\",\"role\":\"trainee\",\"status\":\"active\"}]}")
                .Answer("GET", Base + "/privacy/consent", 200, "{\"needs_consent\":false}")
                .Answer("GET", Base + "/organisations/" + Org + "/sessions?status=any&limit=5", 200,
                    "{\"total\":3,\"limit\":5,\"offset\":0,\"items\":["
                    + "{\"session_id\":\"a\",\"case_id\":\"VAR-S1-E\",\"mode\":\"practice\",\"status\":\"verified\",\"result\":\"SUCCESS\",\"process_score\":2.0,\"received_at\":\"2026-10-14T10:34:39Z\"},"
                    + "{\"session_id\":\"b\",\"case_id\":\"VAR-S1-A\",\"mode\":\"practice\",\"status\":\"pending\",\"result\":null,\"process_score\":null,\"received_at\":\"2026-10-14T11:02:00Z\"},"
                    + "{\"session_id\":\"c\",\"case_id\":\"VAR-S2-B\",\"mode\":\"assessment\",\"status\":\"verified\",\"result\":\"FAIL\",\"process_score\":-3.5,\"received_at\":\"2026-10-13T08:00:00Z\"}]}");
            SignInResult result = null;
            yield return new AuthService(http, Base).SignIn("jane@example.org", "pw", r => result = r);
            Assert.IsTrue(result.Ok, result.Message);
            if (SimulationHost.Instance != null) { SimulationHost.Instance.SaveLogOnDestroy = false; UnityEngine.Object.DestroyImmediate(SimulationHost.Instance.gameObject); }
            var host = new GameObject("host").AddComponent<SimulationHost>();
            host.AutoTick = false; host.SaveLogOnDestroy = false;
            host.StartSession("VAR-S1-A", 42, SessionMode.Practice);
            host.Apply(new SimAction("introduce_self", "patient", Channel.Vr, new Dictionary<string, string>()));
            var tabletGo = new GameObject("tablet");
            var tablet = tabletGo.AddComponent<TabletView>();
            tablet.Build();
            try
            {
                yield return null;
                tablet.ShowTab(0);
                yield return null;
                Snapshots.Photograph(tablet.Canvas, Snapshots.Out("tablet-identity.png"));
                tablet.ShowTab("assess");
                yield return null;
                Snapshots.Photograph(tablet.Canvas, Snapshots.Out("tablet-assess.png"));
                tablet.ShowTab("checklist");
                yield return null;
                Snapshots.Photograph(tablet.Canvas, Snapshots.Out("tablet-checklist.png"));
                tablet.ShowTab("handover");
                tablet.Buttons.Single(b => b.label == "Escalate (SBAR)").button.onClick.Invoke();
                yield return null;
                Snapshots.Photograph(tablet.Canvas, Snapshots.Out("tablet-form-sbar.png"));
                tablet.ShowTab("orders");
                tablet.Buttons.Single(b => b.label == "Request transport").button.onClick.Invoke();
                yield return null;
                Snapshots.Photograph(tablet.Canvas, Snapshots.Out("tablet-form-transport.png"));
                tablet.ShowTab(TabletView.SessionTabId);
                yield return null;
                yield return tablet.SessionTab.RefreshResults();
                yield return null;
                Snapshots.Photograph(tablet.Canvas, Snapshots.Out("tablet-session.png"));
                Assert.Greater(new FileInfo(Snapshots.Out("tablet-session.png")).Length, 20000, "the picture is not blank");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tabletGo);
                UnityEngine.Object.DestroyImmediate(host.gameObject);
                AuthService.Clear();
            }
        }
    }
}
