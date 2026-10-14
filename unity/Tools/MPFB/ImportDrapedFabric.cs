using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class ImportDrapedFabric
{
    public class Fabric { public float[][] vertices; public int[] triangles; public string[] bones; public float[][] weights; }
    const string Folder = "Assets/Models/Characters/MPFBPatient";

    static T Component<T>(GameObject obj) where T : Component
    {
        var component = obj.GetComponent<T>();
        return component != null ? component : obj.AddComponent<T>();
    }

    static Mesh Import(string file, string name)
    {
        var data = Newtonsoft.Json.JsonConvert.DeserializeObject<Fabric>(File.ReadAllText("../build/mpfb/output/" + file));
        if (data.vertices.Any(v => v.Any(x => float.IsNaN(x) || float.IsInfinity(x)))) throw new InvalidOperationException("Invalid cloth geometry.");
        var mesh = new Mesh { name = name };
        mesh.vertices = data.vertices.Select(v => new Vector3(v[0], v[1], v[2])).ToArray();
        mesh.triangles = data.triangles;
        mesh.uv = mesh.vertices.Select(v => new Vector2(v.x * 2, v.z * 2)).ToArray();
        if (data.weights != null)
            mesh.boneWeights = data.weights.Select(w => new BoneWeight { boneIndex0 = (int)w[0], weight0 = w[1], boneIndex1 = (int)w[2], weight1 = w[3], boneIndex2 = (int)w[4], weight2 = w[5], boneIndex3 = (int)w[6], weight3 = w[7] }).ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh SaveMesh(Mesh mesh, string name)
    {
        string path = Folder + "/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
        // CopySerialized can leave a Mesh's native render buffers stale after topology changes.
        existing.Clear();
        existing.vertices = mesh.vertices;
        existing.triangles = mesh.triangles;
        existing.uv = mesh.uv;
        existing.normals = mesh.normals;
        existing.tangents = mesh.tangents;
        existing.boneWeights = mesh.boneWeights;
        existing.bindposes = mesh.bindposes;
        existing.bounds = mesh.bounds;
        UnityEngine.Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(existing);
        return existing;
    }

    static GameObject ObjectFor(string name, Transform parent)
    {
        var child = parent.Find(name);
        if (child != null) return child.gameObject;
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, true);
        return obj;
    }

    public static object Main()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.name != "PatientBay" || scene.isDirty)
            throw new InvalidOperationException("Open the saved PatientBay in Edit Mode first.");
        var bay = GameObject.Find("PatientBay").transform;
        var source = GameObject.Find("MPFB patient").GetComponentsInChildren<SkinnedMeshRenderer>().Single(r => r.name == "PatientGown");
        var data = Newtonsoft.Json.JsonConvert.DeserializeObject<Fabric>(File.ReadAllText("../build/mpfb/output/draped-gown.json"));
        if (!source.bones.Select(b => b.name).SequenceEqual(data.bones)) throw new InvalidOperationException("Cloth bone order changed.");
        var gown = Import("draped-gown.json", "Draped patient gown");
        gown.bindposes = source.bones.Select(b => b.worldToLocalMatrix).ToArray();
        gown = SaveMesh(gown, "patient_gown_draped");
        var garment = ObjectFor("Draped patient gown", bay);
        var renderer = Component<SkinnedMeshRenderer>(garment);
        renderer.sharedMesh = gown;
        renderer.sharedMaterials = source.sharedMaterials;
        renderer.bones = source.bones;
        renderer.rootBone = source.rootBone;
        renderer.localBounds = gown.bounds;
        renderer.updateWhenOffscreen = true;
        renderer.forceMatrixRecalculationPerRender = true;
        source.enabled = false;
        PrefabUtility.RecordPrefabInstancePropertyModifications(source);
        var clothMat = renderer.sharedMaterial;
        clothMat.SetFloat("_Cull", 0);
        clothMat.SetFloat("_Smoothness", .05f);
        EditorUtility.SetDirty(clothMat);

        var blanket = SaveMesh(Import("draped-blanket.json", "Draped bed blanket"), "patient_blanket_draped");
        var cover = ObjectFor("Draped bed blanket", bay);
        Component<MeshFilter>(cover).sharedMesh = blanket;
        var linen = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/Materials/BedLinen.mat");
        Component<MeshRenderer>(cover).sharedMaterial = linen;
        bay.Find("patient_blanket").gameObject.SetActive(false);

        var mattress = SaveMesh(Import("soft-mattress.json", "Soft mattress"), "patient_mattress");
        var mattressObject = ObjectFor("Soft mattress", bay);
        Component<MeshFilter>(mattressObject).sharedMesh = mattress;
        Component<MeshRenderer>(mattressObject).sharedMaterial = linen;
        bay.Find("Bed").GetComponent<Renderer>().enabled = false;
        string frameMaterialPath = Folder + "/Materials/BedFrame.mat";
        var frameMaterial = AssetDatabase.LoadAssetAtPath<Material>(frameMaterialPath);
        if (frameMaterial == null) { frameMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(frameMaterial, frameMaterialPath); }
        frameMaterial.SetColor("_BaseColor", new Color(.22f, .27f, .30f));
        frameMaterial.SetFloat("_Metallic", .35f);
        frameMaterial.SetFloat("_Smoothness", .35f);
        EditorUtility.SetDirty(frameMaterial);
        if (bay.Find("Bed frame deck") == null)
        {
            var deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
            deck.name = "Bed frame deck";
            deck.transform.SetParent(bay, true);
            deck.transform.position = new Vector3(0, .51f, 1);
            deck.transform.localScale = new Vector3(.94f, .10f, 1.98f);
            deck.GetComponent<Renderer>().sharedMaterial = frameMaterial;
            UnityEngine.Object.DestroyImmediate(deck.GetComponent<Collider>());
            foreach (float x in new[] { -.35f, .35f }) foreach (float z in new[] { .3f, 1.7f })
            {
                var leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = "Bed frame support";
                leg.transform.SetParent(deck.transform, true);
                leg.transform.position = new Vector3(x, .28f, z);
                leg.transform.localScale = Vector3.one;
                leg.transform.localScale = new Vector3(.06f / deck.transform.lossyScale.x, .40f / deck.transform.lossyScale.y, .06f / deck.transform.lossyScale.z);
                leg.GetComponent<Renderer>().sharedMaterial = frameMaterial;
                UnityEngine.Object.DestroyImmediate(leg.GetComponent<Collider>());
            }
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return new { gownTriangles = gown.triangles.Length / 3, blanketTriangles = blanket.triangles.Length / 3, rigged = renderer.bones.Length, gownBounds = gown.bounds.ToString(), blanketBounds = blanket.bounds.ToString() };
    }
}
