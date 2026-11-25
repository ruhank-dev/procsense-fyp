using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ProcSense.VR.Views
{
    /// <summary>The bodies and animation controllers for the people who come with the patient (Rocketbox Humanoids).</summary>
    [System.Serializable]
    public sealed class StaffModels { public GameObject Patient, Porter, Nurse, Doctor; public RuntimeAnimatorController Idle, Walk, Sit; [Tooltip("Finished models (Blender, facing the other way round): when set they replace the grey boxes.")] public GameObject Trolley, Wheelchair, Cylinder, Monitor; }

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
        private Transform _label;
        private Vector3 _nurseLast;
        public void Build(TransportPresentation state, bool returning=false, StaffModels staff=null, Transform dressedLike=null)
        {
            Mode = state.Mode;
            HalfSize = new Vector3(.31f, .8f, Mode == "trolley" ? .92f : Mode == "wheelchair" ? .49f : .24f);
            _seatHips = Mode == "wheelchair" && staff?.Wheelchair != null ? .62f : .74f;   // the finished chair's seat is lower than the grey box
            Patient(staff,dressedLike);
            if (Mode == "trolley" && staff?.Trolley != null) Furniture(staff.Trolley, Vector3.zero);
            else if (Mode == "trolley")
            {
                Box("Prototype stretcher mattress", new Vector3(0,.78f,0), new Vector3(.60f,.10f,1.8f), Color.gray);
                Box("Prototype stretcher frame",new Vector3(0,.28f,0),new Vector3(.55f,.10f,1.55f),Color.gray);
                for(int side=-1;side<=1;side+=2)for(int end=-1;end<=1;end+=2)Box("Prototype stretcher support",new Vector3(.23f*side,.52f,.69f*end),new Vector3(.04f,.45f,.04f),Color.gray);
                Wheels(.23f,.69f,.12f);
            }
            else
            {
                bool seated = Mode == "wheelchair";
                if (seated && staff?.Wheelchair != null) Furniture(staff.Wheelchair, Vector3.zero);
                else if(seated)
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
            if (state.Monitor && staff?.Monitor != null) { HalfSize=new Vector3(.4f,HalfSize.y,HalfSize.z); Furniture(staff.Monitor, new Vector3(-.28f,.76f,-.55f)); }   // no readings drawn: Core shows none
            else if (state.Monitor)
            {
                HalfSize=new Vector3(.4f,HalfSize.y,HalfSize.z);
                Box("Monitoring support",new Vector3(-.28f,1.05f,-.55f),new Vector3(.025f,.55f,.025f),Color.gray);
                Box("Monitoring proxy - "+state.MonitoringLevel+" - no invented readings",new Vector3(-.28f,1.38f,-.55f),new Vector3(.2f,.20f,.10f),new Color(.12f,.22f,.24f));
            }
            if (state.Oxygen && staff?.Cylinder != null) Furniture(staff.Cylinder, Mode == "wheelchair" ? new Vector3(0, .30f, -.34f) : new Vector3(0, .12f, -.4f));   // hung behind the chair, or under the trolley between the rails
            else if (state.Oxygen) Part("Oxygen cylinder proxy",PrimitiveType.Capsule,new Vector3(.22f,.42f,-.5f),new Vector3(.13f,.29f,.13f),new Color(.20f,.50f,.40f));
            if (Mode == "trolley" || Mode == "wheelchair") ProcSense.VR.Audio.MovingSound.Attach(gameObject, "eq_trolley_wheels", .5f);
            if (Porter != null) ProcSense.VR.Audio.MovingSound.Attach(Porter.gameObject, "foley_steps_tile", .4f);
            if (_furniture) { Agent = SetUpAgent(); return; }   // finished models: no floating prototype label
            var label=new GameObject("M2 prototype label",typeof(TextMesh)).GetComponent<TextMesh>();
            _label=label.transform;
            label.transform.SetParent(transform,false); label.transform.localPosition=new Vector3(0,1.95f,0);
            label.text="M2 TRANSPORT PROTOTYPE\n"+Mode; label.anchor=TextAnchor.MiddleCenter; label.characterSize=.02f; label.fontSize=40;
            Agent = SetUpAgent();
        }
        private NavMeshAgent SetUpAgent()
        {
            var a=gameObject.AddComponent<NavMeshAgent>(); a.radius=.34f; a.height=1.75f; a.speed=1.1f;
            a.acceleration=3; a.angularSpeed=180; a.stoppingDistance=.06f; a.updatePosition=false; a.updateRotation=false;
            a.obstacleAvoidanceType=ObstacleAvoidanceType.HighQualityObstacleAvoidance; a.avoidancePriority=35;
            return a;
        }
        private bool _furniture;
        private float _seatHips = .74f;
        /// <summary>A finished model as a pure visual: turned round (the Blender export faces the other way), colliders removed.</summary>
        private void Furniture(GameObject model, Vector3 at)
        {
            var g = Instantiate(model, transform); g.name = model.name; g.transform.localPosition = at; g.transform.localRotation = Quaternion.Euler(0, 180, 0);
            foreach (var c in g.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (var r in g.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }   // it moves: Quest has no realtime shadows
            _furniture = true;
            g.AddComponent<WheelSpin>().Setup(transform);
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
        private const float MattressTop = .83f;
        // the patient: lying on the trolley, sitting in the wheelchair, or walking (Walk_Loop while moving)
        private void Patient(StaffModels set, Transform dressedLike)
        {
            var head = new GameObject("Patient head").transform; head.SetParent(transform, false);                 // where the voice comes from
            head.localPosition = Mode == "trolley" ? new Vector3(0, 1.05f, .65f) : new Vector3(0, Mode == "wheelchair" ? 1.35f : 1.60f, 0);
            if (set == null || set.Patient == null) return;
            var body = Instantiate(set.Patient, transform, false); body.name = "Patient body";
            foreach (var c in body.GetComponentsInChildren<Collider>(true)) c.enabled = false;
            foreach (var b in body.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
            if (dressedLike != null)                                                                                    // the same gown as in the bay
                foreach (var r in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    foreach (var o in dressedLike.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (o.name == r.name) { r.sharedMaterials = o.sharedMaterials; break; }
            var anim = body.GetComponent<Animator>() ?? body.AddComponent<Animator>();
            anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.runtimeAnimatorController = Mode == "wheelchair" ? set.Sit : set.Idle;
            if (Mode == "walk") { _crew.Add(new Walker { Root = body.transform, Anim = anim, Last = body.transform.position, Idle = set.Idle, Walk = set.Walk }); return; }
            anim.Update(0f);
            Transform B(HumanBodyBones bone) => anim.GetBoneTransform(bone);
            if (Mode == "wheelchair") { body.transform.position += transform.TransformPoint(new Vector3(0, _seatHips, .04f)) - B(HumanBodyBones.Hips).position; return; }
            LieDown(body.transform, anim);
        }
        // on the back on the mattress, head toward +z, legs together, arms by the sides; the pose is set once and the Animator stays off so it keeps
        private void LieDown(Transform body, Animator anim)
        {
            Transform B(HumanBodyBones bone) => anim.GetBoneTransform(bone);
            var up = (B(HumanBodyBones.Head).position - B(HumanBodyBones.Hips).position).normalized;
            var right = (B(HumanBodyBones.RightUpperLeg).position - B(HumanBodyBones.LeftUpperLeg).position).normalized;
            var face = Vector3.Cross(right, up).normalized;                                                         // Unity is left-handed: right x up = forward
            body.rotation = Quaternion.LookRotation(transform.up, transform.forward) * Quaternion.Inverse(Quaternion.LookRotation(face, up)) * body.rotation;
            body.position += transform.TransformPoint(new Vector3(0, MattressTop + .10f, -.05f)) - B(HumanBodyBones.Hips).position;
            Vector3 Local(Transform t) => transform.InverseTransformPoint(t.position);
            float hipsZ = Local(B(HumanBodyBones.Hips)).z;
            foreach (var (upper, foot) in new[] { (HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftFoot), (HumanBodyBones.RightUpperLeg, HumanBodyBones.RightFoot) })
            {
                var u = B(upper); var f = B(foot); float len = (f.position - u.position).magnitude; float side = Local(f).x >= 0 ? 1 : -1;
                u.rotation = Quaternion.FromToRotation(f.position - u.position, transform.TransformPoint(new Vector3(side * .10f, MattressTop + .045f, hipsZ - len * .98f)) - u.position) * u.rotation;
            }
            foreach (var (upper, lower, hand) in new[] { (HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand), (HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand) })
            {
                var u = B(upper); var e = B(lower); var h = B(hand); float lenU = (e.position - u.position).magnitude, lenF = (h.position - e.position).magnitude; float side = Local(u).x >= 0 ? 1 : -1;
                u.rotation = Quaternion.FromToRotation(e.position - u.position, transform.TransformPoint(new Vector3(side * .24f, MattressTop + .05f, Local(u).z - lenU * .96f)) - u.position) * u.rotation;
                e.rotation = Quaternion.FromToRotation(h.position - e.position, transform.TransformPoint(new Vector3(side * .21f, MattressTop + .05f, Local(e).z - lenF * .98f)) - e.position) * e.rotation;
            }
            anim.enabled = false;
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
            root.gameObject.AddComponent<CharacterPresenter>().Setup(name.Split(' ')[0].ToLowerInvariant(), Bone(body.transform,"Bip01 Head"));      // "Porter prototype" is the porter
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
