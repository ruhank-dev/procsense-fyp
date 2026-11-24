using NUnit.Framework;
using ProcSense.VR.Views;

namespace ProcSense.Tests.EditMode
{
    public class BedBackrestTests
    {
        [Test]
        public void The_hinge_follows_how_far_up_the_patient_is()
        {
            Assert.AreEqual(0f, BedBackrest.Angle(0f, 55f), 1e-4f);
            Assert.AreEqual(27.5f, BedBackrest.Angle(.5f, 55f), 1e-4f);
            Assert.AreEqual(55f, BedBackrest.Angle(2f, 55f), 1e-4f, "never past the patient's own angle");
        }
    }
}
