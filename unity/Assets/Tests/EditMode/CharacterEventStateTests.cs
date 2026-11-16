using NUnit.Framework;
using ProcSense.VR.Views;

namespace ProcSense.Tests.EditMode
{
    /// <summary>The character conversation states move the way P3-ANI-04 describes.</summary>
    public class CharacterEventStateTests
    {
        [Test]
        public void Addressed_then_listening_then_idle_when_nobody_goes_on()
        {
            var c = new CharacterEventState();
            c.Address(); Assert.AreEqual(CharacterState.Addressed, c.State);
            c.Step(CharacterEventState.AddressedSeconds + .01f); Assert.AreEqual(CharacterState.Listening, c.State);
            c.Step(CharacterEventState.ListenSeconds + .01f); Assert.AreEqual(CharacterState.Idle, c.State);
        }

        [Test]
        public void Speaking_holds_until_it_ends_then_the_character_listens()
        {
            var c = new CharacterEventState(); c.Address(); c.StartSpeaking();
            c.Step(100f); Assert.AreEqual(CharacterState.Speaking, c.State, "speech has no timer: it lasts as long as the audio");
            c.EndSpeaking(); Assert.AreEqual(CharacterState.Listening, c.State);
        }

        [Test]
        public void An_accepted_action_nods_a_refused_one_shakes_and_both_return_to_the_conversation()
        {
            var c = new CharacterEventState(); c.Address(); c.Step(1f);
            c.Reply(true); Assert.AreEqual(CharacterState.Acknowledge, c.State);
            c.Step(CharacterEventState.GestureSeconds + .01f); Assert.AreEqual(CharacterState.Listening, c.State);
            c.Reply(false); Assert.AreEqual(CharacterState.Refuse, c.State);
            c.Step(CharacterEventState.GestureSeconds + .01f); Assert.AreEqual(CharacterState.Listening, c.State);
        }

        [Test]
        public void A_reply_outside_a_conversation_goes_back_to_idle()
        {
            var c = new CharacterEventState(); c.Reply(true); Assert.AreEqual(CharacterState.Acknowledge, c.State);
            c.Step(CharacterEventState.GestureSeconds + .01f); Assert.AreEqual(CharacterState.Idle, c.State);
        }

        [Test]
        public void A_reply_never_cuts_into_speech()
        {
            var c = new CharacterEventState(); c.StartSpeaking(); c.Reply(false);
            Assert.AreEqual(CharacterState.Speaking, c.State);
        }

        [Test]
        public void A_busy_character_ignores_conversation_but_remembers_it_was_addressed()
        {
            var c = new CharacterEventState(); c.SetBusy(true);
            c.Address(); c.StartSpeaking(); c.Reply(true);
            Assert.AreEqual(CharacterState.Busy, c.State); Assert.IsTrue(c.GlancedWhileBusy);
            c.SetBusy(false); Assert.AreEqual(CharacterState.Idle, c.State);
        }

        [Test]
        public void Every_change_is_reported_once()
        {
            var c = new CharacterEventState(); int changes = 0; c.Changed += (a, b) => changes++;
            c.Address(); c.Address(); c.StartSpeaking(); c.StartSpeaking(); c.EndSpeaking();
            Assert.AreEqual(3, changes, "addressed, speaking, listening; repeats do not fire again");
        }
    }
}
