using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// Puts a Humanoid character on a seat. The sitting clip is retargeted by Unity's Humanoid system, which scales the hips height by the avatar's leg length; here the pelvis is then set to the seat surface plus a
    /// body-proportional hip offset, once after the first animation evaluation, so adults rest on the seat with feet near the floor and a child (shorter legs) simply dangles his feet.
    /// <see cref="SeatAnchor"/> supplies the seat height and facing; the person's own root stays on the floor under the seat.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Animator))]
    public sealed class SeatedPerson : MonoBehaviour
    {
        [SerializeField, Tooltip("Height of the sitting surface above the floor, metres (a hospital bench is 0.465).")] private float seatHeight = .465f;
        [SerializeField, Tooltip("Pelvis joint height above the seat surface for a 1.70 m person, metres.")] private float hipAboveSeat = .105f;
        [SerializeField, Tooltip("Extra pelvis shift toward the front of the seat (positive = forward), metres, so the thighs lie on the seat and the back meets the backrest.")] private float forwardShift = .03f;
        private bool aligned;

        public float SeatHeight { get => seatHeight; set => seatHeight = value; }

        private static float BodyHeight(Animator a)
        {
            float h = 0f; foreach (var r in a.GetComponentsInChildren<SkinnedMeshRenderer>()) h = Mathf.Max(h, r.localBounds.size.y * Mathf.Abs(r.transform.lossyScale.y));
            return h > .3f ? h : 1.7f;
        }

        /// <summary>Re-seats the person: evaluates the animation once, then moves the root so the Hips bone sits at the seat surface plus the hip offset.</summary>
        public void Align(AnimationClip sample = null)
        {
            var a = GetComponent<Animator>(); var hips = a.GetBoneTransform(HumanBodyBones.Hips); if (hips == null) return;
            if (sample != null) sample.SampleAnimation(gameObject, 0f); else a.Update(0f);                                 // edit-mode callers pass the sitting clip, an Animator is not evaluated there
            float want = transform.parent != null ? transform.parent.position.y : 0f;                                  // floor level under the seat anchor
            float target = want + seatHeight + hipAboveSeat * (BodyHeight(a) / 1.7f);
            transform.position += Vector3.up * (target - hips.position.y) + transform.forward * forwardShift;
            aligned = true;
        }

        private void Start() { if (!aligned) Align(); }
    }
}
