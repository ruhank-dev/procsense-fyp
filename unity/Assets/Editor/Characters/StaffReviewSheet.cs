using System.IO;
using System.Linq;
using ProcSense.VR.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Review tool for the staff conversation layer (P3-ANI-02/03/05): renders Logs/staff_doctor.png, four tiles of "Doctor at the bay": idle, head turned to the player (addressed), speaking (head and mouth), and the doctor in the room from the bed.
    /// The head turn is the presenter's own HeadTurn maths applied to the head bone, the mouth is PatientSpeechFace driven with synthetic speech features. Run (graphics, Unity closed): -executeMethod ProcSense.EditorTools.StaffReviewSheet.Run. The scene is not saved.
    /// </summary>
    public static class StaffReviewSheet
    {
        private const int Size = 512;

        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var doc = GameObject.Find("Doctor at the bay"); if (doc == null) { Debug.LogError("[StaffReview] no 'Doctor at the bay'"); return; }
            var anim = doc.GetComponent<Animator>(); anim.Rebind(); anim.Update(1.2f);
            var head = doc.GetComponentsInChildren<Transform>(true).First(t => t.name == "Bip01 Head");
            var presenter = doc.AddComponent<CharacterPresenter>(); presenter.Setup("doctor", head);
            var mouth = doc.GetComponent<PatientSpeechFace>();

            var camGo = new GameObject("review camera"); var cam = camGo.AddComponent<Camera>(); cam.nearClipPlane = .05f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.18f, .2f, .22f);
            var light = new GameObject("review light").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.1f; light.transform.rotation = Quaternion.Euler(40, 150, 0);
            var rt = new RenderTexture(Size, Size, 24); cam.targetTexture = rt;
            var sheet = new Texture2D(4 * Size, Size, TextureFormat.RGB24, false);

            var headPos = head.position;
            var player = headPos + Quaternion.AngleAxis(40f, Vector3.up) * doc.transform.forward * 2.5f;       // the trainee, 40 degrees to the doctor's side, in front of them
            var baseRot = head.rotation;
            void Face(float yaw, float pitch) { head.rotation = baseRot; head.rotation = Quaternion.AngleAxis(yaw, doc.transform.up) * Quaternion.AngleAxis(pitch, doc.transform.right) * head.rotation; }
            void Shoot(int tile, Vector3 from, Vector3 look, float fov)
            {
                cam.fieldOfView = fov; camGo.transform.position = from; camGo.transform.LookAt(look);
                foreach (var r in doc.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { r.updateWhenOffscreen = true; r.enabled = false; r.enabled = true; }      // re-skin after the bone was turned
                cam.Render(); cam.Render(); RenderTexture.active = rt;
                var t = new Texture2D(Size, Size, TextureFormat.RGB24, false); t.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); t.Apply();
                sheet.SetPixels(tile * Size, 0, Size, Size, t.GetPixels());
            }

            Physics.SyncTransforms();
            // a camera spot with a clear line to the face, nearest the doctor's front: tried round the doctor in 20 degree steps
            Vector3 Clear(float distance, float height)
            {
                for (int a = 0; a < 360; a += 20)
                    foreach (var s in new[] { a, -a })
                    {
                        var p = new Vector3(headPos.x, height, headPos.z) + Quaternion.AngleAxis(s, Vector3.up) * doc.transform.forward * distance;
                        if (!Physics.CheckSphere(p, .12f) && !Physics.Linecast(p, headPos)) return p;
                    }
                return headPos + doc.transform.forward * distance;
            }
            var closeFrom = Clear(1.0f, headPos.y);
            player = closeFrom;                                                                      // the trainee is where the camera is, so the turn is toward the lens
            var turn = CharacterPresenter.HeadTurn(doc.transform, headPos, player);
            Face(0, 0); Shoot(0, closeFrom, headPos, 28f);                                          // idle
            Face(turn.x, turn.y); Shoot(1, closeFrom, headPos, 28f);                                  // addressed: the head turned to the player
            if (mouth != null)
            {
                mouth.Drive(.8f, .2f, .6f, .2f, .05f); for (int i = 0; i < 20; i++) mouth.StepForTest(.05f);
                foreach (var r in doc.GetComponentsInChildren<SkinnedMeshRenderer>(true)) r.enabled = false; foreach (var r in doc.GetComponentsInChildren<SkinnedMeshRenderer>(true)) r.enabled = true;
            }
            Shoot(2, closeFrom, headPos, 28f);                                                        // speaking
            Face(0, 0);
            var bed = GameObject.Find("Bed"); var bedPos = bed != null ? bed.GetComponentsInChildren<Renderer>().Select(r => r.bounds.center).Aggregate((a, b) => (a + b) / 2) : doc.transform.position + doc.transform.forward;
            Shoot(3, Clear(3.2f, 1.7f), doc.transform.position + Vector3.up * 1.0f, 60f);                // the doctor in the room, from a clear spot further back
            Debug.Log($"[StaffReview] doctor {doc.transform.position:F2} head {headPos:F2} close camera {closeFrom:F2}");
            Directory.CreateDirectory("Logs"); File.WriteAllBytes("Logs/staff_doctor.png", sheet.EncodeToPNG());
            Debug.Log($"[StaffReview] written Logs/staff_doctor.png (head turn {turn}, mouth shapes {(mouth != null ? mouth.ShapesFound : -1)})");
        }
    }
}
