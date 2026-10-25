using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.Core.AIContext;
using ProcSense.Core.Content;
using ProcSense.VR.Auth;
using ProcSense.VR.Bridge;
using ProcSense.VR.Views;
using ProcSense.VR.Voice;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>
    /// The path from the trainee's words to the voices in the room (ADR-0026): the clip is encoded and recognised, the engine's context goes to the dialogue service (hidden facts never in the prompt part, small talk
    /// without a topic), the patient's and staff's replies are shown one at a time and spoken one at a time, and every failure leaves an answer. A fake transport and a fake player stand in for the service and the
    /// speakers; the engine session is the real one. The headset check (a real microphone, the real voice) is Sannan's.
    /// </summary>
    public class TeamConversationTests
    {
        private const string Base = "https://service.test";
        private const string Sb = "https://project.supabase.co";
        private const string Org = "22222222-2222-2222-2222-222222222222";
        private static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        private sealed class FakeHttp : IHttp
        {
            public readonly List<string> Urls = new List<string>();
            public readonly List<string> Bodies = new List<string>();
            public readonly List<int> ByteLengths = new List<int>();
            private readonly Dictionary<string, HttpResult> _answers = new Dictionary<string, HttpResult>();
            public FakeHttp Answer(string method, string url, long status, string body = "") { _answers[method + " " + url] = new HttpResult(status, body); return this; }
            public IEnumerator Send(string method, string url, string jsonBody, IDictionary<string, string> headers, int timeoutSeconds, Action<HttpResult> done)
            {
                Urls.Add(method + " " + url); Bodies.Add(jsonBody);
                done(_answers.TryGetValue(method + " " + url, out var r) ? r : new HttpResult(0, "", true));
                yield break;
            }
            public IEnumerator SendBytes(string method, string url, byte[] body, string contentType, IDictionary<string, string> headers, int timeoutSeconds, Action<HttpResult> done)
            {
                Urls.Add(method + " " + url); ByteLengths.Add(body.Length);
                done(_answers.TryGetValue(method + " " + url, out var r) ? r : new HttpResult(0, "", true));
                yield break;
            }
        }

        private sealed class FakePlayer : IClipPlayer
        {
            public readonly List<int> Played = new List<int>();
            public bool IsPlaying => false;
            public IEnumerator Play(byte[] oggVorbis, Action<bool> done) { Played.Add(oggVorbis.Length); done(true); yield break; }
        }

        private static void Run(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                if (!top.MoveNext()) { stack.Pop(); continue; }
                if (top.Current is IEnumerator inner) stack.Push(inner);
            }
        }

        private static AuthService SignedIn(FakeHttp http)
        {
            http.Answer("GET", Base + "/config.json", 200, "{\"supabase_url\":\"" + Sb + "\",\"supabase_publishable_key\":\"k\"}")
                .Answer("POST", Sb + "/auth/v1/token?grant_type=password", 200, "{\"access_token\":\"AT\",\"refresh_token\":\"RT\",\"expires_in\":3600}")
                .Answer("GET", Base + "/me", 200, "{\"user_id\":\"u\",\"pseudonym\":\"T-ab12cd34\",\"display_name\":\"\",\"memberships\":[{\"organisation_id\":\"" + Org + "\",\"organisation_name\":\"Demo\",\"slug\":\"demo\",\"role\":\"trainee\",\"status\":\"active\"}]}")
                .Answer("GET", Base + "/privacy/consent", 200, "{\"needs_consent\":false}");
            var service = new AuthService(http, Base);
            SignInResult result = null;
            Run(service.SignIn("jane@example.org", "pw", r => result = r));
            Assert.IsTrue(result.Ok, result.Message);
            return service;
        }

        private static SimulationHost NewHost(SessionMode mode = SessionMode.Practice)
        {
            var host = new GameObject("host").AddComponent<SimulationHost>();
            host.AutoTick = false;
            host.SaveLogOnDestroy = false;
            host.StartSession("VAR-S1-A", 42, mode, new CompositeContentReader(new FileContentReader(Path.Combine(Repo, "content")), new FileContentReader(Path.Combine(Repo, "tests", "fixtures"))));
            return host;
        }

        private static void Identify(SimulationHost host) { foreach (var v in new[] { "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) host.Apply(new SimAction(v)); }
        private static SimAction Ask(string topic) => new SimAction("ask", "", Channel.Vr, new Dictionary<string, string> { ["topic"] = topic });

        private const string Reply = "{\"replies\":[{\"role\":\"patient\",\"text\":\"No fever and my eyes are not yellow.\",\"intent\":\"answer_question\",\"source\":\"model\",\"fact_ids\":[\"h.red_flags\"],\"reasons\":[]}," +
            "{\"role\":\"nurse\",\"text\":\"I will check the observations now and tell you what I find.\",\"intent\":\"report_observation\",\"source\":\"model\",\"fact_ids\":[],\"reasons\":[]}," +
            "{\"role\":\"anaesthesia\",\"text\":\"I am on my way. Tell me what has changed.\",\"intent\":\"respond_to_escalation\",\"source\":\"model\",\"fact_ids\":[],\"reasons\":[]}],\"cloud\":true,\"model\":\"gemma4:31b-cloud\",\"ms\":812}";

        [Test]
        public void A_clip_is_cut_resampled_and_written_as_a_16_kilohertz_wav()
        {
            var samples = Enumerable.Range(0, 48000).Select(i => (float)Math.Sin(i / 20.0)).ToArray();          // one second at 48 kHz
            var wav = WavEncoder.Encode(samples, 48000);
            Assert.AreEqual("RIFF", System.Text.Encoding.ASCII.GetString(wav, 0, 4));
            Assert.AreEqual("WAVE", System.Text.Encoding.ASCII.GetString(wav, 8, 4));
            Assert.AreEqual(16000, BitConverter.ToInt32(wav, 24));
            Assert.AreEqual(44 + 16000 * 2, wav.Length);
            var tooLong = WavEncoder.Encode(new float[16000 * 40], 16000);
            Assert.AreEqual(44 + 16000 * 15 * 2, tooLong.Length);                                                  // the service refuses clips over 15 s
            Assert.AreEqual(0, WavEncoder.Encode(new float[0], 16000).Length);
            Assert.IsFalse(WavEncoder.HasSpeech(new float[16000], 16000));                                         // silence is not sent
            Assert.IsTrue(WavEncoder.HasSpeech(samples, 48000));
        }

        [Test]
        public void The_recogniser_answer_is_read_and_a_failure_is_the_safe_answer()
        {
            Assert.AreEqual("Where is the pain?", AsrClient.Parse("{\"ok\":true,\"fallback\":false,\"text\":\" Where is the pain? \",\"low_confidence\":false}").Text);
            Assert.IsFalse(AsrClient.Parse("{\"ok\":true,\"fallback\":false,\"text\":\"mumble\",\"low_confidence\":true}").Usable);
            Assert.IsTrue(AsrClient.Parse("{\"ok\":false,\"fallback\":true,\"error\":\"ASR_UNAVAILABLE\"}").Unavailable);
            Assert.IsTrue(AsrClient.Parse("not json").Unavailable);
            var http = new FakeHttp();
            var auth = SignedIn(http);
            http.Answer("POST", Base + "/asr/organisations/" + Org + "/transcribe", 200, "{\"ok\":true,\"fallback\":false,\"text\":\"Do you have any allergies?\",\"low_confidence\":false}");
            AsrResult heard = null;
            Run(AsrClient.Transcribe(auth, WavEncoder.Encode(new float[16000], 16000), r => heard = r));
            Assert.AreEqual("Do you have any allergies?", heard.Text);
            Assert.AreEqual(1, http.ByteLengths.Count);                                                            // the clip went as bytes
            AsrResult offline = null;
            Run(AsrClient.Transcribe(null, new byte[10], r => offline = r));
            Assert.IsTrue(offline.Unavailable);
        }

        [Test]
        public void The_request_holds_the_released_facts_for_the_prompt_and_marks_the_others_so_that_they_are_only_checked_against()
        {
            var host = NewHost();
            try
            {
                Identify(host);
                string body = TeamDialogueClient.RequestBody(host, "Do you have any red flag symptoms?", "red_flags", new List<ContextTurn>());
                StringAssert.Contains("\"asked_fact\":\"h.red_flags\"", body);
                StringAssert.Contains("\"authored\":{\"h.red_flags\":\"No fever, no yellow eyes", body);
                var released = body.Split(new[] { "{\"id\":" }, StringSplitOptions.RemoveEmptyEntries).Where(x => x.Contains("\"disclosable_now\":true")).ToList();
                Assert.IsTrue(released.Any(x => x.Contains("h.red_flags")) && released.Any(x => x.Contains("h.complaint")));          // the earned fact and the volunteered complaint
                Assert.IsFalse(released.Any(x => x.Contains("h.medicines") || x.Contains("h.smoking")));                              // anything else is marked not usable
                StringAssert.Contains("\"staff_roles\":[\"nurse\",\"anaesthesia\"]", body);
                StringAssert.Contains("\"sex\":\"F\"", body);
                string smallTalk = TeamDialogueClient.RequestBody(host, "Hello, how are you feeling?", null, new List<ContextTurn>());
                Assert.IsFalse(smallTalk.Contains("asked_fact"));
                Assert.IsFalse(smallTalk.Split(new[] { "{\"id\":" }, StringSplitOptions.RemoveEmptyEntries).Any(x => x.Contains("h.red_flags") && x.Contains("\"disclosable_now\":true")));      // nothing was released by small talk
                host.Apply(Ask("medicines"));
                string again = TeamDialogueClient.RequestBody(host, "And your tablets?", null, new List<ContextTurn>());
                Assert.IsTrue(again.Split(new[] { "{\"id\":" }, StringSplitOptions.RemoveEmptyEntries).Any(x => x.Contains("h.medicines") && x.Contains("\"disclosable_now\":true")));      // what the trainee has learned can be said again
            }
            finally { UnityEngine.Object.DestroyImmediate(host.gameObject); }
        }

        [Test]
        public void The_service_answer_is_read_in_speaking_order_and_a_bad_or_missing_answer_gives_the_authored_line_or_not_sure()
        {
            var answer = TeamDialogueClient.Parse(Reply);
            CollectionAssert.AreEqual(new[] { "patient", "nurse", "anaesthesia" }, answer.Replies.Select(r => r.Role));
            Assert.IsTrue(answer.Cloud);
            Assert.IsNull(TeamDialogueClient.Parse("{\"replies\":[]}"));
            Assert.IsNull(TeamDialogueClient.Parse("nope"));
            var host = NewHost();
            try
            {
                Identify(host);
                Assert.AreEqual(TeamDialogueClient.NotSure, TeamDialogueClient.Offline(host, "red_flags", "NO_NETWORK").Replies[0].Text);       // not released yet: the patient does not know
                host.Apply(Ask("red_flags"));
                StringAssert.StartsWith("No fever", TeamDialogueClient.Offline(host, "red_flags", "NO_NETWORK").Replies[0].Text);              // released: the authored answer
                Assert.AreEqual(1, TeamDialogueClient.Offline(host, null, "x").Replies.Count);                                                  // no staff without the service
            }
            finally { UnityEngine.Object.DestroyImmediate(host.gameObject); }
        }

        [Test]
        public void A_question_goes_to_the_service_once_and_the_three_voices_are_shown_and_spoken_one_at_a_time()
        {
            var http = new FakeHttp();
            var auth = SignedIn(http);
            http.Answer("POST", Base + "/dialogue/organisations/" + Org + "/team", 200, Reply);
            var line = Convert.ToBase64String(new byte[] { 1, 2, 3, 4 });
            string speak = Base + "/tts/organisations/" + Org + "/speak";
            http.Answer("POST", speak, 200, "{\"text\":\"a\",\"audio_b64\":\"" + line + "\"}\n{\"text\":\"b\",\"audio_b64\":\"" + line + "\"}\n");
            var host = NewHost();
            var go = new GameObject("conversation");
            go.SetActive(false);
            try
            {
                Identify(host);
                var voices = go.AddComponent<TeamVoicesView>();
                var player = new FakePlayer();
                var talk = go.AddComponent<TeamConversation>();
                talk.Voices = voices; talk.Player = player; talk.Auth = () => auth; talk.Host = () => host; talk.SpeakAloud = false;           // the speaking is tested on its own below
                go.SetActive(true);
                var heard = new List<string>();
                voices.Spoke += (role, text, voice) => heard.Add(role + ":" + voice);
                Run(talk.Run("Do you have any red flag symptoms?", "red_flags"));
                Assert.AreEqual(1, http.Urls.Count(u => u.EndsWith("/team")));                                  // one request for the whole turn
                StringAssert.Contains("gemma4", talk.Last.Model);
                voices.Step(0f);
                voices.Step(TeamVoicesView.SecondsPerLine);
                CollectionAssert.AreEqual(new[] { "patient:voice.patient", "nurse:voice.nurse" }, heard);
                voices.Step(TeamVoicesView.SecondsPerLine);
                CollectionAssert.AreEqual(new[] { "patient:voice.patient", "nurse:voice.nurse", "anaesthesia:voice.anaesthetist" }, heard);
                Assert.AreEqual(0, voices.Pending);
                Run(talk.Run("x", null)); // a second turn is a second request, not a replay
                Assert.AreEqual(2, http.Urls.Count(u => u.EndsWith("/team")));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(host.gameObject); }
        }

        [Test]
        public void Every_spoken_line_is_fetched_from_the_voice_service_with_its_own_voice_and_played()
        {
            var http = new FakeHttp();
            var auth = SignedIn(http);
            http.Answer("POST", Base + "/dialogue/organisations/" + Org + "/team", 200, Reply);
            var line = Convert.ToBase64String(new byte[] { 9, 9, 9 });
            http.Answer("POST", Base + "/tts/organisations/" + Org + "/speak", 200, "{\"text\":\"s\",\"audio_b64\":\"" + line + "\"}\n");
            var host = NewHost();
            var go = new GameObject("conversation");
            go.SetActive(false);
            try
            {
                Identify(host);
                var voices = go.AddComponent<TeamVoicesView>();
                var player = new FakePlayer();
                var talk = go.AddComponent<TeamConversation>();
                talk.Voices = voices; talk.Player = player; talk.Auth = () => auth; talk.Host = () => host;
                go.SetActive(true);
                Run(talk.Run("Do you have any red flag symptoms?", "red_flags"));
                var speaking = typeof(TeamConversation).GetMethod("SpeakLine", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Run((IEnumerator)speaking.Invoke(talk, new object[] { "patient", "No fever and my eyes are not yellow." }));
                Assert.AreEqual(1, player.Played.Count);
                Assert.IsTrue(http.Bodies.Any(b => b != null && b.Contains("\"role\":\"patient_female\"")));     // the 42 year old woman's voice
                Run((IEnumerator)speaking.Invoke(talk, new object[] { "anaesthesia", "I am on my way." }));
                Assert.IsTrue(http.Bodies.Any(b => b != null && b.Contains("\"role\":\"anaesthetist\"")));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(host.gameObject); }
        }

        [Test]
        public void With_the_service_down_the_patient_still_answers_and_nothing_throws()
        {
            var http = new FakeHttp();
            var auth = SignedIn(http);                                          // no answer is scripted for the dialogue route: a network error
            var host = NewHost();
            var go = new GameObject("conversation");
            go.SetActive(false);
            try
            {
                Identify(host);
                host.Apply(Ask("red_flags"));
                var voices = go.AddComponent<TeamVoicesView>();
                var talk = go.AddComponent<TeamConversation>();
                talk.Voices = voices; talk.Player = new FakePlayer(); talk.Auth = () => auth; talk.Host = () => host; talk.SpeakAloud = false;
                go.SetActive(true);
                Run(talk.Run("Do you have any red flag symptoms?", "red_flags"));
                Assert.IsFalse(talk.Last.FromService);
                StringAssert.StartsWith("No fever", talk.Last.Replies[0].Text);
                voices.Step(0f);
                Assert.AreEqual("patient", voices.SpokenRoles[0]);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(host.gameObject); }
        }

        [Test]
        public void An_engine_ask_in_practice_starts_a_turn_and_assessment_stays_silent()
        {
            var http = new FakeHttp();
            var auth = SignedIn(http);
            http.Answer("POST", Base + "/dialogue/organisations/" + Org + "/team", 200, Reply);
            var host = NewHost();
            var go = new GameObject("conversation");
            go.SetActive(false);
            try
            {
                Identify(host);
                var talk = go.AddComponent<TeamConversation>();
                talk.Voices = go.AddComponent<TeamVoicesView>(); talk.Player = new FakePlayer(); talk.Auth = () => auth; talk.Host = () => host;
                go.SetActive(true);
                var action = Ask("red_flags");
                var outcome = host.Apply(action);
                Assert.IsTrue(outcome.Accepted);
                Assert.AreEqual("Can you tell me about your red flags?", TeamConversation.QuestionFor("red_flags"));
                // the same call the host's Applied event makes; the coroutine itself is run by hand in this editor test
                var before = talk.RequestsSent;
                talk.OnApplied(new SimAction("introduce_self"), outcome);                                       // another verb: nothing
                Assert.AreEqual(before, talk.RequestsSent);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(host.gameObject); }
        }

        [Test]
        public void An_assessment_session_gets_no_voices_and_no_request()
        {
            var http = new FakeHttp();
            var auth = SignedIn(http);
            http.Answer("POST", Base + "/dialogue/organisations/" + Org + "/team", 200, Reply);
            var host = NewHost();
            var content = ContentLoader.Load(new FileContentReader(Path.Combine(Repo, "content")), "VAR-S1-A");
            var assessment = new Session(content.Cases["VAR-S1-A"], 1, SessionMode.Assessment, ProcSense.Core.Workflow.Subsystems.Default(content));
            var go = new GameObject("conversation");
            go.SetActive(false);
            try
            {
                ModeGate.SessionProvider = () => assessment;
                var talk = go.AddComponent<TeamConversation>();
                talk.Voices = go.AddComponent<TeamVoicesView>(); talk.Auth = () => auth; talk.Host = () => host;
                go.SetActive(true);
                Run(talk.Run("Do you have any allergies?", null));
                Assert.AreEqual(0, talk.RequestsSent);
                Assert.IsFalse(http.Urls.Any(u => u.EndsWith("/team")));
                StringAssert.Contains("off", talk.Status);
            }
            finally
            {
                ModeGate.SessionProvider = () => SimulationHost.Instance != null ? SimulationHost.Instance.Session : null;
                UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(host.gameObject);
            }
        }
    }
}
