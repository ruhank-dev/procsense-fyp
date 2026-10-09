using System;
using ProcSense.Core.Time;

namespace ProcSense.Core.Tests
{
    public class SimClockTests
    {
        [Fact] // T-K3
        public void Advance_runs_whole_steps_only_and_carries_the_remainder()
        {
            var clock = new SimClock(TimeSpan.FromHours(8));
            int total = 0;
            for (int i = 0; i < 100; i++) total += clock.Advance(0.73);

            Assert.Equal(146, total); // 73.0 s / 0.5 s
            Assert.Equal(146, clock.Steps);
            Assert.Equal(73.0, clock.SimSeconds);
        }

        [Fact]
        public void Small_advances_are_never_lost()
        {
            var clock = new SimClock(TimeSpan.Zero);
            Assert.Equal(0, clock.Advance(0.1));
            Assert.Equal(1, clock.Advance(0.4));
        }

        [Fact]
        public void ClockTime_is_start_plus_sim_time_and_skip_moves_it()
        {
            var clock = new SimClock(TimeSpan.FromHours(8));
            clock.TimeSkip(600);

            Assert.Equal(1200, clock.Steps);
            Assert.Equal(new TimeSpan(8, 10, 0), clock.ClockTime);
        }

        [Fact]
        public void Negative_time_is_rejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SimClock(TimeSpan.Zero).Advance(-1));
        }
    }
}
