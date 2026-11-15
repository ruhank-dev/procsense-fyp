using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.AIContext;
using ProcSense.Core.Observation;
using ProcSense.VR.Auth;
using ProcSense.VR.Bridge;
using ProcSense.VR.Flow;
using ProcSense.VR.Views;
using UnityEngine;

namespace ProcSense.VR.Voice
{
    /// <summary>What the nurse says when asked for the observations: the readings the monitor shows now, taken from the engine's state. No model writes a number.</summary>
    public static class ObservationReport
    {
        public static string Build(ObservableState state)
        {
            if (state == null || state.Vitals == null || state.Vitals.Count == 0) return "There are no readings on the monitor yet.";
            return "On the monitor now: " + string.Join(", ", state.Vitals.Select(v => v.Name + " " + v.Value)) + ".";
        }
    }

    /// <summary>
    /// The path from the words the trainee said to the simulator (DT-251, ADR-0027): the recogniser's text goes to the interpreter with the tablet actions enabled now; the actions it names are started with
    /// <see cref="TabletView.PressId"/>, the same call a button press makes, so the engine decides and scores a spoken action exactly as a tapped one; the patient or the staff then answer in the voice path
    /// (<see cref="TeamConversation"/>), and the existing Gemma speech evaluation (ADR-0025) judges the same sentence for communication credit, unchanged. Safeguards: a repeated recognition of the same sentence is
    /// handled once; physical and critical actions are not in the interpreter's list at all; a local model only proposes and the trainee confirms (<see cref="Suggestions"/>); the engine's refusal is shown, not
    /// answered by a patient; nothing here awards credit or skips an engine rule. Works in practice and in the examination (D3, P3-VOX-06): the examination keeps every confirmation and shows no hint text (<see cref="SpokenExamRules"/>).
    /// </summary>
    public sealed class SpokenActionRouter : MonoBehaviour
    {
        public const float RepeatWindowSeconds = 4f;
        public TabletView Tablet;
        public TeamConversation Talk;
        /// <summary>The spoken medical orders (ADR-0028): asked only about a sentence that started no tablet action and was not addressed to the patient, so an ordinary sentence costs no second model call. A proposal waits for the trainee's yes before anything reaches the engine.</summary>
        public SpokenOrderFlow Orders;
        /// <summary>The menu flow the hint and submit buttons use: the spoken phrases "hint" and "submit the patient" go through it, so the engine request, its limit and its cost are the buttons' (P3-VOX-08/06). Submitting is armed by the first phrase and sent by the second.</summary>
        public MenuFlow Flow;
        public Func<AuthService> Auth = () => AuthService.Current;
        public Func<SimulationHost> Host = () => SimulationHost.Instance;
        /// <summary>Replaced by tests; the real one asks the service.</summary>
        public Func<AuthService, string, IEnumerable<string>, IReadOnlyList<ContextTurn>, Action<SpokenActionResult>, IEnumerator> Interpreter =
            (auth, text, ids, turns, done) => SpokenActionClient.Interpret(auth, text, ids, turns, done);
        /// <summary>Judges the sentence for communication credit (ADR-0025). Replaced by tests.</summary>
        public Func<AuthService, SimulationHost, string, string, IReadOnlyList<ContextTurn>, string, Action<ProcSense.VR.Voice.EvaluateResult>, IEnumerator> Evaluator;
        public bool EvaluateSpeech = true;
        /// <summary>The voice feedback table (P3-VOX-07); null uses <see cref="VoiceFeedback.Loaded"/>. Replaced by tests.</summary>
        public VoiceFeedback FeedbackTable;
        /// <summary>Raised with every feedback phrase (subtitle and spoken words) for an outcome that is not an accepted action.</summary>
        public event Action<VoiceFeedbackLine> FeedbackGiven;
        /// <summary>The last feedback phrase given, or null.</summary>
        public VoiceFeedbackLine LastFeedback { get; private set; }

        public string Status { get; private set; } = "";
        public IReadOnlyList<string> Suggestions => _suggestions;
        public string SuggestedText { get; private set; } = "";
        public int Handled { get; private set; }
        public IReadOnlyList<string> LastStarted => _started;

        private readonly List<string> _suggestions = new List<string>();
        private readonly List<string> _started = new List<string>();
        private readonly SpeechEvaluation _evaluation = new SpeechEvaluation();
        private string _lastText = "";
        private float _lastAt = -100f;
        private int _utterance;

        /// <summary>The recogniser heard this. A second event with the same words inside the window (a double event, a retry) is ignored.</summary>
        public bool OnTranscribed(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string key = text.Trim().ToLowerInvariant();
            if (key == _lastText && Time.realtimeSinceStartup - _lastAt < RepeatWindowSeconds) return false;
            _lastText = key;
            _lastAt = Time.realtimeSinceStartup;
            StartCoroutine(Route(text.Trim()));
            return true;
        }

