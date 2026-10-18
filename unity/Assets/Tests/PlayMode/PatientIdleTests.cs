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

namespace ProcSense.Tests.PlayMode
{
    public class PatientIdleTests
    {
        private IEnumerator Load()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/PatientBay.unity", new LoadSceneParameters(LoadSceneMode.Single));
            for (int i = 0; i < 30; i++) yield return null;
            SimulationHost.Instance.AutoTick = false;
            SimulationHost.Instance.SaveLogOnDestroy = false;
        }

        private static PatientIdleView Idle => GameObject.Find("MPFB patient").GetComponent<PatientIdleView>();
        private static SkinnedMeshRenderer Body => GameObject.Find("MPFB patient").GetComponentsInChildren<SkinnedMeshRenderer>().Single(r => r.name.EndsWith(".body"));

        private static Vector3[] WorldVertices(SkinnedMeshRenderer renderer)
        {
            var mesh = new Mesh();
            try { renderer.BakeMesh(mesh, true); return mesh.vertices.Select(renderer.transform.TransformPoint).ToArray(); }
            finally { Object.DestroyImmediate(mesh); }
        }

        [UnityTest]
        public IEnumerator Breathing_moves_body_and_gown_without_moving_rest_pose_targets()
        {
            yield return Load();
            var idle = Idle;
            idle.enabled = false;
            var surfaces = new[] { Body, GameObject.Find("Draped patient gown").GetComponent<SkinnedMeshRenderer>(), GameObject.Find("Patient shorts").GetComponent<SkinnedMeshRenderer>() };
            Assert.IsNull(GameObject.Find("Draped bed blanket"));
            var sockets = Object.FindObjectsByType<SensorAttachment>(FindObjectsSortMode.None).Select(s => s.Destination.transform).ToArray();
            var poses = sockets.Select(s => new Pose(s.position, s.rotation)).ToArray();
            var host = SimulationHost.Instance;
            string observable = host.Observable.Hash();
            int actions = host.Session.Log.Count;
            try
            {
                idle.EvaluatePose(0);
                var rest = surfaces.Select(WorldVertices).ToArray();
                idle.EvaluatePose(1.8f);
                // Isolate breathing from the independent face/head layer for the surface-fit comparison.
                idle.RestorePose();
                foreach (var surface in surfaces) surface.SetBlendShapeWeight(surface.sharedMesh.GetBlendShapeIndex(PatientIdleView.BreathShape), 100);
                for (int s = 0; s < surfaces.Length; s++)
                {
                    var moved = WorldVertices(surfaces[s]);
                    Assert.AreEqual(rest[s].Length, moved.Length);
                    Assert.That(rest[s].Zip(moved, Vector3.Distance).Max(), Is.InRange(.001f, .0145f), surfaces[s].name);
                    if (s < 2) Assert.That(rest[s].Zip(moved, Vector3.Distance).Max(), Is.GreaterThan(.012f), "Breathing must read at bedside distance");
                    for (int i = 0; i < moved.Length; i++)
                    {
                        var delta = moved[i] - rest[s][i];
                        Assert.That(Mathf.Abs(delta.x) + Mathf.Abs(delta.z), Is.LessThan(.00002f), "Breathing must lift, not slide cloth");
                        if (Mathf.Abs(rest[s][i].x) > .28f || rest[s][i].z < .70f || rest[s][i].z > 1.58f)
                            Assert.That(delta.magnitude, Is.LessThan(.00002f), "Hands, feet, neck and draped edges must stay put");
                    }
                }
                for (int i = 0; i < sockets.Length; i++)
                {
                    Assert.That(Vector3.Distance(poses[i].position, sockets[i].position), Is.LessThan(.000001f));
                    Assert.That(Quaternion.Angle(poses[i].rotation, sockets[i].rotation), Is.LessThan(.001f));
                }
                Assert.AreEqual(actions, host.Session.Log.Count);
                Assert.AreEqual(observable, host.Observable.Hash());
            }
            finally
            {
                idle.RestorePose();
                foreach (var surface in surfaces) surface.SetBlendShapeWeight(surface.sharedMesh.GetBlendShapeIndex(PatientIdleView.BreathShape), 0);
            }
        }

