using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.Flow
{
    /// <summary>
    /// Comfort-safe transitions of the game flow (P3-ANI-10, with the results transition of P3-RES-03): when a new case starts the view dips to dark and back (the patient and the room are reset
    /// behind the dark), and on the results the room dims to half while the results panel stays bright and rises into place, and the hands can no longer grab or teleport (the ambience
    /// duck is the AudioDirector's Results state). No post-processing and no extra camera: the dim is one transparent quad on the existing eye render. One black quad just in front of the eyes (a world-space canvas on the head, the
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

        /// <summary>The results panel rises this far (metres) into place while the room dims (P3-RES-03).</summary>
        public const float RiseDistance = .12f;
        /// <summary>True while the results show: hand grabs and teleport are switched off, the menu rays still work (P3-RES-03).</summary>
        public bool InputFrozen => _frozen.Count > 0 || _freezing;

        private Transform _panel;
        private Vector3 _rest;
        private float _rise = 1f;
        private bool _freezing;
        private readonly System.Collections.Generic.Dictionary<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor, UnityEngine.XR.Interaction.Toolkit.InteractionLayerMask> _frozen =
            new System.Collections.Generic.Dictionary<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor, UnityEngine.XR.Interaction.Toolkit.InteractionLayerMask>();

        public void Attach(MenuFlow flow, FlowPanel panel)
        {
            Flow = flow;
            _panel = panel != null && panel.Canvas != null ? panel.Canvas.transform : null;
            if (panel != null && panel.Canvas != null) { panel.Canvas.overrideSorting = true; panel.Canvas.sortingOrder = PanelOrder; }   // the menu stays bright above the dim
            _last = flow.Phase;
            flow.Changed += OnChanged;
        }

        private void OnDestroy()
        {
            if (Flow != null) Flow.Changed -= OnChanged;
            Thaw();
            if (_veil != null) Destroy(_veil.gameObject);
        }

        private void OnChanged()
        {
            var now = Flow.Phase;
            if (now == _last) return;
            var before = _last;
            _last = now;
            if (now == MenuPhase.Briefing && (before == MenuPhase.Surgery || before == MenuPhase.Results || before == MenuPhase.MainMenu)) Dip();
            else if (now == MenuPhase.Results) { To(ResultsDim, DimSeconds); Freeze(); Rise(); }
            if (before == MenuPhase.Results && now != MenuPhase.Results) { if (now != MenuPhase.Briefing) To(0f, DimSeconds); Thaw(); Settle(); }
        }

        /// <summary>Results: every hand interactor (not the equipment sockets) loses its interaction layers, so nothing in the room can be grabbed or teleported to; the UI rays are not
        /// governed by interaction layers, so the results panel still answers. The old layers come back when the results close.</summary>
        public void Freeze()
        {
            _freezing = true;
            foreach (var i in FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (i is UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor || _frozen.ContainsKey(i)) continue;
                _frozen[i] = i.interactionLayers;
                i.interactionLayers = 0;
            }
        }

        public void Thaw()
        {
            foreach (var kv in _frozen) if (kv.Key != null) kv.Key.interactionLayers = kv.Value;
            _frozen.Clear();
            _freezing = false;
        }

        // the panel rises from a little below into its place (decorative: with reduce motion it is simply there)
        private void Rise()
        {
            if (_panel == null) return;
            if (_rise < 1f) _panel.localPosition = _rest;
            _rest = _panel.localPosition;
            _rise = ProcSense.VR.Settings.GameSettings.Current.reduceMotion ? 1f : 0f;
            _panel.localPosition = _rest + Vector3.down * RiseDistance * (1f - _rise);
        }

        private void Settle() { if (_panel != null && _rise < 1f) _panel.localPosition = _rest; _rise = 1f; }

        /// <summary>How far the results panel still is below its place (0 when risen).</summary>
        public float RiseLeft => _panel == null || _rise >= 1f ? 0f : RiseDistance * (1f - ProcSense.VR.UI.Theme.UiMotion.EaseOut(_rise));

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
            if (_panel != null && _rise < 1f)
            {
                _rise = Mathf.Min(1f, _rise + dt / DimSeconds);
                _panel.localPosition = _rest + Vector3.down * RiseLeft;
            }
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
