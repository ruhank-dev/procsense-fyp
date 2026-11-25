using NUnit.Framework;
using ProcSense.VR.Views;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    public sealed class WheelSpinTests
    {
        [TestCase("EQ_transport_trolley_Wheel0", true)]
        [TestCase("EQ_wheelchair_Caster1", true)]
        [TestCase("EQ_wheelchair_Body", false)]
        [TestCase("EQ_wheelchair_Axle", false)]
        public void OnlyWheelsAndCastersRoll(string name, bool wheel) => Assert.AreEqual(wheel, WheelSpin.IsWheel(name));

        [Test]
        public void ARollingWheelTurnsByTravelOverRadiusAndStaysPut()
        {
            var owner = new GameObject("owner");
            var model = new GameObject("model"); model.transform.SetParent(owner.transform, false);
            var wheel = GameObject.CreatePrimitive(PrimitiveType.Sphere); wheel.name = "EQ_x_Wheel0";
            wheel.transform.SetParent(model.transform, false); wheel.transform.localScale = Vector3.one * .2f;   // radius .1
            var spin = model.AddComponent<WheelSpin>(); spin.Setup(owner.transform);
            var at = wheel.transform.position;
            spin.Roll(.1f);   // one radian
            Assert.AreEqual(1, spin.Count);
            Assert.AreEqual(Mathf.Rad2Deg, Quaternion.Angle(Quaternion.identity, wheel.transform.rotation), .5f);
            Assert.AreEqual(0f, Vector3.Distance(at, wheel.transform.position), .001f);
            Object.DestroyImmediate(owner);
        }
    }
}
