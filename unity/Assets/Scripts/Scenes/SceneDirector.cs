using System.Collections.Generic;
using ProcSense.Core.Observation;
using ProcSense.VR.Bridge;
using UnityEngine;
using UnityEngine.AI;

namespace ProcSense.VR.Scenes
{
    /// <summary>
    /// Moves the patient, the porter and, when they go too, the trainee as Core's transport says (p0-vr-platform-s06): it only shows what Core decided (<see cref="TransportStaging"/>), and never decides when anyone
    /// arrives. Anchors are child transforms named after Core's places ("patient_bay", "corridor", "theatre_reception"), their doors ("door:ct") and the hidden space behind a door ("inside:ct"); a missing door
    /// or inside anchor falls back to the place's own. Travel is cosmetic: with a NavMeshAgent on a baked NavMesh the patient walks the mesh, otherwise they move in a straight line at walking pace.
    /// Wheelchair and trolley are greybox props under the patient until the characters (p0-characters-s06) bring the real ones. Not placed in a scene yet; the NavMesh bake and the device check are UX-P0-VRPLAT.
    /// </summary>
    public sealed class SceneDirector : MonoBehaviour
    {
        public const float WalkSpeed = 1.1f, PushSpeed = 0.9f;

        public Transform Patient, Porter, TraineeRig;
        public Staging Current { get; private set; } = new Staging();
        public string From { get; private set; } = "";
        public string State { get; private set; } = "";
        public GameObject Prop { get; private set; }

        private readonly Dictionary<string, Transform> _anchors = new Dictionary<string, Transform>();
        private ObservableState _shown;

        private void Awake() => FindAnchors();

        /// <summary>Reads the anchors under this object again (call after adding some).</summary>
        public void FindAnchors()
        {
            _anchors.Clear();
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t != transform && !_anchors.ContainsKey(t.name)) _anchors[t.name] = t;
        }

        public Transform Anchor(string id)
        {
            if (_anchors.TryGetValue(id, out var a) && a != null) return a;
            int colon = id.IndexOf(':');
            return colon > 0 && _anchors.TryGetValue(id.Substring(colon + 1), out var place) ? place : null;
        }

        private void Update()
        {
            var host = SimulationHost.Instance;
            if (host != null && host.Observable != _shown) Sync();
            Travel(Time.deltaTime);
        }

        /// <summary>Takes Core's state now: what the patient, porter and trainee should show, and snaps whoever is not travelling to their anchor.</summary>
        public void Sync()
        {
            var s = SimulationHost.Instance != null ? SimulationHost.Instance.Observable : null;
            _shown = s;
            if (s == null || Patient == null) return;
            var t = s.Transport;
            string state = t?.State ?? "";
            if ((state == "REQUESTED" || state == "DELAYED") && (State.Length == 0 || State == "BACK" || State == "CANCELLED" || State == "DONE")) From = s.Location;
            if (From.Length == 0) From = s.Location;
            State = state;
            Current = TransportStaging.For(t, s.Location, s.TraineeLocation, From);

            SetVisible(Patient, Current.Visible);
            ShowProp(Current.Mode);
            if (Porter != null) Porter.gameObject.SetActive(Current.Porter);
            if (!Current.Moving) Snap(Current.Anchor);
            if (TraineeRig != null && Current.TraineeFollows && !Current.Moving)
            {
                var a = Anchor(Current.Anchor.StartsWith("inside:") ? TransportStaging.Door(Current.Anchor.Substring(7)) : Current.Anchor);   // with the patient, but outside the hidden room
                if (a != null) TraineeRig.position = a.position + a.right * 0.8f;
            }
        }

        /// <summary>Moves a travelling patient (and the porter and a following trainee) on by this much time, never past the anchor Core allows.</summary>
        public void Travel(float seconds)
        {
            if (!Current.Moving || Patient == null || seconds <= 0f) return;
            var target = Anchor(Current.Anchor);
            if (target == null) return;
            var agent = Patient.GetComponent<NavMeshAgent>();
            if (agent != null && agent.isOnNavMesh) { agent.speed = Speed; agent.SetDestination(target.position); }
            else Patient.position = Vector3.MoveTowards(Patient.position, target.position, Speed * seconds);
            if (Porter != null && Porter.gameObject.activeSelf) Porter.position = Patient.position - Patient.forward * 0.9f;
            if (TraineeRig != null && Current.TraineeFollows) TraineeRig.position = Patient.position + Patient.right * 0.8f;
        }

        private float Speed => Current.Mode == "walk" ? WalkSpeed : PushSpeed;

        private void Snap(string anchor)
        {
            var a = Anchor(anchor);
            if (a == null) return;
            var agent = Patient.GetComponent<NavMeshAgent>();
            if (agent != null && agent.isOnNavMesh) agent.Warp(a.position);
            else Patient.position = a.position;
            Patient.rotation = a.rotation;
            if (Porter != null && Porter.gameObject.activeSelf) Porter.position = a.position - a.forward * 0.9f;
        }

        private static void SetVisible(Transform t, bool visible)
        {
            foreach (var r in t.GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
        }

        // greybox: a seat for the wheelchair, a long flat box for the trolley
        private void ShowProp(string mode)
        {
            string want = mode == "wheelchair" || mode == "trolley" ? mode : "";
            if (Prop != null && Prop.name == "Prop " + want) { SetVisible(Prop.transform, Current.Visible); return; }
            if (Prop != null) Destroy(Prop);
            Prop = null;
            if (want.Length == 0) return;
            Prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Prop.name = "Prop " + want;
            DestroyImmediate(Prop.GetComponent<Collider>());
            Prop.transform.SetParent(Patient, false);
            Prop.transform.localPosition = want == "wheelchair" ? new Vector3(0, 0.25f, 0) : new Vector3(0, 0.4f, 0);
            Prop.transform.localScale = want == "wheelchair" ? new Vector3(0.6f, 0.5f, 0.6f) : new Vector3(0.7f, 0.1f, 2.0f);
            SetVisible(Prop.transform, Current.Visible);
        }
    }
}
