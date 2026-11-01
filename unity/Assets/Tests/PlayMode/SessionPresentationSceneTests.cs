using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.VR.Audio;
using ProcSense.VR.Bridge;
using ProcSense.VR.Records;
using ProcSense.VR.Sync;
using ProcSense.VR.Views;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
namespace ProcSense.Tests.PlayMode
{
    public sealed class SessionPresentationSceneTests
    {
        private SimulationHost _host;
        private SessionPresentation _view;
        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<AudioClip> _clips = new List<AudioClip>();
        private string _originalDirectory;
        private SessionSync _originalSync;
        private float _originalScale;
        private sealed class Clips : IVoiceClips
        {
            public AudioClip Clip;
            public AudioClip Find(string id) => id == "TEST-MISSING" ? null : Clip;
        }
        [SetUp] public void Setup()
        {
            ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride=false;
            _view=null;_originalSync=null;
            _originalScale=Time.timeScale;Time.timeScale=1;
            _originalDirectory=SimulationHost.SessionsDirectoryOverride;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(_view!=null)_view.enabled=false;
            foreach(var go in _objects)if(go!=null)Object.DestroyImmediate(go);
            _objects.Clear();
            foreach(var clip in _clips)if(clip!=null)Object.DestroyImmediate(clip);
            _clips.Clear();
            if(_originalSync!=null&&SessionUploader.Instance!=null)SessionUploader.Instance.Sync=_originalSync;
            SimulationHost.SessionsDirectoryOverride=_originalDirectory;
            Time.timeScale=_originalScale;
            ProcSense.VR.Login.DemoLoginSettings.LoginRequiredOverride=null;
            yield return EditorXRTestEnvironment.Cleanup();
        }
        private AudioClip Clip(float seconds)
        {
            var clip=AudioClip.Create("silent presentation fixture",Mathf.CeilToInt(seconds*44100),1,44100,false);_clips.Add(clip);return clip;
        }
        private IEnumerator Boot()
        {
            yield return SceneManager.LoadSceneAsync("Boot");yield return EditorXRTestEnvironment.EnsureSimulator();
            _host=SimulationHost.Instance;_host.AutoTick=false;_host.SaveLogOnDestroy=false;
            _host.StartSession("VAR-S1-A",42,SessionMode.Practice);
            _view=Object.FindAnyObjectByType<SessionPresentation>();Assert.IsNotNull(_view);_view.Sync();
            Assert.IsNotNull(_view.Wait);Assert.IsNotNull(_view.Voice);
            _view.Voice.Clips=new Clips{Clip=Clip(.1f)};
            yield return null;
        }
        private SimAction Action(string verb,params(string k,string v)[] p)=>new SimAction(verb,"patient",Channel.Vr,p.ToDictionary(x=>x.k,x=>x.v));
        private ActionOutcome Do(string verb,params(string k,string v)[] p)=>_host.Apply(Action(verb,p));
        private void Identify(){foreach(var verb in new[]{"introduce_self","ask_identity","read_wristband","open_notes","confirm_identity"})Assert.IsTrue(Do(verb).Accepted,verb);}

