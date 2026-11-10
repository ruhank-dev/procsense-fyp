#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using ProcSense.VR.Views;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.TestTools;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>Round 3 reassessment of the character culler against the visible-quality requirements: culling an animator must not make the pose snap when the character is seen again, must not lose blend-shape (facial) state, and must wake on the first tick it is seen. Drives a real Animator through a PlayableGraph.</summary>
    public sealed class CharacterPerfCullTests
    {
        private GameObject _root; private PlayableGraph _graph; private CharacterPerfCuller _culler; private bool _wasEnabled; private System.Func<Renderer, bool> _probe;

        [SetUp] public void Setup() { _wasEnabled = CharacterPerfCuller.Enabled; _probe = CharacterPerfCuller.VisibilityProbe; CharacterPerfCuller.Enabled = false; }

        [TearDown]
        public void Teardown()
        {
            CharacterPerfCuller.Enabled = false; if (_graph.IsValid()) _graph.Destroy();
            if (_root != null) Object.DestroyImmediate(_root); if (_culler != null) Object.DestroyImmediate(_culler.gameObject);
            CharacterPerfCuller.Enabled = _wasEnabled; CharacterPerfCuller.VisibilityProbe = _probe;
        }

        private Transform Build(out Animator animator, out SkinnedMeshRenderer smr)
        {
            _root = new GameObject("perf person"); var hips = new GameObject("hips").transform; hips.SetParent(_root.transform, false);
            var hand = new GameObject("hand").transform; hand.SetParent(hips, false);
            var mesh = new Mesh { name = "perf blend quad" }; mesh.vertices = new[] { new Vector3(-.2f, 0, 0), new Vector3(.2f, 0, 0), new Vector3(-.2f, .4f, 0), new Vector3(.2f, .4f, 0) }; mesh.triangles = new[] { 0, 2, 1, 1, 2, 3 };
            mesh.boneWeights = new[] { new BoneWeight { boneIndex0 = 0, weight0 = 1 }, new BoneWeight { boneIndex0 = 1, weight0 = 1 }, new BoneWeight { boneIndex0 = 0, weight0 = 1 }, new BoneWeight { boneIndex0 = 1, weight0 = 1 } };
            mesh.bindposes = new[] { hips.worldToLocalMatrix, hand.worldToLocalMatrix };
            mesh.AddBlendShapeFrame("blink", 100f, new Vector3[4] { Vector3.up * .1f, Vector3.up * .1f, Vector3.zero, Vector3.zero }, new Vector3[4], new Vector3[4]);
            smr = _root.AddComponent<SkinnedMeshRenderer>(); smr.sharedMesh = mesh; smr.bones = new[] { hips, hand }; smr.rootBone = hips;
            animator = _root.AddComponent<Animator>(); animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var clip = new AnimationClip { legacy = false, name = "perf ramp" };
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("hips/hand", typeof(Transform), "m_LocalPosition.x"), AnimationCurve.Linear(0, 0, 1000, 1000));   // one metre per second, no loop wrap
            _graph = PlayableGraph.Create("perf"); var output = AnimationPlayableOutput.Create(_graph, "o", animator); output.SetSourcePlayable(AnimationClipPlayable.Create(_graph, clip)); _graph.Play();
            var host = new GameObject("perf culler host"); host.SetActive(false); _culler = host.AddComponent<CharacterPerfCuller>(); _culler.Scope = _root.transform; host.SetActive(true);
            return hand;
        }

        [UnityTest]
        public IEnumerator A_culled_character_keeps_its_pose_and_resumes_without_a_snap()
        {
            var hand = Build(out var animator, out _); _culler.Rescan(); CharacterPerfCuller.Enabled = true;
            bool visible = true; CharacterPerfCuller.VisibilityProbe = r => visible;
            for (int i = 0; i < 20; i++) { yield return null; _culler.Tick(i * .05f); }
            Assert.Greater(hand.localPosition.x, 0.01f, "the clip is playing while visible");
            visible = false; _culler.Tick(2f); _culler.Tick(3.2f); Assert.AreEqual(AnimatorCullingMode.CullCompletely, animator.cullingMode, "culled after the delay");
            yield return null; float justCulled = hand.localPosition.x;
            for (int i = 0; i < 30; i++) yield return null;
            Assert.AreEqual(justCulled, hand.localPosition.x, 1e-4f, "a culled animator holds its last pose (no drift, no reset to the bind pose)");
            visible = true; _culler.Tick(3.3f); Assert.AreEqual(AnimatorCullingMode.AlwaysAnimate, animator.cullingMode, "released on the first tick it is visible");
            yield return null; yield return null;
            Assert.Less(Mathf.Abs(hand.localPosition.x - justCulled), 0.2f, "no pose snap on the frames after it is seen again (resumes from the held pose)");
            Assert.Greater(hand.localPosition.x, 0.01f, "not reset to the bind pose");
        }

        [UnityTest]
        public IEnumerator Blend_shape_state_survives_a_cull_and_a_switch_off_and_on()
        {
            Build(out var animator, out var smr); _culler.Rescan(); CharacterPerfCuller.Enabled = true;
            smr.SetBlendShapeWeight(0, 63f); bool visible = false; CharacterPerfCuller.VisibilityProbe = r => visible;
            _culler.Tick(0f); _culler.Tick(1.5f); Assert.AreEqual(AnimatorCullingMode.CullCompletely, animator.cullingMode);
            for (int i = 0; i < 10; i++) yield return null;
            Assert.AreEqual(63f, smr.GetBlendShapeWeight(0), 1e-3f, "facial state is held while culled");
            smr.SetBlendShapeWeight(0, 20f); visible = true; _culler.Tick(1.6f); yield return null;
            Assert.AreEqual(20f, smr.GetBlendShapeWeight(0), 1e-3f, "a weight written while culled is kept on wake");
            CharacterPerfCuller.Enabled = false; CharacterPerfCuller.Enabled = true;
            Assert.AreEqual(20f, smr.GetBlendShapeWeight(0), 1e-3f, "lock/restore of the bounds never touches blend shapes");
            CharacterPerfBlend.Set(smr, 0, 20.0004f); Assert.AreEqual(20f, smr.GetBlendShapeWeight(0), 1e-3f, "an unchanged weight is not rewritten");
            CharacterPerfBlend.Set(smr, 0, 50f); Assert.AreEqual(50f, smr.GetBlendShapeWeight(0), 1e-3f, "a changed weight is written");
        }

        [UnityTest]
        public IEnumerator A_culled_animator_wakes_on_the_first_tick_even_when_visible_ones_are_sampled()
        {
            Build(out var animator, out _); _culler.Rescan(); CharacterPerfCuller.Enabled = true; CharacterPerfCuller.FastTick = true;
            bool visible = false; CharacterPerfCuller.VisibilityProbe = r => visible;
            _culler.Tick(0f); _culler.Tick(1.2f); Assert.AreEqual(AnimatorCullingMode.CullCompletely, animator.cullingMode);
            visible = true; _culler.Tick(1.21f);                                    // 10 ms after the previous sample: a culled animator is still checked every tick
            Assert.AreEqual(AnimatorCullingMode.AlwaysAnimate, animator.cullingMode, "wakes on the very next tick, not at the next sample");
            yield return null;
        }
    }
}
#endif
