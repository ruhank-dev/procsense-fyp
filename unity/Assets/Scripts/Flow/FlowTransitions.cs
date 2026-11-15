using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.Flow
{
    /// <summary>
    /// Comfort-safe transitions of the game flow (P3-ANI-10, with the results dim of P3-RES-03): when a new case starts the view dips to dark and back (the patient and the room are reset
    /// behind the dark), and on the results the room dims to half while the results panel stays bright. One black quad just in front of the eyes (a world-space canvas on the head, the
    /// way LocomotionFade does it, so it is the same in both eyes), sorted under the flow panel; the camera is never moved or rotated, so a transition has no frame of camera motion.
    /// </summary>
    public sealed class FlowTransitions : MonoBehaviour
    {
        public const float DipOut = .15f, DipIn = .3f, ResultsDim = .5f, DimSeconds = .4f;
        public const int VeilOrder = 30000, PanelOrder = 31000;

        public MenuFlow Flow { get; private set; }
        /// <summary>The eyes (the main camera unless set).</summary>
        public Transform Head;
        public float Alpha => _veil != null ? _veil.color.a : 0f;
        /// <summary>True while a dip to dark runs (out or back in).</summary>
        public bool Dipping => _dip > 0;

        private Image _veil;
        private MenuPhase _last = (MenuPhase)(-1);
        private float _target, _rate;
        private int _dip;   // 0 none, 1 going dark, 2 coming back

        public void Attach(MenuFlow flow, FlowPanel panel)
        {
            Flow = flow;
            if (panel != null && panel.Canvas != null) { panel.Canvas.overrideSorting = true; panel.Canvas.sortingOrder = PanelOrder; }   // the menu stays bright above the dim
            _last = flow.Phase;
            flow.Changed += OnChanged;
        }

        private void OnDestroy()
        {
            if (Flow != null) Flow.Changed -= OnChanged;
            if (_veil != null) Destroy(_veil.gameObject);
        }

        private void OnChanged()
        {
            var now = Flow.Phase;
            if (now == _last) return;
            var before = _last;
            _last = now;
            if (now == MenuPhase.Briefing && (before == MenuPhase.Surgery || before == MenuPhase.Results || before == MenuPhase.MainMenu)) Dip();
            else if (now == MenuPhase.Results) To(ResultsDim, DimSeconds);
            else if (before == MenuPhase.Results) To(0f, DimSeconds);
        }

        /// <summary>Dark and back: a new case starts.</summary>
        public void Dip() { _dip = 1; To(1f, DipOut); }

        private void To(float target, float seconds)
        {
            Ensure();
            _target = target;
            _rate = Mathf.Abs(target - Alpha) / Mathf.Max(.01f, seconds);
        }

        /// <summary>Advances the veil (Update calls it; tests call it directly).</summary>
        public void Step(float dt)
        {
            if (_veil == null) return;
            var c = _veil.color;
            c.a = Mathf.MoveTowards(c.a, _target, _rate * dt);
            _veil.color = c;
            _veil.enabled = c.a > .001f;
            if (_dip == 1 && Mathf.Approximately(c.a, 1f)) { _dip = 2; To(Flow != null && Flow.Phase == MenuPhase.Results ? ResultsDim : 0f, DipIn); }
            else if (_dip == 2 && Mathf.Approximately(c.a, _target)) _dip = 0;
        }

        private void Update() => Step(Time.unscaledDeltaTime);

        private void Ensure()
        {
            if (_veil != null) return;
            var head = Head != null ? Head : (Camera.main != null ? Camera.main.transform : null);
            var go = new GameObject("Flow transition veil", typeof(RectTransform), typeof(Canvas), typeof(Image));
            if (head != null) go.transform.SetParent(head, false);
            var cam = head != null ? head.GetComponent<Camera>() : null;
            go.transform.localPosition = Vector3.forward * Mathf.Max(.15f, (cam != null ? cam.nearClipPlane : .1f) + .05f);
            go.transform.localRotation = Quaternion.identity;
            ((RectTransform)go.transform).sizeDelta = new Vector2(4, 4);
            go.transform.localScale = Vector3.one * .25f;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = VeilOrder;
            _veil = go.GetComponent<Image>();
            _veil.raycastTarget = false;
            _veil.color = new Color(0, 0, 0, 0);
            _veil.enabled = false;
        }
    }
}
