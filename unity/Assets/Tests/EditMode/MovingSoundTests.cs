using NUnit.Framework;
using ProcSense.VR.Audio;

namespace ProcSense.Tests.EditMode
{
    public class MovingSoundTests
    {
        [Test]
        public void Only_a_walking_pace_or_faster_is_heard()
        {
            Assert.IsFalse(MovingSound.Audible(0f));
            Assert.IsFalse(MovingSound.Audible(.05f));
            Assert.IsTrue(MovingSound.Audible(1.1f));
        }
    }
}
