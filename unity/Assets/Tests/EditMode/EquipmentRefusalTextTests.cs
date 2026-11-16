using NUnit.Framework;
using ProcSense.VR.ActionEmitters;

namespace ProcSense.Tests.EditMode
{
    /// <summary>Plain-word reasons for patient equipment that did not attach: session states in every mode, the engine's hint in training, one neutral sentence in the examination.</summary>
    public class EquipmentRefusalTextTests
    {
        [Test]
        public void Session_states_are_said_the_same_in_training_and_examination()
        {
            foreach (bool training in new[] { true, false })
            {
                Assert.AreEqual("Start a scenario before attaching equipment.", EquipmentRefusalText.For(EquipmentRefusalText.NoSessionCode, training));
                Assert.AreEqual(EquipmentRefusalText.Paused, EquipmentRefusalText.For("SESSION_PAUSED", training));
                Assert.AreEqual(EquipmentRefusalText.Finished, EquipmentRefusalText.For("SESSION_SUBMITTED", training));
            }
        }

        [Test]
        public void Training_gives_the_engine_reason_in_plain_words()
        {
            Assert.AreEqual("Not attached: the patient is not here.", EquipmentRefusalText.For("PATIENT_ELSEWHERE", true));
            Assert.AreEqual("Not attached: Identity not confirmed.", EquipmentRefusalText.For("IDENTITY_NOT_CONFIRMED", true));
            Assert.AreEqual("Not removed: SOME_NEW_CODE.", EquipmentRefusalText.For("SOME_NEW_CODE", true, removing: true), "an unknown code is shown, never hidden");
            Assert.AreEqual(EquipmentRefusalText.WrongTarget, EquipmentRefusalText.For(EquipmentRefusalText.WrongTargetCode, true));
        }

        [Test]
        public void Examination_is_neutral_and_gives_no_hint()
        {
            foreach (var code in new[] { "PATIENT_ELSEWHERE", "IDENTITY_NOT_CONFIRMED", "CUFF_NOT_ATTACHED", "SOME_NEW_CODE" })
            {
                Assert.AreEqual(EquipmentRefusalText.ExamNotAttached, EquipmentRefusalText.For(code, false), code);
                Assert.AreEqual(EquipmentRefusalText.ExamNotRemoved, EquipmentRefusalText.For(code, false, removing: true), code);
            }
            Assert.AreEqual(EquipmentRefusalText.ExamWrongTarget, EquipmentRefusalText.For(EquipmentRefusalText.WrongTargetCode, false));
            Assert.AreEqual("", EquipmentRefusalText.For("", false));
        }
    }
}
