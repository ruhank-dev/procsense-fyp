using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// Reusable hand-object interaction for Humanoid characters (papers, clipboards, phones, tablets, charts). A prop has named grip points; this component
    ///  - keeps the prop at a chosen place in the CHARACTER frame relative to the chest, so it rides breathing and posture changes (two-handed props), or parents it to a hand (one-handed props),
    ///  - drives the hands to the grip points with Animator IK (position and rotation) and the elbows with hint weights,
    ///  - curls the fingers of every holding hand through the Humanoid muscles (rig independent) so the fingers wrap the prop,
    ///  - turns the head and eyes toward a look target (the paper, the phone, a conversation partner).
    /// Needs "IK Pass" enabled on the Animator layer. Presentation only: it reads no clinical state.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Animator))]
    public sealed class PropGrip : MonoBehaviour
    {
        public enum Mode { TwoHandedOnChest, OneHandOnHip, None }

        [SerializeField] private Mode mode = Mode.None;
        [SerializeField] private Transform prop;
        [SerializeField] private Transform gripRight, gripLeft;
        [SerializeField, Tooltip("Prop centre relative to the chest bone, in the character frame (x = character left, y = up, z = forward), metres.")] private Vector3 chestOffset = new Vector3(0, -.10f, .25f);
        [SerializeField, Range(0f, 1f)] private float handWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float curl = .65f;
        [SerializeField] private Transform lookTarget;
        [SerializeField, Range(0f, 1f)] private float lookWeight = .65f;
        [SerializeField, Tooltip("Hand rotation offset applied to the grip rotation (euler degrees), per hand.")] private Vector3 rightHandEuler = new Vector3(0, 0, 0), leftHandEuler = new Vector3(0, 0, 0);

        private Animator animator; private HumanPoseHandler poseHandler; private HumanPose pose; private int[] rightFingers, leftFingers;

        public Mode GripMode { get => mode; set => mode = value; }
        public Transform Prop { get => prop; set => prop = value; }
        public Transform GripRight { get => gripRight; set => gripRight = value; }
        public Transform GripLeft { get => gripLeft; set => gripLeft = value; }
        public Vector3 ChestOffset { get => chestOffset; set => chestOffset = value; }
        public Transform LookTarget { get => lookTarget; set => lookTarget = value; }
        public float Curl { get => curl; set => curl = value; }
        public Vector3 RightHandEuler { get => rightHandEuler; set => rightHandEuler = value; }
        public Vector3 LeftHandEuler { get => leftHandEuler; set => leftHandEuler = value; }

        private void Awake() { animator = GetComponent<Animator>(); }

        private static int[] FingerMuscles(string side)
        {
            var list = new System.Collections.Generic.List<int>();
            for (int i = 0; i < HumanTrait.MuscleCount; i++)
            {
                var n = HumanTrait.MuscleName[i];
                if (n.StartsWith(side + " ") && (n.Contains("Index") || n.Contains("Middle") || n.Contains("Ring") || n.Contains("Little") || n.Contains("Thumb")) && n.Contains("Stretched")) list.Add(i);
            }
            return list.ToArray();
        }

        private void LateUpdate()
        {
            if (mode == Mode.None || animator == null || prop == null) return;
            var chest = animator.GetBoneTransform(HumanBodyBones.UpperChest) ?? animator.GetBoneTransform(HumanBodyBones.Chest);
            if (mode == Mode.TwoHandedOnChest && chest != null)
            {
                var rot = transform.rotation; prop.position = chest.position + rot * new Vector3(-chestOffset.x, chestOffset.y, chestOffset.z) * 1f + rot * Vector3.zero;
                prop.rotation = rot * Quaternion.Euler(-35f, 0, 0);
            }
            else if (mode == Mode.OneHandOnHip)
            {
                var hand = animator.GetBoneTransform(HumanBodyBones.RightHand) ?? animator.GetBoneTransform(HumanBodyBones.LeftHand);
                if (hand != null) { prop.position = hand.position + hand.rotation * chestOffset; }
            }
            CurlFingers();
        }

        /// <summary>Closes the fingers of the holding hands using the Humanoid muscle space (stretched muscles run from -1 curled to +1 extended).</summary>
        private void CurlFingers()
        {
            if (curl <= 0f) return;
            if (poseHandler == null) { poseHandler = new HumanPoseHandler(animator.avatar, animator.transform); rightFingers = FingerMuscles("Right"); leftFingers = FingerMuscles("Left"); }
            poseHandler.GetHumanPose(ref pose);
            float v = Mathf.Lerp(0f, -.85f, curl);
            if (gripRight != null) foreach (int m in rightFingers) pose.muscles[m] = Mathf.Min(pose.muscles[m], Mathf.Lerp(pose.muscles[m], v, 1f));
            if (gripLeft != null) foreach (int m in leftFingers) pose.muscles[m] = Mathf.Min(pose.muscles[m], Mathf.Lerp(pose.muscles[m], v, 1f));
            poseHandler.SetHumanPose(ref pose);
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator == null) return;
            if (lookTarget != null) { animator.SetLookAtWeight(lookWeight, .25f, .7f, .2f, .5f); animator.SetLookAtPosition(lookTarget.position); }
            if (mode == Mode.None) return;
            Hand(AvatarIKGoal.RightHand, gripRight, rightHandEuler); Hand(AvatarIKGoal.LeftHand, gripLeft, leftHandEuler);
        }

        private void Hand(AvatarIKGoal goal, Transform grip, Vector3 euler)
        {
            if (grip == null) { animator.SetIKPositionWeight(goal, 0f); animator.SetIKRotationWeight(goal, 0f); return; }
            animator.SetIKPositionWeight(goal, handWeight); animator.SetIKRotationWeight(goal, handWeight * .8f);
            animator.SetIKPosition(goal, grip.position); animator.SetIKRotation(goal, grip.rotation * Quaternion.Euler(euler));
            animator.SetIKHintPositionWeight(goal == AvatarIKGoal.RightHand ? AvatarIKHint.RightElbow : AvatarIKHint.LeftElbow, 0f);
        }
    }
}
