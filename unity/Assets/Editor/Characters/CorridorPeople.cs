using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProcSense.EditorTools
{
    /// <summary>
    /// 3 Nov 2026 (owner: a populated, busy-but-not-crowded corridor): seats waiting patients, a parent with a child and a woman on her phone on the perforated benches (a deliberate mix of occupied and empty seats),
    /// stands a man reading paperwork near the notice board and two visitors in conversation (one of them coughing quietly into his elbow now and then). The two conversing nurses move to the operating theatre.
    /// Every person is a baked-pose staff-pipeline prefab with StaffIdleView life (blinks, breathing, role behaviour); crowd cost drops on Quest through StaffIdleView.UpdateInterval. Idempotent.
    /// </summary>
    public static class CorridorPeople
    {
        const string Root = "Corridor people", Prefabs = "Assets/Prefabs/Characters/Staff_";

        static GameObject Spawn(string key, Transform parent, Vector3 pos, Quaternion rot)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + key + ".prefab"); if (src == null) { Debug.LogWarning("[CorridorPeople] missing prefab " + key); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src, parent); go.transform.SetPositionAndRotation(pos, rot); go.name = "Person_" + key; return go;
        }

        public static void Apply()
        {
            // 1. take the two nurses out of the corridor (they move to the theatre below)
            var corridor = EditorSceneManager.OpenScene("Assets/Scenes/Corridor.unity", OpenSceneMode.Single);
            var nursesPose = new Dictionary<string, (Vector3 pos, Quaternion rot)>();
            foreach (var go in corridor.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Where(t => t != null && (t.name.Contains("Staff_nurse_") || t.name.Contains("nurse_f") || t.name.Contains("nurse_m")) && PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)).Select(t => t.gameObject).ToArray())
            {
                var key = go.name.Contains("nurse_f") ? "nurse_f" : "nurse_m"; nursesPose[key] = (go.transform.position, go.transform.rotation); Debug.Log($"[CorridorPeople] removing {go.name} at {go.transform.position}");
                Object.DestroyImmediate(go);
            }
            var old = corridor.GetRootGameObjects().FirstOrDefault(g => g.name == Root); if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(Root); SceneManager.MoveGameObjectToScene(root, corridor);

            // 2. seated people on the benches
            // only the outermost bench prefab roots (each bench wrapper holds a model child with the same name)
            var benches = corridor.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).Where(t => t.name == "perforated_bench" && PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)).OrderBy(t => t.position.z).ThenBy(t => t.position.x).ToList();
            // a seat anchor for every seat of every bench (floor point under the seat middle, facing the way a sitter faces, seat height 0.465 m); occupants are parented to them
            // bench-local seat x in Blender metres is -0.535, 0, 0.535; the model wrapper is turned 180 degrees, so the Unity local x is the negative
            var plan = new[] { new[] { "patient_m_old", "", "adult_f_phone" }, new[] { "child", "parent_f", "" }, new[] { "", "patient_f_old", "" } };
            int seated = 0;
            for (int b = 0; b < benches.Count; b++)
            {
                var row = b < plan.Length ? plan[b] : new[] { "", "", "" };
                for (int s = 0; s < 3; s++)
                {
                    var old2 = benches[b].Find("Seat " + s); if (old2 != null) Object.DestroyImmediate(old2.gameObject);
                    var ag = new GameObject("Seat " + s); ag.transform.SetParent(benches[b], false);
                    ag.transform.localPosition = new Vector3(-(-.535f + s * .535f), 0f, 0f); ag.transform.localRotation = Quaternion.Euler(0, 180, 0);       // the person model faces +Z, the bench front is -Z
                    var anchor = ag.AddComponent<ProcSense.VR.Views.SeatAnchor>(); anchor.SeatHeight = .465f;
                    if (string.IsNullOrEmpty(row[s])) continue;
                    var person = Spawn(row[s], root.transform, ag.transform.position, ag.transform.rotation); if (person == null) continue;
                    var keep = person.transform.position; person.transform.SetParent(ag.transform, true); person.transform.position = keep + Vector3.up * anchor.Offset; seated++;
                }
            }

            // 3. standing people: one reading at the notice board, two talking
            int standing = 0;
            var board = corridor.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Renderer>(false)).FirstOrDefault(r => r.name.StartsWith("EQ_notice_board"));
            if (board != null)
            {
                var bp = board.bounds.center; bool leftWall = bp.x < 4.5f; var inward = leftWall ? Vector3.right : Vector3.left;
                var spot = new Vector3(bp.x, 0, bp.z) + inward * 1.0f; if (Spawn("visitor_m_paper", root.transform, spot, Quaternion.LookRotation(-inward)) != null) standing++;
            }
            var talkPairs = new[] { new Vector3(3.55f, 0, 7.2f), new Vector3(5.6f, 0, 2.2f), new Vector3(3.4f, 0, -2.0f) };
            foreach (var c in talkPairs)
            {
                var a = c; var bpos = c + new Vector3(1.05f, 0, .05f);
                if (Physics.CheckCapsule(a + Vector3.up * .3f, a + Vector3.up * 1.6f, .4f) || Physics.CheckCapsule(bpos + Vector3.up * .3f, bpos + Vector3.up * 1.6f, .4f)) continue;
                Spawn("visitor_f_talk", root.transform, a, Quaternion.LookRotation(bpos - a));
                Spawn("visitor_m_cough", root.transform, bpos, Quaternion.LookRotation(a - bpos)); standing += 2; break;
            }
            EditorSceneManager.MarkSceneDirty(corridor); EditorSceneManager.SaveScene(corridor);
            Debug.Log($"[CorridorPeople] corridor: {seated} seated, {standing} standing, {benches.Count} benches found");

            // 4. the two nurses, conversing in the operating theatre beside the table
            var ot = EditorSceneManager.OpenScene("Assets/Scenes/OT.unity", OpenSceneMode.Single);
            var oldOt = ot.GetRootGameObjects().FirstOrDefault(g => g.name == "Theatre staff"); if (oldOt != null) Object.DestroyImmediate(oldOt);
            var tr = new GameObject("Theatre staff"); SceneManager.MoveGameObjectToScene(tr, ot);
            var f = new Vector3(8.55f, 0, 7.15f); var m = new Vector3(9.55f, 0, 7.15f);                                      // by the cupboard end of the room, clear of the table and the anaesthesia unit
            Spawn("nurse_f", tr.transform, f, Quaternion.LookRotation(m - f));
            Spawn("nurse_m", tr.transform, m, Quaternion.LookRotation(f - m));
            EditorSceneManager.MarkSceneDirty(ot); EditorSceneManager.SaveScene(ot);
            Debug.Log("[CorridorPeople] theatre staff placed; applied");
        }
    }
}
