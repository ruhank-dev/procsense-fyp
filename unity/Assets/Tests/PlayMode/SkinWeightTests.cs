using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// Quest 2 skins with two bones per vertex (QualitySettings.skinWeights = TwoBones on the Medium level). Unity then reads only the first two weight entries of a vertex, so a mesh whose weight sits in entries
    /// three and four skins to NaN and logs "First two bone weights should have non-zero sum!". The curtain and gown rig builders used to write unsorted weights (3,267 + 426 vertices); this guards the fixed meshes.
    /// </summary>
    public sealed class SkinWeightTests
    {
        [UnityTest, Timeout(300000)]
        public IEnumerator No_skinned_mesh_skins_to_NaN_at_the_Quest_two_bone_quality()
        {
            int asserts = 0; void Count(string c, string s, LogType t) { if (c.Contains("First two bone weights")) asserts++; }
            Application.logMessageReceived += Count;
            int previous = QualitySettings.GetQualityLevel();
            try
            {
                int two = Enumerable.Range(0, QualitySettings.names.Length).First(i => { QualitySettings.SetQualityLevel(i, true); return QualitySettings.skinWeights == SkinWeights.TwoBones; });
                QualitySettings.SetQualityLevel(two, true);
                foreach (var scene in new[] { "Sys_Core", "PatientBay", "Corridor", "OT" }) { yield return SceneManager.LoadSceneAsync(scene, LoadSceneMode.Additive); for (int i = 0; i < 3; i++) yield return null; }
                var bad = new System.Collections.Generic.List<string>(); var seen = new System.Collections.Generic.HashSet<Mesh>();
                foreach (var r in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (r.sharedMesh == null || r.bones.Length == 0 || !seen.Add(r.sharedMesh)) continue;
                    var baked = new Mesh(); r.BakeMesh(baked);
                    int nan = baked.vertices.Count(v => float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z));
                    if (nan > 0) bad.Add($"{r.sharedMesh.name}: {nan} vertices skin to NaN");
                    Object.Destroy(baked);
                }
                Assert.IsEmpty(bad, string.Join("; ", bad));
                Assert.AreEqual(0, asserts, "Unity logged 'First two bone weights should have non-zero sum!'");
            }
            finally { Application.logMessageReceived -= Count; QualitySettings.SetQualityLevel(previous, true); }
        }
    }
}
