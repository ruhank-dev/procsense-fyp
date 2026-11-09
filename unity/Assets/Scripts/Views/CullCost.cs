using System.Diagnostics;

namespace ProcSense.VR.Views
{
    /// <summary>Diagnostic only (P3-PRF, culling CPU cost): optional Stopwatch accumulators around the culling handlers and counters for forceRenderingOff reads and writes. Off by default; the cost when off is one static bool read per handler.</summary>
    public static class CullCost
    {
        public const int ExtCamera = 0, ExtPortal = 1, RoomCamera = 2, CharUpdate = 3, CharRescan = 4, Count = 5;
        public static readonly string[] Names = { "ExteriorVisibility.OnBeginCamera", "  of which PortalCull", "RoomVisibility.OnBeginCamera", "CharacterPerfCuller.Update", "CharacterPerfCuller.Rescan" };
        public static bool On;
        public static readonly long[] Ticks = new long[Count];
        public static readonly int[] Calls = new int[Count];
        public static int Reads, Writes;
        public static long Begin() => On ? Stopwatch.GetTimestamp() : 0L;
        public static void End(int id, long t) { if (t == 0L) return; Ticks[id] += Stopwatch.GetTimestamp() - t; Calls[id]++; }
        public static void Reset() { System.Array.Clear(Ticks, 0, Count); System.Array.Clear(Calls, 0, Count); Reads = Writes = 0; }
        public static double Ms(int id) => Ticks[id] * 1000.0 / Stopwatch.Frequency;
    }
}
