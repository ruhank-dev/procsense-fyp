using System;
using ProcSense.Core;

namespace ProcSense.VR.Bridge
{
    /// <summary>
    /// Which mode the next session is played in, chosen only before a session starts (plan p0-evaluation-performance-s04: "mode chosen only from the main menu, not changeable mid-session"). The menu calls <see cref="Choose"/>; the host asks
    /// <see cref="For"/> when it starts a session and calls <see cref="Lock"/> at once, after which <see cref="Choose"/> answers false and changes nothing until the session ends and <see cref="Unlock"/> is called. With nothing chosen the case's
    /// own configuration decides, so nothing changes for a scene that has no menu.
    /// </summary>
    public static class ModeChoice
    {
        public static SessionMode? Chosen { get; private set; }
        public static bool Locked { get; private set; }
        public static event Action<SessionMode> Changed;

        /// <summary>False, and no change, while a session is running.</summary>
        public static bool Choose(SessionMode mode)
        {
            if (Locked) return false;
            Chosen = mode;
            Changed?.Invoke(mode);
            return true;
        }

        public static SessionMode For(SessionMode configured) => Chosen ?? configured;
        public static void Lock() => Locked = true;
        public static void Unlock() => Locked = false;
        public static void Reset() { Chosen = null; Locked = false; }
    }
}
