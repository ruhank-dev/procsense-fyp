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
            var materials = GownMaterials();
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
                var sk = go.AddComponent<SkinnedMeshRenderer>(); sk.sharedMesh = mesh; sk.bones = body.bones; sk.rootBone = body.rootBone; sk.sharedMaterials = materials;
                sk.updateWhenOffscreen = true; sk.shadowCastingMode = ShadowCastingMode.Off; sk.receiveShadows = false;
                Debug.Log($"[PatientGowns] {root.name}: gown with {mesh.vertexCount} vertices over {body.name}");
                EditorUtility.SetDirty(root);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Two cloth materials, each with its own tileable woven texture (a twill weave, a fine rib and a soft mottle, all periodic over 64 pixels so it repeats without a seam).</summary>
        private static Material[] GownMaterials()
        {
            return new[] { Cloth("scrubs_top", new Color(.46f, .74f, .92f), "PatientScrubsTop", 3), Cloth("scrubs_trousers", new Color(.26f, .48f, .74f), "PatientScrubsTrousers", 7) };
        }

        private static Material Cloth(string texName, Color baseColour, string matName, int phase)
        {
            string tex = Folder + "/" + texName + ".png", mat = "Assets/Materials/Characters/" + matName + ".mat";
            AssetDatabase.DeleteAsset(tex);
            var t = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float twill = ((x + y) % 8 < 4) ? .035f : 0f;                                   // diagonal weave, period 8
                    float rib = (x % 16 < 2) ? -.03f : 0f;                                          // a fine vertical rib every 16 pixels
                    float thread = (y % 2 == 0 ? .012f : -.008f);                                    // the horizontal threads
                    float mottle = .028f * Mathf.Sin(2 * Mathf.PI * (2 * x + phase) / 64f) * Mathf.Sin(2 * Mathf.PI * (3 * y + phase * 2) / 64f) + .018f * Mathf.Sin(2 * Mathf.PI * (5 * x + 3 * y) / 64f);   // soft cloudy variation
                    float k = twill + rib + thread + mottle;
                    t.SetPixel(x, y, new Color(Mathf.Clamp01(baseColour.r + k), Mathf.Clamp01(baseColour.g + k), Mathf.Clamp01(baseColour.b + k), 1));
                }
            System.IO.File.WriteAllBytes(tex, t.EncodeToPNG()); Object.DestroyImmediate(t); AssetDatabase.ImportAsset(tex);
            var imp = (TextureImporter)AssetImporter.GetAtPath(tex); imp.wrapMode = TextureWrapMode.Repeat; imp.mipmapEnabled = true; imp.filterMode = FilterMode.Bilinear; imp.SaveAndReimport();
            if (!AssetDatabase.IsValidFolder("Assets/Materials/Characters")) AssetDatabase.CreateFolder("Assets/Materials", "Characters");
            var m = AssetDatabase.LoadAssetAtPath<Material>(mat);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, mat); }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex)); m.SetColor("_BaseColor", Color.white); m.SetFloat("_Smoothness", .12f);
            EditorUtility.SetDirty(m); return m;
        }

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
            var map = new Dictionary<int, int>(); var nv = new List<Vector3>(); var nn = new List<Vector3>(); var nw = new List<BoneWeight>(); var uv = new List<Vector2>(); var nt = new List<int>(); var nt2 = new List<int>();
            for (int i = 0; i < tris.Count; i += 3)
            {
                var list = (verts[tris[i]].y + verts[tris[i + 1]].y + verts[tris[i + 2]].y) / 3f >= .93f ? nt : nt2;   // a whole triangle goes to the shirt (waist and above) or the trousers
                for (int c = 0; c < 3; c++)
                {
                    int t = tris[i + c];
                    if (!map.TryGetValue(t, out int k))
                    {
                        k = nv.Count; map[t] = k; nv.Add(verts[t] + normals[t].normalized * Push); nn.Add(normals[t]); nw.Add(weights[t]);
                        uv.Add(new Vector2(verts[t].x * 7f + verts[t].z * 3f, verts[t].y * 7f));   // about 14 cm per repeat of the weave pattern
                    }
                    list.Add(k);
                }
            }
            var mesh = new Mesh { name = "Gown " + label, indexFormat = nv.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(nv); mesh.SetNormals(nn); mesh.SetUVs(0, uv); mesh.boneWeights = nw.ToArray(); mesh.bindposes = src.bindposes; mesh.subMeshCount = 2; mesh.SetTriangles(nt, 0); mesh.SetTriangles(nt2, 1); mesh.RecalculateBounds();
            var path = $"{Folder}/Gown {label}.asset"; AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}
