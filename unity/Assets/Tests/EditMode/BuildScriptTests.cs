using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.EditorTools;
using ProcSense.VR.Boot;

namespace ProcSense.Tests.EditMode
{
    /// <summary>The build variants (p0-vr-platform-s07): Dev, Demo and Study each with its define and APK, one variant's define never left in another's build, and the APK's SHA-256 written next to it.</summary>
    public class BuildScriptTests
    {
        [Test]
        public void There_are_the_three_product_variants_and_two_diagnostic_ones_each_with_its_define_and_apk()
        {
            CollectionAssert.AreEqual(new[] { "dev", "demo", "study", "diag", "diag-gpuskin" }, BuildScript.Variants.Select(v => v.Name).ToList());
            Assert.AreEqual(("PROCSENSE_DEMO", "ProcSense-demo.apk", false), (BuildScript.Variant("Demo").Define, BuildScript.Variant("demo").Apk, BuildScript.Variant("demo").Development));
            Assert.IsTrue(BuildScript.Variant("dev").Development, "only Dev is a development build");
            Assert.Throws<System.ArgumentException>(() => BuildScript.Variant("beta"));
        }

        [Test]
        public void A_variant_replaces_any_other_variants_define_and_keeps_the_projects_own()
        {
            var defines = BuildScript.DefinesFor(BuildScript.Variant("study"), new[] { "UNITY_POST_PROCESSING", "PROCSENSE_DEMO" });
            CollectionAssert.AreEquivalent(new[] { "UNITY_POST_PROCESSING", "PROCSENSE_STUDY" }, defines);
        }

        [Test]
        public void Only_the_diagnostic_variants_carry_the_diagnostic_define_and_only_one_changes_skinning()
        {
            foreach (var name in new[] { "dev", "demo", "study" })
            {
                var defines = BuildScript.DefinesFor(BuildScript.Variant(name), new[] { "PROCSENSE_PERF_DIAG", "PROCSENSE_PERF_GPUSKIN" });
                CollectionAssert.DoesNotContain(defines, "PROCSENSE_PERF_DIAG", name + " must never contain the diagnostic readout");
                Assert.IsFalse(BuildScript.Variant(name).Diagnostic || BuildScript.Variant(name).GpuSkinning, name);
            }
            CollectionAssert.AreEquivalent(new[] { "PROCSENSE_DEMO", "PROCSENSE_PERF_DIAG" }, BuildScript.DefinesFor(BuildScript.Variant("diag"), new string[0]));
            Assert.IsFalse(BuildScript.Variant("diag").GpuSkinning, "the baseline diagnostic build keeps the production CPU skinning");
            Assert.IsTrue(BuildScript.Variant("diag-gpuskin").GpuSkinning);
            Assert.IsFalse(BuildScript.Variant("diag").Development, "measurements need a release build");
        }

        [Test]
        public void The_checksum_is_written_next_to_the_apk()
        {
            string apk = Path.Combine(Path.GetTempPath(), "procsense-checksum-test.apk");
            File.WriteAllText(apk, "abc");
            try
            {
                string hex = BuildScript.Checksum(apk);
                Assert.AreEqual("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hex, "SHA-256 of 'abc'");
                StringAssert.StartsWith(hex + "  procsense-checksum-test.apk", File.ReadAllText(apk + ".sha256"));
            }
            finally { File.Delete(apk); File.Delete(apk + ".sha256"); }
        }

        [Test]
        public void The_editor_is_the_dev_variant()
        {
            Assert.AreEqual(BuildVariant.Dev, BuildVariant.Name);
            Assert.IsFalse(BuildVariant.IsRelease);
        }
    }
}
