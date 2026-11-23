using System.Collections.Generic;
using System.Linq;
using ProcSense.VR.Views;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// P3-AST-02: lays one clothed Rocketbox patient per profile on the bed in the patient bay, posed exactly like the existing male patient (the same lying pose and arm and leg placement as PatientBayPatient), with the same motion, skin and
    /// reaction components, and wires <see cref="PatientProfiles"/> so the one that fits the case is shown (P3-AST-03). The existing "Rocketbox patient" stays the male adult. Idempotent: running it again rebuilds the variants.
    /// Menu: ProcSense > Characters > Build patient profiles. Batch: -executeMethod ProcSense.EditorTools.PatientProfileBuilder.Build (Unity closed).
    /// </summary>
    public static class PatientProfileBuilder
    {
        /// <summary>(profile key, Rocketbox prefab) for every profile except the male adult, which is the patient already in the scene.</summary>
        public static readonly (string key, string prefab)[] Variants =
        {
            ("female_young", "RB_Female_Adult_08"), ("female_adult", "RB_Female_Adult_15"), ("female_older", "RB_Female_Adult_14"), ("male_older", "RB_Male_Adult_03"),
        };

        private static readonly string[] MountNames = { "Thermometer ear contact", "Patient wristband", "spo2 patient socket", "nibp_cuff patient socket" };

        [MenuItem("ProcSense/Characters/Build patient profiles")]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene(); if (scene.path != "Assets/Scenes/PatientBay.unity") scene = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Single);
            var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var baseRoot = all.FirstOrDefault(t => t.name == "Rocketbox patient"); if (baseRoot == null) { Debug.LogError("[PatientProfiles] no 'Rocketbox patient' in the bay"); return; }
            foreach (var old in all.Where(t => t.name.StartsWith("Rocketbox patient (")).ToList()) Object.DestroyImmediate(old.gameObject);

            // the mounted things go back on the base body first, so a rebuild starts from one known state
            var boneOf = new Dictionary<string, string>();
            foreach (var n in MountNames)
            {
                var t = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(x => x.name == n);
                if (t == null) { Debug.LogError("[PatientProfiles] mount '" + n + "' not found"); return; }
                if (!t.IsChildOf(baseRoot)) { var b = baseRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == t.parent.name); if (b != null) t.SetParent(b, false); }
                boneOf[n] = t.parent.name;
            }

            var mattress = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(r => r.gameObject.activeInHierarchy && (r.name.ToLower().Contains("mattress") || (r.sharedMaterial != null && r.sharedMaterial.name.ToLower().Contains("mattress")))).ToList();
            if (mattress.Count == 0) { Debug.LogError("[PatientProfiles] no mattress found"); return; }
            var mb = mattress[0].bounds; foreach (var m in mattress.Skip(1)) mb.Encapsulate(m.bounds);

            var made = new List<PatientProfiles.Profile> { new PatientProfiles.Profile { key = PatientProfiles.Default, root = baseRoot } };
            foreach (var (key, prefabName) in Variants)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{RocketboxSetup.Prefabs}/{prefabName}.prefab");
                if (prefab == null) { Debug.LogError("[PatientProfiles] prefab missing: " + prefabName); continue; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, baseRoot.parent); go.name = "Rocketbox patient (" + key.Replace('_', ' ') + ")";
                LieOnBed(go, mb);
                foreach (var t in new[] { typeof(PatientMotion), typeof(PatientSkin), typeof(PatientReactions) })
                {
                    var src = baseRoot.GetComponent(t); if (src == null) continue;
                    if (go.GetComponent(t) == null) { UnityEditorInternal.ComponentUtility.CopyComponent(src); UnityEditorInternal.ComponentUtility.PasteComponentAsNew(go); }
                }
                go.SetActive(false);
                made.Add(new PatientProfiles.Profile { key = key, root = go.transform });
                Debug.Log($"[PatientProfiles] built {go.name} from {prefabName}");
            }

            var host = Object.FindObjectsByType<PatientProfiles>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault()?.gameObject ?? new GameObject("Patient profiles");
            var profiles = host.GetComponent<PatientProfiles>() ?? host.AddComponent<PatientProfiles>();
            profiles.profiles = made;
            profiles.mounts = MountNames.Select(n => new PatientProfiles.Mount { item = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(x => x.name == n), bone = boneOf[n] }).ToList();
            profiles.transport = Object.FindAnyObjectByType<PatientTransportView>(FindObjectsInactive.Include);
            EditorUtility.SetDirty(profiles);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log($"[PatientProfiles] {made.Count} profiles, {profiles.mounts.Count} mounts, transport view {(profiles.transport != null ? "found" : "MISSING")}");
        }

        /// <summary>The lying pose of PatientBayPatient: face up, head toward the headwall, hips on the mattress, legs together, arms along the sides; the Animator is removed so the sampled bones stay as posed.</summary>
        private static void LieOnBed(GameObject go, Bounds mb)
        {
            var a = go.GetComponent<Animator>(); var clip = AssetDatabase.LoadAssetAtPath<AnimatorController>($"{RocketboxSetup.Ctrl}/RB_Idle_Loop.controller").layers[0].stateMachine.defaultState.motion as AnimationClip;
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); clip.SampleAnimation(go, 0f);
            Transform B(HumanBodyBones b) => a.GetBoneTransform(b);
            var hips = B(HumanBodyBones.Hips).position; var up = (B(HumanBodyBones.Head).position - hips).normalized; var right = (B(HumanBodyBones.RightUpperLeg).position - B(HumanBodyBones.LeftUpperLeg).position).normalized;
            var face = Vector3.Cross(right, up).normalized;
            go.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward) * Quaternion.Inverse(Quaternion.LookRotation(face, up)) * go.transform.rotation;
            float cx = mb.center.x, top = mb.max.y;
            var hn = B(HumanBodyBones.Hips).position; float reach = (B(HumanBodyBones.LeftFoot).position - hn).magnitude;
            go.transform.position += new Vector3(cx - hn.x, (top + .025f) - hn.y, (mb.min.z + .10f + reach) - hn.z);
            foreach (var (legBone, ft) in new[] { (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftFoot), (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightFoot) })
            {
                var ub = B(legBone); var fb = B(ft); var hipsNow = B(HumanBodyBones.Hips).position; float len = (fb.position - ub.position).magnitude; float side = fb.position.x >= hipsNow.x ? 1f : -1f;
                var target = new Vector3(cx + side * .10f, top + .045f, hipsNow.z - len * .98f);
                ub.rotation = Quaternion.FromToRotation(fb.position - ub.position, target - ub.position) * ub.rotation;
            }
            foreach (var (upper, lower, hand) in new[] { (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand), (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand) })
            {
                var us = B(upper); var el = B(lower); var ha = B(hand); float lenU = (el.position - us.position).magnitude, lenF = (ha.position - el.position).magnitude; float side = us.position.x >= cx ? 1f : -1f;
                var tEl = new Vector3(cx + side * .30f, top + .04f, us.position.z - lenU * .96f); us.rotation = Quaternion.FromToRotation(el.position - us.position, tEl - us.position) * us.rotation;
                var tHa = new Vector3(cx + side * .27f, top + .04f, el.position.z - lenF * .98f); el.rotation = Quaternion.FromToRotation(ha.position - el.position, tHa - el.position) * el.rotation;
            }
            Object.DestroyImmediate(a);
            foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) r.updateWhenOffscreen = true;
        }
    }
}
