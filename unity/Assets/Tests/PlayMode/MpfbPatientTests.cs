#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using ProcSense.VR.ActionEmitters;
using ProcSense.VR.Bridge;
using ProcSense.VR.Views;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ProcSense.Tests.PlayMode
{
    public class MpfbPatientTests
    {
        private IEnumerator LoadReview()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/PatientBay.unity", new LoadSceneParameters(LoadSceneMode.Single));
            for (int i = 0; i < 50; i++) yield return null;
            Assert.IsNotNull(SimulationHost.Instance);
            SimulationHost.Instance.AutoTick = false;
            SimulationHost.Instance.SaveLogOnDestroy = false;
        }

        [UnityTest]
        public IEnumerator Rig_moves_skin_and_attached_targets_without_clinical_actions()
        {
            yield return LoadReview();
            var patient = GameObject.Find("MPFB patient");
            var animator = patient.GetComponentInChildren<Animator>();
            Assert.IsTrue(animator.avatar.isValid && animator.avatar.isHuman);
            Assert.AreEqual(1, Camera.allCamerasCount);
            var body = patient.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r => r.name.Contains(".body"));
            var band = patient.GetComponentsInChildren<Transform>().Single(t => t.name == "Patient wristband");
            var forearm = band.parent.parent;
            Assert.That(forearm.name, Does.StartWith("lowerarm_"));
            var before = new Mesh();
            var after = new Mesh();
            var initial = forearm.localRotation;
            var position = band.position;
            int actions = SimulationHost.Instance.Session.Log.Count;
            try
            {
                body.BakeMesh(before);
                // Bend across the bone axis; twisting along it leaves the wrist centre in place.
                forearm.localRotation = initial * Quaternion.Euler(10, 0, 0);
                yield return null;
                body.BakeMesh(after);
                Assert.That(Vector3.Distance(position, band.position), Is.GreaterThan(.005f));
                Assert.That(before.vertices.Zip(after.vertices, Vector3.Distance).Max(), Is.GreaterThan(.000001f));
                Assert.AreEqual(actions, SimulationHost.Instance.Session.Log.Count);
                var gown = GameObject.Find("Draped patient gown").GetComponent<SkinnedMeshRenderer>();
                Assert.IsTrue(gown.enabled);
                Assert.AreEqual(body.bones.Length, gown.bones.Length);
                Assert.IsTrue(gown.sharedMesh.boneWeights.All(w => Mathf.Abs(w.weight0 + w.weight1 + w.weight2 + w.weight3 - 1) < .001f));
                var upperarm = forearm.parent;
                var upperRotation = upperarm.localRotation;
                try
                {
                    gown.BakeMesh(before);
                    upperarm.localRotation = upperRotation * Quaternion.Euler(5, 0, 0);
                    yield return null;
                    gown.BakeMesh(after);
                    Assert.That(before.vertices.Zip(after.vertices, Vector3.Distance).Max(), Is.GreaterThan(.001f));
                    Assert.AreEqual(actions, SimulationHost.Instance.Session.Log.Count);
                }
                finally { upperarm.localRotation = upperRotation; }
            }
            finally
            {
                forearm.localRotation = initial;
                Object.DestroyImmediate(before);
                Object.DestroyImmediate(after);
            }
        }

        [UnityTest]
        public IEnumerator Blink_shape_deforms_the_face_without_changing_simulation()
        {
            yield return LoadReview();
            var body = GameObject.Find("MPFB patient").GetComponentsInChildren<SkinnedMeshRenderer>().Single(r => r.name.Contains(".body"));
            int blink = Enumerable.Range(0, body.sharedMesh.blendShapeCount).Single(i => body.sharedMesh.GetBlendShapeName(i).EndsWith("eyeBlinkLeft"));
            var before = new Mesh();
            var after = new Mesh();
            int actions = SimulationHost.Instance.Session.Log.Count;
            try
            {
                body.BakeMesh(before);
                body.SetBlendShapeWeight(blink, 100);
                yield return null;
                body.BakeMesh(after);
                Assert.That(before.vertices.Zip(after.vertices, Vector3.Distance).Max(), Is.GreaterThan(.000001f));
                Assert.AreEqual(actions, SimulationHost.Instance.Session.Log.Count);
            }
            finally
            {
                body.SetBlendShapeWeight(blink, 0);
                Object.DestroyImmediate(before);
                Object.DestroyImmediate(after);
            }
        }

        [UnityTest]
        public IEnumerator Bone_mounted_sensors_attach_physically_and_stay_removed()
        {
            yield return LoadReview();
            var host = SimulationHost.Instance;
            var tablet = Object.FindFirstObjectByType<TabletView>();
            var bp = GameObject.Find("Start BP cycle").GetComponent<XRSelectEmitter>();
            Assert.IsFalse(bp.Fire().Accepted, "BP without cuff must be rejected");
            foreach (string sensor in new[] { "spo2", "nibp_cuff" })
            {
                var binding = Object.FindObjectsByType<SensorAttachment>().Single(s => s.BuildAction().Params["sensor"] == sensor);
                var grab = binding.GetComponent<XRGrabInteractable>();
                var target = binding.Destination.attachTransform;
                grab.transform.rotation = target.rotation * Quaternion.Inverse(grab.GetAttachTransform(binding.Destination).rotation) * grab.transform.rotation;
                grab.transform.position += target.position - grab.GetAttachTransform(binding.Destination).position;
                var pose = new Pose(grab.transform.position, grab.transform.rotation);
                grab.SetTargetPose(pose);
                var rigidbody = grab.GetComponent<Rigidbody>();
                rigidbody.position = pose.position;
                rigidbody.rotation = pose.rotation;
                Physics.SyncTransforms();
                int count = host.Session.Log.Count;
                for (int i = 0; i < 15; i++) yield return new WaitForFixedUpdate();
                Assert.IsTrue(binding.Attached, sensor);
                Assert.AreEqual(count + 1, host.Session.Log.Count);
                Assert.IsTrue(tablet.Press("detach_sensor", "sensor", sensor));
                for (int i = 0; i < 15; i++) yield return new WaitForFixedUpdate();
                Assert.IsFalse(binding.Attached, sensor + " reattached after removal");
            }
        }
    }
}
#endif
