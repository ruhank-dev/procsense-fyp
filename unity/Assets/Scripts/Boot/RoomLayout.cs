using UnityEngine;

namespace ProcSense.VR.Boot
{
    /// <summary>The authored room's coordinate space in the continuous hospital.</summary>
    public sealed class RoomLayout : MonoBehaviour
    {
        public Vector2 Size;

        public bool Contains(Vector3 world, float inset = .12f)
        {
            var local = transform.InverseTransformPoint(world);
            return Mathf.Abs(local.x) < Size.x / 2 - inset && Mathf.Abs(local.z) < Size.y / 2 - inset
                && local.y > -.5f && local.y < 3.5f;
        }
    }
}
