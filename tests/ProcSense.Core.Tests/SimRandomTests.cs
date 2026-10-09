using System.Linq;
using ProcSense.Core.Random;

namespace ProcSense.Core.Tests
{
    public class SimRandomTests
    {
        // Golden values from an independent Python implementation of SplitMix64 + xoshiro256**.
        [Fact]
        public void Seed_42_gives_known_first_values()
        {
            var r = new SimRandom(42);

            Assert.Equal(1546998764402558742UL, r.NextULong());
            Assert.Equal(6990951692964543102UL, r.NextULong());
            Assert.Equal(12544586762248559009UL, r.NextULong());
        }

        [Fact]
        public void Same_seed_gives_same_sequence()
        {
            var a = new SimRandom(7);
            var b = new SimRandom(7);

            Assert.Equal(Enumerable.Range(0, 50).Select(_ => a.NextULong()), Enumerable.Range(0, 50).Select(_ => b.NextULong()));
        }

        [Fact]
        public void Streams_are_stable_and_independent()
        {
            var events = new SimRandom(42).Stream("events");

            Assert.Equal(17122401918591806535UL, events.NextULong());
            Assert.Equal(5256369845593407946UL, events.NextULong());
            Assert.NotEqual(new SimRandom(42).Stream("physio").NextULong(), new SimRandom(42).Stream("events").NextULong());
        }

        [Fact]
        public void Drawing_from_one_stream_does_not_shift_another()
        {
            var root = new SimRandom(42);
            var physioFirst = root.Stream("physio").NextULong();
            var events = root.Stream("events");
            for (int i = 0; i < 10; i++) events.NextULong();

            Assert.Equal(physioFirst, root.Stream("physio").NextULong());
        }

        [Fact]
        public void NextDouble_and_NextInt_stay_in_range()
        {
            var r = new SimRandom(1);
            for (int i = 0; i < 1000; i++)
            {
                Assert.InRange(r.NextDouble(), 0.0, 0.9999999999999999);
                Assert.InRange(r.NextInt(6), 0, 5);
            }
        }
    }
}
