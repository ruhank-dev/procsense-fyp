using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// A seat on a piece of furniture: its position is the point on the floor under the middle of the seat, its forward axis is the direction a sitter faces, and <see cref="SeatHeight"/> is the height of the sitting surface.
    /// Seated characters are parented to an anchor, so moving or turning the bench moves them with it. The retargeted sitting clips already place the pelvis at the library seat height (see Tools/MPFB/retarget_ual.py:
    /// SEAT_TRIM), so a person whose anchor height matches the bench needs no further adjustment; <see cref="Offset"/> re-aligns a character for a different seat height by moving the root up or down.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SeatAnchor : MonoBehaviour
    {
        [SerializeField, Tooltip("Height of the sitting surface above the floor, metres.")] private float seatHeight = .465f;
        [SerializeField, Tooltip("Library clips assume a seat this high; the root is lifted by the difference.")] private float referenceSeatHeight = .465f;
        [SerializeField] private Transform occupant;

        public float SeatHeight { get => seatHeight; set => seatHeight = value; }
        public Transform Occupant => occupant;
        public float Offset => seatHeight - referenceSeatHeight;

        /// <summary>Seats <paramref name="person"/> here: parents it to the anchor, faces the anchor's direction and lifts it when the seat is higher or lower than the clip assumes.</summary>
        public void Seat(Transform person)
        {
            occupant = person; person.SetParent(transform, true); person.localPosition = new Vector3(0f, Offset, 0f); person.localRotation = Quaternion.identity;
        }
    }
}
