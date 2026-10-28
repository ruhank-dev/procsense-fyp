using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Observation;
using ProcSense.VR.Boot;
using ProcSense.VR.Bridge;
using UnityEngine;
using UnityEngine.AI;

namespace ProcSense.VR.Views
{
    /// <summary>Core-driven presentation only. Cosmetic travel never applies an action or ticks Core.</summary>
    public sealed class PatientTransportView : StateView
    {
        public Vector3 BayStart = new Vector3(1.35f,0,-.8f);
        public Vector3 CorridorClear = new Vector3(4.25f,0,-.8f);
        public Transform BedsidePatient;
        public TransportPresentation Presentation { get; }=new TransportPresentation();
        public TransportActor Actor { get; private set; }
        public string Status { get; private set; }="";
        public string BlockingObject { get; private set; }="";
        public bool AtDoor { get; private set; }
        public bool HiddenInside { get; private set; }
        private readonly Dictionary<Renderer,bool> _bedside = new Dictionary<Renderer,bool>();
        private DiagnosticDoorView _door;
        private TransportDoorSequence _sequence;
        private ProcSense.Core.Session _session;
        private TransportVisualPhase _phase;
        private string _destination="",_mode="";
        private bool _returnFromInside;
        private int _routeLeg;
        private Vector3 DoorStop => _door.OutsideAnchor.position-_door.transform.forward*.55f;
        private Vector3 ApproachLane => new Vector3(CorridorClear.x,0,DoorStop.z);
        private float _passage;
        private float _retry;
        private bool _pathReady;
        private Vector3 _goal;
        private NavMeshObstacle _ownedPlayerObstacle;

        protected override void Render(ObservableState state)
        {
            var session=SimulationHost.Instance?.Session;
            if(!ReferenceEquals(session,_session)) { ResetJourney(); _session=session; }
            Presentation.Observe(state,session);
            if(Presentation.Phase==TransportVisualPhase.Bedside || Presentation.Phase==TransportVisualPhase.Unavailable)
            { ResetJourney(); SetBedside(Presentation.BedsideVisible); _phase=Presentation.Phase; return; }
            SetBedside(false);
            bool changed=_destination!=Presentation.Destination || _mode!=Presentation.Mode;
            if(changed)
            {
                ResetJourney(); SetBedside(false);
                _destination=Presentation.Destination; _mode=Presentation.Mode;
                _door=FindObjectsByType<DiagnosticDoorView>().FirstOrDefault(d=>d.DestinationIds.Contains(_destination));
                if(_door==null) { Status="Transport presentation unavailable: destination has no endpoint"; return; }
                _sequence=_door.GetComponent<TransportDoorSequence>();
            }
            var previous=_phase; _phase=Presentation.Phase;
            if(_phase==TransportVisualPhase.Inside && previous==TransportVisualPhase.Outbound && Actor!=null && !AtDoor) { Destroy(Actor.gameObject); Actor=null; HiddenInside=true; ReleaseBayPassage(); if(_sequence!=null)_sequence.RequestedOpen=false; Status="Patient inside: "+_destination; return; }
            if(_phase==TransportVisualPhase.Inside && Actor==null) { HiddenInside=true; Status="Patient inside: "+_destination; return; }
            if(Actor==null) CreateActor(_phase==TransportVisualPhase.Returning);
            if(Actor==null)return;
            if(_phase==TransportVisualPhase.Returning && previous!=_phase)
            {
                if(previous==TransportVisualPhase.Inside) { _returnFromInside=true; if(Actor.Nurse!=null)Actor.Nurse.gameObject.SetActive(false); if(Actor.Doctor!=null)Actor.Doctor.gameObject.SetActive(false); _passage=0; Actor.transform.position=_door.InsideAnchor.position; Actor.Agent.enabled=false; }
                else { if(!Actor.Agent.enabled) { Actor.Agent.enabled=true; Actor.Agent.Warp(Actor.transform.position); } _routeLeg=Actor.transform.position.x<3.2f?2:1; Goal(_routeLeg==2?BayStart:CorridorClear); }
                HiddenInside=false; AtDoor=false;
            }
        }