        [UnityTest]
        public IEnumerator Blink_is_coordinated_and_gaze_head_motion_are_bounded_without_touching_expression_shapes()
        {
            yield return Load();
            var idle = Idle;
            idle.enabled = false;
            var head = GameObject.Find("MPFB patient").GetComponentsInChildren<Transform>().Single(t => t.name == "head");
            var rotation = head.localRotation;
            var faces = GameObject.Find("MPFB patient").GetComponentsInChildren<SkinnedMeshRenderer>().Where(r => r.enabled).ToArray();
            int smile = Body.sharedMesh.GetBlendShapeIndex("mouthSmileLeft");
            Body.SetBlendShapeWeight(smile, 17);
            try
            {
                idle.EvaluatePose(2.625f);
                foreach (var r in faces)
                {
                    foreach (string shape in new[] { "eyeBlinkLeft", "eyeBlinkRight" })
                    {
                        int index = r.sharedMesh.GetBlendShapeIndex(shape);
                        if (index >= 0) Assert.That(r.GetBlendShapeWeight(index), Is.GreaterThan(99.9f), r.name);
                    }
                }
                for (int time = 0; time <= 60; time++)
                {
                    idle.EvaluatePose(time);
                    Assert.That(Quaternion.Angle(rotation, head.localRotation), Is.LessThan(2.1f));
                    foreach (var r in faces)
                        for (int i = 0; i < r.sharedMesh.blendShapeCount; i++)
                            if (r.sharedMesh.GetBlendShapeName(i).StartsWith("eyeLook")) Assert.That(r.GetBlendShapeWeight(i), Is.InRange(0, 7.01f));
                }
                Assert.AreEqual(17, Body.GetBlendShapeWeight(smile));
                idle.EvaluatePose(float.NaN);
                idle.EvaluatePose(float.PositiveInfinity);
                idle.RestorePose();
                Assert.That(Quaternion.Angle(rotation, head.localRotation), Is.LessThan(.001f));
                Assert.AreEqual(0, Body.GetBlendShapeWeight(Body.sharedMesh.GetBlendShapeIndex("eyeBlinkLeft")));
            }
            finally { Body.SetBlendShapeWeight(smile, 0); idle.RestorePose(); }
        }

        [UnityTest]
        public IEnumerator Idle_runs_resets_between_sessions_and_restores_when_disabled()
        {
            yield return Load();
            var idle = Idle;
            float before = idle.Elapsed;
            for (int i = 0; i < 15; i++) yield return null;
            Assert.Greater(idle.Elapsed, before);
            var host = SimulationHost.Instance;
            host.StartSession(host.Config.caseId, (ulong)host.Config.seed, host.Config.mode);
            yield return null;
            Assert.That(idle.Elapsed, Is.LessThan(.25f));
            idle.enabled = false;
            Assert.AreEqual(0, Body.GetBlendShapeWeight(Body.sharedMesh.GetBlendShapeIndex(PatientIdleView.BreathShape)));
            Assert.AreEqual(0, idle.Elapsed);
            idle.enabled = true;
            yield return null;
            Assert.Greater(idle.Elapsed, 0);
        }

