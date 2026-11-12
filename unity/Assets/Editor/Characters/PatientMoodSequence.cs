using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProcSense.VR.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Review tool: shows how each mood develops over time, with the automatic eye, blink and head motion on. One row per mood, one column per moment after the mood starts (0.25, 0.75, 1.5, 3 and 5 seconds).
    /// Writes Logs/patient_seq_faces.png (every mood, the face) and Logs/patient_seq_bodies.png (pain, distress, and pain returning to calm, head and arm).
    /// Run: Unity -projectPath unity -executeMethod ProcSense.EditorTools.PatientMoodSequence.Run (needs graphics, so no -nographics). The scene is not saved.
    /// </summary>
    public static class PatientMoodSequence
    {
        private static readonly float[] Moments = { .25f, .75f, 1.5f, 3f, 5f };
        private static float clock;
        private static PatientMotion motion;
        private static Camera cam;
        private static GameObject camGo;
        private static SkinnedMeshRenderer face;

        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var patient = GameObject.Find("Rocketbox patient");
            motion = patient.GetComponent<PatientMotion>();
            motion.autoMotion = true; motion.Prepare();
            var pelvis = patient.GetComponentsInChildren<Transform>(true).First(t => t.name == "Bip01 Pelvis");
            var headUp = (motion.Head.position - pelvis.position).normalized;
            face = patient.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.sharedMesh != null && r.sharedMesh.blendShapeCount > 0);

            camGo = new GameObject("review camera"); cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = .05f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.18f, .2f, .22f);
            var light = new GameObject("review light").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.1f; light.transform.rotation = Quaternion.Euler(70, 20, 0);

            var all = PatientExpression.Modes.ToList();
            var faceRows = new List<(string from, string to)> { ("", "") };
            faceRows.AddRange(all.Select(m => ("", m)));
            Write("Logs/patient_seq_faces.png", faceRows, motion.Head.position + Vector3.up * .02f, 1.1f, 22f, headUp, 288);
            var bodyRows = new List<(string from, string to)> { ("", "IN_PAIN"), ("", "DISTRESSED"), ("IN_PAIN", "") };
            Write("Logs/patient_seq_bodies.png", bodyRows, Vector3.Lerp(pelvis.position, motion.Head.position, .55f), 1.9f, 38f, headUp, 400);
            Debug.Log("[MoodSequence] written Logs/patient_seq_faces.png and Logs/patient_seq_bodies.png");
        }

        private static void Advance(float seconds) { for (float s = 0; s < seconds - 1e-4f; s += .05f) { clock += .05f; motion.Step(clock, .05f); } }

        private static void Write(string path, List<(string from, string to)> rows, Vector3 focus, float distance, float fov, Vector3 up, int size)
        {
            cam.fieldOfView = fov; camGo.transform.position = focus + Vector3.up * distance; camGo.transform.LookAt(focus, up);
            var sheet = new Texture2D(Moments.Length * size, rows.Count * size, TextureFormat.RGB24, false);
            var rt = new RenderTexture(size, size, 24); cam.targetTexture = rt;
            for (int r = 0; r < rows.Count; r++)
            {
                motion.SetMood("", true); Advance(6f);
                if (rows[r].from.Length > 0) { motion.SetMood(rows[r].from, true); Advance(5f); }
                motion.SetMood(rows[r].to, true);
                float at = 0;
                for (int c = 0; c < Moments.Length; c++)
                {
                    Advance(Moments[c] - at); at = Moments[c];
                    face.enabled = false; face.enabled = true; face.updateWhenOffscreen = true;
                    cam.Render(); cam.Render(); RenderTexture.active = rt;
                    var tile = new Texture2D(size, size, TextureFormat.RGB24, false); tile.ReadPixels(new Rect(0, 0, size, size), 0, 0); tile.Apply();
                    sheet.SetPixels(c * size, (rows.Count - 1 - r) * size, size, size, tile.GetPixels());
                }
            }
            Directory.CreateDirectory("Logs"); File.WriteAllBytes(path, sheet.EncodeToPNG());
        }
    }
}
