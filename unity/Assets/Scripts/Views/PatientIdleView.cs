using System;
using System.Collections.Generic;
using ProcSense.VR.ActionEmitters;
using ProcSense.VR.Bridge;
using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>Cosmetic bedside idle only. Does not read PatientState, emit actions or stand in for clinical RR.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-30)]
    public sealed class PatientIdleView : MonoBehaviour
    {
        public const string BreathShape = "ProcSense_IdleBreath";
        [SerializeField] private Transform head;
        [SerializeField] private SkinnedMeshRenderer[] breathSurfaces;
        [SerializeField] private SkinnedMeshRenderer[] faceSurfaces;
        [SerializeField] private ArmMotion[] armMotions;
        [SerializeField] private SensorAttachment[] sensors;
        [SerializeField] private LegMotion[] legMotions;
        [Tooltip("Art-only cycle until Core supplies continuous visible respiratory presentation. Not a measured vital.")]
        [SerializeField, Min(1)] private float idleBreathSeconds = 4.5f;

        private readonly List<Shape> _shapes = new List<Shape>();
        private Quaternion _headRest;
        private float _elapsed;
        private string _sessionId;
        private bool _ready;
        private Quaternion[] _armRest;
        private float _armBlend = 1;
        private Quaternion[] _legRest;

        [Serializable]
        public sealed class LegMotion
        {
            public Transform Bone;
            public Vector3 Axis;
            public float Degrees;
            public float Delay;
        }

        [Serializable]
        public sealed class ArmMotion
        {
            public Transform Bone;
            public Vector3 Axis;
            public float Degrees;
            public float Frequency;
            public float Phase;
        }

        private sealed class Shape
        {
            public SkinnedMeshRenderer Renderer;
            public int Index;
            public string Name;
            public float Rest;
        }

        public float Elapsed => _elapsed;

        public void Configure(Transform headBone, SkinnedMeshRenderer[] breathing, SkinnedMeshRenderer[] face)
        {
            RestorePose();
            head = headBone;
            breathSurfaces = breathing;
            faceSurfaces = face;
            CachePose();
        }

        public void ConfigureArms(ArmMotion[] motions, SensorAttachment[] attachments)
        {
            RestorePose();
            armMotions = motions;
            sensors = attachments;
            CachePose();
        }

        public void ConfigureLegs(LegMotion[] motions)
        {
            RestorePose();
            legMotions = motions;
            CachePose();
        }

        private void OnEnable()
        {
            CachePose();
            _sessionId = SimulationHost.Instance != null ? SimulationHost.Instance.SessionId : null;
            _elapsed = 0;
            _armBlend = 1;
        }

        private void CachePose()
        {
            if (_ready) return;
            if (head != null) _headRest = head.localRotation;
            _armRest = new Quaternion[armMotions?.Length ?? 0];
            for (int i = 0; i < _armRest.Length; i++)
                if (armMotions[i]?.Bone != null) _armRest[i] = armMotions[i].Bone.localRotation;
            _legRest = new Quaternion[legMotions?.Length ?? 0];
            for (int i = 0; i < _legRest.Length; i++)
                if (legMotions[i]?.Bone != null) _legRest[i] = legMotions[i].Bone.localRotation;
            Cache(breathSurfaces, true);
            Cache(faceSurfaces, false);
            _ready = true;
        }

        private void Cache(SkinnedMeshRenderer[] renderers, bool breath)
        {
            if (renderers == null) return;
            foreach (var renderer in renderers)
            {
                if (renderer == null || renderer.sharedMesh == null) continue;
                var mesh = renderer.sharedMesh;
                for (int i = 0; i < mesh.blendShapeCount; i++)
                {
                    string name = mesh.GetBlendShapeName(i);
                    bool selected = breath ? name == BreathShape : name.StartsWith("eyeBlink", StringComparison.Ordinal) || name.StartsWith("eyeLook", StringComparison.Ordinal);
                    if (selected) _shapes.Add(new Shape { Renderer = renderer, Index = i, Name = name, Rest = renderer.GetBlendShapeWeight(i) });
                }
            }
        }

        private void LateUpdate()
        {
            var host = SimulationHost.Instance;
            string id = host != null ? host.SessionId : null;
            if (id != _sessionId)
            {
                RestorePose();
                CachePose();
                _sessionId = id;
                _elapsed = 0;
            }
            if (host != null && (host.Paused || host.Ended)) return;
            bool placing = false;
            if (sensors != null)
                foreach (var sensor in sensors) if (sensor != null && sensor.PlacementGuidesVisible) placing = true;
            _armBlend = Mathf.MoveTowards(_armBlend, placing ? 0 : 1, Time.deltaTime * 3);
            _elapsed += Time.deltaTime;
            EvaluatePose(_elapsed);
        }

        /// <summary>Absolute-time sampling also permits repeatable art review without advancing Core or global random state.</summary>
        public void EvaluatePose(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0) return;
            CachePose();
            float period = Mathf.Max(1, idleBreathSeconds);
            float phase = seconds % period / period;
            // Smooth inhale, slightly longer exhale; displacement is authored in the fitted surfaces.
            float breath = phase < .4f ? Smooth(phase / .4f) : 1 - Smooth((phase - .4f) / .6f);
            float blink = Blink(seconds);
            float horizontal = 7 * Mathf.Sin(seconds * .47f) * Mathf.Sin(seconds * .13f);
            float vertical = 3 * Mathf.Sin(seconds * .31f);
            foreach (var shape in _shapes)
            {
                if (shape.Renderer == null) continue;
                float value;
                switch (shape.Name)
                {
                    case BreathShape: value = breath * 100; break;
                    case "eyeBlinkLeft": case "eyeBlinkRight": value = blink * 100; break;
                    case "eyeLookOutLeft": case "eyeLookInRight": value = Mathf.Max(0, horizontal); break;
                    case "eyeLookInLeft": case "eyeLookOutRight": value = Mathf.Max(0, -horizontal); break;
                    case "eyeLookUpLeft": case "eyeLookUpRight": value = Mathf.Max(0, vertical); break;
                    case "eyeLookDownLeft": case "eyeLookDownRight": value = Mathf.Max(0, -vertical); break;
                    default: continue;
                }
                shape.Renderer.SetBlendShapeWeight(shape.Index, Mathf.Lerp(shape.Rest, 100, value / 100));
            }
            if (head != null)
                head.localRotation = _headRest * Quaternion.Euler(.35f * Mathf.Sin(seconds * .37f),
                    1.8f * Mathf.Sin(seconds * .23f) * Mathf.Sin(seconds * .11f), .2f * Mathf.Sin(seconds * .29f));
            for (int i = 0; i < _armRest.Length; i++)
            {
                var motion = armMotions[i];
                if (motion?.Bone == null) continue;
                float shift = (.5f - .5f * Mathf.Cos(seconds * motion.Frequency)) * (.75f + .25f * Mathf.Sin(seconds * .11f + motion.Phase));
                motion.Bone.localRotation = _armRest[i] * Quaternion.AngleAxis(motion.Degrees * shift * _armBlend, motion.Axis);
            }
            for (int i = 0; i < _legRest.Length; i++)
            {
                var motion = legMotions[i];
                if (motion?.Bone != null)
                    motion.Bone.localRotation = _legRest[i] * Quaternion.AngleAxis(motion.Degrees * LegPulse(seconds, motion.Delay), motion.Axis);
            }
        }

        public static float LegPulse(float seconds, float delay)
        {
            if (seconds < delay) return 0;
            float t = (seconds - delay) % 18;
            if (t > 3.8f) return 0;
            float envelope = Mathf.Sin(Mathf.PI * t / 3.8f);
            return envelope * envelope * Mathf.Sin(t * Mathf.PI * 2 / 1.25f);
        }

        public static float Blink(float seconds)
        {
            float cycle = Mathf.Floor(seconds / 5.7f);
            float start = 2.1f + .9f * (.5f + .5f * Mathf.Sin(cycle * 2.39996f));
            float t = seconds % 5.7f - start;
            if (t < 0 || t > .22f) return 0;
            return t < .075f ? Smooth(t / .075f) : 1 - Smooth((t - .075f) / .145f);
        }

        private static float Smooth(float x) => x * x * (3 - 2 * x);

        public void RestorePose()
        {
            if (!_ready) return;
            if (head != null) head.localRotation = _headRest;
            for (int i = 0; i < _armRest.Length; i++)
                if (armMotions[i]?.Bone != null) armMotions[i].Bone.localRotation = _armRest[i];
            for (int i = 0; i < _legRest.Length; i++)
                if (legMotions[i]?.Bone != null) legMotions[i].Bone.localRotation = _legRest[i];
            foreach (var shape in _shapes)
                if (shape.Renderer != null) shape.Renderer.SetBlendShapeWeight(shape.Index, shape.Rest);
            _shapes.Clear();
            _ready = false;
        }

        private void OnDisable() { RestorePose(); _elapsed = 0; }
    }
}
