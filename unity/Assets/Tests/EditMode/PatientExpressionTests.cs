using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.VR.Views;
using UnityEditor;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>The patient has a face for every behaviour mode Core can report, and every shape the table names exists on the real patient model (p0-characters-s03).</summary>
    public class PatientExpressionTests
    {
        private static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        [Test]
        public void Every_behaviour_mode_in_the_content_has_a_face()
        {
            var text = File.ReadAllText(Path.Combine(Repo, "content", "behaviour", "modes.json"));
            var modes = System.Text.RegularExpressions.Regex.Matches(text, @"""mode"":\s*""([A-Z_]+)""").Cast<System.Text.RegularExpressions.Match>().Select(m => m.Groups[1].Value).Distinct().ToList();
            Assert.That(modes, Is.Not.Empty);
            foreach (var mode in modes) Assert.That(PatientExpression.For(mode), Is.Not.Empty, mode + " has no face");
        }

        [Test]
        public void An_empty_or_unknown_mode_is_a_calm_face()
        {
            Assert.IsEmpty(PatientExpression.For(null));
            Assert.IsEmpty(PatientExpression.For(""));
            Assert.IsEmpty(PatientExpression.For("NOT_A_MODE"));
            Assert.IsNotEmpty(PatientExpression.For("in_pain"), "the mode name is not case sensitive");
        }

        [Test]
        public void Every_weight_is_a_fraction_and_every_shape_exists_on_the_patient_model()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/RB_Medical_Male_04.prefab");
            Assert.IsNotNull(prefab, "the Rocketbox patient prefab");
            var names = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.sharedMesh != null)
                .SelectMany(r => Enumerable.Range(0, r.sharedMesh.blendShapeCount).Select(r.sharedMesh.GetBlendShapeName)).ToList();
            foreach (var mode in PatientExpression.Modes)
                foreach (var kv in PatientExpression.For(mode).Where(k => k.Key != PatientExpression.Droop))
                {
                    Assert.That(kv.Value, Is.InRange(0f, 1f), mode + " " + kv.Key);
                    Assert.IsTrue(names.Any(n => n.EndsWith(kv.Key)), mode + ": the patient model has no shape " + kv.Key);
                }
        }
    }
}
