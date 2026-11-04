using System;
using System.Collections.Generic;
using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// Speech-driven face for the patient: while the patient's voice line plays on an AudioSource the jaw, lips and brows follow it, using only the audio itself (loudness and three spectral
    /// bands), so there is no extra dependency and no link to the dialogue text. Loudness opens the jaw; the spectrum picks the mouth shape (low = rounded O/U, mid = open AA, high-mid = E/I,
    /// strong high band = SS), and a fast rise out of silence closes the lips briefly for a plosive (PP). Shapes are the Meta viseme and ARKit blendshapes already on the MPFB patient
    /// (viseme_*, jawOpen, mouthFunnel/Pucker/Close, browInnerUp). Missing shapes are skipped, never an error.
    /// Cosmetic only: it never reads PatientState, sends actions or decides what is said. <see cref="Alertness"/> is a presentation gate (a future Core handoff supplies it): an
    /// unresponsive patient does not speak or mouth words.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-20)]
    public sealed class PatientSpeechFace : MonoBehaviour
    {
        public enum PresentationAlertness { Awake, Drowsy, Unresponsive }

        [SerializeField] private SkinnedMeshRenderer[] faces = Array.Empty<SkinnedMeshRenderer>();
        [SerializeField] private AudioSource source;
        [SerializeField] private PresentationAlertness alertness = PresentationAlertness.Awake;
        [SerializeField, Range(0f, 1.5f)] private float gain = .8f;
        [SerializeField, Min(.005f)] private float attack = .035f;
        [SerializeField, Min(.005f)] private float release = .09f;

        private static readonly string[] Shapes = { "jawOpen", "viseme_aa", "viseme_O", "viseme_U", "viseme_E", "viseme_I", "viseme_SS", "viseme_PP", "mouthClose", "mouthFunnel", "mouthPucker", "browInnerUp", "mouthSmileLeft", "mouthSmileRight" };
        private readonly float[] _target = new float[Shapes.Length], _value = new float[Shapes.Length];
        private int[][] _index = Array.Empty<int[]>();
        private readonly float[] _wave = new float[256], _spec = new float[512];
        private float _lastLevel, _plosive, _scan;

        public PresentationAlertness Alertness { get => alertness; set => alertness = value; }
        public float Level { get; private set; }
        public bool Speaking { get; private set; }
        public int ShapesFound { get { int n = 0; foreach (var f in _index) foreach (var i in f) if (i >= 0) n++; return n; } }
        public int Surfaces => faces.Length;

        public void Configure(SkinnedMeshRenderer[] faceRenderers) { faces = faceRenderers ?? Array.Empty<SkinnedMeshRenderer>(); Resolve(); }

        private void Awake() => Resolve();

        private void Resolve()
        {
            _index = new int[faces.Length][];
            for (int f = 0; f < faces.Length; f++)
            {
                _index[f] = new int[Shapes.Length];
                for (int i = 0; i < Shapes.Length; i++) _index[f][i] = faces[f] != null && faces[f].sharedMesh != null ? faces[f].sharedMesh.GetBlendShapeIndex(Shapes[i]) : -1;
            }
        }

        private AudioSource Find()
        {
            if (source != null && source.isPlaying) return source;
            _scan -= Time.unscaledDeltaTime;
            if (_scan > 0f) return source;
            _scan = .25f;
            source = null;
            foreach (var s in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                if (s.isPlaying && s.clip != null && s.clip.length > .2f && !s.loop) { source = s; break; }
            return source;
        }

        /// <summary>Feature extraction from one audio frame. Public for tests: returns loudness 0..1 and the three band shares (low, mid, high).</summary>
        public static void Analyse(float[] wave, float[] spectrum, float sampleRate, out float level, out float low, out float mid, out float high)
        {
            double sq = 0; foreach (var w in wave) sq += w * w;
            level = Mathf.Clamp01(Mathf.Sqrt((float)(sq / wave.Length)) * 6f);
            float bin = sampleRate * .5f / spectrum.Length;
            float lo = 0, mi = 0, hi = 0;
            for (int i = 1; i < spectrum.Length; i++)
            {
                float f = i * bin;
                if (f >= 100 && f < 600) lo += spectrum[i]; else if (f >= 600 && f < 2200) mi += spectrum[i]; else if (f >= 2800 && f < 6500) hi += spectrum[i];
            }
            float t = lo + mi + hi + 1e-9f; low = lo / t; mid = mi / t; high = hi / t;
        }

        private void LateUpdate()
        {
            Array.Clear(_target, 0, _target.Length);
            var s = alertness == PresentationAlertness.Unresponsive ? null : Find();
            Speaking = s != null && s.isPlaying;
            if (Speaking)
            {
                s.GetOutputData(_wave, 0); s.GetSpectrumData(_spec, 0, FFTWindow.Hanning);
                Analyse(_wave, _spec, AudioSettings.outputSampleRate, out var level, out var lo, out var mid, out var hi);
                Level = level;
                Drive(level, lo, mid, hi, Time.deltaTime);
            }
            else Level = 0f;
            Apply(Time.deltaTime);
        }

        /// <summary>Maps features to target shape weights (0..1). Separate from LateUpdate so a test can feed synthetic features.</summary>
        public void Drive(float level, float low, float mid, float high, float dt)
        {
            float k = alertness == PresentationAlertness.Drowsy ? .55f : 1f;
            if (_lastLevel < .06f && level > .22f) _plosive = .07f;                                    // fast rise from silence: lips close for a moment
            _lastLevel = level; _plosive = Mathf.Max(0f, _plosive - dt);
            float open = Mathf.Clamp01(level * 1.25f) * k;
            bool closed = _plosive > 0f;
            _target[0] = closed ? 0f : open * (.35f + .55f * mid + .25f * low);                        // jawOpen
            _target[1] = closed ? 0f : open * Mathf.Clamp01(mid * 1.4f - .1f);                          // aa
            _target[2] = open * low * .8f;                                                              // O
            _target[3] = open * low * .35f;                                                             // U
            _target[4] = open * Mathf.Clamp01((mid * .6f + high * .8f) - .15f);                        // E
            _target[5] = open * Mathf.Clamp01(high * .7f - .1f);                                        // I
            _target[6] = open * Mathf.Clamp01((high - .45f) * 2f);                                      // SS
            _target[7] = closed ? .9f * k : 0f;                                                         // PP
            _target[8] = closed ? .7f * k : 0f;                                                         // mouthClose
            _target[9] = open * low * .45f; _target[10] = open * low * .3f;                              // funnel / pucker
            _target[11] = Mathf.Clamp01(level - .35f) * .35f * k;                                      // brow lift on stressed syllables
            _target[12] = _target[13] = open * .08f;                                                    // faint corner lift while talking
        }

        private void Apply(float dt)
        {
            if (faces.Length == 0) return;
            for (int i = 0; i < Shapes.Length; i++)
            {
                float t = _target[i], v = _value[i];
                float rate = t > v ? attack : release;
                v = Mathf.Lerp(v, t, 1f - Mathf.Exp(-dt / rate));
                _value[i] = v;
                float w = Mathf.Clamp(v * gain * 100f, 0f, 100f);
                for (int f = 0; f < faces.Length; f++) if (faces[f] != null && _index[f][i] >= 0) faces[f].SetBlendShapeWeight(_index[f][i], w);
            }
        }

        /// <summary>Current smoothed weight (0..1) of a shape by name; used by tests and reviews.</summary>
        public float Weight(string shape) { int i = Array.IndexOf(Shapes, shape); return i < 0 ? 0f : _value[i]; }

        /// <summary>Steps the smoothing without audio (tests): advances toward the last Drive() targets.</summary>
        public void StepForTest(float dt) => Apply(dt);
    }
}
