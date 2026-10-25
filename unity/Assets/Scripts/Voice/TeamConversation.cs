using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.AIContext;
using ProcSense.VR.Auth;
using ProcSense.VR.Bridge;
using ProcSense.VR.Views;
using UnityEngine;

namespace ProcSense.VR.Voice
{
    /// <summary>Plays one recorded sentence. A seam so the conversation is tested without audio hardware; <see cref="UnityClipPlayer"/> is the real one.</summary>
    public interface IClipPlayer
    {
        bool IsPlaying { get; }
        IEnumerator Play(byte[] oggVorbis, Action<bool> done);
    }

    /// <summary>
    /// One trainee turn from the question to the voices (plan p2-ai-advanced-simulation-s03, ADR-0026): the question (typed on the tablet, chosen from the history menu, or the words the recogniser heard) goes to the dialogue
    /// service once; the patient's and the staff's replies are shown as subtitles by <see cref="TeamVoicesView"/> one after another, and each line is spoken by the voice service while the next waits for it. The engine decides
    /// what the patient may say (<see cref="TeamDialogueClient"/> sends only what it released); this class applies no action, credits nothing, and never blocks a frame: every request is a coroutine, a slow or failed call
    /// leaves the patient's authored line or "I am not sure", and a new question drops the one before. Practice only: an assessment session uses its approved authored lines and this class stays silent.
    /// </summary>
    public sealed class TeamConversation : MonoBehaviour
    {
        public TeamVoicesView Voices;
        public IClipPlayer Player;
        /// <summary>Replaced by tests.</summary>
        public Func<AuthService> Auth = () => AuthService.Current;
        public Func<SimulationHost> Host = () => SimulationHost.Instance;
        /// <summary>The voice output can be switched off (subtitles only).</summary>
        public bool SpeakAloud = true;
        /// <summary>React to the history questions the trainee asks from the tablet menu (the engine's <c>ask</c> action), so the patient answers in a voice.</summary>
        public bool AnswerMenuQuestions = true;
        /// <summary>Set by <see cref="SpokenActionRouter"/> while it starts a spoken action, so the tablet-style reaction below does not answer a second time.</summary>
        public bool SuppressAuto;

        public IReadOnlyList<ContextTurn> Turns => _turns;
        public TeamAnswer Last { get; private set; }
        public string Status { get; private set; } = "";
        public int RequestsSent { get; private set; }
        public bool Speaking { get; private set; }

        private readonly List<ContextTurn> _turns = new List<ContextTurn>();
        private int _generation;
        private SimulationHost _subscribed;

        private void OnEnable() { if (Voices != null) { Voices.Spoke += OnSpoke; Voices.Busy = () => Speaking; } }
        private void OnDisable() { if (Voices != null) { Voices.Spoke -= OnSpoke; Voices.Busy = null; } UnsubscribeHost(); }

        private void Update()
        {
            var host = Host();
            if (host != null && host != _subscribed) { UnsubscribeHost(); host.Applied += OnApplied; _subscribed = host; }
        }

        private void UnsubscribeHost() { if (_subscribed != null) _subscribed.Applied -= OnApplied; _subscribed = null; }

        /// <summary>The engine released a history fact because the trainee asked about the topic: the patient answers aloud.</summary>
        public void OnApplied(SimAction action, ActionOutcome outcome)
        {
            if (SuppressAuto || !AnswerMenuQuestions || action == null || outcome == null || action.Verb != "ask" || !outcome.Accepted || !ModeGate.PracticeAids) return;
            string topic = outcome.Effects.Where(e => e.StartsWith("answered:")).Select(e => e.Substring("answered:".Length)).FirstOrDefault();
            if (topic == null) return;
            Ask(QuestionFor(topic), topic);
        }

        public static string QuestionFor(string topic) => "Can you tell me about your " + topic.Replace('_', ' ') + "?";

        /// <summary>Starts a turn. <paramref name="askedTopic"/> is the history topic the engine released for the question, or null.</summary>
        public void Ask(string question, string askedTopic)
        {
            if (string.IsNullOrWhiteSpace(question)) return;
            StartCoroutine(Run(question.Trim(), askedTopic));
        }

