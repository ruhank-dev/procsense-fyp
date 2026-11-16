using NUnit.Framework;
using ProcSense.VR.Views;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>Nod and head shake curves start and end at rest, stay within their size and move the right way.</summary>
    public class HeadGestureTests
    {
        [Test]
        public void No_gesture_and_times_outside_it_are_zero()
        {
            Assert.AreEqual(Vector2.zero, HeadGesture.At(HeadGestureKind.None, .3f));
            foreach (var k in new[] { HeadGestureKind.Nod, HeadGestureKind.Shake })
            {
                Assert.AreEqual(Vector2.zero, HeadGesture.At(k, -.1f)); Assert.AreEqual(Vector2.zero, HeadGesture.At(k, HeadGesture.Seconds + .1f));
                Assert.AreEqual(0f, HeadGesture.At(k, 0f).magnitude, 1e-4f); Assert.AreEqual(0f, HeadGesture.At(k, HeadGesture.Seconds).magnitude, 1e-3f);
            }
        }

        [Test]
        public void A_nod_only_dips_the_chin_and_a_shake_only_turns_the_head_both_ways()
        {
            float lowest = 0f, shakeMin = 0f, shakeMax = 0f;
            for (float t = 0f; t <= HeadGesture.Seconds; t += .01f)
            {
                var n = HeadGesture.At(HeadGestureKind.Nod, t); Assert.LessOrEqual(n.x, 1e-5f, "a nod never lifts the chin"); Assert.AreEqual(0f, n.y); lowest = Mathf.Min(lowest, n.x);
                var s = HeadGesture.At(HeadGestureKind.Shake, t); Assert.AreEqual(0f, s.x); shakeMin = Mathf.Min(shakeMin, s.y); shakeMax = Mathf.Max(shakeMax, s.y);
            }
            Assert.Less(lowest, -HeadGesture.NodDegrees * .6f, "the nod is big enough to read"); Assert.GreaterOrEqual(lowest, -HeadGesture.NodDegrees - 1e-3f);
            Assert.Less(shakeMin, -HeadGesture.ShakeDegrees * .5f); Assert.Greater(shakeMax, HeadGesture.ShakeDegrees * .5f);
        }
    }
}
