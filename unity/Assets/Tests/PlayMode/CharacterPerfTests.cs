using System.Collections.Generic;
using NUnit.Framework;
using ProcSense.VR.Views;
using UnityEngine;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>CharacterPerfCuller must never cull anything visible and must put every original value back when switched off.</summary>
    public sealed class CharacterPerfTests
    {
        private readonly List<Object> _made = new List<Object>();
        private bool _wasEnabled, _wasLock, _wasCull;
        private System.Func<Renderer, bool> _probe;
        private GameObject _scope;
        private CharacterPerfCuller _culler;

        [SetUp]
        public void Setup()
        {
            _wasEnabled = CharacterPerfCuller.Enabled; _wasLock = CharacterPerfCuller.LockSkinnedBounds; _wasCull = CharacterPerfCuller.CullInvisibleAnimators; _probe = CharacterPerfCuller.VisibilityProbe;
            CharacterPerfCuller.Enabled = false; CharacterPerfCuller.LockSkinnedBounds = true; CharacterPerfCuller.CullInvisibleAnimators = true;
            _scope = new GameObject("perf scope"); _made.Add(_scope);
            var host = new GameObject("perf culler"); _made.Add(host); host.SetActive(false);
            _culler = host.AddComponent<CharacterPerfCuller>(); _culler.Scope = _scope.transform; _culler.CullDelaySeconds = 1f;
            host.SetActive(true);
        }

        [TearDown]
        public void Teardown()
        {
            CharacterPerfCuller.Enabled = false;
            foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
            CharacterPerfCuller.Enabled = _wasEnabled; CharacterPerfCuller.LockSkinnedBounds = _wasLock; CharacterPerfCuller.CullInvisibleAnimators = _wasCull; CharacterPerfCuller.VisibilityProbe = _probe;
        }

        private SkinnedMeshRenderer MakeSkinned(out Transform hand)
        {
            var go = new GameObject("person"); go.transform.SetParent(_scope.transform, false);
            var root = new GameObject("hips").transform; root.SetParent(go.transform, false);
            hand = new GameObject("hand").transform; hand.SetParent(root, false); hand.localPosition = new Vector3(.5f, 0, 0);
            var mesh = new Mesh { name = "perf quad" }; _made.Add(mesh);
            mesh.vertices = new[] { new Vector3(-.2f, 0, 0), new Vector3(.2f, 0, 0), new Vector3(-.2f, .4f, 0), new Vector3(.2f, .4f, 0) };
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.boneWeights = new[] { new BoneWeight { boneIndex0 = 0, weight0 = 1 }, new BoneWeight { boneIndex0 = 1, weight0 = 1 }, new BoneWeight { boneIndex0 = 0, weight0 = 1 }, new BoneWeight { boneIndex0 = 1, weight0 = 1 } };
            mesh.bindposes = new[] { root.worldToLocalMatrix, hand.worldToLocalMatrix };
            var smr = go.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh; smr.bones = new[] { root, hand }; smr.rootBone = root; smr.updateWhenOffscreen = true;
            return smr;
        }

        private Animator MakeAnimator(bool rootMotion, out Renderer rend)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.transform.SetParent(_scope.transform, false); Object.DestroyImmediate(go.GetComponent<Collider>());
            var a = go.AddComponent<Animator>(); a.applyRootMotion = rootMotion; a.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            rend = go.GetComponent<Renderer>(); return a;
        }

        [Test]
        public void WhenDisabledNothingChanges()
        {
            var smr = MakeSkinned(out _); var a = MakeAnimator(false, out _);
            _culler.Rescan(); _culler.Tick(0f); _culler.Tick(100f);
            Assert.IsTrue(smr.updateWhenOffscreen);
            Assert.AreEqual(AnimatorCullingMode.CullUpdateTransforms, a.cullingMode);
        }

        [Test]
        public void LockedBoundsContainEveryBoneWithMarginSoAVisibleMeshIsNeverCulled()
        {
            var smr = MakeSkinned(out var hand); _culler.Rescan();
            hand.localPosition = new Vector3(1.4f, 1.1f, -.8f);                 // an animated pose far from the authored one
            CharacterPerfCuller.Enabled = true;
            Assert.IsFalse(smr.updateWhenOffscreen);
            var b = smr.localBounds; var root = smr.rootBone;
            foreach (var bone in smr.bones) Assert.IsTrue(b.Contains(root.InverseTransformPoint(bone.position)), "bone " + bone.name + " must be inside the locked bounds");
            Assert.Greater(b.extents.x, .7f + .3f, "margin around the bone extents");
        }

        [Test]
        public void RootMotionCharacterGetsALargerMargin()
        {
            var plain = MakeSkinned(out _);
            var moving = MakeSkinned(out _); moving.gameObject.AddComponent<Animator>().applyRootMotion = true;
            _culler.Rescan(); CharacterPerfCuller.Enabled = true;
            Assert.Greater(moving.localBounds.extents.x, plain.localBounds.extents.x);
        }

        [Test]
        public void SwitchingOffRestoresOffscreenFlagAndBounds()
        {
            var smr = MakeSkinned(out _); var before = smr.localBounds; _culler.Rescan();
            CharacterPerfCuller.Enabled = true; Assert.IsFalse(smr.updateWhenOffscreen);
            CharacterPerfCuller.Enabled = false;
            Assert.IsTrue(smr.updateWhenOffscreen);
            Assert.AreEqual(before.size.x, smr.localBounds.size.x, 1e-4f); Assert.AreEqual(before.center.x, smr.localBounds.center.x, 1e-4f);
        }

        [Test]
        public void VisibleAnimatorIsNeverCulled()
        {
            var a = MakeAnimator(false, out _); _culler.Rescan(); CharacterPerfCuller.Enabled = true;
            CharacterPerfCuller.VisibilityProbe = r => true;
            for (float t = 0; t < 30f; t += .25f) { _culler.Tick(t); Assert.AreEqual(AnimatorCullingMode.CullUpdateTransforms, a.cullingMode, "visible at t=" + t); }
            Assert.IsFalse(_culler.IsAnimatorCulled(a));
        }

        [Test]
        public void HiddenAnimatorIsCulledOnlyAfterTheDelayAndReleasedTheMomentItIsVisible()
        {
            var a = MakeAnimator(false, out _); _culler.Rescan(); CharacterPerfCuller.Enabled = true;
            bool visible = false; CharacterPerfCuller.VisibilityProbe = r => visible;
            _culler.Tick(10f); _culler.Tick(10.5f);
            Assert.AreEqual(AnimatorCullingMode.CullUpdateTransforms, a.cullingMode, "inside the delay");
            _culler.Tick(11.1f);
            Assert.AreEqual(AnimatorCullingMode.CullCompletely, a.cullingMode);
            visible = true; _culler.Tick(11.12f);
            Assert.AreEqual(AnimatorCullingMode.CullUpdateTransforms, a.cullingMode, "restored on the first tick it is visible");
        }

        [Test]
        public void ABriefGlanceAwayResetsTheDelay()
        {
            var a = MakeAnimator(false, out _); _culler.Rescan(); CharacterPerfCuller.Enabled = true;
            bool visible = false; CharacterPerfCuller.VisibilityProbe = r => visible;
            _culler.Tick(0f); _culler.Tick(.9f); visible = true; _culler.Tick(1.0f); visible = false; _culler.Tick(1.1f); _culler.Tick(2.0f);
            Assert.AreEqual(AnimatorCullingMode.CullUpdateTransforms, a.cullingMode);
        }

        [Test]
        public void RootMotionAndMarkedAnimatorsAreNeverCulled()
        {
            var moving = MakeAnimator(true, out _); var marked = MakeAnimator(false, out _); marked.gameObject.AddComponent<CharacterPerfKeepAnimated>();
            _culler.Rescan(); CharacterPerfCuller.Enabled = true; CharacterPerfCuller.VisibilityProbe = r => false;
            _culler.Tick(0f); _culler.Tick(50f);
            Assert.IsFalse(_culler.IsAnimatorEligible(moving)); Assert.IsFalse(_culler.IsAnimatorEligible(marked));
            Assert.AreEqual(AnimatorCullingMode.CullUpdateTransforms, moving.cullingMode);
            Assert.AreEqual(AnimatorCullingMode.CullUpdateTransforms, marked.cullingMode);
        }

        [Test]
        public void SwitchingOffWhileCulledRestoresTheAnimator()
        {
            var a = MakeAnimator(false, out _); _culler.Rescan(); CharacterPerfCuller.Enabled = true; CharacterPerfCuller.VisibilityProbe = r => false;
            _culler.Tick(0f); _culler.Tick(5f); Assert.AreEqual(AnimatorCullingMode.CullCompletely, a.cullingMode);
            CharacterPerfCuller.Enabled = false;
            Assert.AreEqual(AnimatorCullingMode.CullUpdateTransforms, a.cullingMode);
        }
    }
}
