using ProcSense.VR.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Live review: opens the patient bay and selects the Rocketbox patient so its PatientMotion shows in the Inspector. Press Play, then type a mode (IN_PAIN, ANXIOUS, DISTRESSED, CONFUSED, DROWSY, NAUSEATED, UNCOOPERATIVE)
    /// into Preview Mode and watch the Scene or Game view; empty goes back to the session's own mood. Guard Target moves the guarding hand while you watch. Changes made in Play mode are not saved.
    /// </summary>
    public static class PatientMoodReview
    {
        [MenuItem("ProcSense/Review/Patient moods")]
        public static void Open()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var patient = GameObject.Find("Rocketbox patient");
            if (patient == null) { Debug.LogWarning("[PatientMoodReview] no Rocketbox patient in PatientBay"); return; }
            Selection.activeGameObject = patient;
            SceneView.lastActiveSceneView?.Frame(new Bounds(patient.transform.position, Vector3.one * 1.2f), false);
            Debug.Log("[PatientMoodReview] Press Play, then type a mode into Patient Motion > Preview Mode.");
        }
    }
}
