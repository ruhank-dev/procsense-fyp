using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>The final-look floor: clean light-grey medical vinyl (replacing the retired wood floor): opaque Lit, subtle satin response (never a mirror), real albedo and normal detail with
    /// mips and anisotropic filtering, geometry and collider untouched, and no other room picking up the PatientBay floor material.</summary>
    public class GreyVinylFloorContractTests
    {
        const string Mat = "Assets/Materials/Hospital/PatientBayLook/PatientBay grey vinyl.mat";

        [Test]
        public void Material_is_opaque_lit_light_grey_with_a_subtle_satin_response()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(Mat); Assert.IsNotNull(m);
            Assert.That(m.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            Assert.That(m.GetFloat("_Surface"), Is.EqualTo(0f), "opaque");
            Assert.That(m.GetFloat("_Metallic"), Is.EqualTo(0f));
            Assert.That(m.GetFloat("_Smoothness"), Is.InRange(.2f, .45f), "satin vinyl, not a mirror");
            var c = m.GetColor("_BaseColor");
            Assert.That(c.r, Is.InRange(.9f, 1.05f), "the albedo texture carries the colour; the tint is neutral");
        }

        [Test]
        public void Textures_carry_fine_detail_with_correct_import_settings()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(Mat);
            var albedo = m.GetTexture("_BaseMap") as Texture2D; var normal = m.GetTexture("_BumpMap") as Texture2D;
            Assert.IsNotNull(albedo); Assert.IsNotNull(normal);
            foreach (var t in new[] { albedo, normal })
            {
                var imp = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(t));
                Assert.IsTrue(imp.mipmapEnabled, t.name + " mips"); Assert.That(imp.anisoLevel, Is.GreaterThanOrEqualTo(4), t.name + " anisotropic filtering");
                Assert.That(imp.maxTextureSize, Is.GreaterThanOrEqualTo(2048), t.name + " is not downsized");
                var android = imp.GetPlatformTextureSettings("Android"); Assert.IsTrue(android.overridden, t.name + " has an ASTC override for Quest");
            }
            Assert.That(((TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal))).textureType, Is.EqualTo(TextureImporterType.NormalMap));
        }

        [Test]
        public void Floor_is_clean_grey_not_wood()
        {
            var albedo = (Texture2D)AssetDatabase.LoadAssetAtPath<Material>(Mat).GetTexture("_BaseMap");
            var path = AssetDatabase.GetAssetPath(albedo);
            var bytes = System.IO.File.ReadAllBytes(path); var tex = new Texture2D(2, 2); tex.LoadImage(bytes);
            var px = tex.GetPixels32(); float r = 0, g = 0, b = 0; foreach (var p in px) { r += p.r; g += p.g; b += p.b; }
            r /= px.Length * 255f; g /= px.Length * 255f; b /= px.Length * 255f;
            Assert.That(r, Is.InRange(.5f, .78f)); Assert.That(b, Is.GreaterThanOrEqualTo(r), "cool grey, never warm wood");
            Assert.That(Mathf.Abs(r - g), Is.LessThan(.06f), "low saturation");
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void Only_PatientBays_floor_changed_and_its_geometry_and_collider_are_untouched()
        {
            var bay = EditorSceneManager.OpenScene("Assets/Scenes/PatientBay.unity", OpenSceneMode.Additive);
            try
            {
                var floor = bay.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)).Single(r => r.name == "Floor");
                Assert.That(floor.sharedMaterial.name, Is.EqualTo("PatientBay grey vinyl"));
                Assert.That(floor.transform.lossyScale, Is.EqualTo(new Vector3(5f, .1f, 4f)));
                Assert.That(floor.transform.position, Is.EqualTo(new Vector3(0f, -.05f, 0f)));
                var box = floor.GetComponent<BoxCollider>(); Assert.That(box, Is.Not.Null); Assert.IsFalse(box.isTrigger);
                Assert.That(GameObjectUtility.GetStaticEditorFlags(floor.gameObject).HasFlag(StaticEditorFlags.ContributeGI), "the floor still bounces baked light");
            }
            finally { EditorSceneManager.CloseScene(bay, true); }
            foreach (var room in new[] { "Corridor", "OT" })
            {
                var scene = EditorSceneManager.OpenScene($"Assets/Scenes/{room}.unity", OpenSceneMode.Additive);
                try
                {
                    var mats = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)).SelectMany(r => r.sharedMaterials).Where(x => x != null).Select(x => x.name);
                    Assert.IsFalse(mats.Any(n => n == "PatientBay grey vinyl"), room + " must not use the PatientBay floor material");
                }
                finally { EditorSceneManager.CloseScene(scene, true); }
            }
        }
    }
}
