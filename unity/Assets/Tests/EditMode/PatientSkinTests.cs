using NUnit.Framework;
using ProcSense.VR.Views;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>Skin signs tint the skin the right way, blend smoothly, and leave the skin alone when there are none (p0-characters-s03).</summary>
    public class PatientSkinTests
    {
        [Test]
        public void No_signs_leave_the_skin_untouched()
        {
            Assert.AreEqual(Color.white, PatientSkin.Tint(0, 0, 0, 0));
            Assert.AreEqual(.3f, PatientSkin.Gloss(.3f, 0), 1e-6f);
        }

        [Test]
        public void Each_sign_moves_the_colour_its_own_way()
        {
            var pale = PatientSkin.Tint(1, 0, 0, 0); Assert.Less(pale.r, pale.b, "pallor takes the red out");
            var flush = PatientSkin.Tint(0, 1, 0, 0); Assert.Greater(flush.r, flush.g, "flush is red");
            var jaundice = PatientSkin.Tint(0, 0, 1, 0); Assert.Greater(jaundice.g, jaundice.b, "jaundice is yellow");
            var blue = PatientSkin.Tint(0, 0, 0, 1); Assert.Greater(blue.b, blue.r, "cyanosis is blue");
            Assert.Greater(PatientSkin.Gloss(.3f, 1), PatientSkin.Gloss(.3f, 0), "sweat shines");
            Assert.LessOrEqual(PatientSkin.Gloss(.9f, 1), 1f);
        }

        [Test]
        public void A_sign_blends_in_over_the_blend_time_and_back_out()
        {
            var go = new GameObject("skin"); var skin = go.AddComponent<PatientSkin>(); skin.blendSeconds = 1f;
            try
            {
                skin.SetSigns(1, 0, 0, 0, 0);
                skin.Step(.5f); Assert.AreEqual(.5f, skin.Current(0), 1e-5f);
                skin.Step(1f); Assert.AreEqual(1f, skin.Current(0), 1e-5f);
                skin.SetSigns(0, 0, 0, 0, 0); skin.Step(2f); Assert.AreEqual(0f, skin.Current(0), 1e-5f);
                skin.SetSigns(5, -1, 0, 0, 0); Assert.AreEqual(1f, skin.pallor); Assert.AreEqual(0f, skin.flush, "signs are clamped to 0..1");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
