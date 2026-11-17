using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProcSense.VR.Interaction
{
    /// <summary>
    /// P3-CTX-02: puts an <see cref="Interactable"/> on the patient, the staff, the equipment and the records of the three rooms, by the names the scene builders give them (the rule table below),
    /// once when a scene loads; and on the people that appear only during a session (the transport escort, the visiting nurse) by their components, once a second. The rules sit in one table so a
    /// renamed object needs one line changed; <see cref="Rules"/> is public for the scene coverage test.
    /// </summary>
    public sealed class InteractableBinder : MonoBehaviour
    {
        /// <summary>(exact name or name prefix ending in '*', entity type). First match wins; a match's children are not searched again.</summary>
        public static readonly (string name, string type)[] Rules =
        {
            ("Rocketbox patient", "patient"), ("mpfb_patient", "patient"),
            ("Nurse*", "nurse"), ("Staff nurse*", "nurse"),
            ("Staff receptionist", "porter"), ("Porter*", "porter"),
            ("Doctor*", "doctor"),
            ("Person_Medical_Female_02", "anaesthetist"),                 // the theatre pair placed by RocketboxPeople: she leads the anaesthetic review
            ("Theatre staff RB", "theatre_team"),
            ("Physical bedside monitoring", "monitor"), ("anaesthesia_unit", "monitor"), ("sensor_equipment_table", "monitor"),
            ("charting_counter", "records"), ("Physical notes folder", "records"),   // the records workstation and the results phone are a screen and an anchor on the charting counter, with no mesh of their own
        };

        public static string TypeFor(string objectName)
        {
            foreach (var (n, type) in Rules)
            {
                if (n.EndsWith("*") ? objectName.StartsWith(n.Substring(0, n.Length - 1)) : objectName == n) return type;
            }
            return null;
        }

        /// <summary>Marks everything the rules name in a scene; returns what was marked. A person already marked under a marked group (the theatre pair) keeps its own type.</summary>
        public static List<Interactable> BindScene(Scene scene)
        {
            var found = new List<Interactable>();
            if (!scene.IsValid() || !scene.isLoaded) return found;
            foreach (var root in scene.GetRootGameObjects()) Walk(root.transform, found);
            return found;
        }

        private static void Walk(Transform t, List<Interactable> found)
        {
            var type = TypeFor(t.name);
            if (type != null)
            {
                // the anaesthetist inside the theatre group is marked on her own first, so the group is "the rest of the team"
                if (type == "theatre_team") foreach (Transform c in t) Walk(c, found);   // only a group holds people of their own type; a prefab's inner mesh of the same name is the same thing
                // an empty holder (the visiting nurse, filled during a session) has nothing to point at yet: BindRuntime marks the actor when it appears
                if (t.GetComponentInChildren<Renderer>(true) != null) { found.Add(Interactable.Mark(t.gameObject, type)); GivePresenter(t.gameObject, type); }
                return;
            }
            foreach (Transform c in t) Walk(c, found);
        }

        /// <summary>A person in a room (the anaesthetist, a corridor nurse) gets the shared conversation layer: they turn to the player when addressed and their mouth follows their own lines. Play mode only.</summary>
        private static void GivePresenter(GameObject person, string type)
        {
            if (!Application.isPlaying || person.GetComponent<ProcSense.VR.Views.CharacterPresenter>() != null) return;
            if (type == "nurse" || type == "doctor" || type == "anaesthetist" || type == "porter") person.AddComponent<ProcSense.VR.Views.CharacterPresenter>().Setup(type, null);
        }

        private float _next;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnLoaded;
            for (int i = 0; i < SceneManager.sceneCount; i++) BindScene(SceneManager.GetSceneAt(i));
        }

        private void OnDisable() => SceneManager.sceneLoaded -= OnLoaded;
        private void OnLoaded(Scene s, LoadSceneMode m) => BindScene(s);

        private void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 1f;
            BindRuntime();
        }

        /// <summary>The people of a session: the transport escort (nurse, porter, doctor) and the visiting nurse; marked once each.</summary>
        public static void BindRuntime()
        {
            var transport = FindAnyObjectByType<ProcSense.VR.Views.PatientTransportView>();
            var actor = transport != null ? transport.Actor : null;
            if (actor != null)
            {
                MarkOnce(actor.Nurse, "nurse"); MarkOnce(actor.Porter, "porter"); MarkOnce(actor.Doctor, "doctor");
            }
            var visiting = ProcSense.VR.Views.NurseWristbandController.Instance;
            if (visiting != null && visiting.Actor != null) MarkOnce(visiting.Actor.transform, "nurse");
        }

        private static void MarkOnce(Transform t, string type)
        {
            if (t != null && t.GetComponent<Interactable>() == null) Interactable.Mark(t.gameObject, type);
        }
    }
}
