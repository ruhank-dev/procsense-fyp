using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ProcSense.VR.Diagnostics
{
    /// <summary>One measured window of frames: what the diagnostic build writes per row (P3-PRF-01). Times are milliseconds; FPS is derived from the frame times, never assumed.</summary>
    public readonly struct PerfSummary
    {
        public readonly int Frames;
        public readonly double AvgFps, OnePercentLowFps, AvgFrameMs, MaxFrameMs, AvgCpuMs, AvgGpuMs;
        public readonly bool HasCpu, HasGpu;

        public PerfSummary(int frames, double avgFps, double low, double avgMs, double maxMs, double cpu, bool hasCpu, double gpu, bool hasGpu)
        { Frames = frames; AvgFps = avgFps; OnePercentLowFps = low; AvgFrameMs = avgMs; MaxFrameMs = maxMs; AvgCpuMs = cpu; HasCpu = hasCpu; AvgGpuMs = gpu; HasGpu = hasGpu; }

        /// <summary>A CSV-safe, culture-invariant rendering; "n/a" where the platform gave no CPU or GPU time.</summary>
        public string Csv() => string.Join(",", Frames.ToString(CultureInfo.InvariantCulture), F(AvgFps), F(OnePercentLowFps), F(AvgFrameMs), F(MaxFrameMs), HasCpu ? F(AvgCpuMs) : "n/a", HasGpu ? F(AvgGpuMs) : "n/a");

        public const string CsvHeader = "frames,avg_fps,one_percent_low_fps,avg_frame_ms,max_frame_ms,avg_cpu_ms,avg_gpu_ms";

        private static string F(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Collects frame times (and the CPU/GPU times from FrameTimingManager when the platform reports them) and summarises them. Pure C#, so EditMode tests can check the arithmetic.
    /// "1% low" is the average FPS of the slowest 1% of frames (at least one frame), the usual definition in frame-time analysis.
    /// </summary>
    public sealed class PerfWindow
    {
        private readonly List<double> _frameMs = new List<double>(), _cpuMs = new List<double>(), _gpuMs = new List<double>();

        public int Count => _frameMs.Count;

        public void Add(double frameMs, double cpuMs = double.NaN, double gpuMs = double.NaN)
        {
            if (!(frameMs > 0) || double.IsInfinity(frameMs)) return;     // a zero or broken delta is not a frame
            _frameMs.Add(frameMs);
            if (cpuMs > 0 && !double.IsInfinity(cpuMs)) _cpuMs.Add(cpuMs);
            if (gpuMs > 0 && !double.IsInfinity(gpuMs)) _gpuMs.Add(gpuMs);
        }

        public void Clear() { _frameMs.Clear(); _cpuMs.Clear(); _gpuMs.Clear(); }

        public PerfSummary Summarise()
        {
            if (_frameMs.Count == 0) return new PerfSummary(0, 0, 0, 0, 0, 0, false, 0, false);
            double total = _frameMs.Sum();
            double avgFps = 1000.0 * _frameMs.Count / total;
            int worstN = Math.Max(1, (int)Math.Ceiling(_frameMs.Count * 0.01));
            double worstMs = _frameMs.OrderByDescending(x => x).Take(worstN).Average();
            return new PerfSummary(_frameMs.Count, avgFps, 1000.0 / worstMs, total / _frameMs.Count, _frameMs.Max(),
                _cpuMs.Count > 0 ? _cpuMs.Average() : 0, _cpuMs.Count > 0, _gpuMs.Count > 0 ? _gpuMs.Average() : 0, _gpuMs.Count > 0);
        }
    }
}
