using UnityEngine;
namespace ProcSense.VR.Views
{
    /// <summary>Follows a camera without reparenting the view out of its lifecycle owner.</summary>
    public sealed class CameraHudAnchor : MonoBehaviour
    {
        public Transform Follow;
        public Vector3 Offset = new Vector3(0, .30f, 1.2f);
        private void LateUpdate() => Sync();
        public void Sync()
        {
            if (Follow == null) return;
            transform.SetPositionAndRotation(Follow.TransformPoint(Offset), Follow.rotation);
        }
    }
}
