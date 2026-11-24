using NUnit.Framework;
using ProcSense.VR.Views;

namespace ProcSense.Tests.EditMode
{
    public class CuffInflateTests
    {
        [Test]
        public void The_cuff_swells_while_pumping_relaxes_while_bleeding_and_then_rests()
        {
            Assert.AreEqual(1f, CuffInflate.Factor(0), 1e-4f);
            Assert.AreEqual(1f + CuffInflate.Swell, CuffInflate.Factor(CuffInflate.Pump), 1e-4f);
            Assert.AreEqual(1f, CuffInflate.Factor(CuffInflate.Pump + CuffInflate.Bleed + .1f), 1e-4f);
            Assert.AreEqual(1f, CuffInflate.Factor(-1f), 1e-4f, "before a measurement it is at rest");
        }
    }
}
