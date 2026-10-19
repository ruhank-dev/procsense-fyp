using ProcSense.Core;
using UnityEngine;

namespace ProcSense.VR.Bridge
{
    /// <summary>
    /// The one central check for practice-only presentation (plan p0-evaluation-performance-s04): hints, the consent element tracker and nurse prompts are shown only when the running session has practice aids, which Core derives from the mode
    /// (<see cref="Session.PracticeAids"/>). Views never read the mode themselves: they ask this class, so a missed view is one place to fix and one place to test. With no session running the aids are on (the editor and the menus).
    /// </summary>
    public static class ModeGate
    {
        /// <summary>Where the running session comes from; replaced by tests, which have no scene and so no <see cref="SimulationHost.Instance"/>.</summary>
        public static System.Func<Session> SessionProvider { get; set; } = () => SimulationHost.Instance != null ? SimulationHost.Instance.Session : null;

        public static bool PracticeAids
        {
            get
            {
                var session = SessionProvider();
                return session == null || session.PracticeAids;
            }
        }

        /// <summary>The text when practice aids are on; an empty string in assessment mode.</summary>
        public static string Practice(string practiceOnlyText) => PracticeAids ? practiceOnlyText : "";

        /// <summary>The mode as the session log header and the debrief write it.</summary>
        public static string Label
        {
            get
            {
                var session = SessionProvider();
                return session == null ? "" : session.Mode == SessionMode.Assessment ? "ASSESSMENT" : "PRACTICE";
            }
        }
    }

    /// <summary>Put this on any object that is practice-only (the hint chip, the consent element tracker, a nurse prompt): it is active only while <see cref="ModeGate.PracticeAids"/> is true.</summary>
    public class PracticeOnly : MonoBehaviour
    {
        private void OnEnable() => Apply();
        private void Update() => Apply();

        public void Apply()
        {
            bool on = ModeGate.PracticeAids;
            for (int i = 0; i < transform.childCount; i++)
                if (transform.GetChild(i).gameObject.activeSelf != on) transform.GetChild(i).gameObject.SetActive(on);
        }
    }
}
