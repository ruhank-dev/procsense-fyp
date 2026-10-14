using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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

        private static string Out(string name)
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "artifacts", "unity"));
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, name);
        }

        private static void Photograph(Canvas canvas, string file)
        {
            var camGo = new GameObject("snapshot camera");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.03f, 0.05f);
            var rect = (RectTransform)canvas.transform;
            float width = rect.sizeDelta.x * rect.localScale.x, height = rect.sizeDelta.y * rect.localScale.y;
            camGo.transform.position = canvas.transform.position - canvas.transform.forward * 1f;   // the canvas is centred on its own position
            camGo.transform.rotation = canvas.transform.rotation;
            cam.orthographicSize = height / 2f * 1.04f;
            cam.aspect = width / height;
            var texture = new RenderTexture(1800, 1200, 24);
            cam.targetTexture = texture;
            cam.Render();
            cam.targetTexture = null;
            RenderTexture.active = texture;
            var picture = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            picture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            picture.Apply();
            File.WriteAllBytes(file, picture.EncodeToPNG());
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(picture);
            UnityEngine.Object.DestroyImmediate(camGo);
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
                Photograph(tablet.Canvas, Out("tablet-identity.png"));
                tablet.ShowTab(4);
                yield return null;
                yield return tablet.SessionTab.RefreshResults();
                yield return null;
                Photograph(tablet.Canvas, Out("tablet-session.png"));
                Assert.Greater(new FileInfo(Out("tablet-session.png")).Length, 20000, "the picture is not blank");
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