        [UnityTest]
        public IEnumerator Missing_optional_surfaces_are_safe_and_no_global_random_state_is_consumed()
        {
            yield return Load();
            var go = new GameObject("Optional idle test");
            var random = Random.state;
            try
            {
                var view = go.AddComponent<PatientIdleView>();
                view.Configure(null, new SkinnedMeshRenderer[] { null }, null);
                view.EvaluatePose(2.625f);
                view.EvaluatePose(-1);
                view.enabled = false;
                Assert.AreEqual(JsonUtility.ToJson(random), JsonUtility.ToJson(Random.state));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [UnityTest]
        public IEnumerator Relaxed_arms_and_fingers_move_within_bedside_bounds_and_restore_on_disable()
        {
            yield return Load();
            var idle = Idle;
            idle.enabled = false;
            var bones = GameObject.Find("MPFB patient").GetComponentsInChildren<Transform>();
            var hands = bones.Where(t => t.name == "hand_l" || t.name == "hand_r").ToArray();
            var fingers = bones.Where(t => t.name == "middle_01_l" || t.name == "ring_01_r").ToArray();
            var handPoses = hands.Select(t => t.position).ToArray();
            var fingerPoses = fingers.Select(t => t.localRotation).ToArray();
            float largestShift = 0, largestCurl = 0;
            try
            {
                for (int time = 0; time < 60; time++)
                {
                    idle.EvaluatePose(time);
                    float bedTop = GameObject.Find("Soft mattress").GetComponent<Renderer>().bounds.max.y;
                    float contact = WorldVertices(Body).Where(p => Mathf.Abs(p.x) > .28f && p.z > .85f && p.z < 1.15f).Min(p => p.y);
                    Assert.That(contact, Is.InRange(bedTop - .001f, bedTop + .025f), "Hands stay supported without entering the mattress");
                    for (int i = 0; i < hands.Length; i++)
                    {
                        largestShift = Mathf.Max(largestShift, Vector3.Distance(hands[i].position, handPoses[i]));
                        Assert.That(Mathf.Abs(hands[i].position.x), Is.LessThan(.38f), "Hands remain on the mattress, clear of table");
                        Assert.That(hands[i].position.y, Is.InRange(.76f, .80f));
                    }
                    for (int i = 0; i < fingers.Length; i++) largestCurl = Mathf.Max(largestCurl, Quaternion.Angle(fingerPoses[i], fingers[i].localRotation));
                }
                Assert.That(largestShift, Is.InRange(.002f, .02f));
                Assert.That(largestCurl, Is.InRange(.5f, 3.1f));
                idle.RestorePose();
                for (int i = 0; i < hands.Length; i++) Assert.That(Vector3.Distance(handPoses[i], hands[i].position), Is.LessThan(.000001f));
            }
            finally { idle.RestorePose(); }
        }

        [UnityTest]
        public IEnumerator Mounted_sensors_follow_arm_and_finger_motion_without_duplicate_actions()
        {
            yield return Load();
            var host = SimulationHost.Instance;
            var tablet = Object.FindFirstObjectByType<TabletView>();
            Assert.IsTrue(tablet.Press("attach_sensor", "sensor", "spo2"));
            Assert.IsTrue(tablet.Press("attach_sensor", "sensor", "nibp_cuff"));
            for (int i = 0; i < 8; i++) yield return new WaitForFixedUpdate();
            var sensors = Object.FindObjectsByType<SensorAttachment>(FindObjectsSortMode.None);
            var idle = Idle;
            idle.enabled = false;
            int actions = host.Session.Log.Count;
            string hash = host.Observable.Hash();
            try
            {
                for (int time = 0; time < 60; time += 2)
                {
                    idle.EvaluatePose(time);
                    foreach (var sensor in sensors)
                    {
                        sensor.SyncNow();
                        var grab = sensor.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
                        var actual = grab.GetAttachTransform(sensor.Destination);
                        var target = sensor.Destination.attachTransform;
                        Assert.IsTrue(sensor.Attached);
                        Assert.That(Vector3.Distance(actual.position, target.position), Is.LessThan(.0002f), sensor.name);
                        Assert.That(Quaternion.Angle(actual.rotation, target.rotation), Is.LessThan(.1f), sensor.name);
                    }
                    yield return null;
                }
                Assert.AreEqual(actions, host.Session.Log.Count);
                Assert.AreEqual(hash, host.Observable.Hash());
            }
            finally { idle.RestorePose(); }
        }

        [UnityTest]
        public IEnumerator Legs_wiggle_in_separate_bursts_with_pauses_and_keep_feet_above_the_mattress()
        {
            yield return Load();
            var idle = Idle; idle.enabled = false;
            var bones = GameObject.Find("MPFB patient").GetComponentsInChildren<Transform>().Where(t => new[] { "thigh_l", "calf_l", "foot_l", "ball_l", "thigh_r", "calf_r", "foot_r", "ball_r" }.Contains(t.name)).ToArray();
            var rest = bones.Select(t => t.localRotation).ToArray();
            float largest = 0;
            float top = GameObject.Find("Soft mattress").GetComponent<Renderer>().bounds.max.y;
            int count = SimulationHost.Instance.Session.Log.Count;
            string hash = SimulationHost.Instance.Observable.Hash();
            try
            {
                for (float time = 0; time <= 40; time += .5f)
                {
                    idle.EvaluatePose(time);
                    for (int i = 0; i < bones.Length; i++) largest = Mathf.Max(largest, Quaternion.Angle(rest[i], bones[i].localRotation));
                    float heel = WorldVertices(Body).Where(p => p.z < .36f && Mathf.Abs(p.x) < .3f).Min(p => p.y);
                    Assert.That(heel, Is.InRange(top - .001f, top + .025f));
                }
                Assert.That(largest, Is.InRange(1, 3.6f));
                foreach (float time in new[] { 0f, 3f, 8.5f, 16f, 20f })
                {
                    idle.EvaluatePose(time);
                    for (int i = 0; i < bones.Length; i++) Assert.That(Quaternion.Angle(rest[i], bones[i].localRotation), Is.LessThan(.05f), "Quiet interval");
                }
                Assert.AreEqual(count, SimulationHost.Instance.Session.Log.Count);
                Assert.AreEqual(hash, SimulationHost.Instance.Observable.Hash());
                idle.RestorePose();
                for (int i = 0; i < bones.Length; i++) Assert.That(Quaternion.Angle(rest[i], bones[i].localRotation), Is.LessThan(.05f));
            }
            finally { idle.RestorePose(); }
        }

        [UnityTest]
        public IEnumerator Opaque_shorts_cover_the_central_foot_end_view_at_rest_and_through_leg_shifts()
        {
            yield return Load();
            var idle = Idle; idle.enabled = false;
            var shorts = GameObject.Find("Patient shorts").GetComponent<SkinnedMeshRenderer>();
            Assert.AreEqual(1, shorts.sharedMaterial.color.a);
            Assert.AreEqual(0, shorts.sharedMaterial.GetFloat("_Surface"));
            var go = new GameObject("Temporary coverage collision check");
            var collider = go.AddComponent<MeshCollider>();
            var mesh = new Mesh();
            try
            {
                foreach (float time in new[] { 0f, 5.4f, 11.4f, 17f, 23.4f })
                {
                    idle.EvaluatePose(time);
                    shorts.BakeMesh(mesh, true);
                    mesh.vertices = mesh.vertices.Select(shorts.transform.TransformPoint).ToArray();
                    mesh.RecalculateBounds(); collider.sharedMesh = null; collider.sharedMesh = mesh;
                    foreach (float height in new[] { .78f, .82f, .86f })
                    {
                        var origin = new Vector3(0, height, -.5f);
                        Assert.IsTrue(collider.Raycast(new Ray(origin, (new Vector3(0, .82f, 1.04f) - origin).normalized), out _, 2), "Cloth must close the foot-end view at " + time);
                    }
                }
            }
            finally { idle.RestorePose(); Object.DestroyImmediate(go); Object.DestroyImmediate(mesh); }
        }
    }
}
#endif
