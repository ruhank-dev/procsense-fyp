using ProcSense.Core;
using UnityEngine;

namespace ProcSense.VR.Bridge
{
    /// <summary>Which case to play, with which seed and mode. A ScriptableObject for now (plan p0-vr-platform-s03); the case chooser comes later.</summary>
    [CreateAssetMenu(menuName = "ProcSense/Session Config", fileName = "SessionConfig")]
    public class SessionConfig : ScriptableObject
    {
        public string caseId = "VAR-S1-A";
        public long seed = 42;
        public SessionMode mode = SessionMode.Practice;
    }
}
