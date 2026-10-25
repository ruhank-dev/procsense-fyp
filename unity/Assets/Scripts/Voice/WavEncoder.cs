using System;

namespace ProcSense.VR.Voice
{
    /// <summary>
    /// Turns what the microphone heard into the clip the speech recogniser takes (plan p1-voice-input-s01): mono, 16 kHz, 16-bit PCM in a WAV container, at most <see cref="MaxSeconds"/> long (the service refuses longer clips).
    /// Pure arithmetic, so it is tested without a microphone.
    /// </summary>
    public static class WavEncoder
    {
        public const int SampleRate = 16000;
        public const int MaxSeconds = 15;

        /// <summary>Linear resampling to 16 kHz; a clip already at 16 kHz is returned as it is.</summary>
        public static float[] Resample(float[] samples, int fromRate)
        {
            if (samples == null || samples.Length == 0 || fromRate <= 0) return new float[0];
            if (fromRate == SampleRate) return samples;
            int outLength = (int)((long)samples.Length * SampleRate / fromRate);
            var result = new float[outLength];
            double step = (double)fromRate / SampleRate;
            for (int i = 0; i < outLength; i++)
            {
                double position = i * step;
                int lo = (int)position;
                int hi = Math.Min(lo + 1, samples.Length - 1);
                double fraction = position - lo;
                result[i] = (float)(samples[lo] * (1 - fraction) + samples[hi] * fraction);
            }
            return result;
        }

        /// <summary>The WAV bytes of the samples (resampled to 16 kHz and cut at the longest clip the service takes); empty when there is nothing to send.</summary>
        public static byte[] Encode(float[] samples, int fromRate)
        {
            var mono = Resample(samples, fromRate);
            if (mono.Length == 0) return new byte[0];
            int count = Math.Min(mono.Length, SampleRate * MaxSeconds);
            int dataBytes = count * 2;
            var wav = new byte[44 + dataBytes];
            Write(wav, 0, "RIFF"); WriteInt(wav, 4, 36 + dataBytes); Write(wav, 8, "WAVE"); Write(wav, 12, "fmt ");
            WriteInt(wav, 16, 16); WriteShort(wav, 20, 1); WriteShort(wav, 22, 1); WriteInt(wav, 24, SampleRate); WriteInt(wav, 28, SampleRate * 2); WriteShort(wav, 32, 2); WriteShort(wav, 34, 16);
            Write(wav, 36, "data"); WriteInt(wav, 40, dataBytes);
            for (int i = 0; i < count; i++)
            {
                float clamped = Math.Max(-1f, Math.Min(1f, mono[i]));
                WriteShort(wav, 44 + i * 2, (short)Math.Round(clamped * short.MaxValue));
            }
            return wav;
        }

        /// <summary>True when the clip is long enough to be speech (at least a quarter of a second) and not silence.</summary>
        public static bool HasSpeech(float[] samples, int fromRate, float silenceLevel = 0.01f)
        {
            if (samples == null || fromRate <= 0 || samples.Length < fromRate / 4) return false;
            foreach (var s in samples) if (Math.Abs(s) > silenceLevel) return true;
            return false;
        }

        private static void Write(byte[] to, int at, string text) { for (int i = 0; i < text.Length; i++) to[at + i] = (byte)text[i]; }
        private static void WriteInt(byte[] to, int at, int value) { to[at] = (byte)value; to[at + 1] = (byte)(value >> 8); to[at + 2] = (byte)(value >> 16); to[at + 3] = (byte)(value >> 24); }
        private static void WriteShort(byte[] to, int at, short value) { to[at] = (byte)value; to[at + 1] = (byte)(value >> 8); }
    }
}