        /// <summary>A pending order proposal is answered first ("yes", "no"); any other sentence goes the ordinary way and may start a new proposal.</summary>
        public IEnumerator Route(string text)
        {
            if (_suggestions.Count > 0 && AnswerSuggestionBySpeech(text)) yield break;
            if (Orders != null && Orders.Pending)
            {
                bool answered = false;
                yield return Orders.AnswerBySpeech(text, c => answered = c);
                if (answered) { Status = "Heard: " + text; yield break; }
            }
            if (Flow == null) Flow = MenuFlow.Current;                                 // wired lazily: the flow may start after the panels are installed
            if (Flow != null && FlowVoiceIntents.Classify(text) != FlowVoiceIntents.Intent.None)
            {
                Status = HandleFlowIntent(text);
                yield break;
            }
            yield return Handle(text);
        }

        private string HandleFlowIntent(string text)
        {
            var host = Host();
            if (host == null || !SpokenExamRules.Allowed(host.Session)) return "No session is running.";
            var intent = FlowVoiceIntents.Classify(text);
            if (intent == FlowVoiceIntents.Intent.Hint)
            {
                var outcome = Flow.Hint();
                if (!outcome.Accepted) { Give(host, VoiceFeedback.EngineRejected, SpokenExamRules.RefusalText(host.Session, outcome.RejectReason)); return "Hint: " + SpokenExamRules.RefusalText(host.Session, outcome.RejectReason) + "."; }
                var given = host.Session.Hints;
                return "Hint: " + (given.Count > 0 ? SpokenExamRules.HintToShow(host.Session, given[given.Count - 1].Text) : "");
            }
            var sub = Flow.Submit();
            if (!sub.Accepted && sub.RejectReason != MenuFlow.ConfirmNeeded) Give(host, VoiceFeedback.EngineRejected, SpokenExamRules.RefusalText(host.Session, sub.RejectReason));
            return sub.Accepted ? "Submitted." : sub.RejectReason == MenuFlow.ConfirmNeeded ? "Say it again to submit the patient." : "Submit: " + SpokenExamRules.RefusalText(host.Session, sub.RejectReason) + ".";
        }

        public IEnumerator Handle(string text)
        {
            var host = Host();
            if (Tablet == null) Tablet = FindFirstObjectByType<TabletView>();
            if (host == null || host.Session == null || !SpokenExamRules.Allowed(host.Session) || Tablet == null) { Status = "No session is running."; yield break; }
            Handled++;
            string utteranceId = "u" + (++_utterance) + "-" + Math.Abs(text.GetHashCode());
            _suggestions.Clear();
            var turns = Talk != null ? Talk.Turns : new List<ContextTurn>();
            SpokenActionResult result = null;
            yield return Interpreter(Auth(), text, Tablet.EnabledActionIds(), turns, r => result = r);
            Status = "Heard: " + text;
            result.ActionIds = SpokenExamRules.StartableIds(result.ActionIds);          // a critical physical action is never started by speech, whatever was proposed
            string failed = result.FromService ? null : VoiceFeedback.CodeForReason(result.Reason);
            if (failed != null) Give(host, failed);                                       // the interpreter was not reached or not readable: say so; the conversation below still answers
            if (result.NeedsConfirmation)
            {
                _suggestions.AddRange(result.ActionIds);
                SuggestedText = text;
                Give(host, VoiceFeedback.NeedsConfirmation, null, string.Join(" and ", result.ActionIds.Select(Tablet.LabelOf)));
                Status = "Did you mean: " + string.Join(" and ", result.ActionIds.Select(Tablet.LabelOf)) + "? Say yes, or confirm on the tablet.";
                Talk?.Remember("trainee", text);
                yield break;
            }
            if (Orders != null && result.ActionIds.Count == 0 && result.Addressee != "patient")
            {
                bool taken = false;
                yield return Orders.Offer(text, t => taken = t);
                if (taken)                                                                    // a proposal, a question or "no such order": the order panel answers, the staff dialogue does not
                {
                    Talk?.Remember("trainee", text);
                    Status = "Heard: " + text;
                    if (EvaluateSpeech) StartCoroutine(Judge(host, text, utteranceId, turns, result.Addressee));
                    yield break;
                }
            }
            string asked = null;
            var shown = new List<string>();
            if (result.ActionIds.Count > 0) asked = StartActions(host, result.ActionIds, shown);
            if (Talk != null)
            {
                if (result.ActionIds.Count > 0 && shown.Count > 0 && asked == null) { /* the engine answered with its own authored text */ Talk.Remember("trainee", text); Talk.Say("patient", shown[0]); }
                else if (result.ActionIds.Count > 0 && asked == null && _refused) { Talk.Remember("trainee", text); Give(host, VoiceFeedback.EngineRejected, SpokenExamRules.RefusalText(host.Session, _refusedReason)); Status = "The simulator did not allow that yet: " + LastFeedback.Subtitle; }
                else Talk.Ask(text, asked, result.Addressee);
            }
            if (EvaluateSpeech) StartCoroutine(Judge(host, text, utteranceId, turns, result.Addressee));
        }