        [UnityTest] public IEnumerator One_voice_owner_follows_camera_replacement_and_reuses_the_tablet_wait_card()
        {
            yield return Boot();
            string hash=_host.Session.Log.Hash(),state=_host.Observable.Hash();
            var voice=_view.Voice;var wait=_view.Wait;
            for(int i=0;i<20;i++)_view.Sync();Assert.AreSame(voice,_view.Voice);
            Assert.IsNotNull(wait.GetComponent<TabletView>());Assert.IsTrue(voice.Subtitles.transform.IsChildOf(_view.transform));
            Assert.AreNotSame(voice.transform,voice.Subtitles.transform,"moving subtitles cannot move pooled audio sources");
            Assert.IsTrue(wait.Canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).All(g=>!g.raycastTarget));
            Assert.IsTrue(voice.Subtitles.Canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).All(g=>!g.raycastTarget));
            var review=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="ProcSense.Editor").GetType("ProcSense.EditorTools.ReviewPanels");
            for(int i=0;i<2;i++)_objects.Add((GameObject)review.GetMethod("Place").Invoke(null,null));
            yield return null;
            Assert.AreEqual(1,Object.FindObjectsByType<VoicePlayer>().Length);Assert.AreEqual(1,Object.FindObjectsByType<DiagnosticWaitOverlay>().Length);
            var duplicate=new GameObject("Duplicate presenter fixture");_objects.Add(duplicate);
            Assert.IsFalse(duplicate.AddComponent<SessionPresentation>().enabled);Assert.AreSame(_view,SessionPresentation.Instance);
            var original=Camera.main;string tag=original.tag;bool enabled=original.enabled;
            var go=new GameObject("Replacement camera fixture");_objects.Add(go);var replacement=go.AddComponent<Camera>();
            try
            {
                original.tag="Untagged";original.enabled=false;replacement.tag="MainCamera";
                replacement.transform.SetPositionAndRotation(new Vector3(.2f,1.8f,-.4f),Quaternion.Euler(10,25,0));_view.Sync();
                Assert.AreSame(voice,_view.Voice);Assert.AreSame(replacement.transform,voice.Subtitles.Follow);
                Assert.AreSame(wait,_view.Wait,"Camera replacement must reuse the tablet-owned wait card");
            }
            finally{replacement.tag="Untagged";original.tag=tag;original.enabled=enabled;_view.Sync();}
            // Review fallback creates only speech; the tablet continues to own exactly one wait card.
            _view.enabled=false;
            for(int i=0;i<2;i++)_objects.Add((GameObject)review.GetMethod("Place").Invoke(null,null));
            yield return null;
            var fallback=_objects.Last();
            Assert.IsEmpty(fallback.GetComponentsInChildren<DiagnosticWaitOverlay>());
            Assert.AreEqual(1,Object.FindObjectsByType<DiagnosticWaitOverlay>().Length);
            Assert.AreEqual(1,Object.FindObjectsByType<VoicePlayer>().Length);
            fallback.SetActive(false);_view.enabled=true;_view.Sync();
            Assert.AreEqual(hash,_host.Session.Log.Hash());Assert.AreEqual(state,_host.Observable.Hash());
        }

        [UnityTest] public IEnumerator Core_wait_is_nonmodal_and_announces_results_without_changing_truth()
        {
            yield return Boot();Identify();
            Assert.IsTrue(Do("order_investigation",("test","fbc")).Accepted);Assert.IsTrue(Do("take_sample",("test","fbc"),("tube","edta_purple")).Accepted);
            Assert.IsTrue(Do("request_transport",("to","ct"),("mode","trolley"),("escort","porter,nurse"),("monitoring","none"),("oxygen","false"),("accompany","false")).Accepted);
            _view.Wait.RenderNow();
            for(int i=0;i<600&&_host.Observable.Transport?.State!="ARRIVED";i++){_host.Tick(15);_view.Wait.RenderNow();}
            Assert.AreEqual("ARRIVED",_host.Observable.Transport.State);Assert.IsTrue(_view.Wait.Canvas.gameObject.activeSelf);
            StringAssert.Contains("CT scanner",_view.Wait.LocationText);
            string hash=_host.Session.Log.Hash(),state=_host.Observable.Hash();
            for(int i=0;i<20;i++){_view.Sync();_view.Wait.RenderNow();}
            Assert.AreEqual(hash,_host.Session.Log.Hash());Assert.AreEqual(state,_host.Observable.Hash());
            yield return null;Photograph("hud-wait-full.png");
            var emr=Object.FindAnyObjectByType<RecordsWorkstation>().Emr;
            Assert.IsTrue(emr.Find("khan").Accepted,"other records work remains available during waiting");
            Assert.AreEqual(1,Object.FindObjectsByType<DiagnosticWaitOverlay>().Length);
            Photograph("hud-wait-corner.png");
            Assert.IsTrue(Do("time_skip",("minutes","1")).Accepted);StringAssert.Contains("Time advanced",_view.Wait.AdvanceText);
            for(int i=0;i<40&&!_host.Observable.Investigations.Any(x=>x.Test=="fbc"&&x.State=="RESULT_AVAILABLE");i++){_host.Tick(300);_view.Wait.RenderNow();}
            StringAssert.Contains("result ready",_view.Wait.AlertText);Photograph("hud-result-note.png");
        }

        [UnityTest] public IEnumerator Real_frame_subtitles_keep_their_minimum_and_audio_tracks_the_real_head()
        {
            yield return Boot();
            var voice=_view.Voice;voice.Clips=new Clips{Clip=Clip(6)};
            string hash=_host.Session.Log.Hash(),state=_host.Observable.Hash();
            var head=Object.FindAnyObjectByType<PatientIdleView>().Head;Assert.AreSame(head,voice.Heads["patient"]);
            voice.Say(new VoiceLine{Id="TEST-LONG",Speaker="patient",Text="Presentation test line."});
            var source=voice.GetComponentsInChildren<AudioSource>().Single(a=>a.isPlaying);
            var position=head.localPosition;
            try{head.localPosition+=Vector3.right*.05f;yield return null;Assert.Less(Vector3.Distance(source.transform.position,head.position),.001f);}
            finally{head.localPosition=position;}
            voice.Tick(10);voice.Clips=new Clips{Clip=Clip(.1f)};
            voice.Say(new VoiceLine{Id="TEST-SHORT",Speaker="patient",Text="Hello."});
            yield return new WaitForSeconds(1.2f);
            Assert.IsTrue(voice.Subtitles.Showing,"an uninterrupted two-second subtitle must not expire in one second");
            Photograph("hud-subtitles.png");
            yield return new WaitForSeconds(1.05f);Assert.IsFalse(voice.Subtitles.Showing);
            Assert.AreEqual(hash,_host.Session.Log.Hash());Assert.AreEqual(state,_host.Observable.Hash());
        }

        [UnityTest] public IEnumerator Missing_audio_and_missing_heads_keep_the_words_without_spurious_spatial_audio()
        {
            yield return Boot();var voice=_view.Voice;
            LogAssert.Expect(LogType.Warning,new Regex("TEST-MISSING has no audio: subtitle only"));
            voice.Say(new VoiceLine{Id="TEST-MISSING",Speaker="patient",Text="Missing audio fixture."});
            StringAssert.Contains("Missing audio fixture.",voice.Subtitles.Text);CollectionAssert.Contains(voice.Missing,"TEST-MISSING");
            voice.Tick(10);
            LogAssert.Expect(LogType.Warning,new Regex("TEST-UNBOUND has no speaker anchor: subtitle only"));
            voice.Say(new VoiceLine{Id="TEST-UNBOUND",Speaker="fixture_unbound",Text="Unbound speaker fixture."});
            CollectionAssert.Contains(voice.Unanchored,"TEST-UNBOUND");StringAssert.Contains("Unbound speaker fixture.",voice.Subtitles.Text);
            Assert.IsTrue(voice.GetComponentsInChildren<AudioSource>().All(a=>!a.isPlaying));
        }

        [UnityTest] public IEnumerator Session_reset_detaches_old_listeners_and_finish_clears_speech_and_status()
        {
            yield return Boot();var oldVoice=_view.Voice;var oldWait=_view.Wait;
            oldVoice.Clips=new Clips{Clip=Clip(6)};oldVoice.Say(new VoiceLine{Id="TEST-OLD",Speaker="patient",Text="Old session fixture."});
            oldVoice.Say(new VoiceLine{Id="TEST-QUEUED",Speaker="patient",Text="Queued old fixture."});
            _host.StartSession("VAR-S1-A",43,SessionMode.Practice);_view.Sync();
            Assert.IsFalse(oldVoice.gameObject.activeInHierarchy);Assert.AreSame(oldWait,_view.Wait,"The tablet owns the retained card across sessions");_view.Wait.RenderNow();
            Assert.IsTrue(oldVoice.GetComponentsInChildren<AudioSource>(true).All(a=>!a.isPlaying));
            Assert.IsEmpty(_view.Voice.Played);Assert.IsEmpty(_view.Voice.Queue);Assert.IsFalse(_view.Wait.Canvas.gameObject.activeSelf);
            _view.Voice.Clips=new Clips{Clip=Clip(.1f)};
            int count=oldVoice.Played.Count+oldVoice.Queue.Count;
            var action=Action("ask_identity");var outcome=_host.Apply(action);Assert.IsTrue(outcome.Accepted);
            var expected=_view.Voice.Catalog.For("VAR-S1-A",action,outcome).Select(l=>l.Id).ToList();
            CollectionAssert.AreEquivalent(expected,_view.Voice.Played.Concat(_view.Voice.Queue.Select(l=>l.Id)).ToList());
            Assert.AreEqual(count,oldVoice.Played.Count+oldVoice.Queue.Count,"old host listeners were detached");
            Object.DestroyImmediate(_host.gameObject);_view.Sync();Assert.IsNull(_view.Wait);Assert.IsNull(_view.Voice);
            var hostObject=new GameObject("Replacement host fixture");_objects.Add(hostObject);
            _host=hostObject.AddComponent<SimulationHost>();_host.AutoTick=false;_host.SaveLogOnDestroy=false;
            _view.Sync();Assert.IsNull(_view.Voice,"a host without a session creates no view");
            _host.StartSession("VAR-S1-A",44,SessionMode.Practice);_view.Sync();
            Assert.IsNotNull(_view.Voice);_view.Voice.Clips=new Clips{Clip=Clip(.1f)};
            Assert.IsTrue(Do("ask_identity").Accepted);
            var dir=Path.GetFullPath(Path.Combine(Application.dataPath,"../Logs/SessionPresentation/Sessions/"+Guid.NewGuid().ToString("N")));
            SimulationHost.SessionsDirectoryOverride=dir;_originalSync=SessionUploader.Instance.Sync;
            SessionUploader.Instance.Sync=new SessionSync(new UploadQueue(Path.Combine(dir,"outbox"),SessionUploader.UnixNow),()=>null,SessionUploader.UnixNow);
            var hash=_host.Session.Log.Hash();var state=_host.Observable.Hash();
            Assert.IsNotNull(_host.EndSession());Assert.IsNull(_view.Voice);Assert.IsNull(_view.Wait);
            Assert.AreEqual(hash,_host.Session.Log.Hash());Assert.AreEqual(state,_host.Observable.Hash());
            yield return null;
            Assert.IsEmpty(Object.FindObjectsByType<VoicePlayer>());Assert.IsTrue(Object.FindObjectsByType<DiagnosticWaitOverlay>().All(w=>!w.Canvas.gameObject.activeSelf));
        }
        [UnityTest] public IEnumerator Visible_transport_uses_its_actual_patient_and_escort_heads()
        {
            yield return Boot();Identify();
            Assert.IsTrue(Do("request_transport",("to","ct"),("mode","wheelchair"),("escort","porter,nurse,doctor"),("monitoring","none"),("oxygen","false"),("accompany","false")).Accepted);
            for(int i=0;i<1000&&_host.Observable.Transport?.State!="IN_TRANSIT";i++)_host.Tick(.5);
            Assert.AreEqual("IN_TRANSIT",_host.Observable.Transport.State);
            var transport=Object.FindAnyObjectByType<PatientTransportView>();transport.RenderNow();_view.Sync();
            var actor=transport.Actor;Assert.IsNotNull(actor);
            Assert.AreSame(actor.transform.Find("Patient head"),_view.Voice.Heads["patient"]);
            Assert.AreSame(actor.Nurse.Find("Nurse prototype head"),_view.Voice.Heads["nurse"]);
            Assert.AreSame(actor.Porter.Find("Porter prototype head"),_view.Voice.Heads["porter"]);
            Assert.AreSame(actor.Doctor.Find("Doctor prototype head"),_view.Voice.Heads["clinician"]);
            var hash=_host.Session.Log.Hash();var state=_host.Observable.Hash();
            yield return null;_view.Sync();
            Assert.AreEqual(hash,_host.Session.Log.Hash());Assert.AreEqual(state,_host.Observable.Hash());
        }

        private static void Photograph(string name)
        {
            Canvas.ForceUpdateCanvases();var cam=Camera.main;var oldTarget=cam.targetTexture;var active=RenderTexture.active;
            var rt=new RenderTexture(1600,1200,24);var picture=new Texture2D(1600,1200,TextureFormat.RGB24,false);
            try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;picture.ReadPixels(new Rect(0,0,1600,1200),0,0);picture.Apply();File.WriteAllBytes(Snapshots.Out(name),picture.EncodeToPNG());}
            finally{cam.targetTexture=oldTarget;RenderTexture.active=active;Object.DestroyImmediate(picture);Object.DestroyImmediate(rt);}
        }
    }
}
