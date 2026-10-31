using System;
using System.Linq;
using ProcSense.Core.Observation;
using ProcSense.VR.Bridge;
using TMPro;
using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>Read-only information outside a diagnostic door. The closed room remains inaccessible to the trainee.</summary>
    public sealed class DiagnosticDoorView : StateView
    {
        public TextAsset Presentation;
        public string[] DestinationIds = Array.Empty<string>();
        public bool Laboratory;
        public Transform OutsideAnchor, InsideAnchor, Viewer;
        public Vector3 PanelOffset = new Vector3(1.14f, .55f, -.12f);
        public float ApproachRadius = 1.65f;
        public const float NoticeSeconds = 5;
        private Canvas _canvas;
        private TMP_Text _notice, _status;
        private Wording _words;
        private bool _inside;
        private float _noticeUntil, _nextNotice;
        private ProcSense.Core.Session _session;
        public string StatusText => _status != null ? _status.text : "";
        public string NoticeText => _notice != null ? _notice.text : "";
        public int NoticeCount { get; private set; }
        public bool NoticeVisible => _notice != null && _notice.gameObject.activeInHierarchy;
        public Canvas Canvas => _canvas;

        [Serializable]
        public sealed class Wording
        {
            public string accessNotice, noSession, noTransport, patient, transport, mode, escort, monitoring, arrival, oxygenYes, oxygenNo, laboratoryHint, labOrders, noLabOrders;
            public string ct, ultrasound, xray, lab, mri;
        }

        private void Start() => Build();

        public void Build()
        {
            if (_canvas != null) return;
            if (Presentation == null) throw new InvalidOperationException("Diagnostic door needs its presentation TextAsset.");
            _words = JsonUtility.FromJson<Wording>(Presentation.text);
            if (_words == null || string.IsNullOrEmpty(_words.accessNotice) || string.IsNullOrEmpty(_words.noSession))
                throw new InvalidOperationException("Diagnostic door presentation is incomplete.");
            _canvas = UiKit.WorldCanvas("Diagnostic information", transform, new Vector2(560, 360), .0014f);
            _canvas.transform.localPosition = PanelOffset;
            _notice = UiKit.Label(_canvas.transform, "Access notice", _words.accessNotice, new Vector2(14, 12), new Vector2(532, 74), 24, color: UiKit.WarnColor);
            _status = UiKit.Label(_canvas.transform, "Journey status", _words.noSession, new Vector2(14, 96), new Vector2(532, 250), 22);
            _canvas.gameObject.SetActive(false);
            RenderNow();
        }

        protected override void Update()
        {
            if (_canvas == null) return;
            base.Update();
            ResetSession();
            if (_session == null) SetText(_status, _words.noSession);
            var viewer = Viewer != null ? Viewer : Camera.main != null ? Camera.main.transform : null;
            if (viewer != null) ObserveApproach(viewer.position, Time.unscaledTime);
            else { _inside = false; _canvas.gameObject.SetActive(false); }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ResetVisit();
            if (_canvas != null) _canvas.gameObject.SetActive(false);
        }

        private void ResetSession()
        {
            var current = SimulationHost.Instance != null ? SimulationHost.Instance.Session : null;
            if (ReferenceEquals(current, _session)) return;
            _session = current;
            ResetVisit();
        }

        private void ResetVisit()
        {
            _inside = false; _noticeUntil = _nextNotice = 0; NoticeCount = 0;
        }

        /// <summary>Presentation only: one notice on entry from the corridor, then a five-second re-entry cooldown.</summary>
        public bool ObserveApproach(Vector3 position, float now)
        {
            if (_canvas == null) return false;
            bool finite = float.IsFinite(position.x) && float.IsFinite(position.y) && float.IsFinite(position.z) && float.IsFinite(now);
            var local = transform.InverseTransformPoint(position);
            var delta = position - transform.position; delta.y = 0;
            bool near = finite && local.z < -.02f && delta.sqrMagnitude <= ApproachRadius * ApproachRadius;
            if (near && !_inside && now >= _nextNotice)
            {
                NoticeCount++;
                _noticeUntil = _nextNotice = now + NoticeSeconds;
            }
            _inside = near;
            _canvas.gameObject.SetActive(near);
            _notice.gameObject.SetActive(near && now < _noticeUntil);
            return near;
        }

        private static string LocationName(string id) => SimulationHost.Instance?.Content?.Locations.Find(id)?.Label ?? id;

        protected override void Render(ObservableState state)
        {
            if (_status == null) return;
            ResetSession();
            string text = _words.patient.Replace("{location}", LocationName(state.Location));
            if (Laboratory)
            {
                var catalogue = SimulationHost.Instance?.InvestigationCatalogue;
                var orders = state.Investigations.Where(i => catalogue != null && catalogue.Any(d => d.Id == i.Test && d.Kind == "blood")).Reverse().Take(3).ToArray();
                text += "\n" + _words.laboratoryHint + "\n" + _words.labOrders + "\n" + (orders.Length == 0 ? _words.noLabOrders : string.Join("\n", orders.Select(i => i.Label + ": " + i.State)));
            }
            else
            {
                var trip = state.Transport;
                if (trip == null || !DestinationIds.Contains(trip.To)) text += "\n" + _words.noTransport;
                else
                {
                    text += "\n" + _words.transport.Replace("{destination}", LocationName(trip.To)).Replace("{state}", trip.State)
                        + "\n" + _words.mode.Replace("{mode}", trip.Mode)
                        + "\n" + _words.escort.Replace("{escort}", string.Join(", ", trip.Escort))
                        + "\n" + _words.monitoring.Replace("{monitoring}", trip.Monitoring).Replace("{oxygen}", trip.Oxygen ? _words.oxygenYes : _words.oxygenNo);
                    if (!string.IsNullOrEmpty(trip.Arrives)) text += "\n" + _words.arrival.Replace("{time}", trip.Arrives);
                    if (!string.IsNullOrEmpty(trip.Message)) text += "\n" + trip.Message;
                }
            }
            SetText(_status, text);
        }
    }
}