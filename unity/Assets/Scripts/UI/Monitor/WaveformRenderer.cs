using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.UI.Monitor
{
    /// <summary>The shape of one monitor trace over one cycle (phase 0 to 1).</summary>
    public enum TraceKind { Ecg, Pleth, Capno }

    /// <summary>
    /// The trace shapes. Pure functions of the phase, so the same rate always draws the same trace. They are teaching shapes (a clean sinus beat, a pulse wave with its
    /// notch, a square-topped capnogram), not recordings: the PTB-XL beat templates the plan names are not in the content yet.
    /// </summary>
    public static class TraceShapes
    {
        public static float Sample(TraceKind kind, float phase, float amplitude = 1f)
        {
            phase -= Mathf.Floor(phase);
            switch (kind)
            {
                case TraceKind.Ecg:
                    return amplitude * (G(phase, .16f, .025f, .12f) - G(phase, .36f, .008f, .10f) + G(phase, .39f, .010f, 1f) - G(phase, .42f, .010f, .22f) + G(phase, .66f, .045f, .25f));
                case TraceKind.Pleth:
                    return amplitude * (G(phase, .22f, .07f, 1f) + G(phase, .48f, .06f, .38f) - .05f);
                default:
                    // inspiration (0 to .45) is near zero, expiration rises to a plateau and falls at the next breath
                    float up = Mathf.Clamp01((phase - .45f) / .06f), down = Mathf.Clamp01((phase - .94f) / .05f);
                    return amplitude * (up * (.92f + .08f * (phase - .5f) / .44f) * (1 - down));
            }
        }

        private static float G(float x, float mu, float sigma, float height) => height * Mathf.Exp(-(x - mu) * (x - mu) / (2 * sigma * sigma));
    }

    /// <summary>
    /// A sweeping monitor trace: samples go into a ring buffer at a fixed rate (not the frame rate, so no jitter) and the sweep erases a gap ahead of the pen, as a real
    /// monitor does. The mesh is rebuilt only when a sample was added. A flat line with "no signal" text is drawn when the trace is off.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WaveformRenderer : MaskableGraphic
    {
        public TraceKind Kind = TraceKind.Pleth;
        public float Thickness = 3f;

        /// <summary>Cycles per minute (heart rate, or breaths for the capnogram); 0 or less is no signal.</summary>
        public float Rate { get; set; }
        /// <summary>0 to 1: a poorly placed probe gives a small, noisy pleth.</summary>
        public float Amplitude { get; set; } = 1f;
        public float Noise { get; set; }
        public bool On { get; set; }

        private float[] _ring = new float[400];
        private int _pen;
        private float _phase, _accum;
        private int _sampleHz = 100;
        private uint _noiseState = 2463534242;
        private bool _dirty = true;

        /// <summary>Samples written so far (tests).</summary>
        public int Written { get; private set; }

        public void Configure(int sampleHz, float sweepSeconds)
        {
            _sampleHz = Mathf.Max(10, sampleHz);
            _ring = new float[Mathf.Max(20, Mathf.RoundToInt(_sampleHz * sweepSeconds))];
            _pen = 0;
            _dirty = true;
        }

        /// <summary>Advances the trace by dt seconds at the fixed sample rate; returns the samples added.</summary>
        public int Advance(float dt)
        {
            _accum += dt;
            float step = 1f / _sampleHz;
            int n = 0;
            while (_accum >= step && n < _ring.Length)
            {
                _accum -= step;
                float y = 0;
                if (On && Rate > 0)
                {
                    _phase += Rate / 60f * step;
                    if (_phase > 1) _phase -= 1;
                    y = TraceShapes.Sample(Kind, _phase, Amplitude);
                    if (Noise > 0) y += Noise * (Next() - .5f);
                }
                _ring[_pen] = y;
                _pen = (_pen + 1) % _ring.Length;
                n++;
            }
            if (_accum > step) _accum = 0;   // a long frame drops the backlog rather than racing to catch up
            if (n > 0) { Written += n; _dirty = true; }
            return n;
        }

        private void Update()
        {
            Advance(Time.unscaledDeltaTime);
            if (_dirty) { _dirty = false; SetVerticesDirty(); }
        }

        private float Next()
        {
            _noiseState ^= _noiseState << 13; _noiseState ^= _noiseState >> 17; _noiseState ^= _noiseState << 5;
            return (_noiseState & 0xFFFFFF) / (float)0x1000000;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            int count = _ring.Length, gap = Mathf.Max(2, count / 25);
            float dx = r.width / (count - 1);
            float Y(float v) => r.yMin + r.height * (.15f + .7f * Mathf.Clamp(v * .8f + (Kind == TraceKind.Ecg ? .2f : .05f), -.1f, 1.1f));
            for (int i = 0; i < count - 1; i++)
            {
                int ahead = (i - _pen + count) % count;
                if (ahead < gap) continue;   // the erase bar just ahead of the pen
                var a = new Vector2(r.xMin + i * dx, Y(_ring[i]));
                var b = new Vector2(r.xMin + (i + 1) * dx, Y(_ring[i + 1]));
                AddSegment(vh, a, b);
            }
        }

        private void AddSegment(VertexHelper vh, Vector2 a, Vector2 b)
        {
            var d = (b - a).normalized;
            var n = new Vector2(-d.y, d.x) * (Thickness * .5f);
            int start = vh.currentVertCount;
            var v = UIVertex.simpleVert;
            v.color = color;
            v.position = a - n; vh.AddVert(v);
            v.position = a + n; vh.AddVert(v);
            v.position = b + n; vh.AddVert(v);
            v.position = b - n; vh.AddVert(v);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
