using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Observation;
using ProcSense.VR.ActionEmitters;
using ProcSense.VR.Bridge;
using ProcSense.VR.UI.Monitor;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// The bedside monitor: a value appears only for a sensor that is on the patient or a reading the trainee has taken, with the time it was read.
    /// Has the "Measure" button for the blood-pressure cuff, the pleth trace while the probe is on (small and noisy when Core says the signal is poor, with what to check),
    /// the cuff cycling until its reading arrives, and the alarms by display zone (p0-vr-interaction-ui-s04). Acknowledging silences the sound for a while and is logged
    /// as a Core action; the alarm's light and text stay while the value is out of range.
    /// </summary>
    public class MonitorView : StateView
    {
        private static readonly (string slot, string unit, string[] names)[] Slots =
        {
            ("SpO2", "%", new[] { "spo2" }), ("Pulse", "bpm", new[] { "heart_rate" }), ("BP", "mmHg", new[] { "systolic_bp", "diastolic_bp" }),
            ("Breathing", "breaths/min", new[] { "respiratory_rate" }), ("Temp", "°C", new[] { "temperature_c" }), ("Alert", "", new[] { "acvpu" }), ("Pain", "/10", new[] { "pain_score" }),
        };

        // zone colours with a word beside the value, so the zone is never told by colour alone (p0-vr-interaction-ui-s10)
        private static readonly Dictionary<string, (Color color, string word)> Zones = new Dictionary<string, (Color, string)>
        {
            ["normal"] = (new Color(.45f, .85f, .55f), ""), ["watch"] = (new Color(1f, .86f, .35f), "watch"),
            ["worry"] = (new Color(1f, .62f, .25f), "worry"), ["emergency"] = (new Color(1f, .36f, .36f), "EMERGENCY"),
        };

        private readonly TMP_Text[] _values = new TMP_Text[Slots.Length];
        private readonly TMP_Text[] _zones = new TMP_Text[Slots.Length];
        private TMP_Text _alarmText, _probeText, _cuffText;
        private Image _alarmLight;
        private Button _acknowledge;
        private WaveformRenderer _pleth;
        private MonitorAlarms _alarms;
        private SimulationHost _appliedHost;
        private bool _cycling;
        private string _cuffAt = "";
        private float _nextSilenceCheck;

        /// <summary>The text on one monitor slot (SpO2, Pulse, BP, Breathing, Temp, Alert, Pain).</summary>
        public string ValueText(string slot)
        {
            for (int i = 0; i < Slots.Length; i++)
                if (Slots[i].slot == slot) return _values[i].text;
            return null;
        }

        /// <summary>The zone word beside a slot ("" when normal or no value).</summary>
        public string ZoneText(string slot)
        {
            for (int i = 0; i < Slots.Length; i++)
                if (Slots[i].slot == slot) return _zones[i].text;
            return null;
        }

        public string AlarmText => _alarmText.text;
        public string ProbeText => _probeText.text;
        public string CuffText => _cuffText.text;
        public MonitorAlarms Alarms => _alarms;
        public WaveformRenderer Pleth => _pleth;
        public Button AcknowledgeButton => _acknowledge;

        private void Awake()
        {
            var table = MonitorTable.Loaded;
            _alarms = new MonitorAlarms(table);
            var canvas = UiKit.WorldCanvas("Monitor", transform, new Vector2(900, 470), 0.0009f);
            UiKit.Label(canvas.transform, "Title", "MONITOR", new Vector2(14, 8), new Vector2(300, 30), 22, TextAlignmentOptions.TopLeft, UiKit.DimTextColor);
            for (int i = 0; i < Slots.Length; i++)
            {
                UiKit.Label(canvas.transform, "Name " + Slots[i].slot, Slots[i].slot, new Vector2(18, 46 + i * 44), new Vector2(150, 40), 26, TextAlignmentOptions.MidlineLeft, UiKit.DimTextColor);
                _values[i] = UiKit.Label(canvas.transform, "Value " + Slots[i].slot, "--", new Vector2(170, 46 + i * 44), new Vector2(260, 40), 28, TextAlignmentOptions.MidlineLeft);
                _zones[i] = UiKit.Label(canvas.transform, "Zone " + Slots[i].slot, "", new Vector2(430, 46 + i * 44), new Vector2(120, 40), 22, TextAlignmentOptions.MidlineLeft);
            }
            var measure = UiKit.MakeButton(canvas.transform, "Measure BP", new Vector2(330, 6), new Vector2(176, 36), 20);
            var emitter = measure.gameObject.AddComponent<UIButtonEmitter>();
            emitter.Configure("measure_nibp", "patient", fallbackLabel: "Measure BP");
            _cuffText = UiKit.Label(canvas.transform, "Cuff", "", new Vector2(520, 6), new Vector2(370, 36), 22, TextAlignmentOptions.MidlineLeft, UiKit.WarnColor);

            // the pleth trace
            UiKit.Panel(canvas.transform, "Pleth panel", new Vector2(560, 50), new Vector2(326, 130), new Color(0, 0, 0, .55f));
            UiKit.Label(canvas.transform, "Pleth label", "Pleth", new Vector2(566, 52), new Vector2(120, 26), 20, TextAlignmentOptions.TopLeft, UiKit.DimTextColor);
            var go = new GameObject("Pleth trace", typeof(RectTransform), typeof(CanvasRenderer), typeof(WaveformRenderer));
            go.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(566, -78);
            rt.sizeDelta = new Vector2(314, 96);
            _pleth = go.GetComponent<WaveformRenderer>();
            _pleth.Kind = TraceKind.Pleth;
            _pleth.color = new Color(.45f, .8f, 1f);
            _pleth.raycastTarget = false;
            _pleth.Configure(table.SampleHz, table.SweepSeconds);
            _probeText = UiKit.Label(canvas.transform, "Probe", "", new Vector2(560, 186), new Vector2(326, 120), 20, TextAlignmentOptions.TopLeft, UiKit.WarnColor);

            // the alarm bar
            _alarmLight = UiKit.Panel(canvas.transform, "Alarm light", new Vector2(14, 362), new Vector2(26, 92), Color.clear).GetComponent<Image>();
            _alarmText = UiKit.Label(canvas.transform, "Alarm", "", new Vector2(50, 362), new Vector2(620, 92), 24, TextAlignmentOptions.TopLeft);
            _acknowledge = UiKit.MakeButton(canvas.transform, "Silence alarm", new Vector2(686, 384), new Vector2(200, 50), 22);
            _acknowledge.onClick.AddListener(Acknowledge);
            _acknowledge.gameObject.SetActive(false);
            if (GetComponent<Audio.AlarmAudioView>() == null) gameObject.AddComponent<Audio.AlarmAudioView>();   // its sound, so no scene needs editing
        }

        protected override void OnEnable() { base.OnEnable(); BindActions(); }
        protected override void OnDisable()
        {
            base.OnDisable();
            if (_appliedHost != null) _appliedHost.Applied -= OnApplied;
            _appliedHost = null;
            _cycling = false;
        }

        protected override void Update()
        {
            BindActions();
            base.Update();
            if (_alarmLight == null) return;
            // the light flashes at the priority's pace while sounding and stays on while silenced; the sound is AlarmAudioView's
            var top = _alarms.Active.Count > 0 ? _alarms.Active[0] : null;
            if (top == null) { _alarmLight.color = Color.clear; return; }
            var c = Zones.TryGetValue(top.Zone, out var z) ? z.color : UiKit.WarnColor;
            float rate = top.Priority.Rank >= 3 ? 2f : top.Priority.Rank == 2 ? .7f : 0f;
            bool lit = top.Silenced || rate == 0 || Mathf.Repeat(Time.unscaledTime * rate, 1f) < .5f;
            _alarmLight.color = lit ? c : c * .3f;
            // an acknowledged alarm comes back when its silence ends (checked once a second)
            if (top.Silenced && Last != null && Time.unscaledTime >= _nextSilenceCheck) { _nextSilenceCheck = Time.unscaledTime + 1f; RefreshAlarms(Last); }
        }

        private void BindActions()
        {
            var host = SimulationHost.Instance;
            if (ReferenceEquals(host, _appliedHost)) return;
            if (_appliedHost != null) _appliedHost.Applied -= OnApplied;
            _appliedHost = host;
            _cycling = false;
            if (_appliedHost != null) _appliedHost.Applied += OnApplied;
        }

        private void OnApplied(SimAction action, ActionOutcome outcome)
        {
            if (action.Verb != "measure_nibp" || !outcome.Accepted) return;
            _cycling = true;
            _cuffAt = _appliedHost.Observable?.Vitals.FirstOrDefault(v => v.Name == "systolic_bp")?.At ?? "";
            RenderNow();
        }

        /// <summary>Silences the alarms sounding now; each one is an acknowledge_alarm action in the log.</summary>
        public void Acknowledge()
        {
            var host = SimulationHost.Instance;
            foreach (var vital in _alarms.Acknowledge(Time.unscaledTime))
            {
                var alarm = _alarms.Active.First(a => a.Vital == vital);
                host?.Apply(new SimAction("acknowledge_alarm", "monitor", Channel.Vr, new Dictionary<string, string> { ["vital"] = vital, ["priority"] = alarm.Priority.Id }));
            }
            if (Last != null) RefreshAlarms(Last);
        }

        protected override void Render(ObservableState s)
        {
            for (int i = 0; i < Slots.Length; i++)
            {
                var found = Slots[i].names.Select(n => s.Vitals.FirstOrDefault(v => v.Name == n)).Where(v => v != null).ToList();
                string text = found.Count == 0 ? "--" : string.Join("/", found.Select(v => v.Value)) + " " + Slots[i].unit + "  (" + found[0].At + ")";
                if (Slots[i].slot == "SpO2" && found.Count > 0) text = found[0].Value + "%";   // just "98%": whether the patient is on air or oxygen is not shown on the monitor
                SetText(_values[i], text);
                string zone = found.Count > 0 ? found[0].Zone : "";
                bool known = Zones.TryGetValue(zone ?? "", out var z);
                SetText(_zones[i], known ? z.word : "");
                if (known) { _zones[i].color = z.color; _values[i].color = zone == "normal" ? UiKit.TextColor : z.color; }
                else _values[i].color = UiKit.TextColor;
            }

            var spo2 = s.Vitals.FirstOrDefault(v => v.Name == "spo2");
            var hr = s.Vitals.FirstOrDefault(v => v.Name == "heart_rate");
            bool probeOn = spo2 != null && s.Vitals.Any(v => v.Name == "oxygen");   // Core lists air or oxygen only while the probe is on (a live reading)
            bool poor = probeOn && !string.IsNullOrEmpty(spo2.Quality);
            _pleth.On = probeOn;
            _pleth.Rate = probeOn && hr != null && float.TryParse(hr.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var rate) ? rate : 0;
            _pleth.Amplitude = poor ? .3f : 1f;
            _pleth.Noise = poor ? .35f : 0f;
            var table = MonitorTable.Loaded;
            SetText(_probeText, !probeOn ? "No probe: no pleth" : poor ? (table.ProbeAdvice.TryGetValue(spo2.Quality, out var advice) ? advice : "Poor signal: recheck the probe") : "");

            if (_cycling && (s.Vitals.FirstOrDefault(v => v.Name == "systolic_bp")?.At ?? "") != _cuffAt) _cycling = false;
            SetText(_cuffText, _cycling ? table.CycleLabel + "…" : "");
            RefreshAlarms(s);
        }

        private void RefreshAlarms(ObservableState s)
        {
            _alarms.Update(s.Vitals, Time.unscaledTime);
            SetText(_alarmText, string.Join("\n", _alarms.Active.Take(3).Select(a => a.Text)));
            _acknowledge.gameObject.SetActive(_alarms.Sounding != null);
        }
    }
}
