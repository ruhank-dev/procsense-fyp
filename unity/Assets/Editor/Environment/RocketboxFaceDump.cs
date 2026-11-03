using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ProcSense.EditorTools
{
    /// <summary>Read-only: what the Rocketbox patient can move: humanoid eye/jaw bones, facial bones and blend shapes.</summary>
    public static class RocketboxFaceDump
    {
        public static void Run()
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/RB_Medical_Male_04.prefab"));
            var a = go.GetComponent<Animator>();
            foreach (var b in new[] { HumanBodyBones.LeftEye, HumanBodyBones.RightEye, HumanBodyBones.Jaw, HumanBodyBones.Neck, HumanBodyBones.Head, HumanBodyBones.LeftHand, HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftThumbProximal })
                Debug.Log($"[RBFace] humanoid {b} = {(a.GetBoneTransform(b) ? a.GetBoneTransform(b).name : "NONE")}");
            var names = go.GetComponentsInChildren<Transform>(true).Select(t => t.name).Where(n => System.Text.RegularExpressions.Regex.IsMatch(n, "(?i)eye|lid|brow|jaw|mouth|lip|cheek|tongue|nose|face")).ToList();
            Debug.Log($"[RBFace] facial-ish bones ({names.Count}): {string.Join(", ", names)}");
            foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) Debug.Log($"[RBFace] mesh {r.name} blendShapes={r.sharedMesh.blendShapeCount} bones={r.bones.Length}");
            Debug.Log($"[RBFace] total transforms {go.GetComponentsInChildren<Transform>(true).Length}");
            Object.DestroyImmediate(go);
        }
    }
}
