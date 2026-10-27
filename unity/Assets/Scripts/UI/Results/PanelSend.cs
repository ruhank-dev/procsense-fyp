using System.Collections.Generic;
using ProcSense.Core.Actions;
using ProcSense.VR.UI.Tablet;

namespace ProcSense.VR.UI.Results
{
    /// <summary>
    /// How the results viewer, the phone and the picker send an action: through one emitter, like a tablet form (<see cref="FormSendEmitter"/>), so every action is logged on the VR channel and Core alone decides.
    /// A click keeps the normal debounce; a direct call (tests, the desktop dev menu) sends at once.
    /// </summary>
    public static class PanelSend
    {
        public static ActionOutcome Send(FormSendEmitter emitter, string verb, IReadOnlyDictionary<string, string> parameters, bool click)
        {
            emitter.ConfigureParams(verb, parameters);
            if (click) return emitter.Fire();
            float debounce = emitter.DebounceSeconds;
            try { emitter.DebounceSeconds = 0f; return emitter.Fire(); }
            finally { emitter.DebounceSeconds = debounce; }
        }
    }
}
