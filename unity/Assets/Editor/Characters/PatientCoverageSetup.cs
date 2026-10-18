using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProcSense.VR.Views;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProcSense.EditorTools
{
    public static class PatientCoverageSetup
    {
        private const string Folder = "Assets/Models/Characters/MPFBPatient/";
        public static string Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning || Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Stop Play/baking and preserve dirty scenes first.");
            if (GameObject.Find("Patient shorts") != null) throw new InvalidOperationException("Preserve the installed shorts and leg setup.");
            var patient = GameObject.Find("MPFB patient");
            var idle = patient.GetComponent<PatientIdleView>();
            idle.RestorePose();
            string path = Folder + "patient_shorts.fbx";
            File.Copy("Logs/PatientCoverageReview/patient_shorts.fbx", path, true);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.importAnimation = false; importer.importBlendShapes = false;
            importer.importCameras = false; importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentInChildren<SkinnedMeshRenderer>();
            var body = patient.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r => r.name.EndsWith(".body", StringComparison.Ordinal));
            var bones = patient.GetComponentsInChildren<Transform>().GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            var obj = new GameObject("Patient shorts");
            obj.transform.SetParent(body.transform.parent, false);
            obj.transform.localPosition = body.transform.localPosition;
            obj.transform.localRotation = body.transform.localRotation;
            obj.transform.localScale = body.transform.localScale;
            var shorts = obj.AddComponent<SkinnedMeshRenderer>();
            shorts.sharedMesh = source.sharedMesh;
            shorts.bones = source.bones.Select(b => bones[b.name]).ToArray();
            shorts.rootBone = body.rootBone;
            shorts.localBounds = source.localBounds;
            shorts.updateWhenOffscreen = true;
            shorts.forceMatrixRecalculationPerRender = true;
            string materialPath = Folder + "Materials/PatientShorts_Navy.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.name = "PatientShorts_Navy";
                material.SetColor("_BaseColor", new Color(.035f, .075f, .12f, 1));
                material.SetFloat("_Smoothness", .1f); material.SetFloat("_Cull", 0);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            shorts.sharedMaterial = material;
            var legMotions = new List<PatientIdleView.LegMotion>();
            foreach (string side in new[] { "l", "r" })
            {
                float delay = side == "l" ? 4 : 10;
                foreach (var item in new[] { ("thigh_", Vector3.up, .45f), ("calf_", Vector3.up, .65f),
                    ("foot_", new Vector3(1, 0, side == "l" ? .3f : -.3f).normalized, 2.3f), ("ball_", Vector3.right, 3.5f) })
                {
                    var bone = bones[item.Item1 + side];
                    legMotions.Add(new PatientIdleView.LegMotion { Bone = bone, Axis = bone.InverseTransformDirection(item.Item2), Degrees = item.Item3, Delay = delay });
                }
            }
            idle.ConfigureLegs(legMotions.ToArray());
            var gown = GameObject.Find("Draped patient gown").GetComponent<SkinnedMeshRenderer>();
            idle.Configure(bones["head"], new[] { body, gown, shorts }, patient.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r => r.enabled).ToArray());
            EditorSceneManager.MarkSceneDirty(patient.scene); EditorSceneManager.SaveScene(patient.scene);
            return PatientIdleSetup.UpdateSurfaces();
        }
    }
}
