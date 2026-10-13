using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

public static class ExportFabricFit
{
    static object Snapshot(SkinnedMeshRenderer renderer)
    {
        var mesh = renderer.sharedMesh;
        var vertices = mesh.vertices;
        var weights = mesh.boneWeights;
        var matrices = renderer.bones.Select((bone, i) => bone.localToWorldMatrix * mesh.bindposes[i]).ToArray();
        var world = vertices.Select((vertex, i) =>
        {
            var w = weights[i];
            return matrices[w.boneIndex0].MultiplyPoint3x4(vertex) * w.weight0
                + matrices[w.boneIndex1].MultiplyPoint3x4(vertex) * w.weight1
                + matrices[w.boneIndex2].MultiplyPoint3x4(vertex) * w.weight2
                + matrices[w.boneIndex3].MultiplyPoint3x4(vertex) * w.weight3;
        }).ToArray();
        return new
        {
            vertices = world.Select(v => new[] { v.x, v.y, v.z }).ToArray(),
            triangles = mesh.triangles,
            bones = renderer.bones.Select(b => b.name).ToArray(),
            weights = weights.Select(w => new[] { (float)w.boneIndex0, w.weight0, w.boneIndex1, w.weight1, w.boneIndex2, w.weight2, w.boneIndex3, w.weight3 }).ToArray()
        };
    }

    public static object Main()
    {
        if (EditorApplication.isPlaying || SceneManager.GetActiveScene().name != "MPFBPatientReview" || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Open the saved MPFB review in Edit Mode first.");
        const string path = "Assets/Models/Characters/MPFBPatient/mpfb_patient.fbx";
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        bool readable = importer.isReadable;
        try
        {
            if (!readable) { importer.isReadable = true; importer.SaveAndReimport(); }
            var renderers = GameObject.Find("MPFB patient").GetComponentsInChildren<SkinnedMeshRenderer>();
            var bed = GameObject.Find("PatientBay").transform.Find("Bed").GetComponent<Renderer>().bounds;
            string output = Path.GetFullPath("../build/mpfb/output/fabric-fit.json");
            File.WriteAllText(output, Newtonsoft.Json.JsonConvert.SerializeObject(new
            {
                body = Snapshot(renderers.Single(r => r.name.Contains(".body"))),
                gown = Snapshot(renderers.Single(r => r.name == "PatientGown")),
                bed = new { min = new[] { bed.min.x, bed.min.y, bed.min.z }, max = new[] { bed.max.x, bed.max.y, bed.max.z } }
            }));
            return new { output, bed = bed.ToString() };
        }
        finally
        {
            if (!readable) { importer.isReadable = false; importer.SaveAndReimport(); }
        }
    }
}
