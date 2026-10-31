using System.Linq;
using UnityEngine;
namespace ProcSense.VR.Views
{
    /// <summary>Cosmetic NPC leaf. The separate architectural barrier always blocks trainees.</summary>
    public sealed class TransportDoorSequence : MonoBehaviour
    {
        public Transform Leaf;
        private Vector3 _closed;
        private float _open;
        public bool RequestedOpen { get; set; }
        public bool Open => _open > .98f;
        public bool Blocked { get; private set; }
        private void Awake() { if(Leaf!=null) _closed=Leaf.localPosition; }
        private void Update()
        {
            if(Leaf==null)return;
            float next=Mathf.MoveTowards(_open,RequestedOpen?1:0,Time.deltaTime*2.5f);
            var displacement=Leaf.parent.TransformVector(Vector3.right*((next-_open)*.92f));
            Blocked=false;
            int count=Mathf.Max(1,Mathf.CeilToInt(displacement.magnitude/.04f));
            foreach(var renderer in Leaf.GetComponentsInChildren<Renderer>())
            {
                if(!renderer.enabled)continue;
                var bounds=renderer.bounds;
                for(int i=1;i<=count;i++)
                    if(Physics.OverlapBox(bounds.center+displacement*((float)i/count),bounds.extents+Vector3.one*.025f,Quaternion.identity,~0,QueryTriggerInteraction.Ignore).Any(c=>c is CharacterController))
                    { Blocked=true;return; }
            }
            _open=next;
            Leaf.localPosition=_closed+Vector3.right*(_open*.92f);
        }
        public void Restore() { RequestedOpen=false; if(!Application.isPlaying) { _open=0;Blocked=false;if(Leaf!=null)Leaf.localPosition=_closed; } }
        private void OnDisable()=>Restore();
    }
}
