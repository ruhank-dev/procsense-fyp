using System;

namespace ProcSense.VR.Settings
{
    /// <summary>
    /// Menu or voice (P3-VOX-02, ADR-0032 D3: voice in both Training and Examination). Kept in the settings record, so it survives a restart, and
    /// switchable at any time, also mid-session: switching is presentation only (which panels show, whether the talk button listens) and never
    /// reaches the engine, so the session state is the same before and after. Views subscribe to <see cref="Changed"/>. Recording the mode in the
    /// session log header comes with the session request (P3-SES-06), which reworks that header.
    /// </summary>
    public static class InteractionMode
    {
        /// <summary>Raised with the new mode after a real change (not when the same mode is chosen again).</summary>
        public static event Action<InteractionModeSetting> Changed;

        public static InteractionModeSetting Current => GameSettings.Current.interactionMode;
        public static bool Voice => Current == InteractionModeSetting.Voice;

        /// <summary>The modes chosen during this run, in order, for the session record (P3-SES-06 writes them).</summary>
        public static System.Collections.Generic.IReadOnlyList<InteractionModeSetting> History => _history;
        private static readonly System.Collections.Generic.List<InteractionModeSetting> _history = new System.Collections.Generic.List<InteractionModeSetting>();

        public static void Set(InteractionModeSetting mode)
        {
            if (mode == Current) return;
            GameSettings.Update(s => s.interactionMode = mode);
            _history.Add(mode);
            Changed?.Invoke(mode);
        }

        public static void Toggle() => Set(Voice ? InteractionModeSetting.Menu : InteractionModeSetting.Voice);

        /// <summary>A new session starts its own history with the mode in use now.</summary>
        public static void BeginSession()
        {
            _history.Clear();
            _history.Add(Current);
        }
    }
}
