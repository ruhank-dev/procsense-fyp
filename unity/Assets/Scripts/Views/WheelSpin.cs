using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>Turns the wheels and casters of a finished model as its owner moves (P3-ANI-08), so a pushed trolley or wheelchair rolls instead of sliding.</summary>
    public sealed class WheelSpin : MonoBehaviour
    {
        private static readonly Regex WheelName = new Regex(@"(wheel|caster)\d*$", RegexOptions.IgnoreCase);
        private struct Wheel { public Transform T; public Renderer R; public float Radius; }
        private readonly List<Wheel> _wheels = new List<Wheel>();
        private Transform _owner;
        private Vector3 _last;
        private bool _ready;

        public int Count => _wheels.Count;
        public static bool IsWheel(string name) => WheelName.IsMatch(name);

        /// <summary>Find the wheel nodes under the model; <paramref name="owner"/> is the thing that moves.</summary>
        public void Setup(Transform owner)
        {
            _owner = owner; _wheels.Clear();
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t == transform || !IsWheel(t.name)) continue;
                var r = t.GetComponentInChildren<Renderer>();
                if (r == null) continue;
                _wheels.Add(new Wheel { T = t, R = r, Radius = Mathf.Max(.03f, r.bounds.extents.y) });
            }
            _last = owner.position; _ready = true;
        }

        /// <summary>Roll every wheel for a travel of <paramref name="metres"/> along the owner's forward.</summary>
        public void Roll(float metres)
        {
            foreach (var w in _wheels)
                if (w.T != null) w.T.RotateAround(w.R.bounds.center, _owner.right, metres / w.Radius * Mathf.Rad2Deg);
        }

        private void LateUpdate()
        {
            if (!_ready || _owner == null) return;
            var move = _owner.position - _last; _last = _owner.position;
            Roll(Vector3.Dot(move, _owner.forward));
        }
    }
}
