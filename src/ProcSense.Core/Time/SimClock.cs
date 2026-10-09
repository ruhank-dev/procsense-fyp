using System;

namespace ProcSense.Core.Time
{
    /// <summary>Simulated time in fixed 0.5 s steps. Never reads the wall clock.</summary>
    public sealed class SimClock
    {
        public const double Step = 0.5;

        private double _carry; // real time not yet worth a whole step

        public SimClock(TimeSpan scenarioStart) { Start = scenarioStart; }

        public TimeSpan Start { get; }

        /// <summary>Whole steps taken so far. Seconds derive from this, so no float drift builds up.</summary>
        public long Steps { get; private set; }

        public double SimSeconds => Steps * Step;

        /// <summary>Scenario clock time, e.g. 08:00 + elapsed. Can pass 24 h.</summary>
        public TimeSpan ClockTime => Start + TimeSpan.FromSeconds(SimSeconds);

        /// <summary>Adds dt seconds, moves the clock by the whole steps it contains and returns that count. The remainder is kept for next time.</summary>
        public int Advance(double dt)
        {
            if (dt < 0) throw new ArgumentOutOfRangeException(nameof(dt));
            _carry += dt;
            int n = (int)Math.Floor(_carry / Step + 1e-9); // epsilon: 0.1+0.4 must count as one step
            _carry -= n * Step;
            Steps += n;
            return n;
        }

        /// <summary>Jumps forward (e.g. "wait 10 minutes"). Same rounding as Advance; the caller decides which subsystems run for the skipped steps.</summary>
        public int TimeSkip(double seconds) => Advance(seconds);
    }
}