        private void CreateActor(bool returning)
        {
            if(_door==null)return;
            var rig=FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>();
            if(rig!=null && rig.GetComponent<NavMeshObstacle>()==null) { _ownedPlayerObstacle=rig.gameObject.AddComponent<NavMeshObstacle>(); _ownedPlayerObstacle.shape=NavMeshObstacleShape.Capsule; _ownedPlayerObstacle.radius=.28f; _ownedPlayerObstacle.height=1.8f; _ownedPlayerObstacle.center=Vector3.up*.9f; _ownedPlayerObstacle.carving=false; }
            var start=returning?DoorStop:BayStart;
            if(!NavMesh.SamplePosition(start,out var hit,.35f,NavMesh.AllAreas)) { Status="Transport route unavailable: no NavMesh at start"; return; }
            var go=new GameObject("M2 Core-driven transport"); go.transform.position=hit.position;
            go.transform.rotation=returning?Quaternion.LookRotation(-_door.transform.forward):Quaternion.Euler(0,90,0);
            Actor=go.AddComponent<TransportActor>(); Actor.Build(Presentation,returning);
            if(!Actor.Agent.Warp(hit.position))
            { Actor.Agent.enabled=false; Status="Transport route unavailable: no NavMesh at start"; return; }
            go.transform.position=hit.position;
            _routeLeg=0; _passage=0; AtDoor=false; HiddenInside=false;
            Goal(returning?ApproachLane:CorridorClear);
        }

        protected override void Update()
        {
            base.Update();
            var current=SimulationHost.Instance?.Session;
            if(!ReferenceEquals(current,_session)) { ResetJourney(); _session=current; Presentation.Clear(); return; }
            if(Actor==null || _door==null)return;
            if(SimulationHost.Instance.Paused || SimulationHost.Instance.Ended) { if(Actor.Agent.enabled&&Actor.Agent.isOnNavMesh)Actor.Agent.isStopped=true; if(Actor.NurseAgent!=null&&Actor.NurseAgent.isOnNavMesh)Actor.NurseAgent.isStopped=true; if(Actor.DoctorAgent!=null&&Actor.DoctorAgent.isOnNavMesh)Actor.DoctorAgent.isStopped=true; return; }
            if(Actor.NurseAgent!=null&&Actor.NurseAgent.isOnNavMesh)Actor.NurseAgent.isStopped=false;

            var label=Actor.GetComponentInChildren<TextMesh>(); if(label!=null)label.text="M2 TRANSPORT PROTOTYPE\n"+Presentation.Mode+"\n"+(Status.Contains("blocked")?"Route blocked — waiting":Status.Contains("unavailable")?"Route unavailable":Status);

            if(_returnFromInside) { StopStaff(); Passage(false); return; }
            if(_phase==TransportVisualPhase.Inside && AtDoor) { StopStaff(); Passage(true); return; }
            FollowStaff(Actor.NurseAgent,Presentation.Mode=="trolley"?2.1f:1.5f,.55f);
            FollowStaff(Actor.DoctorAgent,Presentation.Mode=="trolley"?2.8f:2.2f,-.55f);
            if(!Actor.Agent.enabled || !Actor.Agent.isOnNavMesh)return;
            var agent=Actor.Agent;
            foreach(var door in FindObjectsByType<SwingDoor>())
                door.SetNpcPassage(door.Connection=="BayCorridor" && ((_phase!=TransportVisualPhase.Returning&&(_routeLeg==0||StaffNeedsBayDoor()))||(_phase==TransportVisualPhase.Returning&&_routeLeg==2)));
            if(Time.unscaledTime>=_retry)
            {
                _retry=Time.unscaledTime+.8f;
                var path=new NavMeshPath();
                if(!agent.CalculatePath(_goal,path) || path.status!=NavMeshPathStatus.PathComplete)
                { _pathReady=false; agent.ResetPath();agent.isStopped=true; Status="Transport route blocked; waiting for a clear path"; return; }
                agent.SetPath(path); _pathReady=true;
            }
            if(!_pathReady)return;
            agent.isStopped=false;
            var next=agent.nextPosition; next.y=Actor.transform.position.y;
            var delta=next-Actor.transform.position;
            var rotation=delta.sqrMagnitude>.000001f?Quaternion.RotateTowards(Actor.transform.rotation,Quaternion.LookRotation(delta),Time.deltaTime*160):Actor.transform.rotation;
            if(!MotionClear(next,rotation))
            {
                string obstruction=BlockingObject; if(MotionClear(Actor.transform.position,rotation))Actor.transform.rotation=rotation; BlockingObject=obstruction;
                agent.nextPosition=Actor.transform.position; agent.isStopped=true;
                Status="Transport route blocked by "+BlockingObject+"; waiting for obstacle removal"; return;
            }
            Actor.transform.SetPositionAndRotation(next,rotation); Actor.Animate(delta.magnitude);
            agent.speed=Presentation.Behaviour=="IN_PAIN"?.7f:1.1f;
            Status=_phase==TransportVisualPhase.Returning?"Returning to bedside":"Travelling to "+_destination;
            if(Vector3.Distance(Actor.transform.position,_goal)>.13f)return;
            agent.isStopped=true;
            if(_phase==TransportVisualPhase.Returning)
            {
                if(_routeLeg==0) { _routeLeg=1; Goal(CorridorClear); }
                else if(_routeLeg==1) { _routeLeg=2; Goal(BayStart); }
                else Status="At bedside; awaiting Core return";
            }
            else if(_routeLeg==0) { _routeLeg=1; Goal(ApproachLane); }
            else if(_routeLeg==1) { _routeLeg=2; Goal(DoorStop); }
            else { AtDoor=true; Status="At door; awaiting Core arrival"; }
        }

