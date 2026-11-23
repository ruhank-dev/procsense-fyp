using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.VR.Bridge;
using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// P3-AST-03: the patient in the bay looks like the case's patient. The scene holds one clothed Rocketbox patient per profile (female young, female adult, female older, male adult, male older), all lying on the bed, and only the one
    /// that fits the case's sex and age is active. The things that belong on the patient's body (the finger and arm sensor sockets, the wristband, the ear contact for the thermometer) are moved onto the same bones of the chosen body, so
    /// every binding the rest of the scene holds (the sensors' destinations, the nurse's wrist target) keeps working. Presentation only: it reads the case's sex and age and decides nothing clinical.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PatientProfiles : MonoBehaviour
    {
        [Serializable] public sealed class Profile { public string key = ""; public Transform root; }
        [Serializable] public sealed class Mount { public Transform item; [Tooltip("The bone it hangs on, by name (the Rocketbox skeleton names are the same on every avatar).")] public string bone = ""; }

        public const string Default = "male_adult";

        public List<Profile> profiles = new List<Profile>();
        public List<Mount> mounts = new List<Mount>();
        [Tooltip("The transport view hides the patient while they are away; it must follow the body that is shown.")] public PatientTransportView transport;
        public string Active { get; private set; } = "";
        private object session;

        /// <summary>The profile for a case: sex "F" or "M" and age in years. Women under 40 are young, under 65 adult, older after that; men from 65 are older, every other man is the adult.</summary>
        public static string KeyFor(string sex, long age)
        {
            bool woman = string.Equals(sex, "F", StringComparison.OrdinalIgnoreCase);
            if (woman) return age < 40 ? "female_young" : age < 65 ? "female_adult" : "female_older";
            return age >= 65 ? "male_older" : Default;
        }

        /// <summary>Shows the body for this profile (the default when it is not in the scene) and puts the mounted things on its bones. Returns the key now shown.</summary>
        public string Select(string key)
        {
            var next = profiles.FirstOrDefault(p => p.root != null && p.key == key) ?? profiles.FirstOrDefault(p => p.root != null && p.key == Default) ?? profiles.FirstOrDefault(p => p.root != null);
            if (next == null) return Active;
            if (Active == next.key && next.root.gameObject.activeSelf) return Active;
            foreach (var p in profiles) if (p.root != null) p.root.gameObject.SetActive(p == next);
            foreach (var m in mounts)
            {
                if (m.item == null) continue;
                var bone = next.root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == m.bone);
                if (bone != null && m.item.parent != bone) m.item.SetParent(bone, false);       // the local position and rotation are kept: the bone is the same one on every avatar
            }
            if (transport != null && transport.AdditionalBedsideVisuals != null)
                for (int i = 0; i < transport.AdditionalBedsideVisuals.Length; i++)
                    if (profiles.Any(p => p.root == transport.AdditionalBedsideVisuals[i])) transport.AdditionalBedsideVisuals[i] = next.root;
            GiveMouth(next.root);
            Active = next.key;
            return Active;
        }

        /// <summary>Lip sync for the patient (P3-ANI-05): the jaw and visemes follow the audio while the patient's own line plays.</summary>
        private static void GiveMouth(Transform root)
        {
            if (root.GetComponent<PatientSpeechFace>() != null) return;
            var faces = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => PatientSpeechFace.HasMouth(r.sharedMesh)).ToArray();
            var reactions = root.GetComponent<PatientReactions>();
            if (faces.Length == 0 || reactions == null) return;
            var mouth = root.gameObject.AddComponent<PatientSpeechFace>(); mouth.Configure(faces); mouth.Gate = () => reactions.State.State == CharacterState.Speaking;
        }

        private void Update()
        {
            var host = SimulationHost.Instance; var now = host != null ? host.Session : null;
            if (ReferenceEquals(now, session)) return;
            session = now;
            // sex is in the case file (the running case card carries the age only)
            if (now != null && host.Content != null && host.Content.CaseFiles.TryGetValue(now.Case.Id, out var file)) Select(KeyFor(file.Identity.Sex, file.Identity.Age));
        }
    }
}
