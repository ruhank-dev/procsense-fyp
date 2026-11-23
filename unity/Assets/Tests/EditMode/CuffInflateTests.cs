using NUnit.Framework;
using ProcSense.VR.Views;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    public class CuffInflateTests
    {
        [Test]
        public void The_cuff_swells_holds_relaxes_and_rests()
        {
            Assert.AreEqual(1f, CuffInflate.Factor(0), 1e-4f);
            Assert.AreEqual(1f + CuffInflate.Swell, CuffInflate.Factor(CuffInflate.Inflate + .5f), 1e-4f);
            Assert.AreEqual(1f, CuffInflate.Factor(CuffInflate.Inflate + CuffInflate.Hold + CuffInflate.Deflate + 1f), 1e-4f);
            Assert.AreEqual(CuffInflate.Factor(2f), CuffInflate.Factor(2f + 14f), 1e-4f, "it repeats");
        }
    }
}