        private void ReleaseBayPassage() { foreach(var door in FindObjectsByType<SwingDoor>())if(door.Connection=="BayCorridor")door.SetNpcPassage(false); }
        private bool StaffNeedsBayDoor() => new[]{Actor.Nurse,Actor.Doctor}.Any(t=>t!=null&&t.gameObject.activeInHierarchy&&t.position.x<3.4f);
        private void StopStaff()
        {
            foreach(var agent in new[]{Actor.NurseAgent,Actor.DoctorAgent})if(agent!=null&&agent.isOnNavMesh)agent.isStopped=true;
        }
        private void FollowStaff(NavMeshAgent agent,float behind,float side)
        {
            if(agent==null||!agent.isOnNavMesh)return;
            agent.isStopped=false;
            var target=Actor.transform.position-Actor.transform.forward*behind+Actor.transform.right*side;
            var room=FindObjectsByType<RoomLayout>().FirstOrDefault(r=>r.Contains(Actor.transform.position,0));
            if(room!=null)
            {
                var local=room.transform.InverseTransformPoint(target);
                local.x=Mathf.Clamp(local.x,-room.Size.x*.5f+.45f,room.Size.x*.5f-.45f);
                local.z=Mathf.Clamp(local.z,-room.Size.y*.5f+.45f,room.Size.y*.5f-.45f);
                local.y=0;target=room.transform.TransformPoint(local);
            }
            if(NavMesh.SamplePosition(target,out var hit,1,NavMesh.AllAreas))agent.SetDestination(hit.position);
        }
        private bool MotionClear(Vector3 point,Quaternion rotation)
        {
            int count=Mathf.Max(1,Mathf.CeilToInt(Mathf.Max(Vector3.Distance(Actor.transform.position,point)/.08f,Quaternion.Angle(Actor.transform.rotation,rotation)/5)));
            for(int i=1;i<=count;i++)if(!ClearFootprint(Vector3.Lerp(Actor.transform.position,point,(float)i/count),Quaternion.Slerp(Actor.transform.rotation,rotation,(float)i/count)))return false;
            return true;
        }
        public bool ClearFootprint(Vector3 point,Quaternion rotation)
        {
            BlockingObject="";
            if(Actor==null)return false;
            var half=Actor.HalfSize; half.y=.76f;
            foreach(var hit in Physics.OverlapBox(point+Vector3.up*.84f,half,rotation,~0,QueryTriggerInteraction.Ignore))
            {
                if(hit.transform.IsChildOf(Actor.transform))continue;
                if(hit.bounds.max.y<.08f)continue; BlockingObject=hit.name+" ("+hit.transform.parent?.name+")"; return false;
            }
            // The pusher and escort are part of the prototype formation, not clinical interactables.
            foreach(var staff in new[]{Actor.Porter})
            {
                if(staff==null)continue;
                var at=point+rotation*staff.localPosition;
                var obstruction=Physics.OverlapCapsule(at+Vector3.up*.3f,at+Vector3.up*1.45f,.19f,~0,QueryTriggerInteraction.Ignore).FirstOrDefault();
                if(obstruction!=null) { BlockingObject=staff.name+": "+obstruction.name; return false; }
            }
            return true;
        }
        private void Goal(Vector3 goal) { _goal=goal; _retry=0; _pathReady=false; AtDoor=false; if(Actor!=null&&Actor.Agent.enabled&&Actor.Agent.isOnNavMesh)Actor.Agent.isStopped=false; }
        private void Passage(bool entering)
        {
            if(_sequence==null) { Status="Diagnostic leaf unavailable; movement paused"; return; }
            _sequence.RequestedOpen=true;
            if(!_sequence.Open) { if(_sequence.Blocked)Status="Door leaf blocked by trainee; waiting"; return; }
            Actor.Agent.enabled=false;
            var facing=Quaternion.LookRotation(entering?_door.transform.forward:-_door.transform.forward);
            Actor.transform.rotation=Quaternion.RotateTowards(Actor.transform.rotation,facing,Time.deltaTime*160);
            if(Quaternion.Angle(Actor.transform.rotation,facing)>2)return;
            float next=Mathf.MoveTowards(_passage,1,Time.deltaTime*.7f);
            var point=Vector3.Lerp(entering?DoorStop:_door.InsideAnchor.position,entering?_door.InsideAnchor.position:DoorStop,next);
            if(Physics.OverlapCapsule(point+Vector3.up*.4f,point+Vector3.up*1.4f,.34f,~0,QueryTriggerInteraction.Ignore).Any(c=>c is CharacterController)) { Status="NPC passage blocked by trainee; waiting"; return; }
            _passage=next; Actor.transform.position=point;
            if(_passage<1)return;
            _sequence.RequestedOpen=false; _passage=0;
            if(entering) { Destroy(Actor.gameObject); Actor=null; HiddenInside=true; ReleaseBayPassage(); Status="Patient inside: "+_destination; }
            else
            {
                _returnFromInside=false; if(Actor.Nurse!=null) { Actor.Nurse.gameObject.SetActive(true); if(Actor.NurseAgent.isOnNavMesh)Actor.NurseAgent.Warp(DoorStop+Actor.transform.forward*.35f); } Actor.Agent.enabled=true;
                if(Actor.Doctor!=null) { Actor.Doctor.gameObject.SetActive(true); if(Actor.DoctorAgent.isOnNavMesh)Actor.DoctorAgent.Warp(DoorStop+Actor.transform.right*.4f); }
                if(Actor.Agent.Warp(DoorStop)) { _routeLeg=0; Goal(ApproachLane); }
                else { Actor.Agent.enabled=false; Status="Return route unavailable"; }
            }
        }
        private void LateUpdate()
        {
            if(!Presentation.BedsideVisible)SetBedside(false);
        }
        private void SetBedside(bool visible)
        {
            if(BedsidePatient==null) BedsidePatient=FindAnyObjectByType<PatientIdleView>()?.transform;
            if(!visible)
            {
                var hidden=new HashSet<Renderer>();
                if(BedsidePatient!=null) foreach(var renderer in BedsidePatient.GetComponentsInChildren<Renderer>(true))hidden.Add(renderer);
                foreach(var sensor in FindObjectsByType<ProcSense.VR.ActionEmitters.SensorAttachment>().Where(s=>s.Attached))
                    foreach(var renderer in sensor.GetComponentsInChildren<Renderer>(true))hidden.Add(renderer);
                foreach(var renderer in _bedside.Keys.Where(r=>r!=null&&!hidden.Contains(r)).ToArray())
                { renderer.enabled=_bedside[renderer];_bedside.Remove(renderer); }
                foreach(var renderer in hidden)
                { if(!_bedside.ContainsKey(renderer))_bedside[renderer]=renderer.enabled; renderer.enabled=false; }
            }
            else { foreach(var item in _bedside)if(item.Key!=null)item.Key.enabled=item.Value; _bedside.Clear(); }
        }
        private void ResetJourney()
        {
            if(Actor!=null)Destroy(Actor.gameObject); Actor=null;
            if(_sequence!=null)_sequence.Restore();
            foreach(var door in FindObjectsByType<SwingDoor>())door.SetNpcPassage(false);
            SetBedside(true); _door=null; _sequence=null; _destination=_mode="";
            _phase=TransportVisualPhase.Bedside; _routeLeg=0; _returnFromInside=AtDoor=HiddenInside=false; _passage=0; Status="";
        }
        protected override void OnDisable() { base.OnDisable(); ResetJourney(); Presentation.Clear(); if(_ownedPlayerObstacle!=null)Destroy(_ownedPlayerObstacle); _ownedPlayerObstacle=null; }
    }
}
