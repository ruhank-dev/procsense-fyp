using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// 3 Nov 2026 (owner: replace every generated character with professionally made, clothed, rigged ones): places the Microsoft Rocketbox avatars (MIT) built by <see cref="RocketboxSetup"/> in the corridor (seated on the
    /// perforated benches, standing talkers and a reader) and the operating theatre (two conversing medical staff), driven by Quaternius UAL (CC0) clips retargeted through Unity Humanoid. Idempotent.
    /// </summary>
    public static class RocketboxPeople
    {
        const string Root = "Corridor people", Staff = "Theatre staff";

        static GameObject Spawn(string avatar, string clip, Transform parent, Vector3 pos, Quaternion rot)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{RocketboxSetup.Prefabs}/RB_{avatar}.prefab"); if (src == null) { Debug.LogWarning("[RocketboxPeople] missing " + avatar); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src, parent); go.transform.SetPositionAndRotation(pos, rot); go.name = "Person_" + avatar;
            var a = go.GetComponent<Animator>(); a.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>($"{RocketboxSetup.Ctrl}/RB_{clip}.controller");
            var legacy = go.GetComponent<ProcSense.VR.Views.StaffIdleView>(); if (legacy != null) Object.DestroyImmediate(legacy);
            return go;
        }

        public static void Apply()
        {
            var corridor = EditorSceneManager.OpenScene("Assets/Scenes/Corridor.unity", OpenSceneMode.Single);
            foreach (var n in new[] { Root, "Corridor people RB" }) { var o = corridor.GetRootGameObjects().FirstOrDefault(g => g.name == n); if (o != null) Object.DestroyImmediate(o); }
            var root = new GameObject("Corridor people RB"); SceneManager.MoveGameObjectToScene(root, corridor);

            var benches = corridor.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Where(t => t.name == "perforated_bench" && PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)).OrderBy(t => t.position.z).ThenBy(t => t.position.x).ToList();
            var plan = new[] { new[] { "Male_Adult_16", "", "Female_Adult_14" }, new[] { "Male_Child_01", "Female_Adult_05", "" }, new[] { "", "Female_Adult_08", "" } };
            int seated = 0;
            for (int b = 0; b < benches.Count; b++)
            {
                var row = b < plan.Length ? plan[b] : new[] { "", "", "" };
                for (int s = 0; s < 3; s++)
                {
                    var ag = benches[b].Find("Seat " + s);
                    if (ag != null) for (int k = ag.childCount - 1; k >= 0; k--) Object.DestroyImmediate(ag.GetChild(k).gameObject);        // earlier occupants (generated staff-pipeline people) are parented to the anchors
                    if (ag == null)
                    {
                        // the model wrapper is turned 180 degrees: seat x in the bench frame is -0.535, 0, 0.535 m, a sitter faces opposite the bench front
                        var g = new GameObject("Seat " + s); g.transform.SetParent(benches[b], false); g.transform.localPosition = new Vector3(-(-.535f + s * .535f), 0f, 0f); g.transform.localRotation = Quaternion.Euler(0, 180, 0);
                        g.AddComponent<ProcSense.VR.Views.SeatAnchor>().SeatHeight = .465f; ag = g.transform;
                    }
                    if (string.IsNullOrEmpty(row[s])) continue;
                    var p = Spawn(row[s], s == 1 && b == 1 ? "Sitting_Talking_Loop" : "Sitting_Idle_Loop", root.transform, ag.position, ag.rotation); if (p == null) continue;
                    p.transform.SetParent(ag, true); var sp = p.AddComponent<ProcSense.VR.Views.SeatedPerson>(); sp.SeatHeight = .465f; sp.Align(((AnimatorController)p.GetComponent<Animator>().runtimeAnimatorController).layers[0].stateMachine.defaultState.motion as AnimationClip); seated++;
                }
            }
            int standing = 0;
            var board = corridor.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(false)).FirstOrDefault(r => r.name.StartsWith("EQ_notice_board"));
            if (board != null)
            {
                var bp = board.bounds.center; var inward = bp.x < 4.5f ? Vector3.right : Vector3.left;
                if (Spawn("Male_Adult_13", "Idle_Loop", root.transform, new Vector3(bp.x, 0, bp.z) + inward * 1.0f, Quaternion.LookRotation(-inward)) != null) standing++;
            }
            var c = new Vector3(3.55f, 0, 7.2f); var d = c + new Vector3(1.05f, 0, .05f);
            if (!Physics.CheckCapsule(c + Vector3.up * .3f, c + Vector3.up * 1.6f, .4f) && !Physics.CheckCapsule(d + Vector3.up * .3f, d + Vector3.up * 1.6f, .4f))
            { Spawn("Female_Adult_15", "Idle_Talking_Loop", root.transform, c, Quaternion.LookRotation(d - c)); Spawn("Male_Adult_03", "Idle_Talking_Loop", root.transform, d, Quaternion.LookRotation(c - d)); standing += 2; }
            EditorSceneManager.MarkSceneDirty(corridor); EditorSceneManager.SaveScene(corridor);
            foreach (var t in root.GetComponentsInChildren<Animator>()) Debug.Log($"[RocketboxPeople] at {t.name} {t.transform.position:F2}");
            Debug.Log($"[RocketboxPeople] corridor: {seated} seated, {standing} standing, {benches.Count} benches");

            var ot = EditorSceneManager.OpenScene("Assets/Scenes/OT.unity", OpenSceneMode.Single);
            foreach (var n in new[] { Staff, "Theatre staff RB" }) { var o = ot.GetRootGameObjects().FirstOrDefault(g => g.name == n); if (o != null) Object.DestroyImmediate(o); }
            var tr = new GameObject("Theatre staff RB"); SceneManager.MoveGameObjectToScene(tr, ot);
            var f = new Vector3(8.55f, 0, 7.15f); var m = new Vector3(9.55f, 0, 7.15f);
            Spawn("Medical_Female_02", "Idle_Talking_Loop", tr.transform, f, Quaternion.LookRotation(m - f));
            Spawn("Medical_Male_03", "Idle_Talking_Loop", tr.transform, m, Quaternion.LookRotation(f - m));
            EditorSceneManager.MarkSceneDirty(ot); EditorSceneManager.SaveScene(ot);
            Debug.Log("[RocketboxPeople] theatre staff placed");
        }
    }
}
