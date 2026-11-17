using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// The shared conversation layer for a staff character (P3-ANI-02): the nurse, porter, doctor and any later staff. It owns a <see cref="CharacterEventState"/>, hears the same events the patient does
    /// (a spoken sentence addressed to this person, one of their voice lines playing) and turns their head to the player while they are addressed, speaking or listening, within <see cref="LookAtLimits"/>.
    /// The patient keeps <see cref="PatientReactions"/> because its head belongs to <see cref="PatientMotion"/>. Presentation only: it decides nothing clinical.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterPresenter : MonoBehaviour
    {
        [Tooltip("Who this is, as the voice catalogue and the speech routes name them: nurse, porter, doctor, anaesthesia.")] public string who = "";
        [Tooltip("The head bone. Empty: the Humanoid head, then 'Bip01 Head'.")] public Transform head;
        [Tooltip("Who to look at: the player's head. Empty: the main camera.")] public Transform player;
        public CharacterEventState State { get; } = new CharacterEventState();
        /// <summary>The head turn being applied now, in degrees (yaw, pitch) from the character's forward.</summary>
        public Vector2 Turn { get; private set; }

        private ProcSense.VR.Audio.VoicePlayer voice;
        private bool bound;

        /// <summary>Names the character and its head bone, and starts listening for addressee events (Awake does the second part too; a test can call it directly).</summary>
        public void Setup(string who, Transform head)
        {
            this.who = who; this.head = head; Bind();
            if (GetComponent<PatientSpeechFace>() != null) return;
            var faces = new System.Collections.Generic.List<SkinnedMeshRenderer>();      // lip sync (P3-ANI-05): the jaw and visemes follow the audio while this person speaks
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (PatientSpeechFace.HasMouth(r.sharedMesh)) faces.Add(r);
            if (faces.Count == 0) return;
            var mouth = gameObject.AddComponent<PatientSpeechFace>(); mouth.Configure(faces.ToArray()); mouth.Gate = () => State.State == CharacterState.Speaking;
        }

        public void Bind()
        {
            if (bound) return;
            bound = true; ProcSense.VR.Interaction.AddresseeTarget.Chosen += OnAddressee;
        }

        private void Awake() => Bind();

        /// <summary>Follows this voice player: this person is "speaking" while one of their lines plays, then listens. Safe to call every frame with the same player.</summary>
        public void Attach(ProcSense.VR.Audio.VoicePlayer player)
        {
            if (voice == player) return;
            if (voice != null) voice.Started -= OnLine;
            voice = player;
            if (voice != null) voice.Started += OnLine;
        }

        private bool IsMyLine(string speaker) => speaker == who || who == "doctor" && speaker == "clinician";       // the catalogue calls the escalation doctor "clinician"
        private void OnLine(ProcSense.VR.Audio.VoiceLine line) { if (IsMyLine(line.Speaker)) State.StartSpeaking(); }
        private void OnAddressee(string route) { if (route == (ProcSense.VR.Interaction.AddresseeTarget.RouteName(who) ?? who)) State.Address(); }     // the anaesthetist's speech route is "anaesthesia"

        public void Address() => State.Address();
        public void SetBusy(bool busy) => State.SetBusy(busy);

        private void OnDestroy()
        {
            if (voice != null) voice.Started -= OnLine;
            ProcSense.VR.Interaction.AddresseeTarget.Chosen -= OnAddressee;
        }

        private Transform Target => player != null ? player : (Camera.main != null ? Camera.main.transform : null);
        private bool Attending => State.State == CharacterState.Addressed || State.State == CharacterState.Speaking || State.State == CharacterState.Listening;

        /// <summary>The yaw and pitch (degrees) that turn a head at <paramref name="from"/> toward <paramref name="to"/>, measured from the body frame and limited to what <see cref="LookAtLimits"/> allows.</summary>
        public static Vector2 HeadTurn(Transform body, Vector3 from, Vector3 to)
        {
            var a = LookAtLimits.Angles(body.forward, body.up, body.right, to - from);
            var s = LookAtLimits.Share(a.x, a.y);
            return new Vector2(s.EyeYaw + s.HeadYaw, s.EyePitch + s.HeadPitch);       // no eye bones are driven here, so the head takes the whole limited turn
        }

        private void Update()
        {
            var session = SessionPresentation.Instance; if (session != null && session.Voice != null) Attach(session.Voice);     // follows the session's voice player, also after it is rebuilt
            if (State.State == CharacterState.Speaking && (voice == null || voice.Current == null || !IsMyLine(voice.Current.Speaker))) State.EndSpeaking();     // their line is over
            State.Step(Time.deltaTime);
        }

        private void LateUpdate()
        {
            if (head == null) head = FindHead();
            if (head == null) return;
            var target = Attending ? Target : null;
            var want = target != null ? HeadTurn(transform, head.position, target.position) : Vector2.zero;
            Turn = Vector2.Lerp(Turn, want, 1f - Mathf.Exp(-Time.deltaTime * 6f));
            if (Turn.sqrMagnitude < 1e-4f) return;
            head.rotation = Quaternion.AngleAxis(Turn.x, transform.up) * Quaternion.AngleAxis(Turn.y, transform.right) * head.rotation;
        }

        private Transform FindHead()
        {
            var animator = GetComponentInChildren<Animator>();
            var bone = animator != null && animator.avatar != null && animator.avatar.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            if (bone != null) return bone;
            foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == "Bip01 Head") return t;
            return null;
        }
    }
}
