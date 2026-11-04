using System.Collections;
using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// The shared city outside the hospital windows extends about 2.4 km (skyline impostor rings); the rig camera's stock far clip of 1000 m would cut it. This raises the far clip of the cameras that view the
    /// hospital to <see cref="FarClip"/> while the exterior is loaded and makes sure they clear to the skybox. Placed on the "City exterior" root, so rooms without a city are unaffected.
    /// </summary>
    public class ExteriorViewRange : MonoBehaviour
    {
        public const float FarClip = 4500f;

        void OnEnable() { StartCoroutine(Apply()); }

        IEnumerator Apply()
        {
            // the rig can arrive after the room scene (additive load), so look for cameras for a few seconds
            for (int i = 0; i < 6; i++)
            {
                foreach (var c in Camera.allCameras)
                {
                    if (c.targetTexture != null || c.cameraType != CameraType.Game) continue;
                    if (c.farClipPlane < FarClip) c.farClipPlane = FarClip;
                    if (c.clearFlags == CameraClearFlags.Nothing) c.clearFlags = CameraClearFlags.Skybox;
                }
                yield return new WaitForSeconds(1f);
            }
        }
    }
}
