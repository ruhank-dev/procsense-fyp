using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Observation;
using ProcSense.Core.State;
using ProcSense.VR.Bridge;
using ProcSense.VR.UI.Tablet;
using ProcSense.VR.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.UI.Results
{
    /// <summary>One read-back the trainee can choose: the words on the button and the values sent ("potassium=6.6").</summary>
    public sealed class ReadBackChoice
    {
        public string Label = "", Values = "";
        public bool Exact;
    }

    /// <summary>
    /// The ward phone (p0-vr-interaction-ui-s05): it rings when Core's lab calls with a critical result (<see cref="ObservedPhone"/>). The trainee answers, confirms the patient with the caller from the wristband,
    /// and reads the values back by choosing what they heard: the values as phoned, or one of two misheard versions. Core keeps the call (answer, identity, read-back, then acknowledgement with an action and a note on
    /// the results viewer) and refuses any step out of order; the phone only shows Core's state. The ring is drawn as a flashing panel; the lab's voice and the ring tone belong to the audio steps.
    /// </summary>
    public class PhoneView : MonoBehaviour
    {
        public const string AnswerVerb = "answer_phone", ConfirmVerb = "confirm_patient_on_call", ReadBackVerb = "read_back";
        private static readonly Color Ring = new Color(0.85f, 0.25f, 0.20f, 1f);

        public Canvas Canvas { get; private set; }
        public string State => Host?.Observable?.Phone.State ?? "";
        public bool Ringing => State == "RINGING";
        public string StatusText => _status != null ? _status.text : "";
        public string MessageText => _message != null ? _message.text : "";
        public string HintText => _hint != null ? _hint.text : "";
        public IReadOnlyList<ReadBackChoice> Choices => _choices;
        public bool AnswerEnabled => _answer != null && _answer.interactable;
        public bool ConfirmEnabled => _confirm != null && _confirm.interactable;
        public bool ReadBackEnabled => _readBack.Count > 0 && _readBack[0].interactable;

        private readonly List<ReadBackChoice> _choices = new List<ReadBackChoice>();
        private readonly List<Button> _readBack = new List<Button>();
        private TMP_Text _status, _message, _hint;
        private Button _answer, _confirm;
        private Image _lamp;
        private FormSendEmitter _send;
        private ObservableState _shown;
        private int _callShown = -1;

        private static SimulationHost Host => SimulationHost.Instance;

        private void Awake() => Build();

        public void Build()
        {
            if (Canvas != null) return;
            Canvas = UiKit.WorldCanvas("Phone", transform, new Vector2(640, 440), 0.0006f);
            var root = Canvas.transform;
            _send = gameObject.AddComponent<FormSendEmitter>();
            _lamp = UiKit.Panel(root, "Ring", new Vector2(12, 12), new Vector2(40, 40), UiKit.ButtonDisabledColor).GetComponent<Image>();
            _status = UiKit.Label(root, "Status", "", new Vector2(64, 12), new Vector2(564, 40), 22, TextAlignmentOptions.MidlineLeft);
            _message = UiKit.Label(root, "Caller", "", new Vector2(12, 60), new Vector2(616, 110), 19);
            _answer = UiKit.MakeButton(root, LayoutText.Label(AnswerVerb), new Vector2(12, 178), new Vector2(300, 52), 18f);
            _answer.onClick.AddListener(() => Answer(true));
            _confirm = UiKit.MakeButton(root, LayoutText.Label(ConfirmVerb), new Vector2(328, 178), new Vector2(300, 52), 16f);
            _confirm.onClick.AddListener(() => ConfirmIdentity(true));
            for (int i = 0; i < 3; i++)
            {
                int n = i;
                var b = UiKit.MakeButton(root, "", new Vector2(12, 240 + i * 58), new Vector2(616, 52), 17f);
                b.onClick.AddListener(() => ReadBack(n, true));
                _readBack.Add(b);
            }
            _hint = UiKit.Label(root, "Hint", "", new Vector2(12, 416), new Vector2(616, 22), 15, TextAlignmentOptions.MidlineLeft, UiKit.WarnColor);
            RenderNow();
        }

        private void Update()
        {
            if (Host != null && Host.Observable != _shown) RenderNow();
            if (_lamp != null) _lamp.color = Ringing && Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.4f ? Ring : UiKit.ButtonDisabledColor;
        }

        public ActionOutcome Answer(bool click = false) => After(PanelSend.Send(_send, AnswerVerb, new Dictionary<string, string>(), click));

        /// <summary>Confirms the patient with the caller using the name and date of birth on the wristband (null until the band has been read).</summary>
        public ActionOutcome ConfirmIdentity(bool click = false)
        {
            var band = Host?.Observable?.Wristband;
            if (band == null) return null;
            return After(PanelSend.Send(_send, ConfirmVerb, new Dictionary<string, string> { ["name"] = band.NameText, ["dob"] = band.DobText }, click));
        }

        public ActionOutcome ReadBack(int choice, bool click = false) =>
            choice < 0 || choice >= _choices.Count ? null : After(PanelSend.Send(_send, ReadBackVerb, new Dictionary<string, string> { ["values"] = _choices[choice].Values }, click));

        private ActionOutcome After(ActionOutcome outcome)
        {
            if (outcome != null)
                _hint.text = !outcome.Accepted ? ModeGate.Practice(HintTexts.For(outcome.RejectReason)) : outcome.Effects.Contains("readback:wrong") ? ModeGate.Practice("That is not what the caller said: read it back again") : "";
            RenderNow();
            return outcome;
        }

        public void RenderNow()
        {
            if (Canvas == null) return;
            var s = Host != null ? Host.Observable : null;
            _shown = s;
            string state = s?.Phone.State ?? "";
            _status.text = state switch
            {
                "RINGING" => "The phone is ringing",
                "ANSWERED" => "On the call",
                "IDENTITY_CONFIRMED" => "On the call: patient confirmed",
                "READ_BACK" => "Read back: acknowledge the result with an action and a note",
                _ => "No call",
            };
            _message.text = s?.Phone.Message ?? "";
            var call = Host?.Session?.Patient.Clinical.CriticalCalls.FirstOrDefault(c => c.State != "DONE");
            if (call == null) { _choices.Clear(); _callShown = -1; }
            else if (call.Id != _callShown) { _choices.Clear(); _choices.AddRange(ChoicesFor(call)); _callShown = call.Id; }
            _answer.interactable = state == "RINGING";
            _confirm.interactable = state == "ANSWERED" && s?.Wristband != null;
            for (int i = 0; i < _readBack.Count; i++)
            {
                bool used = i < _choices.Count && state.Length > 0 && state != "RINGING";
                _readBack[i].gameObject.SetActive(used);
                if (used) _readBack[i].GetComponentInChildren<TMP_Text>().text = _choices[i].Label;
                _readBack[i].interactable = state == "IDENTITY_CONFIRMED";
            }
        }

        /// <summary>
        /// The three read-backs offered for a call: the values exactly as phoned, the first value three units higher in its last digit, and the last value one whole unit lower (the commonest mishearings).
        /// The words are Core's ("Potassium 6.6 mmol/L") with only the number changed. The exact one moves place from call to call, so its position teaches nothing.
        /// </summary>
        public static IReadOnlyList<ReadBackChoice> ChoicesFor(CriticalCall call)
        {
            var values = call.Values.ToList();
            if (values.Count == 0) return new List<ReadBackChoice>();
            var exact = Choice(values, values.Select(v => v.Value).ToList(), true);
            var high = values.Select(v => v.Value).ToList();
            high[0] = high[0] + 3 * Step(values[0].Value);
            var low = values.Select(v => v.Value).ToList();
            int last = low.Count - 1;
            low[last] = low[last] >= 1 ? low[last] - 1 : low[last] + 1;
            var list = new List<ReadBackChoice> { Choice(values, high, false), Choice(values, low, false) };
            list.Insert(call.Id % 3, exact);
            return list;
        }

        private static ReadBackChoice Choice(IReadOnlyList<CriticalValue> phoned, IReadOnlyList<double> said, bool exact)
        {
            var labels = new List<string>();
            var values = new List<string>();
            for (int i = 0; i < phoned.Count; i++)
            {
                string was = Text(phoned[i].Value, phoned[i].Value);
                string now = Text(said[i], phoned[i].Value);
                labels.Add(phoned[i].Spoken.Contains(was) ? phoned[i].Spoken.Replace(was, now) : phoned[i].Analyte + " " + now);
                values.Add(phoned[i].Analyte + "=" + now);
            }
            return new ReadBackChoice { Label = string.Join(", ", labels), Values = string.Join(",", values), Exact = exact };
        }

        private static int Decimals(double v)
        {
            string t = v.ToString("0.####", CultureInfo.InvariantCulture);
            int dot = t.IndexOf('.');
            return dot < 0 ? 0 : t.Length - dot - 1;
        }

        private static double Step(double v) => System.Math.Pow(10, -Decimals(v));

        // a value written with as many decimals as the value the lab phoned
        private static string Text(double v, double phoned) => v.ToString("F" + Decimals(phoned), CultureInfo.InvariantCulture);
    }
}
