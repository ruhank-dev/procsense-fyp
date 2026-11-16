using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ProcSense.VR.Views
{
    /// <summary>The bodies and animation controllers for the people who come with the patient (Rocketbox Humanoids).</summary>
    [System.Serializable]
    public sealed class StaffModels { public GameObject Porter, Nurse, Doctor; public RuntimeAnimatorController Idle, Walk; }

    /// <summary>Marked M2 geometry, replaceable with approved character/equipment prefabs.</summary>
    public sealed class TransportActor : MonoBehaviour
    {
        public NavMeshAgent Agent { get; private set; }
        public string Mode { get; private set; }
        public Transform Porter, Nurse, Doctor;
        public NavMeshAgent DoctorAgent { get; private set; }
        private Vector3 _doctorLast;
        public NavMeshAgent NurseAgent { get; private set; }
        public Vector3 HalfSize { get; private set; }
        private readonly List<Material> _materials = new List<Material>();
        private Transform _leftLeg, _rightLeg, _label;
        private float _gait;
        private Vector3 _nurseLast;
        public void Build(TransportPresentation state, bool returning=false, StaffModels staff=null)
        {
            Mode = state.Mode;
            HalfSize = new Vector3(.31f, .8f, Mode == "trolley" ? .92f : Mode == "wheelchair" ? .49f : .24f);
            var patient = new Color(.48f,.69f,.76f);
            if (Mode == "trolley")
            {
                Box("Prototype stretcher mattress", new Vector3(0,.78f,0), new Vector3(.60f,.10f,1.8f), Color.gray);
                Part("Patient lying", PrimitiveType.Capsule, new Vector3(0,.99f,-.08f), new Vector3(.33f,.62f,.25f), patient, new Vector3(90,0,0));
                Part("Patient head", PrimitiveType.Sphere, new Vector3(0,1.01f,.70f), Vector3.one*.26f, patient);
                Box("Prototype stretcher frame",new Vector3(0,.28f,0),new Vector3(.55f,.10f,1.55f),Color.gray);
                for(int side=-1;side<=1;side+=2)for(int end=-1;end<=1;end+=2)Box("Prototype stretcher support",new Vector3(.23f*side,.52f,.69f*end),new Vector3(.04f,.45f,.04f),Color.gray);
                Wheels(.23f,.69f,.12f);
            }
            else
            {
                bool seated = Mode == "wheelchair";
                Part("Patient head", PrimitiveType.Sphere, new Vector3(0,seated?1.35f:1.60f,0), Vector3.one*.26f, patient);
                Part("Patient torso", PrimitiveType.Capsule, new Vector3(0,seated?1.02f:1.15f,0), new Vector3(.34f,seated?.23f:.32f,.25f), patient);
                _leftLeg = Box("Patient left leg", new Vector3(-.1f,seated?.39f:.40f,seated?.25f:0),new Vector3(.13f,.65f,.15f),patient);
                _rightLeg = Box("Patient right leg", new Vector3(.1f,seated?.39f:.40f,seated?.25f:0),new Vector3(.13f,.65f,.15f),patient);
                Box("Patient hips",new Vector3(0,seated?.75f:.78f,seated?.08f:0),new Vector3(.3f,.2f,.25f),patient);
                if(seated)
                {
                    Box("Prototype wheelchair seat",new Vector3(0,.62f,0),new Vector3(.58f,.08f,.55f),Color.gray);
                    Box("Prototype wheelchair back",new Vector3(0,.88f,-.25f),new Vector3(.58f,.48f,.06f),Color.gray);
                    Wheels(.28f,.18f,.26f);
                }
            }
            float back = Mode == "trolley" ? -1.2f : -.75f;
            if (state.Porter)
            {
                Porter = Staff("Porter prototype",new Vector3(0,0,back),staff?.Porter,staff);
                for(int i=0;i<2;i++)
                {
                    float x=i==0?-.23f:.23f;
                    var handle=new GameObject("GRAB_Push_"+(i==0?"Left":"Right")).transform;
                    handle.SetParent(transform,false); handle.localPosition=new Vector3(x,.96f,back+.30f);
                    PushArm(i==0?"L":"R",handle,i==0?-1:1);
                }
            }
            if (state.Nurse)
            {
                Nurse=Staff("Nurse prototype",new Vector3(0,0,back-.65f),staff?.Nurse,staff);
                Nurse.SetParent(null,true); if(returning)Nurse.position=transform.position+transform.forward*.35f; _nurseLast=Nurse.position;
                NurseAgent=Nurse.gameObject.AddComponent<NavMeshAgent>(); NurseAgent.radius=.2f; NurseAgent.height=1.75f; NurseAgent.speed=1.2f; NurseAgent.stoppingDistance=.2f; NurseAgent.avoidancePriority=55;
            }
            if(state.Doctor)
            {
                Doctor=Staff("Doctor prototype",new Vector3(0,0,back-1.3f),staff?.Doctor,staff);
                Doctor.SetParent(null,true); if(returning)Doctor.position=transform.position+transform.right*.4f; _doctorLast=Doctor.position;
                DoctorAgent=Doctor.gameObject.AddComponent<NavMeshAgent>(); DoctorAgent.radius=.2f; DoctorAgent.height=1.75f; DoctorAgent.speed=1.2f; DoctorAgent.stoppingDistance=.2f; DoctorAgent.avoidancePriority=60;
            }
            if (state.Monitor)
            {
                HalfSize=new Vector3(.4f,HalfSize.y,HalfSize.z);
                Box("Monitoring support",new Vector3(-.28f,1.05f,-.55f),new Vector3(.025f,.55f,.025f),Color.gray);
                Box("Monitoring proxy - "+state.MonitoringLevel+" - no invented readings",new Vector3(-.28f,1.38f,-.55f),new Vector3(.2f,.20f,.10f),new Color(.12f,.22f,.24f));
            }
            if (state.Oxygen) Part("Oxygen cylinder proxy",PrimitiveType.Capsule,new Vector3(.22f,.42f,-.5f),new Vector3(.13f,.29f,.13f),new Color(.20f,.50f,.40f));
            var label=new GameObject("M2 prototype label",typeof(TextMesh)).GetComponent<TextMesh>();
            _label=label.transform;
            label.transform.SetParent(transform,false); label.transform.localPosition=new Vector3(0,1.95f,0);
            label.text="M2 TRANSPORT PROTOTYPE\n"+Mode; label.anchor=TextAnchor.MiddleCenter; label.characterSize=.02f; label.fontSize=40;
            Agent=gameObject.AddComponent<NavMeshAgent>(); Agent.radius=.34f; Agent.height=1.75f; Agent.speed=1.1f;
            Agent.acceleration=3; Agent.angularSpeed=180; Agent.stoppingDistance=.06f; Agent.updatePosition=false; Agent.updateRotation=false;
            Agent.obstacleAvoidanceType=ObstacleAvoidanceType.HighQualityObstacleAvoidance; Agent.avoidancePriority=35;
        }
        private void LateUpdate() { Crew(); if(_label!=null && Camera.main!=null) { var facing=Vector3.ProjectOnPlane(_label.position-Camera.main.transform.position,Vector3.up); if(facing.sqrMagnitude>.000001f)_label.rotation=Quaternion.LookRotation(facing,Vector3.up); }
            if(DoctorAgent!=null && DoctorAgent.isOnNavMesh)
            {
                var doctor=Doctor.position;
                if(Physics.CheckCapsule(doctor+Vector3.up*.3f,doctor+Vector3.up*1.45f,.19f,~0,QueryTriggerInteraction.Ignore)) { Doctor.position=_doctorLast;DoctorAgent.nextPosition=_doctorLast; }
                else _doctorLast=doctor;
            }
            if(NurseAgent==null || !NurseAgent.isOnNavMesh)return;
            var point=Nurse.position;
            if(Physics.CheckCapsule(point+Vector3.up*.3f,point+Vector3.up*1.45f,.19f,~0,QueryTriggerInteraction.Ignore))
            { Nurse.position=_nurseLast; NurseAgent.nextPosition=_nurseLast; }
            else _nurseLast=point;
        }
        public void Animate(float distance)
        {
            _gait+=distance*7;
            if(Mode=="walk"&&_leftLeg!=null)
            { _leftLeg.localRotation=Quaternion.Euler(Mathf.Sin(_gait)*18,0,0); _rightLeg.localRotation=Quaternion.Euler(-Mathf.Sin(_gait)*18,0,0); }
        }
        private sealed class Walker { public Transform Root; public Animator Anim; public Vector3 Last; public bool Walking; public RuntimeAnimatorController Idle, Walk; }
        private readonly List<Walker> _crew = new List<Walker>();
        private readonly List<(Transform upper, Transform fore, Transform hand, Transform handle, int side, Transform root)> _pushArms = new List<(Transform, Transform, Transform, Transform, int, Transform)>();
        private static Transform Bone(Transform root, string n) { foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == n) return t; return null; }
        private Transform Staff(string name,Vector3 at,GameObject model,StaffModels set)
        {
            var root=new GameObject(name).transform; root.SetParent(transform,false); root.localPosition=at;
            var head=new GameObject(name+" head").transform; head.SetParent(root,false); head.localPosition=Vector3.up*1.62f;          // where the voice comes from
            if(model==null)return root;
            var body=Instantiate(model,root,false); body.name=name+" body";
            foreach(var c in body.GetComponentsInChildren<Collider>(true))c.enabled=false;
            foreach(var b in body.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=false;
            var anim=body.GetComponent<Animator>()??body.AddComponent<Animator>();
            anim.applyRootMotion=false; anim.cullingMode=AnimatorCullingMode.AlwaysAnimate; anim.runtimeAnimatorController=set.Idle;
            _crew.Add(new Walker{Root=root,Anim=anim,Last=root.position,Idle=set.Idle,Walk=set.Walk});
            return root;
        }
        // the porter's hands on the push handles
        private void PushArm(string side,Transform handle,int sign)
        {
            var body=Porter!=null?Porter.Find("Porter prototype body"):null; if(body==null)return;
            _pushArms.Add((Bone(body,"Bip01 "+side+" UpperArm"),Bone(body,"Bip01 "+side+" Forearm"),Bone(body,"Bip01 "+side+" Hand"),handle,sign,Porter));
        }
        // walk while moving, stand otherwise; then the arms, after the Animator has posed the bones
        private void Crew()
        {
            foreach(var w in _crew)
            {
                if(w.Root==null)continue;
                float speed=(w.Root.position-w.Last).magnitude/Mathf.Max(Time.deltaTime,.0001f); w.Last=w.Root.position;
                bool walk=w.Walking?speed>.05f:speed>.15f;
                if(walk!=w.Walking){w.Walking=walk; w.Anim.runtimeAnimatorController=walk?w.Walk:w.Idle;}
            }
            foreach(var a in _pushArms)
                if(a.upper!=null&&a.fore!=null&&a.hand!=null&&a.handle!=null)
                    TwoBoneArm.Reach(a.upper,a.fore,a.hand,a.handle.position,-a.root.forward+a.root.right*a.side*.5f+Vector3.down*.3f,1);
        }
        private void Wheels(float x,float z,float radius)
        {
            for(int side=-1;side<=1;side+=2) for(int end=-1;end<=1;end+=2)
                Part("Prototype wheel",PrimitiveType.Cylinder,new Vector3(x*side,radius,z*end),new Vector3(radius*2,.035f,radius*2),new Color(.13f,.17f,.18f),new Vector3(0,0,90));
        }
        private Transform Box(string name,Vector3 at,Vector3 scale,Color color)=>Part(name,PrimitiveType.Cube,at,scale,color);
        private Transform Part(string name,PrimitiveType type,Vector3 at,Vector3 scale,Color color,Vector3 euler=default)
        {
            var go=GameObject.CreatePrimitive(type); go.name=name; go.transform.SetParent(transform,false);
            go.transform.localPosition=at; go.transform.localScale=scale; go.transform.localEulerAngles=euler;
            var collider=go.GetComponent<Collider>(); collider.enabled=false; Destroy(collider);
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit")); material.color=color; _materials.Add(material);
            go.GetComponent<Renderer>().sharedMaterial=material; return go.transform;
        }
        private void OnDestroy() { if(Nurse!=null)Destroy(Nurse.gameObject); if(Doctor!=null)Destroy(Doctor.gameObject); foreach(var material in _materials) if(material!=null) Destroy(material); }
    }
}
