using NUnit.Framework;
using ProcSense.Core.Observation;
using ProcSense.VR.Scenes;

namespace ProcSense.Tests.EditMode
{
    /// <summary>What the scene shows at each transport state (p0-vr-platform-s06): Core decides, the staging follows.</summary>
    public class TransportStagingTests
    {
        private static ObservedTransport T(string state, string to = "ct", string mode = "trolley") => new ObservedTransport(state, to, mode, new string[0], "none", false, "", "");

        [Test]
        public void A_scanner_is_entered_through_its_door_and_the_patient_is_hidden_until_returning()
        {
            Assert.AreEqual("door:ct", TransportStaging.For(T("IN_TRANSIT"), "corridor", "patient_bay", "patient_bay").Anchor);
            var inside = TransportStaging.For(T("ARRIVED"), "ct", "patient_bay", "patient_bay");
            Assert.AreEqual(("inside:ct", false), (inside.Anchor, inside.Visible));
            var back = TransportStaging.For(T("RETURNING"), "corridor", "patient_bay", "patient_bay");
            Assert.AreEqual(("patient_bay", true, true), (back.Anchor, back.Visible, back.Moving));
        }

        [Test]
        public void Theatre_reception_is_a_built_place_so_the_patient_stays_in_view()
        {
            var s = TransportStaging.For(T("ARRIVED", "theatre_reception"), "theatre_reception", "theatre_reception", "patient_bay");
            Assert.AreEqual(("theatre_reception", true, true), (s.Anchor, s.Visible, s.TraineeFollows));
        }

        [Test]
        public void Before_the_porter_comes_nothing_moves_and_after_BACK_the_patient_is_where_Core_says()
        {
            var waiting = TransportStaging.For(T("DELAYED"), "patient_bay", "patient_bay", "patient_bay");
            Assert.AreEqual(("patient_bay", false, false, ""), (waiting.Anchor, waiting.Moving, waiting.Porter, waiting.Mode));
            var done = TransportStaging.For(T("BACK"), "patient_bay", "patient_bay", "patient_bay");
            Assert.AreEqual(("patient_bay", true, ""), (done.Anchor, done.Visible, done.Mode));
            Assert.AreEqual("patient_bay", TransportStaging.For(null, "patient_bay", "patient_bay", "").Anchor);
        }
    }
}
