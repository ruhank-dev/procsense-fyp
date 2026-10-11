using ProcSense.Core.Observation;
using ProcSense.VR.Bridge;
using TMPro;
using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// Base for everything that shows Core's ObservableState. A view only reads it (never PatientState) and never decides anything.
    /// Text panels redraw at most 10 times a second and only when something changed (plan p0-vr-platform-s03).
    /// </summary>
    public abstract class StateView : MonoBehaviour
    {
        [SerializeField] private float minInterval = 0.1f;

        private SimulationHost _host;
        private ObservableState _pending;
        private float _lastRender = float.NegativeInfinity;

        protected ObservableState Last { get; private set; }

        public float MinInterval { get => minInterval; set => minInterval = value; }

        protected virtual void OnEnable() => Bind();

        protected virtual void OnDisable()
        {
            if (_host != null) _host.Changed -= OnChanged;
            _host = null;
            _pending = null;
        }

        private void Bind()
        {
            if (_host != null) _host.Changed -= OnChanged;
            _host = SimulationHost.Instance;
            _pending = null;
            if (_host == null) return;
            _host.Changed += OnChanged;
            if (_host.Observable != null) _pending = _host.Observable;
        }

        private void OnChanged(ObservableState state) => _pending = state;

        protected virtual void Update()
        {
            if (!ReferenceEquals(_host, SimulationHost.Instance)) Bind();
            if (_pending != null && Time.unscaledTime - _lastRender >= minInterval) RenderNow();
        }

        /// <summary>Draws the newest state immediately (tests and first frame).</summary>
        public void RenderNow()
        {
            if (_pending == null) _pending = SimulationHost.Instance != null ? SimulationHost.Instance.Observable : null;
            if (_pending == null) return;
            Last = _pending;
            _pending = null;
            _lastRender = Time.unscaledTime;
            Render(Last);
        }

        protected abstract void Render(ObservableState state);

        /// <summary>Sets text only when it changed, so an unchanged panel costs nothing.</summary>
        protected static void SetText(TMP_Text label, string value)
        {
            if (label != null && label.text != value) label.text = value;
        }
    }
}
