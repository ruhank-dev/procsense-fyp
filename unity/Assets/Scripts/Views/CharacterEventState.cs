using System;

namespace ProcSense.VR.Views
{
    public enum CharacterState { Idle, Addressed, Speaking, Listening, Acknowledge, Refuse, Busy }

    /// <summary>
    /// The conversation state of one character (P3-ANI-04): idle, addressed (turn and look), speaking, listening (attentive), acknowledge (nod after an accepted action), refuse (head shake after a rejected one) and busy.
    /// Pure logic with no scene: whatever hears the events (speech playback, <c>SimFeedback</c> when it exists) calls the event methods, a presenter calls <see cref="Step"/> each frame and plays what <see cref="State"/> says.
    /// It decides nothing clinical: whether an action was accepted is the engine's answer, passed in.
    /// </summary>
    public sealed class CharacterEventState
    {
        public const float AddressedSeconds = .6f, GestureSeconds = .8f, ListenSeconds = 6f;

        private float timer;
        private bool inConversation;

        public CharacterState State { get; private set; } = CharacterState.Idle;
        /// <summary>True when the character was addressed while busy: the presenter can show a quick glance without leaving the task.</summary>
        public bool GlancedWhileBusy { get; private set; }
        public event Action<CharacterState, CharacterState> Changed;

        private void Go(CharacterState to) { var from = State; if (from == to) return; State = to; Changed?.Invoke(from, to); }

        /// <summary>Someone speaks to this character (named, selected or looked at).</summary>
        public void Address()
        {
            if (State == CharacterState.Busy) { GlancedWhileBusy = true; return; }
            inConversation = true;
            if (State == CharacterState.Speaking) return;
            timer = AddressedSeconds; Go(CharacterState.Addressed);
        }

        public void StartSpeaking() { if (State == CharacterState.Busy) return; inConversation = true; Go(CharacterState.Speaking); }

        public void EndSpeaking() { if (State != CharacterState.Speaking) return; timer = ListenSeconds; Go(CharacterState.Listening); }

        /// <summary>An action aimed at this character: nod when the engine accepted it, shake the head when it refused. Not while speaking (the spoken reply carries it) or busy.</summary>
        public void Reply(bool accepted)
        {
            if (State == CharacterState.Speaking || State == CharacterState.Busy) return;
            timer = GestureSeconds; Go(accepted ? CharacterState.Acknowledge : CharacterState.Refuse);
        }

        /// <summary>The character is doing a task (walking in, writing): it takes no part in conversation until it is free.</summary>
        public void SetBusy(bool busy)
        {
            if (busy) { GlancedWhileBusy = false; inConversation = false; Go(CharacterState.Busy); }
            else if (State == CharacterState.Busy) Go(CharacterState.Idle);
        }

        public void Step(float dt)
        {
            if (State == CharacterState.Idle || State == CharacterState.Speaking || State == CharacterState.Busy) return;
            timer -= dt; if (timer > 0f) return;
            if (State == CharacterState.Addressed || State == CharacterState.Acknowledge || State == CharacterState.Refuse) { if (inConversation) { timer = ListenSeconds; Go(CharacterState.Listening); } else Go(CharacterState.Idle); return; }
            inConversation = false; Go(CharacterState.Idle);                                  // listening ran out: the conversation is over
        }
    }
}
