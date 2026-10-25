using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.AIContext;
using ProcSense.Core.Observation;
using ProcSense.VR.Auth;
using ProcSense.VR.Bridge;
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
    /// answered by a patient; nothing here awards credit or skips an engine rule. Practice only.
    /// </summary>
    public sealed class SpokenActionRouter : MonoBehaviour
    {
        public const float RepeatWindowSeconds = 4f;
        public TabletView Tablet;
        public TeamConversation Talk;
        public Func<AuthService> Auth = () => AuthService.Current;
        public Func<SimulationHost> Host = () => SimulationHost.Instance;
        /// <summary>Replaced by tests; the real one asks the service.</summary>
        public Func<AuthService, string, IEnumerable<string>, IReadOnlyList<ContextTurn>, Action<SpokenActionResult>, IEnumerator> Interpreter =
            (auth, text, ids, turns, done) => SpokenActionClient.Interpret(auth, text, ids, turns, done);
        /// <summary>Judges the sentence for communication credit (ADR-0025). Replaced by tests.</summary>
        public Func<AuthService, SimulationHost, string, string, IReadOnlyList<ContextTurn>, string, Action<ProcSense.VR.Voice.EvaluateResult>, IEnumerator> Evaluator;
        public bool EvaluateSpeech = true;

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
            StartCoroutine(Handle(text.Trim()));
            return true;
        }

        public IEnumerator Handle(string text)
        {
            var host = Host();
            if (Tablet == null) Tablet = FindFirstObjectByType<TabletView>();
            if (host == null || host.Session == null || !ModeGate.PracticeAids || Tablet == null) { Status = "Speaking is for practice mode."; yield break; }
            Handled++;
            string utteranceId = "u" + (++_utterance) + "-" + Math.Abs(text.GetHashCode());
            _suggestions.Clear();
            var turns = Talk != null ? Talk.Turns : new List<ContextTurn>();
            SpokenActionResult result = null;
            yield return Interpreter(Auth(), text, Tablet.EnabledActionIds(), turns, r => result = r);
            Status = "Heard: " + text;
            if (result.NeedsConfirmation)
            {
                _suggestions.AddRange(result.ActionIds);
                SuggestedText = text;
                Status = "Did you mean: " + string.Join(" and ", result.ActionIds.Select(Tablet.LabelOf)) + "? Confirm on the tablet.";
                Talk?.Remember("trainee", text);
                yield break;
            }
            string asked = null;
            var shown = new List<string>();
            if (result.ActionIds.Count > 0) asked = StartActions(host, result.ActionIds, shown);
            if (Talk != null)
            {
                if (result.ActionIds.Count > 0 && shown.Count > 0 && asked == null) { /* the engine answered with its own authored text */ Talk.Remember("trainee", text); Talk.Say("patient", shown[0]); }
                else if (result.ActionIds.Count > 0 && asked == null && _refused) { Talk.Remember("trainee", text); Status = "The simulator did not allow that yet."; }
                else Talk.Ask(text, asked);
            }
            if (EvaluateSpeech) StartCoroutine(Judge(host, text, utteranceId, turns, result.Addressee));
        }

        private bool _refused;

        /// <summary>Starts the actions the way the tablet does and returns the history topic the engine released (answered or already known), or null.</summary>
        private string StartActions(SimulationHost host, IReadOnlyList<string> ids, List<string> engineAnswers)
        {
            string asked = null;
            _refused = false;
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
                if (!o.Accepted) { _refused = true; continue; }
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
