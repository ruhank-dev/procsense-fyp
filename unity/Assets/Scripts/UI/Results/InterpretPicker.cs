using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.VR.Bridge;
using ProcSense.VR.UI.Tablet;
using ProcSense.VR.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.UI.Results
{
    /// <summary>
    /// The interpretation picker under the results viewer (p0-vr-interaction-ui-s05): for a lab result normal, abnormal or artefact (interpret_result), for imaging the categories of the imaging table (interpret_imaging),
    /// with the words of the tablet layout. Choosing one sends it; the decision card then says what the trainee decided, and in practice only whether the results bear it out. Core judges; the picker only reports Core's answer.
    /// </summary>
    public sealed class InterpretPicker
    {
        public const string LabVerb = "interpret_result", ImagingVerb = "interpret_imaging";

        private readonly List<(TabletOption option, Button button)> _buttons = new List<(TabletOption, Button)>();
        private readonly Transform _root;
        private readonly FormSendEmitter _send;
        private readonly TMP_Text _card;
        private readonly Vector2 _at;
        private string _test = "", _verb = "";

        public InterpretPicker(Transform parent, FormSendEmitter send, Vector2 topLeft)
        {
            _root = parent;
            _send = send;
            _at = topLeft;
            _card = UiKit.Label(parent, "Decision card", "", topLeft + new Vector2(0, 64), new Vector2(740, 56), 20, color: UiKit.WarnColor);
        }

        public string Test => _test;
        public string Verb => _verb;
        public string CardText => _card.text;
        public IReadOnlyList<TabletOption> Options => _buttons.Select(b => b.option).ToList();
        public IEnumerable<(string label, Button button)> Buttons => _buttons.Select(b => (b.option.Label, b.button));

        /// <summary>Offers the choices for this test (none while nothing is selected or the result has not been opened).</summary>
        public void Show(string test, bool imaging, bool viewed)
        {
            string verb = test.Length == 0 || !viewed ? "" : imaging ? ImagingVerb : LabVerb;
            if (test == _test && verb == _verb) return;
            if (test != _test) _card.text = "";
            _test = test;
            _verb = verb;
            foreach (var b in _buttons) { if (Application.isPlaying) Object.Destroy(b.button.gameObject); else Object.DestroyImmediate(b.button.gameObject); }
            _buttons.Clear();
            if (verb.Length == 0) return;
            var options = LayoutText.Options(verb, imaging ? "category" : "interpretation");
            float width = Mathf.Min(180f, (740f - 8f * (options.Count - 1)) / Mathf.Max(1, options.Count));
            for (int i = 0; i < options.Count; i++)
            {
                var option = options[i];
                var button = UiKit.MakeButton(_root, option.Label, _at + new Vector2(i * (width + 8f), 0), new Vector2(width, 56), 18f);
                button.onClick.AddListener(() => Choose(option.Value, true));
                _buttons.Add((option, button));
            }
        }

        /// <summary>Sends the interpretation and fills the decision card. Returns Core's answer (null when there is nothing to interpret).</summary>
        public ActionOutcome Choose(string value, bool click = false)
        {
            var option = _buttons.Select(b => b.option).FirstOrDefault(o => o.Value == value);
            if (option == null) return null;
            string key = _verb == ImagingVerb ? "category" : "interpretation";
            var outcome = PanelSend.Send(_send, _verb, new Dictionary<string, string> { ["test"] = _test, [key] = value }, click);
            if (outcome == null) return null;
            if (!outcome.Accepted) { _card.text = HintTexts.For(outcome.RejectReason); return outcome; }   // the words, not the code
            bool right = outcome.Effects.Any(e => e.EndsWith(":correct"));
            _card.text = option.Label + ModeGate.Practice(right ? "  -  the results bear this out" : "  -  the results do not bear this out");
            return outcome;
        }
    }
}
