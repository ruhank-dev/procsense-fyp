using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProcSense.VR.ActionEmitters;
using ProcSense.VR.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProcSense.EditorTools
{
    public static class PatientArmSetup
    {
        private const string Folder = "Assets/Models/Characters/MPFBPatient/";
        private const string Review = "Logs/PatientArmsReview/";
        [Serializable] private class Surface { public Vector3[] vertices; public int[] triangles; }
        [Serializable] private class Fit { public Surface body; public Surface gown; public Surface shorts; }

        private static void Guard()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Stop Play/baking and preserve dirty scenes first.");
        }

        public static string Prepare()
        {
            Guard();
            var patient = GameObject.Find("MPFB patient");
            var idle = patient.GetComponent<PatientIdleView>();
            if (new SerializedObject(idle).FindProperty("armMotions").arraySize > 0) throw new InvalidOperationException("Preserve the installed arm pose; do not curl the fingers twice.");
            idle.RestorePose();
            var bones = patient.GetComponentsInChildren<Transform>().GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            var motions = new List<PatientIdleView.ArmMotion>();
            foreach (string side in new[] { "l", "r" })
            {
                float sign = side == "l" ? 1 : -1;
                var upper = bones["upperarm_" + side]; var lower = bones["lowerarm_" + side]; var hand = bones["hand_" + side];
                Aim(upper, lower, new Vector3(sign * .40f, -.33f, -1));
                Aim(lower, hand, new Vector3(sign * .22f, -.01f, -1));
                var normal = Vector3.Cross(bones["index_01_" + side].position - hand.position, bones["pinky_01_" + side].position - hand.position).normalized;
                if (normal.y < 0) normal = -normal;
                hand.rotation = Quaternion.FromToRotation(normal, Vector3.up) * hand.rotation;
                foreach (string finger in new[] { "index", "middle", "ring", "pinky" })
                {
                    float curl = finger == "index" ? 4 : finger == "middle" ? 7 : finger == "ring" ? 9 : 11;
                    for (int joint = 1; joint <= 2; joint++)
                    {
                        var bone = bones[finger + "_0" + joint + "_" + side]; var next = bones[finger + "_0" + (joint + 1) + "_" + side];
                        var axis = Vector3.Cross(next.position - bone.position, Vector3.down).normalized;
                        bone.rotation = Quaternion.AngleAxis(curl * (joint == 1 ? 1 : .5f), axis) * bone.rotation;
                        motions.Add(Motion(bone, next, Vector3.down, finger == "index" ? 1.5f : 3, .48f + joint * .07f, sign));
                        PrefabUtility.RecordPrefabInstancePropertyModifications(bone);
                    }
                }
                motions.Add(Motion(upper, lower, Vector3.up, .65f, .28f, sign));
                motions.Add(Motion(lower, hand, Vector3.up, 1.3f, .35f, sign + .8f));
                motions.Add(Motion(hand, bones["index_01_" + side], Vector3.up, .7f, .4f, sign + 1.7f));
                foreach (var bone in new[] { upper, lower, hand }) PrefabUtility.RecordPrefabInstancePropertyModifications(bone);
            }
            var body = patient.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r => r.name.EndsWith(".body", StringComparison.Ordinal));
            var gown = GameObject.Find("Draped patient gown").GetComponent<SkinnedMeshRenderer>();
            var blanket = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).SingleOrDefault(t => t.name == "Draped bed blanket");
            idle.Configure(bones["head"], new[] { body, gown }, patient.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r => r.enabled).ToArray());
            idle.ConfigureArms(motions.ToArray(), UnityEngine.Object.FindObjectsByType<SensorAttachment>(FindObjectsSortMode.None));
            if (blanket != null) UnityEngine.Object.DestroyImmediate(blanket.gameObject);
            AssetDatabase.DeleteAsset(Folder + "patient_blanket_idle.asset");
            Directory.CreateDirectory(Review);
            File.WriteAllText(Review + "gown-fit-input.json", JsonUtility.ToJson(new Fit { body = Snapshot(body), gown = Snapshot(gown) }));
            EditorSceneManager.MarkSceneDirty(patient.scene); EditorSceneManager.SaveScene(patient.scene);
            File.WriteAllText(Review + "arm-setup-prepared.txt", "Relaxed rest pose and arm/finger motion wired; blanket removed. Original fabric sources retained.");
            EditorApplication.QueuePlayerLoopUpdate(); SceneView.RepaintAll();
            return "Relaxed arms prepared; gown snapshot ready for Blender fit.";
        }

        public static string FitRestPose()
        {
            Guard();
            var patient = GameObject.Find("MPFB patient");
            var idle = patient.GetComponent<PatientIdleView>();
            idle.RestorePose();
            var bones = patient.GetComponentsInChildren<Transform>().GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            foreach (string side in new[] { "l", "r" })
            {
                float sign = side == "l" ? 1 : -1;
                var upper = bones["upperarm_" + side]; var lower = bones["lowerarm_" + side]; var hand = bones["hand_" + side];
                Aim(upper, lower, new Vector3(sign * .40f, -.33f, -1));
                Aim(lower, hand, new Vector3(sign * .22f, -.01f, -1));
                var normal = Vector3.Cross(bones["index_01_" + side].position - hand.position, bones["pinky_01_" + side].position - hand.position).normalized;
                if (normal.y < 0) normal = -normal;
                hand.rotation = Quaternion.FromToRotation(normal, Vector3.up) * hand.rotation;
                foreach (var bone in new[] { upper, lower, hand }) PrefabUtility.RecordPrefabInstancePropertyModifications(bone);
            }
            var settings = new SerializedObject(idle);
            var motions = settings.FindProperty("armMotions");
            for (int i = 0; i < motions.arraySize; i++)
            {
                var motion = motions.GetArrayElementAtIndex(i);
                var bone = (Transform)motion.FindPropertyRelative("Bone").objectReferenceValue;
                var next = bone.name.StartsWith("hand_", StringComparison.Ordinal) ? bones["index_01_" + bone.name.Substring(bone.name.Length - 1)] : bone.GetChild(0);
                bool finger = !bone.name.StartsWith("upperarm_", StringComparison.Ordinal) && !bone.name.StartsWith("lowerarm_", StringComparison.Ordinal) && !bone.name.StartsWith("hand_", StringComparison.Ordinal);
                motion.FindPropertyRelative("Axis").vector3Value = bone.InverseTransformDirection(Vector3.Cross(next.position - bone.position, finger ? Vector3.down : Vector3.up).normalized);
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(patient.scene); EditorSceneManager.SaveScene(patient.scene);
            return ExportFit();
        }

        public static string ExportFit()
        {
            Guard();
            var patient = GameObject.Find("MPFB patient");
            patient.GetComponent<PatientIdleView>().RestorePose();
            var body = patient.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r => r.name.EndsWith(".body", StringComparison.Ordinal));
            var gown = GameObject.Find("Draped patient gown").GetComponent<SkinnedMeshRenderer>();
            var saved = gown.sharedMesh;
            try
            {
                gown.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "patient_gown_draped.asset");
                Directory.CreateDirectory(Review);
                var shorts = GameObject.Find("Patient shorts")?.GetComponent<SkinnedMeshRenderer>();
                File.WriteAllText(Review + "gown-fit-input.json", JsonUtility.ToJson(new Fit { body = Snapshot(body), gown = Snapshot(gown), shorts = shorts == null ? null : Snapshot(shorts) }));
            }
            finally { gown.sharedMesh = saved; }
            return "Exported original-gown fit snapshot in supported arm pose.";
        }

        private static void Aim(Transform root, Transform next, Vector3 direction) => root.rotation = Quaternion.FromToRotation(next.position - root.position, direction) * root.rotation;

        private static PatientIdleView.ArmMotion Motion(Transform bone, Transform next, Vector3 direction, float degrees, float frequency, float phase)
        {
            return new PatientIdleView.ArmMotion { Bone = bone, Axis = bone.InverseTransformDirection(Vector3.Cross(next.position - bone.position, direction).normalized), Degrees = degrees, Frequency = frequency, Phase = phase };
        }

        private static Surface Snapshot(SkinnedMeshRenderer renderer)
        {
            var mesh = new Mesh();
            try { renderer.BakeMesh(mesh, true); return new Surface { vertices = mesh.vertices.Select(renderer.transform.TransformPoint).ToArray(), triangles = mesh.triangles }; }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }

        public static string ImportGown()
        {
            Guard();
            var renderer = GameObject.Find("Draped patient gown").GetComponent<SkinnedMeshRenderer>();
            GameObject.Find("MPFB patient").GetComponent<PatientIdleView>().RestorePose();
            var fit = JsonUtility.FromJson<Surface>(File.ReadAllText(Review + "gown-fit-output.json"));
            var mesh = UnityEngine.Object.Instantiate(renderer.sharedMesh);
            mesh.name = "patient_gown_relaxed"; mesh.ClearBlendShapes();
            if (fit.vertices.Length != mesh.vertexCount) throw new InvalidOperationException("Preserve the gown's topology and weights.");
            mesh.vertices = fit.vertices;
            mesh.bindposes = renderer.bones.Select(b => b.worldToLocalMatrix * renderer.transform.localToWorldMatrix).ToArray();
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
            string path = Folder + "patient_gown_relaxed.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) AssetDatabase.CreateAsset(mesh, path);
            else { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; EditorUtility.SetDirty(existing); AssetDatabase.SaveAssetIfDirty(existing); }
            renderer.sharedMesh = mesh;
            EditorSceneManager.MarkSceneDirty(renderer.gameObject.scene); EditorSceneManager.SaveScene(renderer.gameObject.scene);
            return PatientIdleSetup.UpdateSurfaces();
        }
    }
}