        private bool _refused;
        private string _refusedReason;

        /// <summary>Shows and speaks the feedback phrase for an outcome (P3-VOX-07): the subtitle goes to <see cref="Status"/> (the push-to-talk panel shows it), the words to the voice path.</summary>
        public VoiceFeedbackLine Give(SimulationHost host, string code, string reasonText = null, string actionText = null)
        {
            bool practice = host == null || host.Session == null || host.Session.PracticeAids;
            var line = (FeedbackTable ?? VoiceFeedback.Loaded).Resolve(code, practice, reasonText, actionText);
            LastFeedback = line;
            Status = line.Subtitle;
            FeedbackGiven?.Invoke(line);
            Talk?.SayFeedback(line);
            return line;
        }

        private static readonly HashSet<string> Yes = new HashSet<string> { "yes", "yes please", "yeah", "yep", "confirm", "go ahead", "yes go ahead", "correct" };
        private static readonly HashSet<string> No = new HashSet<string> { "no", "no thanks", "no thank you", "cancel", "nope" };

        /// <summary>A pending suggestion answered by voice: "yes" confirms it (the only one; with two it asks which), "no" dismisses it. Anything else is a new sentence.</summary>
        private bool AnswerSuggestionBySpeech(string text)
        {
            string key = System.Text.RegularExpressions.Regex.Replace(text.Trim().ToLowerInvariant(), "[^a-z ]+", "").Trim();
            if (No.Contains(key)) { Dismiss(); Status = "Cancelled."; return true; }
            if (!Yes.Contains(key)) return false;
            if (_suggestions.Count == 1) { var id = _suggestions[0]; Status = Confirm(id) ? "Done: " + (Tablet != null ? Tablet.LabelOf(id) : id) : Status; return true; }
            Give(Host(), VoiceFeedback.Ambiguous);
            return true;
        }

        /// <summary>Starts the actions the way the tablet does and returns the history topic the engine released (answered or already known), or null.</summary>
        private string StartActions(SimulationHost host, IReadOnlyList<string> ids, List<string> engineAnswers)
        {
            string asked = null;
            _refused = false;
            _refusedReason = null;
            _started.Clear();
            var seen = new List<(SimAction, ActionOutcome)>();
            Action<SimAction, ActionOutcome> collect = (a, o) => seen.Add((a, o));
            if (Talk != null) Talk.SuppressAuto = true;
            host.Applied += collect;
            int factsBefore = host.Session.Observable.DisclosedFacts.Count;
            try
            {
                foreach (var id in ids)
                    if (Tablet.PressId(id)) _started.Add(id);
            }
            finally { host.Applied -= collect; if (Talk != null) Talk.SuppressAuto = false; }
            foreach (var (a, o) in seen)
            {
                if (!o.Accepted) { _refused = true; _refusedReason ??= o.RejectReason; continue; }
                foreach (var e in o.Effects)
                {
                    if (a.Verb == "ask" && (e.StartsWith("answered:") || e.StartsWith("already_known:")) && asked == null) asked = e.Substring(e.IndexOf(':') + 1);
                }
            }
            if (asked == null)
            {
                var facts = host.Session.Observable.DisclosedFacts;
                for (int i = factsBefore; i < facts.Count; i++) engineAnswers.Add(facts[i].Answer);              // an ask that is not a history topic (allergies, reason, home support): the engine's own authored answer
            }
            return asked;
        }

        /// <summary>The trainee confirms a suggestion from the local model: the tablet action starts and the patient or staff answer as for a spoken action.</summary>
        public bool Confirm(string id)
        {
            var host = Host();
            if (host == null || !_suggestions.Contains(id)) return false;
            var shown = new List<string>();
            string asked = StartActions(host, new[] { id }, shown);
            _suggestions.Clear();
            if (Talk != null)
            {
                if (asked == null && shown.Count > 0) { Talk.Remember("trainee", SuggestedText); Talk.Say("patient", shown[0]); }
                else if (!_refused) Talk.Ask(SuggestedText, asked);
            }
            if (_refused) Give(host, VoiceFeedback.EngineRejected, SpokenExamRules.RefusalText(host.Session, _refusedReason));
            return _started.Contains(id);
        }

        public void Dismiss() { _suggestions.Clear(); }

        private IEnumerator Judge(SimulationHost host, string text, string utteranceId, IReadOnlyList<ContextTurn> turns, string addressee)
        {
            ProcSense.VR.Voice.EvaluateResult result = null;
            var cancel = new EvaluateCancellation();
            var eval = Evaluator ?? ((auth, h, t, id, tr, ad, done) => _evaluation.Evaluate(auth, h, t, id, tr, ad, cancel, done));
            yield return eval(Auth(), host, text, utteranceId, turns, addressee, r => result = r);
            if (result != null) _evaluation.Apply(host, result);                  // once per utterance id; an unavailable answer applies nothing
        }
    }
}
