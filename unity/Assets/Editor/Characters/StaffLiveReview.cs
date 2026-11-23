using System.IO;
using System.Linq;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.VR.Bridge;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Live review of the staff (doctor, nurse): menu ProcSense > Review > Staff in Play opens the patient bay, enters Play, starts the test case with NO wristband (the only way the nurse visit happens; the real case has a correct band),
    /// does the two steps that must come first (ask identity, open the notes), and puts the player beside the doctor looking at him. Then: say "Doctor, ..." to him, and press "Ask nurse for a wristband" on the tablet to make the nurse walk in.
    /// Editor only; nothing is saved to the scene.
    /// </summary>
    [InitializeOnLoad]
    public static class StaffLiveReview
    {
        private const string Key = "ProcSense.StaffLiveReview";
        private static int frames;

        static StaffLiveReview() => EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.EnteredPlayMode && EditorPrefs.GetBool(Key)) { frames = 0; EditorApplication.update += Tick; } };

        [MenuItem("ProcSense/Review/Staff in Play")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            EditorPrefs.SetBool(Key, true); EditorApplication.EnterPlaymode();
        }

        // Saying a line needs the microphone, the backend and a sign-in; these two send the same sentence the recogniser would, so the head turn can be watched without them.
        [MenuItem("ProcSense/Review/Say 'Doctor, please review the patient'")] public static void SayDoctor() => Say("Doctor, please review the patient");
        [MenuItem("ProcSense/Review/Say 'Nurse, please check the drip'")] public static void SayNurse() => Say("Nurse, please check the drip");
        private static void Say(string line) { if (!EditorApplication.isPlaying) { Debug.LogWarning("[StaffLiveReview] press Play first"); return; } ProcSense.VR.Interaction.AddresseeTarget.Choose(line); Debug.Log("[StaffLiveReview] said: " + line + " | " + Report()); }

        /// <summary>What each presenter in the scene is doing now, and whether there is a camera to turn toward, so a silent test says why.</summary>
        private static string Report()
        {
            var all = Object.FindObjectsByType<ProcSense.VR.Views.CharacterPresenter>(FindObjectsSortMode.None);
            var cam = Camera.main != null ? "main camera ok" : "NO main camera (using the first screen camera)";
            return $"{cam}; {all.Length} presenter(s): " + string.Join(", ", all.Select(p => $"{p.name}[{p.who}] {p.State.State} turn {p.Turn} turnsHead={p.turnsHead}"));
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) { EditorApplication.update -= Tick; return; }
            if (++frames < 90) return;                                                   // let the rig and the session presentation come up
            var host = SimulationHost.Instance; var rig = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).FirstOrDefault(m => m.GetType().Name == "XROrigin");
            if (host == null || rig == null) { if (frames > 600) { Debug.LogWarning("[StaffLiveReview] no host or XR origin after 10 seconds"); Done(); } return; }
            Done();
            var repo = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            var reader = new CompositeContentReader(new FileContentReader(Path.Combine(repo, "content")), new FileContentReader(Path.Combine(repo, "tests", "fixtures")));
            host.StartSession("TEST-ID-MISSING-BAND", 42, SessionMode.Practice, reader);
            foreach (var verb in new[] { "ask_identity", "open_notes" })
                ActionGateway.Apply(host, new SimAction(verb, "patient", Channel.Vr, new System.Collections.Generic.Dictionary<string, string>()), ActionSource.Menu);
            // The binder that gives staff their conversation layer is created by the menu flow; pressing Play on PatientBay directly has no flow, so without this nobody would react.
            if (Object.FindAnyObjectByType<ProcSense.VR.Interaction.InteractableBinder>() == null) new GameObject("Interactable binder (review)").AddComponent<ProcSense.VR.Interaction.InteractableBinder>();
            var doctor = GameObject.Find("Doctor at the bay");
            if (doctor != null)
            {
                var at = doctor.transform.position + doctor.transform.forward * 1.2f;       // in front of him, so he can be addressed
                rig.transform.position = new Vector3(at.x, 0, at.z);
                var to = doctor.transform.position - rig.transform.position; rig.transform.rotation = Quaternion.LookRotation(new Vector3(to.x, 0, to.z));
            }
            Debug.Log("[StaffLiveReview] Ready: you stand in front of the doctor. Say or pick a line starting 'Doctor, ...'. For the nurse press 'Ask nurse for a wristband' on the tablet (Admission tab).");
        }

        private static void Done() { EditorApplication.update -= Tick; EditorPrefs.DeleteKey(Key); }
    }
}
