using System.Linq;
using ProcSense.Core.Observation;
using ProcSense.VR.ActionEmitters;
using TMPro;
using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// The bedside monitor: a value appears only for a sensor that is on the patient or a reading the trainee has taken, with the time it was read.
    /// Has the "Measure" button for the blood-pressure cuff.
    /// </summary>
    public class MonitorView : StateView
    {
        private static readonly (string slot, string unit, string[] names)[] Slots =
        {
            ("SpO2", "%", new[] { "spo2" }), ("Pulse", "/min", new[] { "heart_rate" }), ("BP", "mmHg", new[] { "systolic_bp", "diastolic_bp" }),
            ("Breathing", "/min", new[] { "respiratory_rate" }), ("Temp", "C", new[] { "temperature_c" }), ("Alert", "", new[] { "acvpu" }), ("Pain", "/10", new[] { "pain_score" }),
        };

        private readonly TMP_Text[] _values = new TMP_Text[Slots.Length];

        /// <summary>The text on one monitor slot (SpO2, Pulse, BP, Breathing, Temp, Alert, Pain).</summary>
        public string ValueText(string slot)
        {
            for (int i = 0; i < Slots.Length; i++)
                if (Slots[i].slot == slot) return _values[i].text;
            return null;
        }

        private void Awake()
        {
            var canvas = UiKit.WorldCanvas("Monitor", transform, new Vector2(520, 420), 0.0009f);
            UiKit.Label(canvas.transform, "Title", "MONITOR", new Vector2(14, 8), new Vector2(300, 30), 22, TextAlignmentOptions.TopLeft, UiKit.DimTextColor);
            for (int i = 0; i < Slots.Length; i++)
            {
                UiKit.Label(canvas.transform, "Name " + Slots[i].slot, Slots[i].slot, new Vector2(18, 46 + i * 44), new Vector2(150, 40), 26, TextAlignmentOptions.MidlineLeft, UiKit.DimTextColor);
                _values[i] = UiKit.Label(canvas.transform, "Value " + Slots[i].slot, "--", new Vector2(170, 46 + i * 44), new Vector2(340, 40), 28, TextAlignmentOptions.MidlineLeft);
            }
            var measure = UiKit.MakeButton(canvas.transform, "Measure BP", new Vector2(330, 6), new Vector2(176, 36), 20);
            var emitter = measure.gameObject.AddComponent<UIButtonEmitter>();
            emitter.Configure("measure_nibp", "patient", fallbackLabel: "Measure BP");
        }

        protected override void Render(ObservableState s)
        {
            for (int i = 0; i < Slots.Length; i++)
            {
                var found = Slots[i].names.Select(n => s.Vitals.FirstOrDefault(v => v.Name == n)).Where(v => v != null).ToList();
                string text = found.Count == 0 ? "--" : string.Join("/", found.Select(v => v.Value)) + " " + Slots[i].unit + "  (" + found[0].At + ")";
                if (Slots[i].slot == "SpO2" && found.Count > 0)
                {
                    var oxygen = s.Vitals.FirstOrDefault(v => v.Name == "oxygen");
                    if (oxygen != null) text += "  " + oxygen.Value.Replace('_', ' ');
                }
                SetText(_values[i], text);
            }
        }
    }
}
