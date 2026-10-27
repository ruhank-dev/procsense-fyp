using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Location;
using ProcSense.Core.Observation;
using ProcSense.VR.Bridge;
using ProcSense.VR.Scenes;
using ProcSense.VR.Views;
using TMPro;
using UnityEngine;

namespace ProcSense.VR.Diagnostics
{
    /// <summary>
    /// The waiting overlay of a B-room visit (p0-diagnostic-transitions-s06). When Core says the patient has gone behind a door (a scanner or the X-ray room), a panel says where the patient is and, on the sim
    /// clock, about when they will be back, and that the trainee can carry on with other tasks; after the content's wait (content/diagnostics/diagnostics.json, wait_overlay.seconds) it shrinks to a corner note that
    /// stays until the patient is on the way back. A time skip shows a fast-forward note; a result coming back shows "Results are back" (the tablet and the EMR badge say so too). The back time is worked out as Core
    /// does it: from arriving, the stay in that room, the preparation and the trip back (content/locations). Nothing here changes Core.
    /// </summary>
    public sealed class WaitOverlay : MonoBehaviour
    {
        public const float NoteSeconds = 4f;

        public Canvas Canvas { get; private set; }
        public bool FullShown => _full != null && _full.activeSelf;
        public bool CornerShown => _corner != null && _corner.activeSelf;
        public string FullText => _fullText != null ? _fullText.text : "";
        public string CornerText => _cornerText != null ? _cornerText.text : "";
        public string NoteText => _noteText != null && _noteText.gameObject.activeSelf ? _noteText.text : "";
        public string BackAt { get; private set; } = "";
        public float WaitSeconds { get; private set; } = 10f;

        private GameObject _full, _corner;
        private TMP_Text _fullText, _cornerText, _noteText;
        private float _fullLeft, _noteLeft;
        private string _from = "", _away = "", _lastState = "";
        private readonly HashSet<string> _ready = new HashSet<string>();
        private SimulationHost _host;
        private ObservableState _shown;

        private void Awake() => Build();

        public void Build()
        {
            if (Canvas != null) return;
            Canvas = UiKit.WorldCanvas("Waiting overlay", transform, new Vector2(900, 420), 0.0009f);
            var root = Canvas.transform;
            Canvas.GetComponentInChildren<UnityEngine.UI.Image>().color = new Color(0, 0, 0, 0);
            _full = UiKit.Panel(root, "Full", new Vector2(100, 40), new Vector2(700, 220), UiKit.PanelColor).gameObject;
            _fullText = UiKit.Label(_full.transform, "Text", "", new Vector2(24, 16), new Vector2(652, 188), 26, TextAlignmentOptions.Center);
            _corner = UiKit.Panel(root, "Corner", new Vector2(560, 0), new Vector2(340, 70), UiKit.PanelColor).gameObject;
            _cornerText = UiKit.Label(_corner.transform, "Text", "", new Vector2(10, 4), new Vector2(320, 62), 17, TextAlignmentOptions.MidlineLeft);
            _noteText = UiKit.Label(root, "Note", "", new Vector2(100, 300), new Vector2(700, 60), 22, TextAlignmentOptions.Center, UiKit.WarnColor);
            _full.SetActive(false);
            _corner.SetActive(false);
            _noteText.gameObject.SetActive(false);
        }

        private void OnEnable() => Attach(SimulationHost.Instance);
        private void OnDisable() => Attach(null);

        public void Attach(SimulationHost host)
        {
            if (_host != null) _host.Applied -= OnApplied;
            _host = host;
            if (_host == null) return;
            _host.Applied += OnApplied;
            WaitSeconds = (float)(_host.Content?.Diagnostics.WaitOverlaySeconds ?? 10);
            _ready.Clear();
            foreach (var i in _host.Observable?.Investigations ?? new List<ObservedInvestigation>())
                if (i.State != "REQUESTED" && i.State != "PROCESSING") _ready.Add(i.Test + "#" + i.Instance);
        }

        private void OnApplied(SimAction a, ActionOutcome o)
        {
            if (a.Verb != "time_skip" || !o.Accepted) return;
            Note("Time moves on" + (a.Params.TryGetValue("minutes", out var m) ? ": +" + m + " min" : ""));
        }

        private void Update()
        {
            if (_host != null && _host.Observable != _shown) Sync();
            Advance(Time.deltaTime);
        }

        /// <summary>Takes Core's state now.</summary>
        public void Sync()
        {
            var s = _host?.Observable;
            _shown = s;
            if (s == null) return;
            var t = s.Transport;
            string state = t?.State ?? "";
            if ((state == "REQUESTED" || state == "DELAYED") && _lastState != state && _away.Length == 0) _from = s.Location;
            bool behindDoor = t != null && (state == "ARRIVED" || state == "DONE") && TransportStaging.HiddenRooms.Contains(t.To);
            if (behindDoor && _away != t.To)
            {
                _away = t.To;
                BackAt = Back(t.To, state);
                string place = _host.Content.Locations.Find(t.To)?.Label ?? t.To;
                _fullText.text = "The patient is in the " + place + "\nBack about " + BackAt + " (sim clock)\nYou can carry on with other tasks meanwhile";
                _cornerText.text = place + ": back about " + BackAt;
                _full.SetActive(true);
                _fullLeft = WaitSeconds;
            }
            if (!behindDoor && _away.Length > 0) { _away = ""; BackAt = ""; _full.SetActive(false); _corner.SetActive(false); }
            _lastState = state;

            foreach (var i in s.Investigations.Where(i => i.State == "RESULT_AVAILABLE" && _ready.Add(i.Test + "#" + i.Instance))) Note("Results are back: " + i.Label);
        }

        /// <summary>Moves the overlay on by this much real time: the full panel gives way to the corner note after the content's wait.</summary>
        public void Advance(float seconds)
        {
            if (_full.activeSelf)
            {
                _fullLeft -= seconds;
                if (_fullLeft <= 0f) { _full.SetActive(false); _corner.SetActive(_away.Length > 0); }
            }
            if (_noteLeft > 0f)
            {
                _noteLeft -= seconds;
                if (_noteLeft <= 0f) _noteText.gameObject.SetActive(false);
            }
        }

        private void Note(string text)
        {
            _noteText.text = text;
            _noteText.gameObject.SetActive(true);
            _noteLeft = NoteSeconds;
        }

        // As Core does it (TransportSubsystem): ARRIVED lasts the room's stay, DONE the preparation, then the trip back takes the route's minutes.
        private string Back(string place, string state)
        {
            var tables = _host.Content.Locations;
            double stay = tables.Find(place)?.StayMinutes ?? 0;
            double travel = new LocationGraph(tables.Locations).Shortest(_from.Length > 0 ? _from : "patient_bay", place)?.Minutes ?? 0;
            double minutes = (state == "ARRIVED" ? stay : 0) + tables.PrepareMinutes + travel;
            var at = _host.Session.Clock.Start + TimeSpan.FromSeconds(_host.Session.Clock.SimSeconds) + TimeSpan.FromMinutes(minutes);   // a time of day on the sim clock
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}", at.Hours, at.Minutes);
        }
    }
}