        public IEnumerator Run(string question, string askedTopic)
        {
            var host = Host();
            if (host == null || host.Session == null || !ModeGate.PracticeAids) { Status = "The voices are off in this session."; yield break; }
            int mine = ++_generation;                                        // a newer question makes this one stale
            Status = "Asking...";
            RequestsSent++;
            var turns = _turns.ToList();
            _turns.Add(new ContextTurn("trainee", question));
            TeamAnswer answer = null;
            yield return TeamDialogueClient.Ask(Auth(), host, question, askedTopic, turns, a => answer = a);
            if (mine != _generation) yield break;                            // the trainee has already said something else
            Last = answer;
            foreach (var r in answer.Replies)
                if (r.Role == "nurse" && r.Intent == "report_observation") r.Text = ObservationReport.Build(host.Session.Observable);        // the nurse's numbers are the engine's, never a model's
            foreach (var r in answer.Replies) _turns.Add(new ContextTurn(r.Role, r.Text));
            while (_turns.Count > 12) _turns.RemoveAt(0);
            Status = answer.FromService ? (answer.Cloud ? "Answered (cloud model)." : "Answered (local model).") : "Answered without the service (" + answer.Reason + ").";
            if (Voices != null) Voices.Offer(answer.Replies);
        }

        /// <summary>Speaks a line the engine itself produced (the patient's authored answer to an action that is not a history topic) through the same subtitle and voice path. The turn is remembered.</summary>
        public void Say(string role, string text, string intent = "answer_question")
        {
            if (string.IsNullOrWhiteSpace(text) || Voices == null) return;
            _generation++;
            _turns.Add(new ContextTurn(role, text));
            Last = new TeamAnswer { Replies = { new TeamReply(role, text) { Intent = intent, Source = "engine" } }, Reason = "ENGINE" };
            Voices.Offer(Last.Replies);
        }

        /// <summary>Remember what the trainee said when no turn follows it (a spoken action the engine refused, or a suggestion waiting for a yes).</summary>
        public void Remember(string speaker, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            _turns.Add(new ContextTurn(speaker, text));
            while (_turns.Count > 12) _turns.RemoveAt(0);
        }

        private void OnSpoke(string role, string text, string voice)
        {
            if (!SpeakAloud || Player == null) return;
            StartCoroutine(SpeakLine(role, text));
        }

        private IEnumerator SpeakLine(string role, string text)
        {
            int mine = _generation;
            Speaking = true;
            List<TtsLine> lines = null;
            string sex = "M";
            var host = Host();
            if (host != null && host.Session != null && host.Content != null && host.Content.CaseFiles.TryGetValue(host.Session.Case.Id, out var file) && !string.IsNullOrEmpty(file.Identity.Sex)) sex = file.Identity.Sex.Substring(0, 1);
            yield return TtsClient.Speak(Auth(), text, TtsClient.VoiceFor(role, sex), l => lines = l);
            if (lines != null && mine == _generation)
                foreach (var line in lines)
                {
                    if (mine != _generation) break;
                    yield return Player.Play(line.Audio, ok => { });
                }
            Speaking = false;
        }
    }

    /// <summary>The real player: decodes the Ogg Vorbis bytes from a temporary file and plays them on an AudioSource.</summary>
    public sealed class UnityClipPlayer : MonoBehaviour, IClipPlayer
    {
        public AudioSource Source;
        public bool IsPlaying => Source != null && Source.isPlaying;
        /// <summary>While the trainee is recording the speaker is silent (the microphone would hear it).</summary>
        public bool Muted { get; private set; }
        public void SetMuted(bool muted) { Muted = muted; if (Source != null) Source.mute = muted; }

        public IEnumerator Play(byte[] oggVorbis, Action<bool> done)
        {
            if (oggVorbis == null || oggVorbis.Length == 0) { done(false); yield break; }
            if (Source == null) Source = gameObject.AddComponent<AudioSource>();
            string path = System.IO.Path.Combine(Application.temporaryCachePath, "tts_" + oggVorbis.Length + "_" + oggVorbis[oggVorbis.Length / 2] + ".ogg");
            System.IO.File.WriteAllBytes(path, oggVorbis);
            using (var request = UnityEngine.Networking.UnityWebRequestMultimedia.GetAudioClip("file://" + path, AudioType.OGGVORBIS))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityEngine.Networking.UnityWebRequest.Result.Success) { done(false); yield break; }
                Source.clip = UnityEngine.Networking.DownloadHandlerAudioClip.GetContent(request);
                Source.mute = Muted;
                Source.Play();
                while (Source.isPlaying) yield return null;
            }
            done(true);
        }
    }
}
