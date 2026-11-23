using System.Collections.Generic;
using UnityEngine;

namespace ProcSense.VR.Interaction
{
    /// <summary>
    /// P3-VOX-05: who a spoken sentence is for, sent as <c>addressee</c> to the speech routes (P3-API-02: "patient", "nurse", "anaesthesia" or nothing). In order: a name at the start of the sentence
    /// ("Nurse, …" goes to the nurse whoever the trainee is looking at), else the entity whose contextual panel is open, else the person nearest the centre of the gaze within
    /// <see cref="GazeCone"/> degrees; otherwise none and the service decides. Only the people the service knows are returned.
    /// </summary>
    public static class AddresseeTarget
    {
        public const float GazeCone = 20f, GazeRange = 4f;

        /// <summary>The speech-route name of an entity type, or null for one the service has no addressee for (equipment, records, porter, doctor, theatre team).</summary>
        public static string RouteName(string entityType)
        {
            switch (entityType)
            {
                case "patient": return "patient";
                case "nurse": return "nurse";
                case "anaesthetist": return "anaesthesia";
                default: return null;
            }
        }

        private static readonly (string word, string who)[] Names =
        {
            ("nurse", "nurse"), ("sister", "nurse"), ("anaesthetist", "anaesthesia"), ("anesthetist", "anaesthesia"), ("doctor", null), ("patient", "patient"),
        };

        /// <summary>A role named at the start of the sentence ("Nurse, …", "Anaesthetist please …"), or null.</summary>
        public static string Named(string utterance)
        {
            if (string.IsNullOrEmpty(utterance)) return null;
            var s = utterance.TrimStart().ToLowerInvariant();
            foreach (var (word, who) in Names)
                if (s.StartsWith(word) && (s.Length == word.Length || !char.IsLetter(s[word.Length]))) return who;
            return null;
        }

        private static bool StartsWithWord(string utterance, string word)
        {
            if (string.IsNullOrEmpty(utterance)) return false;
            var s = utterance.TrimStart().ToLowerInvariant();
            return s.StartsWith(word) && (s.Length == word.Length || !char.IsLetter(s[word.Length]));
        }

        /// <summary>The person nearest the centre of the gaze, within the cone and range, or null.</summary>
        public static Interactable Gazed(Transform head, IEnumerable<Interactable> all)
        {
            if (head == null) return null;
            Interactable best = null; float bestAngle = GazeCone;
            foreach (var i in all)
            {
                if (i == null || !i.isActiveAndEnabled || RouteName(i.EntityType) == null) continue;
                var to = i.PanelAnchor.position - head.position;
                if (to.magnitude > GazeRange) continue;
                float a = Vector3.Angle(head.forward, to);
                if (a < bestAngle) { bestAngle = a; best = i; }
            }
            return best;
        }

        /// <summary>The addressee to send with this sentence, or null to let the service decide.</summary>
        public static string Choose(string utterance, Interactable selected, Transform head, IEnumerable<Interactable> all)
        {
            var named = Named(utterance);
            if (named != null) return named;
            var fromSelection = selected != null ? RouteName(selected.EntityType) : null;
            if (fromSelection != null) return fromSelection;
            var gazed = Gazed(head, all);
            return gazed != null ? RouteName(gazed.EntityType) : null;
        }

        /// <summary>The same, from the game: the open contextual panel and the main camera.</summary>
        public static string Choose(string utterance)
        {
            var who = Choose(utterance, ContextPanel.Open != null ? ContextPanel.Open.Entity : null, Camera.main != null ? Camera.main.transform : null, Interactable.All);
            // the doctor has no speech route (the service does not know him) but still turns to the trainee who names him
            var turnTo = who ?? (Named(utterance) == null && StartsWithWord(utterance, "doctor") ? "doctor" : null);
            if (turnTo != null) Chosen?.Invoke(turnTo);
            return who;
        }

        /// <summary>Raised with the speech-route name ("patient", "nurse", "anaesthesia") when a spoken sentence goes out to that person, so they can turn to look at the trainee.</summary>
        public static event System.Action<string> Chosen;
    }
}
