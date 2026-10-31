using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core.Actions;
using ProcSense.VR.Boot;
using ProcSense.VR.Bridge;
using ProcSense.VR.Views;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace ProcSense.Tests.PlayMode
{
    public sealed class PatientTransportSceneTests
    {
        [SetUp] public void BypassLogin()=>ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride=false;
        [TearDown] public void RestoreLogin()=>ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride=null;
        [UnityTearDown] public IEnumerator Cleanup()=>EditorXRTestEnvironment.Cleanup();
        private SimulationHost _host;
        private PatientTransportView _view;
        private static SimAction Action(string verb,params(string key,string value)[] p)=>new SimAction(verb,"patient",Channel.Vr,p.ToDictionary(x=>x.key,x=>x.value));
        private void Until(string stage)
        {
            for(int i=0;i<12000&&_host.Observable.Transport?.State!=stage;i++)_host.Tick(.5);
            Assert.AreEqual(stage,_host.Observable.Transport?.State);_view.RenderNow();
        }
        private IEnumerator Boot()
        {
            yield return SceneManager.LoadSceneAsync("Boot");yield return EditorXRTestEnvironment.EnsureSimulator();
            _host=SimulationHost.Instance;_host.AutoTick=false;_host.SaveLogOnDestroy=false;
            _view=Object.FindAnyObjectByType<PatientTransportView>();Assert.IsNotNull(_view);
        }
        private void Request(string mode,string to="ct",bool accompany=false)
        {
            foreach(var verb in new[]{"ask_identity","read_wristband","open_notes","confirm_identity"})Assert.IsTrue(_host.Apply(Action(verb)).Accepted);
            Assert.IsTrue(_host.Apply(Action("request_transport",("to",to),("mode",mode),("escort","porter,nurse"),("monitoring","portable"),("oxygen","true"),("accompany",accompany?"true":"false"))).Accepted);
            Until("IN_TRANSIT");
        }
        [UnityTest]
        public IEnumerator CT_modes_wait_for_Core_and_return()=>AllModesFor("ct");
        [UnityTest] public IEnumerator Ultrasound_modes_wait_for_Core_and_return()=>AllModesFor("ultrasound");
        [UnityTest] public IEnumerator Xray_modes_wait_for_Core_and_return()=>AllModesFor("xray");
        [UnityTest] public IEnumerator MRI_modes_wait_for_Core_and_return()=>AllModesFor("mri");
        private IEnumerator AllModesFor(string destination)
        {
            yield return Boot();
            foreach(var door in Object.FindObjectsByType<DiagnosticDoorView>().Where(d=>!d.Laboratory))
            {
                var path=new NavMeshPath();
                Assert.IsTrue(NavMesh.CalculatePath(_view.BayStart,door.OutsideAnchor.position,NavMesh.AllAreas,path));
                Assert.AreEqual(NavMeshPathStatus.PathComplete,path.status,door.name);
            }
            ulong seed=42;
            foreach(var mode in new[]{"walk","wheelchair","trolley"})
            {
                _host.StartSession("VAR-S1-A",seed++,ProcSense.Core.SessionMode.Practice);Request(mode,destination);
                string hash=_host.Session.Log.Hash(),state=_host.Observable.Hash();
                float deadline=Time.realtimeSinceStartup+30;
                while(!_view.AtDoor&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.IsTrue(_view.AtDoor,mode+": "+_view.Status+" position "+_view.Actor?.transform.position);
                Assert.AreEqual("IN_TRANSIT",_host.Observable.Transport.State);Assert.IsFalse(_view.HiddenInside);
                Assert.IsNotNull(_view.Actor);Assert.AreEqual(mode,_view.Actor.Mode);
                var corridor=Object.FindObjectsByType<RoomLayout>().Single(r=>r.gameObject.scene.name=="Corridor");
                deadline=Time.realtimeSinceStartup+6; while(!corridor.Contains(_view.Actor.Nurse.position,0)&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.IsTrue(corridor.Contains(_view.Actor.Nurse.position,0),mode+" nurse must clear the bay door: "+_view.Actor.Nurse.position);
                yield return null;Photograph(mode+"-at-"+destination,_view.Actor.transform);
                Assert.AreEqual(hash,_host.Session.Log.Hash());Assert.AreEqual(state,_host.Observable.Hash());
                Until("ARRIVED");deadline=Time.realtimeSinceStartup+7;
                while(!_view.HiddenInside&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.IsTrue(_view.HiddenInside,"Core-authorized inside passage");
                Assert.IsFalse(Object.FindObjectsByType<SwingDoor>().Single(d=>d.Connection=="BayCorridor").NpcPassageRequested,"No stale NPC bay-door request after group disappearance");
                Until("RETURNING");deadline=Time.realtimeSinceStartup+30;
                while(!_view.Status.StartsWith("At bedside")&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.IsTrue(_view.Status.StartsWith("At bedside"),mode+": "+_view.Status);
                Assert.IsFalse(_view.Presentation.BedsideVisible,"Do not restore bedside before Core BACK");
                Until("BACK");yield return null;Assert.IsNull(_view.Actor);Assert.IsTrue(_view.Presentation.BedsideVisible);
            }
        }

        [UnityTest]
        public IEnumerator Dynamic_blocker_stops_motion_cancel_button_returns_and_session_reset_restores_bedside()
        {
            yield return Boot();
            _host.StartSession("VAR-S1-A",42,ProcSense.Core.SessionMode.Practice);
            var original=_view.BedsidePatient.GetComponentsInChildren<Renderer>(true).ToDictionary(r=>r,r=>r.enabled);
            Request("trolley");
            var obstruction=GameObject.CreatePrimitive(PrimitiveType.Cube);obstruction.name="Transport test dynamic obstruction";
            obstruction.transform.position=_view.BayStart+new Vector3(.8f,.85f,0);obstruction.transform.localScale=new Vector3(.4f,1.7f,1.1f);Physics.SyncTransforms();
            string hash=_host.Session.Log.Hash(),state=_host.Observable.Hash();
            yield return new WaitForSeconds(.8f);
            StringAssert.Contains("blocked",_view.Status);
            Assert.Less(_view.Actor.transform.position.x,obstruction.transform.position.x);
            Assert.AreEqual(hash,_host.Session.Log.Hash());Assert.AreEqual(state,_host.Observable.Hash());
            Object.Destroy(obstruction);yield return null;
            float deadline=Time.realtimeSinceStartup+20;
            while(!_view.AtDoor&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(_view.AtDoor,"Dynamic obstruction removal resumes movement: "+_view.Status);
            var diagnostics=Object.FindAnyObjectByType<DiagnosticsTabView>(FindObjectsInactive.Include);diagnostics.RenderNow();
            var cancel=diagnostics.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b=>b.GetComponentInChildren<TMPro.TMP_Text>()?.text=="Cancel transport");
            Assert.IsTrue(cancel.interactable);cancel.onClick.Invoke();
            Assert.AreEqual("RETURNING",_host.Observable.Transport.State);
            _host.Tick(7200);_view.RenderNow();diagnostics.RenderNow();yield return null;
            Assert.IsNull(_view.Actor);Assert.IsFalse(cancel.interactable);
            foreach(var item in original)Assert.AreEqual(item.Value,item.Key.enabled,item.Key.name);
            Request("wheelchair");yield return null;
            _host.StartSession("VAR-S1-A",99,ProcSense.Core.SessionMode.Practice);_view.RenderNow();
            yield return null;yield return null;
            Assert.IsNull(_view.Actor);Assert.IsTrue(_view.Presentation.BedsideVisible);
            Assert.AreEqual(0,Object.FindObjectsByType<TransportActor>().Length);
            foreach(var item in original)Assert.AreEqual(item.Value,item.Key.enabled,item.Key.name);
        }

        [UnityTest]
        public IEnumerator Accompany_moves_only_after_Core_changes_location_and_never_enters_a_diagnostic_room()
        {
            yield return Boot();
            var companion=Object.FindAnyObjectByType<TransportAccompanyView>();
            var rig=Object.FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
            var rooms=Object.FindAnyObjectByType<RoomLoader>();
            _host.StartSession("VAR-S1-A",42,ProcSense.Core.SessionMode.Practice);companion.RenderNow();
            Vector3 initial=rig.transform.position;yield return new WaitForSeconds(.3f);
            Assert.Less(Vector3.Distance(initial,rig.transform.position),.05f,"Initial/session snapshot never warps");
            Request("walk","ct",true);companion.RenderNow();string hash=_host.Session.Log.Hash();
            float deadline=Time.realtimeSinceStartup+4;
            while((rooms.Busy||rooms.CurrentRoom!="Corridor")&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return new WaitForSeconds(.8f);
            Assert.AreEqual("Corridor",rooms.CurrentRoom,companion.LastError);
            Assert.AreEqual(hash,_host.Session.Log.Hash());
            var ct=Object.FindObjectsByType<DiagnosticDoorView>().Single(d=>d.DestinationIds.Contains("ct"));
            Assert.Less(Vector3.Distance(rig.transform.position,ct.OutsideAnchor.position),2.2f);
            Assert.Greater(ct.transform.InverseTransformPoint(rig.transform.position).z*-1,0,"Stay on corridor side");
            _host.Tick(7200);_view.RenderNow();companion.RenderNow();
            deadline=Time.realtimeSinceStartup+4;
            while((rooms.Busy||rooms.CurrentRoom!="PatientBay")&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.AreEqual("PatientBay",rooms.CurrentRoom,companion.LastError);
        }

        [UnityTest]
        public IEnumerator Reset_during_accompany_fade_cancels_the_owned_transition()
        {
            yield return Boot();
            var companion=Object.FindAnyObjectByType<TransportAccompanyView>();
            var rig=Object.FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
            var rooms=Object.FindAnyObjectByType<RoomLoader>();
            _host.StartSession("VAR-S1-A",42,ProcSense.Core.SessionMode.Practice);companion.RenderNow();
            var start=rig.transform.position;
            Request("walk","ct",true);companion.RenderNow();
            Assert.IsTrue(rooms.Busy);
            _host.StartSession("VAR-S1-A",43,ProcSense.Core.SessionMode.Practice);_view.RenderNow();companion.RenderNow();
            yield return new WaitForSeconds(1);
            Assert.IsFalse(rooms.Busy);Assert.AreEqual("PatientBay",rooms.CurrentRoom);
            Assert.Less(Vector3.Distance(start,rig.transform.position),.05f);
            Assert.IsNull(_view.Actor);
        }


        [UnityTest]
        public IEnumerator Doctor_and_nurse_selection_is_rendered_without_inventing_a_porter()
        {
            yield return Boot();
            _host.StartSession("VAR-S1-A",42,ProcSense.Core.SessionMode.Practice);
            foreach(var verb in new[]{"ask_identity","read_wristband","open_notes","confirm_identity"})Assert.IsTrue(_host.Apply(Action(verb)).Accepted);
            Assert.IsTrue(_host.Apply(Action("request_transport",("to","ct"),("mode","trolley"),("escort","doctor,nurse"),("monitoring","full"),("oxygen","true"))).Accepted);
            Until("IN_TRANSIT");Assert.IsNotNull(_view.Actor);Assert.IsNull(_view.Actor.Porter);
            Assert.IsNotNull(_view.Actor.Nurse);Assert.IsNotNull(_view.Actor.Doctor);
            Assert.IsTrue(_view.Presentation.Doctor);Assert.AreEqual("full",_view.Presentation.MonitoringLevel);
            Assert.IsNotNull(_view.Actor.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name.StartsWith("Monitoring proxy - full")));
            string hash=_host.Session.Log.Hash(); float deadline=Time.realtimeSinceStartup+30;
            var corridor=Object.FindObjectsByType<RoomLayout>().Single(r=>r.gameObject.scene.name=="Corridor");
            while((!_view.AtDoor||!corridor.Contains(_view.Actor.Doctor.position,0)||!corridor.Contains(_view.Actor.Nurse.position,0))&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(_view.AtDoor); Assert.IsTrue(corridor.Contains(_view.Actor.Doctor.position,0)); Assert.IsTrue(corridor.Contains(_view.Actor.Nurse.position,0)); Assert.AreEqual(hash,_host.Session.Log.Hash());
            _host.StartSession("VAR-S1-A",43,ProcSense.Core.SessionMode.Practice);_view.RenderNow();yield return null;yield return null;
            Assert.AreEqual(0,Object.FindObjectsByType<TransportActor>().Length);Assert.IsFalse(_view.Presentation.Doctor);
        }

        [UnityTest]
        public IEnumerator Reset_during_owned_door_teleport_fade_does_not_apply_a_stale_warp()
        {
            yield return Boot();
            var companion=Object.FindAnyObjectByType<TransportAccompanyView>();
            var rig=Object.FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
            var provider=Object.FindAnyObjectByType<ProcSense.VR.XR.SafeTeleportProvider>();
            _host.StartSession("VAR-S1-A",42,ProcSense.Core.SessionMode.Practice);companion.RenderNow();
            Request("walk","ct",true);companion.RenderNow();
            float deadline=Time.realtimeSinceStartup+4;
            while(!provider.Busy&&Time.realtimeSinceStartup<deadline)yield return null;
            Assert.IsTrue(provider.Busy,"The accompanying door teleport should use the safe provider");
            var before=rig.transform.position;
            _host.StartSession("VAR-S1-A",43,ProcSense.Core.SessionMode.Practice);_view.RenderNow();companion.RenderNow();
            yield return new WaitForSeconds(.6f);
            Assert.IsFalse(provider.Busy);Assert.Less(Vector3.Distance(before,rig.transform.position),.05f);
        }


        [UnityTest]
        public IEnumerator Diagnostic_sliding_leaf_waits_for_a_trainee_in_its_sweep()
        {
            yield return Boot();
            var ct=Object.FindObjectsByType<DiagnosticDoorView>().Single(d=>d.DestinationIds.Contains("ct"));
            var sequence=ct.GetComponent<TransportDoorSequence>();
            var leaf=sequence.Leaf.GetComponentsInChildren<Renderer>().First(c=>c.name=="Door leaf");
            var blocker=new GameObject("Test trainee blocking diagnostic leaf",typeof(CharacterController));
            var capsule=blocker.GetComponent<CharacterController>();capsule.radius=.25f;capsule.height=1.8f;capsule.center=Vector3.up*.9f;
            var point=leaf.bounds.center+sequence.Leaf.parent.right*.8f-ct.transform.forward*.28f;point.y=0;
            blocker.transform.position=point;Physics.SyncTransforms();string hash=_host.Session.Log.Hash();
            sequence.RequestedOpen=true;yield return new WaitForSeconds(.7f);
            Assert.IsTrue(sequence.Blocked);Assert.IsFalse(sequence.Open);Assert.AreEqual(hash,_host.Session.Log.Hash());
            Object.Destroy(blocker);yield return new WaitForSeconds(.7f);
            Assert.IsTrue(sequence.Open);sequence.Restore();
        }


        [UnityTest]
        public IEnumerator Core_arrival_time_jump_skips_an_unfinished_outbound_animation()
        {
            yield return Boot();_host.StartSession("VAR-S1-A",42,ProcSense.Core.SessionMode.Practice);Request("walk");
            Assert.IsFalse(_view.AtDoor);Assert.IsNotNull(_view.Actor);
            yield return null;var bayDoor=Object.FindObjectsByType<SwingDoor>().Single(d=>d.Connection=="BayCorridor");Assert.IsTrue(bayDoor.NpcPassageRequested);
            Until("ARRIVED");string hash=_host.Session.Log.Hash(),state=_host.Observable.Hash();
            yield return null;Assert.IsTrue(_view.HiddenInside);Assert.IsNull(_view.Actor);Assert.IsFalse(bayDoor.NpcPassageRequested);
            Assert.AreEqual(hash,_host.Session.Log.Hash());Assert.AreEqual(state,_host.Observable.Hash());
        }


        [UnityTest]
        public IEnumerator Attached_sensor_objects_stay_enabled_and_emit_no_detach_during_transport()
        {
            yield return Boot();_host.StartSession("VAR-S1-A",42,ProcSense.Core.SessionMode.Practice);
            foreach(var verb in new[]{"ask_identity","read_wristband","open_notes","confirm_identity"})Assert.IsTrue(_host.Apply(Action(verb)).Accepted);
            foreach(var sensor in new[]{"spo2","nibp_cuff"})Assert.IsTrue(_host.Apply(Action("attach_sensor",("sensor",sensor))).Accepted);
            yield return null;yield return null;
            var sensors=Object.FindObjectsByType<ProcSense.VR.ActionEmitters.SensorAttachment>();
            Assert.AreEqual(2,sensors.Length);Assert.IsTrue(sensors.All(s=>s.Attached));
            var flags=sensors.SelectMany(s=>s.GetComponentsInChildren<Renderer>(true)).ToDictionary(r=>r,r=>r.enabled);
            Request("trolley");string hash=_host.Session.Log.Hash();
            yield return new WaitForSeconds(1);
            Assert.AreEqual(hash,_host.Session.Log.Hash());
            Assert.IsTrue(sensors.All(s=>s.Attached&&s.isActiveAndEnabled));
            Assert.IsTrue(sensors.SelectMany(s=>s.GetComponentsInChildren<Renderer>(true)).All(r=>!r.enabled));
            _host.Tick(7200);_view.RenderNow();yield return null;
            foreach(var flag in flags)Assert.AreEqual(flag.Value,flag.Key.enabled,flag.Key.name);
            Assert.IsTrue(sensors.All(s=>s.Attached&&s.isActiveAndEnabled));
        }

        private static void Photograph(string name,Transform target)
        {
            var go=new GameObject("Transport evidence camera",typeof(Camera));var camera=go.GetComponent<Camera>();
            var rt=new RenderTexture(1400,1000,24);var texture=new Texture2D(1400,1000,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try
            {
                camera.fieldOfView=70;camera.nearClipPlane=.03f;camera.transform.position=target.position+(new Vector3(4.5f,0,2.2f)-target.position).normalized*2.8f+Vector3.up*1.9f;
                camera.transform.LookAt(target.position+Vector3.up);
                var label=target.GetComponentInChildren<TextMesh>(); if(label!=null) { var facing=Vector3.ProjectOnPlane(label.transform.position-camera.transform.position,Vector3.up); if(facing.sqrMagnitude>.000001f)label.transform.rotation=Quaternion.LookRotation(facing,Vector3.up); }
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                texture.ReadPixels(new Rect(0,0,1400,1000),0,0);texture.Apply();
                var dir=Path.GetFullPath(Path.Combine(Application.dataPath,"..","Logs","TransportReview"));Directory.CreateDirectory(dir);
                File.WriteAllBytes(Path.Combine(dir,name+".png"),texture.EncodeToPNG());
            }
            finally{camera.targetTexture=null;RenderTexture.active=previous;Object.DestroyImmediate(rt);Object.DestroyImmediate(texture);Object.DestroyImmediate(go);}
        }
    }
}
