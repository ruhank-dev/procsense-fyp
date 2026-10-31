using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// Soft contact-shadow decals on the mattress and pillow under the patient (torso, pelvis, each leg, head, each arm): one multiply-blended quad each using the project's existing soft
    /// shadow texture. They ground the body the way game characters are grounded; the dynamic patient has no baked occlusion of its own. Cheap: 10 transparent quads, no lights.
    /// </summary>
    public static class PatientContactShadows
    {
        const string Root = "Patient contact shadows";

        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var old = GameObject.Find(Root); if (old != null) Object.DestroyImmediate(old);
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Hospital/PatientBayLook/PatientBay contact shadow.mat");
            var root = new GameObject(Root).transform;
            float y = .7035f;
            // x, z (metres, bed space), width (x), length (z), darkness
            var spots = new (string n, float x, float z, float w, float l, float a)[]
            {
                ("torso", 0f, 1.08f, .62f, .78f, .62f), ("pelvis", 0f, .72f, .56f, .40f, .6f), ("leg_l", -.13f, .30f, .20f, .78f, .6f), ("leg_r", .13f, .30f, .20f, .78f, .6f),
                ("heel_l", -.13f, .03f, .14f, .16f, .5f), ("heel_r", .13f, .03f, .14f, .16f, .5f), ("arm_l", -.36f, 1.02f, .15f, .62f, .5f), ("arm_r", .36f, 1.02f, .15f, .62f, .5f),
                ("head", 0f, 1.76f, .34f, .36f, .6f),
            };
            foreach (var s in spots)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad); q.name = "contact_" + s.n; Object.DestroyImmediate(q.GetComponent<Collider>());
                q.transform.SetParent(root, false); q.transform.position = new Vector3(s.x, s.n == "head" ? .775f : y, s.z); q.transform.rotation = Quaternion.Euler(90, 0, 0);
                q.transform.localScale = new Vector3(s.w, s.l, 1f);
                var mr = q.GetComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
                var mpb = new MaterialPropertyBlock(); mr.GetPropertyBlock(mpb); mpb.SetColor("_BaseColor", new Color(0f, 0f, 0f, s.a)); mr.SetPropertyBlock(mpb);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log($"[ContactShadows] {spots.Length} decals under the patient");
        }
    }
}
