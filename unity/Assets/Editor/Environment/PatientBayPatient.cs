using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// 3 Nov 2026 (owner: the generated patient's clothes could not be made right, use an existing, professionally made character): lays a clothed Microsoft Rocketbox avatar (MIT, same source as the corridor people) on the patient bed,
    /// posed from the Quaternius UAL idle clip (arms relaxed at the sides) and turned onto its back, head toward the headwall. The generated "MPFB patient" with its gown and shorts is switched off (kept in the scene, reversible).
    /// Also tidies the bay after the window frame removal: the plant that stood on the sill is hidden and the bay plaster is toned down so the sunlit wall stops clipping.
    /// </summary>
    public static class PatientBayPatient
    {
        /// <summary>Fully transparent, shadowless material for the staff avatar's mask and cap submesh (a patient wears neither).</summary>
        static Material Hidden()
        {
            const string hp = "Assets/Characters/Rocketbox/Medical_Male_04/Materials/Hidden part.mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(hp);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, hp); }
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f); m.SetColor("_BaseColor", new Color(1, 1, 1, 0)); m.SetFloat("_ZWrite", 0f); m.SetFloat("_SrcBlend", 5f); m.SetFloat("_DstBlend", 10f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = 3000; m.SetShaderPassEnabled("ShadowCaster", false); EditorUtility.SetDirty(m); return m;
        }

        public static void Apply()
        {
            var sc = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var old = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).FirstOrDefault(t => t.name == "MPFB patient");
            foreach (var n in new[] { "Rocketbox patient" })
            {
                var o = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).FirstOrDefault(t => t.name == n);
                if (o != null && o.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>(true).Length > 0)
                    throw new System.InvalidOperationException("Preserve the fitted patient and physical interaction anchors; edit its pose instead of replacing it.");
                if (o != null) Object.DestroyImmediate(o.gameObject);
            }
            if (old == null) { Debug.LogError("[PatientBayPatient] MPFB patient not found"); return; }
            var oldBody = old.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.name.EndsWith(".body")).Select(r => r.bounds).ToList(); var tb = oldBody[0]; foreach (var b in oldBody.Skip(1)) tb.Encapsulate(b);
            Debug.Log($"[PatientBayPatient] old patient parent={(old.parent ? old.parent.name : "-")} pos={old.position} rot={old.eulerAngles} bounds center={tb.center} size={tb.size}");

            var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{RocketboxSetup.Prefabs}/RB_Medical_Male_04.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src, old.parent); go.name = "Rocketbox patient";
            const string tp = "Assets/Characters/Rocketbox/Medical_Male_04/m162_body_color_patient.png"; AssetDatabase.ImportAsset(tp, ImportAssetOptions.ForceSynchronousImport);
            var gownTex = AssetDatabase.LoadAssetAtPath<Texture2D>(tp); var gm = AssetDatabase.LoadAssetAtPath<Material>("Assets/Characters/Rocketbox/Medical_Male_04/Materials/Patient clothes.mat"); Material patientMat = null;
            foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mats = r.sharedMaterials; for (int i = 0; i < mats.Length; i++)
                {
                    Debug.Log($"[PatientBayPatient] renderer {r.name} slot {i} material {mats[i].name}");
                    if (mats[i].name.ToLower().Contains("body")) { if (patientMat == null) { patientMat = gm ?? new Material(mats[i]); if (gm == null) AssetDatabase.CreateAsset(patientMat, "Assets/Characters/Rocketbox/Medical_Male_04/Materials/Patient clothes.mat"); patientMat.CopyPropertiesFromMaterial(mats[i]); patientMat.SetTexture("_BaseMap", gownTex); patientMat.SetColor("_BaseColor", Color.white); EditorUtility.SetDirty(patientMat); } mats[i] = patientMat; }
                    if (mats[i].name.ToLower().Contains("protection")) mats[i] = Hidden();                                   // surgical mask / cap parts of the staff avatar: a patient wears none
                }
                r.sharedMaterials = mats;
            }
            var a = go.GetComponent<Animator>(); var clip = ((AnimatorController)AssetDatabase.LoadAssetAtPath<AnimatorController>($"{RocketboxSetup.Ctrl}/RB_Idle_Loop.controller")).layers[0].stateMachine.defaultState.motion as AnimationClip;
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); clip.SampleAnimation(go, 0f);
            Transform B(HumanBodyBones b) => a.GetBoneTransform(b);
            var hips = B(HumanBodyBones.Hips).position; var up = (B(HumanBodyBones.Head).position - hips).normalized; var right = (B(HumanBodyBones.RightUpperLeg).position - B(HumanBodyBones.LeftUpperLeg).position).normalized;
            var face = Vector3.Cross(right, up).normalized;                                                               // the way the person looks (Unity is left-handed: right x up = forward)
            var turn = Quaternion.LookRotation(Vector3.up, Vector3.forward) * Quaternion.Inverse(Quaternion.LookRotation(face, up));   // face up, head toward +z (the headwall)
            go.transform.rotation = turn * go.transform.rotation;
            // the bed: centre the body on the mattress, lie it on the surface (hips half a body-thickness above it), feet near the foot end
            var mats2 = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include).Where(r => r.gameObject.activeInHierarchy && (r.name.ToLower().Contains("mattress") || (r.sharedMaterial != null && r.sharedMaterial.name.ToLower().Contains("mattress")))).ToList();
            foreach (var m in mats2) Debug.Log($"[PatientBayPatient] mattress renderer {m.name} mat={m.sharedMaterial?.name} center={m.bounds.center} size={m.bounds.size}");
            var mb = mats2.Count > 0 ? mats2[0].bounds : tb; foreach (var m in mats2.Skip(1)) mb.Encapsulate(m.bounds);
            float cx = mb.center.x, top = mb.max.y;
            var hn = B(HumanBodyBones.Hips).position; float reach = (B(HumanBodyBones.LeftFoot).position - hn).magnitude;
            go.transform.position += new Vector3(cx - hn.x, (top + .025f) - hn.y, (mb.min.z + .10f + reach) - hn.z);
            foreach (var (legBone, ft) in new[] { (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftFoot), (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightFoot) })        // legs together, resting on the mattress
            {
                var ub = B(legBone); var fb2 = B(ft); var hipsNow = B(HumanBodyBones.Hips).position; float len = (fb2.position - ub.position).magnitude; float side = fb2.position.x >= hipsNow.x ? 1f : -1f;
                var target = new Vector3(cx + side * .10f, top + .045f, hipsNow.z - len * .98f);
                ub.rotation = Quaternion.FromToRotation(fb2.position - ub.position, target - ub.position) * ub.rotation;
            }
            foreach (var (upper, lower, hand) in new[] { (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand), (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand) })        // arms along the sides, on the mattress, elbows above it
            {
                var us = B(upper); var el = B(lower); var ha = B(hand); float lenU = (el.position - us.position).magnitude, lenF = (ha.position - el.position).magnitude; float side = us.position.x >= cx ? 1f : -1f;
                var tEl = new Vector3(cx + side * .30f, top + .04f, us.position.z - lenU * .96f); us.rotation = Quaternion.FromToRotation(el.position - us.position, tEl - us.position) * us.rotation;
                var tHa = new Vector3(cx + side * .27f, top + .04f, el.position.z - lenF * .98f); el.rotation = Quaternion.FromToRotation(ha.position - el.position, tHa - el.position) * el.rotation;
            }
            Debug.Log($"[PatientBayPatient] mattress top {top:F3} centre x {cx:F3}; hips {B(HumanBodyBones.Hips).position} head {B(HumanBodyBones.Head).position} feet {B(HumanBodyBones.LeftFoot).position} {B(HumanBodyBones.RightFoot).position} elbows {B(HumanBodyBones.LeftLowerArm).position} {B(HumanBodyBones.RightLowerArm).position}");
            foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (r.sharedMesh.blendShapeCount > 0) Debug.Log("[PatientBayPatient] blendshapes: " + string.Join(", ", Enumerable.Range(0, r.sharedMesh.blendShapeCount).Select(k => r.sharedMesh.GetBlendShapeName(k)).Where(n => System.Text.RegularExpressions.Regex.IsMatch(n, "(?i)blink|eye|lid|close|jaw"))));
            Object.DestroyImmediate(a);
            if (go.GetComponent<ProcSense.VR.Views.PatientMotion>() == null) go.AddComponent<ProcSense.VR.Views.PatientMotion>();            // breathing, blinking, gaze and head drift                                                                                      // static pose: the editor-sampled bones stay as they are
            foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) { r.updateWhenOffscreen = true; }
            old.gameObject.SetActive(true);                                                                                  // the generated rig stays live but invisible: mounted sensors, idle view and the simulation hooks keep working
            foreach (var r in old.GetComponentsInChildren<SkinnedMeshRenderer>(true)) r.enabled = false;
            foreach (var n in new[] { "Draped patient gown", "Patient shorts" }) { var o = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).FirstOrDefault(t => t.name == n); if (o != null) { o.gameObject.SetActive(true); foreach (var r in o.GetComponentsInChildren<SkinnedMeshRenderer>(true)) r.enabled = false; } }
            var vine = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).FirstOrDefault(t => t.name == "ref_vine"); if (vine != null) vine.gameObject.SetActive(false);

            var wall = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include).First(r => r.name == "Wall front").sharedMaterial;
            var c = wall.GetColor("_BaseColor"); Debug.Log($"[PatientBayPatient] wall material {wall.name} base {c}");
            wall.SetColor("_BaseColor", new Color(.8f, .8f, .8f, 1f)); EditorUtility.SetDirty(wall);
            EditorSceneManager.MarkSceneDirty(sc); EditorSceneManager.SaveScene(sc); AssetDatabase.SaveAssets();
            var fb = go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r => r.bounds).ToList(); var f = fb[0]; foreach (var b in fb.Skip(1)) f.Encapsulate(b);
            Debug.Log($"[PatientBayPatient] new patient bounds center={f.center} size={f.size}");
        }
    }
}
