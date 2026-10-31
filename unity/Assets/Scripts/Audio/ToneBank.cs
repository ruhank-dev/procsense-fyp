using System.Collections.Generic;
using UnityEngine;

namespace ProcSense.VR.Audio
{
    /// <summary>
    /// The monitor and equipment sounds, made in code (p0-voice-audio-s05): alarm bursts by priority, the cuff pump, the lab phone and the room beds of noise. Made here, not
    /// recorded, so they need no licence and no download; docs/research/sounds.csv lists each one. The same name always gives the same clip (a fixed noise seed).
    /// </summary>
    public static class ToneBank
    {
        public const int Rate = 22050;
        private static readonly Dictionary<string, AudioClip> Made = new Dictionary<string, AudioClip>();

        /// <summary>An alarm burst: pulses of a tone with a few harmonics (high: 10 pulses in the 3+2 rhythm at a higher pitch; medium: 3; low: 2, lower and softer).</summary>
        public static AudioClip Alarm(string priority, int pulses)
        {
            string key = "alarm_" + priority + "_" + pulses;
            if (Made.TryGetValue(key, out var made)) return made;
            float f = priority == "high" ? 880f : priority == "medium" ? 660f : 520f, gain = priority == "low" ? .35f : .55f;
            const float on = .14f, off = .08f;
            var starts = new List<float>();
            float t = 0;
            for (int i = 0; i < pulses; i++)
            {
                starts.Add(t);
                t += on + off;
                if (priority == "high" && (i == 2 || i == 4 || i == 7)) t += .25f;   // 3 + 2 + 3 + 2
            }
            var data = new float[Mathf.CeilToInt((t + .1f) * Rate)];
            foreach (var s in starts)
                for (int n = 0; n < on * Rate; n++)
                {
                    float x = n / (float)Rate, env = Mathf.Clamp01(x / .01f) * Mathf.Clamp01((on - x) / .02f);
                    float y = Mathf.Sin(2 * Mathf.PI * f * x) + .4f * Mathf.Sin(4 * Mathf.PI * f * x) + .2f * Mathf.Sin(6 * Mathf.PI * f * x);
                    data[Mathf.Min(data.Length - 1, (int)(s * Rate) + n)] += gain * env * y / 1.6f;
                }
            return Keep(key, data);
        }

        /// <summary>The cuff pump: a buzzing motor under a hiss, then the slow bleed.</summary>
        public static AudioClip Cuff()
        {
            if (Made.TryGetValue("cuff", out var made)) return made;
            var rnd = new Xor(7);
            var data = new float[Rate * 5];
            float lp = 0;
            for (int n = 0; n < data.Length; n++)
            {
                float x = n / (float)Rate;
                lp += .08f * (rnd.Next() - .5f - lp);
                float motor = x < 3f ? .25f * Mathf.Sign(Mathf.Sin(2 * Mathf.PI * (90 + 10 * x) * x)) * .4f : 0f;
                float bleed = x >= 3f ? .5f * (1 - (x - 3f) / 2f) : 1f;
                data[n] = (motor + 1.2f * lp) * bleed * Mathf.Clamp01(x / .05f);
            }
            return Keep("cuff", data);
        }

        /// <summary>The lab phone: two close tones, ring-ring then a pause (one cycle, looped).</summary>
        public static AudioClip Phone()
        {
            if (Made.TryGetValue("phone", out var made)) return made;
            var data = new float[Rate * 3];
            for (int n = 0; n < data.Length; n++)
            {
                float x = n / (float)Rate;
                bool ringing = x < .4f || (x >= .6f && x < 1f);
                data[n] = ringing ? .3f * (Mathf.Sin(2 * Mathf.PI * 400 * x) + Mathf.Sin(2 * Mathf.PI * 450 * x)) * Mathf.Sign(Mathf.Sin(2 * Mathf.PI * 20 * x)) * .5f : 0f;
            }
            return Keep("phone", data);
        }

        /// <summary>A room bed (looped): soft brown noise, plus the ventilation hum in theatre and distant voices' murmur on the ward and in the ED.</summary>
        public static AudioClip Room(string room)
        {
            string key = "room_" + room;
            if (Made.TryGetValue(key, out var made)) return made;
            var rnd = new Xor(Fnv(room));
            var data = new float[Rate * 8];
            float brown = 0, murmur = 0;
            for (int n = 0; n < data.Length; n++)
            {
                float x = n / (float)Rate;
                brown = Mathf.Clamp(brown + .02f * (rnd.Next() - .5f), -1, 1) * .998f;
                murmur += .01f * (rnd.Next() - .5f - murmur);
                float y = .5f * brown;
                if (room == "theatre") y += .08f * Mathf.Sin(2 * Mathf.PI * 100 * x) + .05f * Mathf.Sin(2 * Mathf.PI * 50 * x);
                else y += 2.5f * murmur * (.6f + .4f * Mathf.Sin(2 * Mathf.PI * .3f * x));
                data[n] = y;
            }
            // fade the ends together so the loop has no click
            int fade = Rate / 4;
            for (int n = 0; n < fade; n++) { float k = n / (float)fade; data[n] = data[n] * k + data[data.Length - fade + n] * (1 - k); }
            System.Array.Resize(ref data, data.Length - fade);
            return Keep(key, data);
        }

        private static AudioClip Keep(string key, float[] data)
        {
            var clip = AudioClip.Create(key, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            Made[key] = clip;
            return clip;
        }

        private static uint Fnv(string s)
        {
            uint h = 2166136261;
            foreach (char c in s) { h ^= c; h *= 16777619; }
            return h | 1;
        }

        private struct Xor
        {
            private uint _s;
            public Xor(uint seed) { _s = seed == 0 ? 1u : seed; }
            public float Next() { _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5; return (_s & 0xFFFFFF) / (float)0x1000000; }
        }
    }
}
