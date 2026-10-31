using System.Collections;
using System.Linq;
using ProcSense.Core.Observation;
using ProcSense.VR.Boot;
using ProcSense.VR.Bridge;
using ProcSense.VR.XR;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;
namespace ProcSense.VR.Views
{
    /// <summary>Only changes after Core moves an accompanying trainee; initial/session snapshots never warp.</summary>
    public sealed class TransportAccompanyView : StateView
    {
        public string LastError { get; private set; }="";
        private ProcSense.Core.Session _session;
        private string _location;
        private Coroutine _moving;
        private RoomLoader _ownedTransition;
        private SafeTeleportProvider _ownedTeleport;
        protected override void Render(ObservableState state)
        {
            var session=SimulationHost.Instance?.Session;
            if(!ReferenceEquals(session,_session))
            { Cancel();_session=session;_location=state.TraineeLocation;LastError="";return; }
            if(_location==state.TraineeLocation)return;
            _location=state.TraineeLocation;Cancel();
            if(state.Transport==null)return;
            var door=FindObjectsByType<DiagnosticDoorView>().FirstOrDefault(d=>d.DestinationIds.Contains(state.Transport.To));
            if(_location=="patient_bay")_moving=StartCoroutine(Move("PatientBay",null));
            else if(door!=null && (_location=="corridor" || _location==state.Transport.To))_moving=StartCoroutine(Move("Corridor",door));
        }
        protected override void Update()
        {
            base.Update();
            if(!ReferenceEquals(_session,SimulationHost.Instance?.Session)) { Cancel(); _session=null;_location=null; }
        }
        private IEnumerator Move(string room,DiagnosticDoorView door)
        {
            LastError="";var rooms=FindAnyObjectByType<RoomLoader>();var provider=FindAnyObjectByType<SafeTeleportProvider>();
            if(rooms==null || provider==null){LastError="Accompany route unavailable";yield break;}
            float deadline=Time.unscaledTime+5;
            while((rooms.Busy||provider.Busy)&&Time.unscaledTime<deadline)yield return null;
            if(rooms.Busy||provider.Busy){LastError="Accompany route busy; use normal navigation";yield break;}
            if(rooms.CurrentRoom!=room)
            {
                // Every authored room is adjacent via the corridor.
                if(rooms.CurrentRoom=="OT"&&room=="PatientBay")
                {
                    if(!rooms.RequestRoom("Corridor")){LastError=rooms.LastError;yield break;}
                    _ownedTransition=rooms;while(rooms.Busy)yield return null;_ownedTransition=null;
                }
                if(!rooms.RequestRoom(room)){LastError=rooms.LastError;yield break;}
                _ownedTransition=rooms;while(rooms.Busy)yield return null;_ownedTransition=null;
                if(rooms.CurrentRoom!=room){LastError=rooms.LastError;yield break;}
            }
            if(door==null)yield break; // RoomLoader's safe bay anchor is the return destination.
            var actor=FindAnyObjectByType<PatientTransportView>()?.Actor;
            foreach(float offset in new[]{1.05f,-1.05f,1.4f,-1.4f})
            {
                var point=door.OutsideAnchor.position+door.transform.right*offset;
                if(actor!=null&&Vector3.Distance(point,actor.transform.position)<1)continue;
                if(provider.QueueTeleportRequest(new TeleportRequest{destinationPosition=point}))
                { _ownedTeleport=provider; while(provider.Busy)yield return null;_ownedTeleport=null;yield break; }
            }
            LastError="Diagnostic viewing spot blocked; use normal corridor navigation";
        }
        private void Cancel()
        {
            if(_ownedTransition!=null)_ownedTransition.CancelPendingTransition();
            if(_ownedTeleport!=null)_ownedTeleport.CancelPendingTeleport(); _ownedTeleport=null;
            _ownedTransition=null;if(_moving!=null)StopCoroutine(_moving);_moving=null;
        }
        protected override void OnDisable(){base.OnDisable();Cancel();_session=null;_location=null;}
    }
}
