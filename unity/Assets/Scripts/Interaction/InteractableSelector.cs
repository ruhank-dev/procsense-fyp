using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ProcSense.VR.Interaction
{
    /// <summary>
    /// P3-CTX-03: choosing what to act on. Each hand's ray (the controller's interactor, or the camera in the Editor) is tested against the bounds of every <see cref="Interactable"/>; the
    /// nearest one under a ray is highlighted (a brighter tint through a property block on its own renderers: no extra pass, no outline mesh). The trigger opens its contextual panel, the
    /// trigger again or B (Escape on the keyboard) closes it; a hand within <see cref="PokeReach"/> of an entity picks it without the ray. Nothing happens while the hand holds something or the ray is
    /// on a panel, so grabbing and the menus keep working.
    /// </summary>
    public sealed class InteractableSelector : MonoBehaviour
    {
        public const float MaxDistance = 6f, PokeReach = .12f, Brighten = 1.35f;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor"), Color = Shader.PropertyToID("_Color");

        public Interactable Hovered { get; private set; }

        private InputAction _left, _right, _back;
        private readonly List<(Renderer r, MaterialPropertyBlock saved)> _lit = new List<(Renderer, MaterialPropertyBlock)>();
        private readonly List<(Transform t, bool left)> _hands = new List<(Transform, bool)>();
        private float _nextHandScan;

        private void OnEnable()
        {
            _left = new InputAction("context select left", InputActionType.Button, "<XRController>{LeftHand}/{TriggerButton}");
            _right = new InputAction("context select right", InputActionType.Button, "<XRController>{RightHand}/{TriggerButton}");
            _right.AddBinding("<Mouse>/rightButton");
            _back = new InputAction("context back", InputActionType.Button, "<XRController>{RightHand}/secondaryButton");
            _back.AddBinding("<XRController>{LeftHand}/secondaryButton");
            _back.AddBinding("<Keyboard>/escape");
            _left.Enable(); _right.Enable(); _back.Enable();
        }

        private void OnDisable()
        {
            _left?.Dispose(); _right?.Dispose(); _back?.Dispose();
            SetHover(null);
        }

        /// <summary>The nearest interactable whose bounds the ray enters within <see cref="MaxDistance"/>, or null.</summary>
        public static Interactable Pick(Ray ray, IEnumerable<Interactable> all, out float distance)
        {
            Interactable best = null; distance = MaxDistance;
            foreach (var i in all)
            {
                if (i == null || !i.isActiveAndEnabled || !Bounds(i, out var b)) continue;
                if (b.IntersectRay(ray, out float d) && d < distance) { distance = d; best = i; }
            }
            return best;
        }

        /// <summary>An interactable within reach of a hand (poke selection at arm's length), or null.</summary>
        public static Interactable Poke(Vector3 hand, IEnumerable<Interactable> all)
        {
            Interactable best = null; float bestD = PokeReach;
            foreach (var i in all)
            {
                if (i == null || !i.isActiveAndEnabled || !Bounds(i, out var b)) continue;
                float d = Mathf.Sqrt(b.SqrDistance(hand));
                if (d <= bestD) { bestD = d; best = i; }
            }
            return best;
        }

        public static bool Bounds(Interactable i, out Bounds b)
        {
            b = default; bool any = false;
            foreach (var r in i.Highlight)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return any;
        }

        /// <summary>Highlights one entity (or none): its renderers drawn a little brighter until the hover moves on.</summary>
        public void SetHover(Interactable i)
        {
            if (i == Hovered) return;
            foreach (var (r, saved) in _lit) if (r != null) r.SetPropertyBlock(saved);
            _lit.Clear();
            Hovered = i;
            if (i == null) return;
            foreach (var r in i.Highlight)
            {
                if (r == null) continue;
                var saved = new MaterialPropertyBlock();
                r.GetPropertyBlock(saved);
                _lit.Add((r, saved));
                var lit = new MaterialPropertyBlock();
                r.GetPropertyBlock(lit);
                var m = r.sharedMaterial;
                if (m == null) continue;
                if (m.HasProperty(BaseColor)) lit.SetColor(BaseColor, m.GetColor(BaseColor) * Brighten);
                if (m.HasProperty(Color)) lit.SetColor(Color, m.GetColor(Color) * Brighten);
                r.SetPropertyBlock(lit);
            }
        }

        /// <summary>A trigger from this ray (or hand position): the entity under it opens, or the open panel's entity closes.</summary>
        public ContextPanel Select(Ray ray, Vector3? hand = null)
        {
            var target = (hand.HasValue ? Poke(hand.Value, Interactable.All) : null) ?? Pick(ray, Interactable.All, out _);
            if (target == null) return null;
            return ContextPanel.Toggle(target);
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextHandScan) { _nextHandScan = Time.unscaledTime + 2f; ScanHands(); }
            Interactable hover = null;
            foreach (var (t, left) in _hands)
            {
                if (t == null || Busy(t)) continue;
                var h = Pick(new Ray(t.position, t.forward), Interactable.All, out _);
                if (h != null) hover = h;
                var press = left ? _left : _right;
                if (press != null && press.WasPressedThisFrame()) Select(new Ray(t.position, t.forward), t.position);
            }
            if (_hands.Count == 0)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    var ray = Mouse.current != null ? cam.ScreenPointToRay(Mouse.current.position.ReadValue()) : new Ray(cam.transform.position, cam.transform.forward);
                    hover = Pick(ray, Interactable.All, out _);
                    if (_right.WasPressedThisFrame()) Select(ray);
                }
            }
            SetHover(hover);
            if (_back.WasPressedThisFrame() && ContextPanel.Open != null) ContextPanel.Open.Close();
        }

        // the ray interactors of the rig, by hand; a hand that is holding something or pointing at a panel does not select
        private void ScanHands()
        {
            _hands.Clear();
            foreach (var i in FindObjectsByType<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor>(FindObjectsSortMode.None))
            {
                if (!(i is UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor) && !(i is UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor)) continue;
                _hands.Add((i.transform, i.handedness == UnityEngine.XR.Interaction.Toolkit.Interactors.InteractorHandedness.Left));
            }
        }

        private static bool Busy(Transform hand)
        {
            var i = hand.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInputInteractor>();
            if (i == null) return false;
            if (i.hasSelection) return true;
            return i is UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor ray && ray.TryGetCurrentUIRaycastResult(out _);
        }
    }
}
