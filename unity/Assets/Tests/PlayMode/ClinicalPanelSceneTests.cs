using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.Core.Pipeline;
using ProcSense.Core.Workflow;
using ProcSense.Core.Replay;
using ProcSense.VR.Bridge;
using ProcSense.VR.Sync;
using ProcSense.VR.UI;
using ProcSense.VR.UI.Debrief;
using ProcSense.VR.Views;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Object = UnityEngine.Object;

namespace ProcSense.Tests.PlayMode
{
    public sealed class ClinicalPanelSceneTests
    {
        private SimulationHost _host;
        private ClinicalPanelStation _bay, _ot;
        private string _originalDirectory;
        private SessionSync _originalSync;
        private GameObject _reviewRoot, _reviewHud;
        [SetUp] public void Setup()
        {
            ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = false;
            _originalDirectory = SimulationHost.SessionsDirectoryOverride;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if (_reviewRoot != null) Object.DestroyImmediate(_reviewRoot);
            if (_reviewHud != null) Object.DestroyImmediate(_reviewHud);
            ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride = null;
            SimulationHost.SessionsDirectoryOverride = _originalDirectory;
            if (_originalSync != null && SessionUploader.Instance != null) SessionUploader.Instance.Sync = _originalSync;
            yield return EditorXRTestEnvironment.Cleanup();
        }
        private IEnumerator Boot()
        {
            yield return SceneManager.LoadSceneAsync("Boot"); yield return EditorXRTestEnvironment.EnsureSimulator();
            _host = SimulationHost.Instance; _host.AutoTick = false; _host.SaveLogOnDestroy = false;
            _host.StartSession("VAR-S1-A", 42, SessionMode.Practice);
            _bay = Object.FindObjectsByType<ClinicalPanelStation>().Single(s => s.WardAnchor != null);
            _ot = Object.FindObjectsByType<ClinicalPanelStation>().Single(s => s.WhoAnchor != null);
            Sync(); yield return null;
        }
        private void Sync() { _bay.Sync(); _ot.Sync(); }
        private static SimAction Action(string verb, System.Collections.Generic.Dictionary<string,string> p) => new SimAction(verb, "patient", Channel.Vr, p);
        private System.Collections.Generic.List<TimedAction> Script() => ActionScript.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "../../tests/golden/VAR-S1-A_correct.actions.json")));

        [UnityTest] public IEnumerator Saved_checklist_panels_follow_the_full_golden_with_Stop_correction_and_identical_Core_log()
        {
            yield return Boot();
            Assert.AreEqual("PatientBay", _bay.gameObject.scene.name); Assert.AreEqual("OT", _ot.gameObject.scene.name);
            var reference = new Session(_host.Content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(_host.Content));
            string hash = _host.Session.Log.Hash(), state = _host.Observable.Hash();
            for(int i=0;i<20;i++)Sync();
            Assert.AreEqual(hash,_host.Session.Log.Hash()); Assert.AreEqual(state,_host.Observable.Hash());
            Assert.IsNull(_bay.Debrief); Assert.IsTrue(_bay.WaitingForDebrief);
            foreach(var canvas in _bay.GetComponentsInChildren<Canvas>().Concat(_ot.GetComponentsInChildren<Canvas>()))
                Assert.IsNotNull(canvas.GetComponent<TrackedDeviceGraphicRaycaster>(),canvas.name);
            bool stopped=false;
            foreach(var step in Script())
            {
                _host.Tick(step.T-_host.Session.Clock.SimSeconds); reference.Tick(step.T-reference.Clock.SimSeconds);
                var p=step.Action.Params.ToDictionary(k=>k.Key,k=>k.Value);
                if(step.Action.Verb=="checklist_item")
                {
                    _ot.Who.ShowPhase(p["phase"]);
                    if(p["item"]=="sign_in.05")
                    {
                        var wrong=p.ToDictionary(k=>k.Key,k=>k.Value); wrong["response"]="yes";
                        reference.Apply(Action("checklist_item",wrong)); _ot.Who.Answer(p["item"],"yes");
                        Assert.AreEqual("STOPPED",_ot.Who.PhaseText); StringAssert.Contains("STOPPED",_ot.Who.StopText);
                        Assert.Greater(_ot.Who.RowColour(p["item"]).r,_ot.Who.RowColour(p["item"]).g);
                        Photograph("ot-who-stopped.png",new Vector3(12.1f,1.65f,5.2f),_ot.WhoAnchor.position);
                        stopped=true;
                    }
                }
                reference.Apply(Action(step.Action.Verb,p));
                switch(step.Action.Verb)
                {
                    case "tick_ward_item": Assert.IsTrue(_bay.Ward.Verify(p["item"]).Accepted); break;
                    case "sign_ward_checklist": Assert.IsTrue(_bay.Ward.Sign().Accepted); break;
                    case "checklist_item": Assert.IsTrue(_ot.Who.Answer(p["item"],p["response"]).Accepted,p["item"]); break;
                    case "complete_time_out": Assert.IsTrue(_ot.Who.CompleteTimeOut().Accepted); break;
                    default: _host.Apply(Action(step.Action.Verb,p)); break;
                }
                if(step.Action.Verb=="complete_time_out")break;
            }
            Assert.IsTrue(stopped); Assert.AreEqual(reference.Log.Hash(),_host.Session.Log.Hash());
            Assert.AreEqual("COMPLETE",_ot.Who.PhaseText);
            _bay.Ward.RenderNow(); Assert.AreEqual(_host.Content.Ward.Items.Count,_bay.Ward.Lines.Count);
            Photograph("ot-who-complete.png",new Vector3(12.1f,1.65f,5.2f),_ot.WhoAnchor.position);
            Photograph("bay-ward-checklist.png",new Vector3(.15f,1.65f,-.65f),_bay.WardAnchor.position);
            Assert.IsNull(_bay.Debrief,"a running case never constructs the score report");
        }

        [UnityTest] public IEnumerator Finish_reveals_debrief_without_clinical_writes_and_session_reset_clears_it()
        {
            yield return Boot();
            Assert.IsNull(_bay.Debrief); Assert.IsTrue(_bay.WaitingForDebrief);
            var review = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "ProcSense.Editor").GetType("ProcSense.EditorTools.ReviewPanels");
            var previousHuds = Object.FindObjectsByType<ProcSense.VR.Diagnostics.WaitOverlay>(FindObjectsInactive.Include);
            var root = (GameObject)review.GetMethod("Place").Invoke(null, null);
            _reviewRoot = root;
            _reviewHud = Object.FindObjectsByType<ProcSense.VR.Diagnostics.WaitOverlay>(FindObjectsInactive.Include).Single(h => !previousHuds.Contains(h)).gameObject;
            Assert.IsEmpty(root.GetComponentsInChildren<ProcSense.VR.UI.Checklist.ChecklistBoard>());
            Assert.IsEmpty(root.GetComponentsInChildren<ProcSense.VR.UI.Checklist.WardChecklistPanel>());
            Assert.IsEmpty(root.GetComponentsInChildren<DebriefView>(), "review placement must not bypass the permanent debrief gate");
            Photograph("bay-debrief-locked.png",new Vector3(1.0f,1.75f,.6f),_bay.DebriefAnchor.position);
            _host.Apply(Action("ask_identity",new System.Collections.Generic.Dictionary<string,string>()));
            var dir=Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/ClinicalPanels/Sessions/"+Guid.NewGuid().ToString("N")));
            SimulationHost.SessionsDirectoryOverride=dir;
            _originalSync=SessionUploader.Instance.Sync;
            SessionUploader.Instance.Sync=new SessionSync(new UploadQueue(Path.Combine(dir,"outbox"),SessionUploader.UnixNow),()=>null,SessionUploader.UnixNow);
            var before=_host.Session.Log.Hash();var state=_host.Observable.Hash();
            var tab=Object.FindAnyObjectByType<SessionTabView>(FindObjectsInactive.Include);
            tab.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(b=>b.GetComponentInChildren<TMPro.TMP_Text>()?.text=="Finish session and upload").onClick.Invoke();
            Assert.IsTrue(_host.Ended); Assert.IsNotNull(_bay.Debrief,"SessionEnded builds it immediately");
            Assert.IsFalse(_bay.WaitingForDebrief); Assert.IsFalse(_bay.Ward.gameObject.activeInHierarchy); Assert.IsFalse(_ot.Who.gameObject.activeInHierarchy);
            Assert.AreEqual(before,_host.Session.Log.Hash());Assert.AreEqual(state,_host.Observable.Hash());
            Assert.AreEqual("VAR-S1-A",_bay.Debrief.Facts.Case);Assert.IsFalse(_bay.Debrief.AiTabVisible);
            StringAssert.Contains("Session ended before a clinical outcome", _bay.Debrief.HeadlineText);
            Assert.IsNotNull(_bay.Debrief.Canvas.GetComponent<TrackedDeviceGraphicRaycaster>());
            int seq=_host.Session.Log.Entries.Last().Seq; _bay.Debrief.OpenSeq(seq);
            StringAssert.Contains("#"+seq,_bay.Debrief.Drawer.Text);
            Photograph("bay-debrief-finished.png",new Vector3(1.0f,1.75f,.6f),_bay.DebriefAnchor.position);
            var old=_bay.Debrief;
            _host.StartSession("VAR-S1-A",43,SessionMode.Practice); Sync();
            Assert.IsFalse(old.gameObject.activeInHierarchy); Assert.IsNull(_bay.Debrief);Assert.IsTrue(_bay.WaitingForDebrief);
            Assert.IsTrue(_bay.Ward.gameObject.activeInHierarchy); Assert.IsTrue(_ot.Who.gameObject.activeInHierarchy);
            yield return null;
            Assert.AreEqual(0,Object.FindObjectsByType<DebriefView>().Length);
        }
        private static void Photograph(string name,Vector3 eye,Vector3 target)
        {
            Canvas.ForceUpdateCanvases();var go=new GameObject("Clinical station evidence camera");var camera=go.AddComponent<Camera>();
            go.transform.position=eye;go.transform.rotation=Quaternion.LookRotation(target-eye);camera.fieldOfView=55;camera.nearClipPlane=.05f;
            var rt=new RenderTexture(1600,1200,24);var old=RenderTexture.active;var picture=new Texture2D(1600,1200,TextureFormat.RGB24,false);
            try {camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;picture.ReadPixels(new Rect(0,0,1600,1200),0,0);picture.Apply();File.WriteAllBytes(Snapshots.Out(name),picture.EncodeToPNG());}
            finally{camera.targetTexture=null;RenderTexture.active=old;Object.DestroyImmediate(picture);Object.DestroyImmediate(rt);Object.DestroyImmediate(go);}
        }
    }
}
