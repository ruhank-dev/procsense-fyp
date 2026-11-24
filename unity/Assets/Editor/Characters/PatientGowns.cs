using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Dresses every lying patient in the same pale-blue hospital gown. The gown is a second skinned mesh made from the avatar's own clothed body (the first submesh): the triangles that belong to the trunk, arms down to the wrist,
    /// legs and feet, pushed out 8 mm along their normals and skinned to the same bones, so it breathes and moves with the body. The head, neck and hands stay skin. Idempotent.
    /// Menu ProcSense > Characters > Dress patients in gowns. Batch: -executeMethod ProcSense.EditorTools.PatientGowns.Run (Unity closed).
    /// </summary>
    public static class PatientGowns
    {
        public const string GownName = "Patient gown";
        private const string Folder = "Assets/Characters/PatientGowns";
        private static readonly string[] Cover = { "Pelvis", "Spine", "Clavicle", "UpperArm", "Forearm", "Thigh", "Calf", "Foot", "Toe" };
        private const float Push = .012f;

        [MenuItem("ProcSense/Characters/Dress patients in gowns")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Characters", "PatientGowns");
            var material = GownMaterial();
            foreach (var motion in Object.FindObjectsByType<ProcSense.VR.Views.PatientMotion>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var root = motion.gameObject;
                foreach (var old in root.GetComponentsInChildren<Transform>(true).Where(t => t.name == GownName).ToList()) Object.DestroyImmediate(old.gameObject);
                var body = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.sharedMesh != null);
                if (body == null) continue;
                var original = PrefabUtility.GetCorrespondingObjectFromSource(body) is SkinnedMeshRenderer prefabBody ? prefabBody.sharedMesh : body.sharedMesh;   // always dress from the untouched mesh
                body.sharedMesh = original;
                var mesh = Build(body, root.name); if (mesh == null) continue;
                body.sharedMesh = TakeShoesOff(body, root.name);
                var go = new GameObject(GownName); go.transform.SetParent(body.transform.parent, false);
                go.transform.localPosition = body.transform.localPosition; go.transform.localRotation = body.transform.localRotation; go.transform.localScale = body.transform.localScale;
                var sk = go.AddComponent<SkinnedMeshRenderer>(); sk.sharedMesh = mesh; sk.bones = body.bones; sk.rootBone = body.rootBone; sk.sharedMaterial = material;
                sk.updateWhenOffscreen = true; sk.shadowCastingMode = ShadowCastingMode.Off; sk.receiveShadows = false;
                Debug.Log($"[PatientGowns] {root.name}: gown with {mesh.vertexCount} vertices over {body.name}");
                EditorUtility.SetDirty(root);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static Material GownMaterial()
        {
            const string tex = Folder + "/gown_print.png", mat = "Assets/Materials/Characters/PatientGown.mat";
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(tex) == null)
            {
                var t = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                var baseCol = new Color(.62f, .78f, .86f); var dot = new Color(.50f, .68f, .78f);
                for (int y = 0; y < 64; y++)
                    for (int x = 0; x < 64; x++)
                    {
                        int dx = (x % 16) - 8, dy = (y % 16) - 8, ex = ((x + 8) % 16) - 8, ey = ((y + 8) % 16) - 8;
                        bool d = dx * dx + dy * dy < 5 || ex * ex + ey * ey < 3;
                        float weave = (x + y) % 2 == 0 ? .012f : 0f;
                        t.SetPixel(x, y, (d ? dot : baseCol) - new Color(weave, weave, weave, 0));
                    }
                System.IO.File.WriteAllBytes(tex, t.EncodeToPNG()); Object.DestroyImmediate(t); AssetDatabase.ImportAsset(tex);
                var imp = (TextureImporter)AssetImporter.GetAtPath(tex); imp.wrapMode = TextureWrapMode.Repeat; imp.mipmapEnabled = true; imp.SaveAndReimport();
            }
            if (!AssetDatabase.IsValidFolder("Assets/Materials/Characters")) AssetDatabase.CreateFolder("Assets/Materials", "Characters");
            var m = AssetDatabase.LoadAssetAtPath<Material>(mat);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, mat); }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex)); m.SetColor("_BaseColor", Color.white); m.SetFloat("_Smoothness", .12f);
            EditorUtility.SetDirty(m); return m;
        }

        /// <summary>A copy of the body mesh without the triangles of the shoes (the foot and toe bones), so no sole pokes out under the gown. The prefab's own mesh is not touched.</summary>
        private static Mesh TakeShoesOff(SkinnedMeshRenderer body, string label)
        {
            var src = body.sharedMesh; var bones = body.bones; var w = src.boneWeights; var copy = Object.Instantiate(src); copy.name = "Body " + label;
            System.Func<int, bool> foot = i => { var b = w[i]; return (b.weight0 >= .5f && IsFoot(bones[b.boneIndex0].name)) || (b.weight1 >= .5f && IsFoot(bones[b.boneIndex1].name)); };
            var tr = src.GetTriangles(0); var kept = new List<int>();
            for (int i = 0; i < tr.Length; i += 3) if (!(foot(tr[i]) && foot(tr[i + 1]) && foot(tr[i + 2]))) { kept.Add(tr[i]); kept.Add(tr[i + 1]); kept.Add(tr[i + 2]); }
            copy.SetTriangles(kept, 0);
            var path = $"{Folder}/Body {label}.asset"; AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(copy, path);
            return copy;
        }

        private static bool IsHand(string bone) => bone.Contains("Hand") || bone.Contains("Finger");

        private static bool IsFoot(string bone) => bone.Contains("Foot") || bone.Contains("Toe");

        private static bool IsCover(string bone) => Cover.Any(n => bone.Contains(n)) && !bone.Contains("Hand") && !bone.Contains("Finger");

        private static Mesh Build(SkinnedMeshRenderer body, string label)
        {
            var src = body.sharedMesh; var bones = body.bones; var weights = src.boneWeights; var verts = src.vertices; var normals = src.normals;
            var covered = new bool[verts.Length]; var hand = new bool[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                var w = weights[i]; float c = 0;
                if (w.weight0 > 0 && IsCover(bones[w.boneIndex0].name)) c += w.weight0;
                if (w.weight1 > 0 && IsCover(bones[w.boneIndex1].name)) c += w.weight1;
                if (w.weight2 > 0 && IsCover(bones[w.boneIndex2].name)) c += w.weight2;
                if (w.weight3 > 0 && IsCover(bones[w.boneIndex3].name)) c += w.weight3;
                covered[i] = c > .45f;
                float h = 0;
                if (w.weight0 > 0 && IsHand(bones[w.boneIndex0].name)) h += w.weight0;
                if (w.weight1 > 0 && IsHand(bones[w.boneIndex1].name)) h += w.weight1;
                if (w.weight2 > 0 && IsHand(bones[w.boneIndex2].name)) h += w.weight2;
                if (w.weight3 > 0 && IsHand(bones[w.boneIndex3].name)) h += w.weight3;
                hand[i] = h > .15f; if (hand[i]) covered[i] = false;
            }
            var tr = src.GetTriangles(0); var tris = new List<int>();
            for (int i = 0; i < tr.Length; i += 3) if ((covered[tr[i]] ? 1 : 0) + (covered[tr[i + 1]] ? 1 : 0) + (covered[tr[i + 2]] ? 1 : 0) >= 2 && !hand[tr[i]] && !hand[tr[i + 1]] && !hand[tr[i + 2]])
            {   // two of three: the gown reaches one ring past the bone boundary, over the cuff and the shoe
                tris.Add(tr[i]); tris.Add(tr[i + 1]); tris.Add(tr[i + 2]);
            }
            if (tris.Count == 0) { Debug.LogError("[PatientGowns] nothing to cover on " + label); return null; }
            var map = new Dictionary<int, int>(); var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nw = new List<BoneWeight>(); var uv = new List<Vector2>(); var nt = new List<int>();
            foreach (int t in tris)
            {
                if (!map.TryGetValue(t, out int k))
                {
                    k = nv.Count; map[t] = k; nv.Add(verts[t] + normals[t].normalized * Push); nn.Add(normals[t]); nw.Add(weights[t]);
                    uv.Add(new Vector2(verts[t].x, verts[t].y) * 5.5f + new Vector2(verts[t].z, 0) * .7f);
                }
                nt.Add(k);
            }
            var mesh = new Mesh { name = "Gown " + label, indexFormat = nv.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(nv); mesh.SetNormals(nn); mesh.SetUVs(0, uv); mesh.boneWeights = nw.ToArray(); mesh.bindposes = src.bindposes; mesh.SetTriangles(nt, 0); mesh.RecalculateBounds();
            var path = $"{Folder}/Gown {label}.asset"; AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}
