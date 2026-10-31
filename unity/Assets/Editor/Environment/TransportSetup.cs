using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.VR.Boot;
using ProcSense.VR.Views;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ProcSense.EditorTools
{
    public static class TransportSetup
    {
        public static object Apply()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning ||
                Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("Preserve unsaved scenes and stop Play/baking before transport setup.");
            foreach(var name in new[]{"PatientBay","Corridor","OT"})
                if(!SceneManager.GetSceneByName(name).isLoaded)EditorSceneManager.OpenScene("Assets/Scenes/"+name+".unity",OpenSceneMode.Additive);
            var bay=SceneManager.GetSceneByName("PatientBay");
            var root=bay.GetRootGameObjects().Single(g=>g.GetComponent<RoomLayout>()!=null);
            var director=root.GetComponent<PatientTransportView>()??root.AddComponent<PatientTransportView>();
            director.CorridorClear=new Vector3(4.25f,0,-.8f);
            if(root.GetComponent<TransportAccompanyView>()==null)root.AddComponent<TransportAccompanyView>();
            director.BedsidePatient=UnityEngine.Object.FindAnyObjectByType<PatientIdleView>()?.transform;
            if(director.BedsidePatient==null)throw new InvalidOperationException("Missing authored patient.");
            var navigation=root.transform.Find("Transport navigation");
            if(navigation==null){navigation=new GameObject("Transport navigation").transform;navigation.SetParent(root.transform,false);}
            var surface=navigation.GetComponent<NavMeshSurface>()??navigation.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects=CollectObjects.All; surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideVoxelSize=true;surface.voxelSize=.05f;
            var markups=new List<NavMeshBuildMarkup>();
            foreach(var door in UnityEngine.Object.FindObjectsByType<SwingDoor>())
                markups.Add(new NavMeshBuildMarkup{root=door.transform,ignoreFromBuild=true});
            foreach(var rig in UnityEngine.Object.FindObjectsByType<Unity.XR.CoreUtils.XROrigin>())
                markups.Add(new NavMeshBuildMarkup{root=rig.transform,ignoreFromBuild=true});
            var bounds=new Bounds(new Vector3(5,1,2),new Vector3(22,8,22));
            var sources=new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(bounds,~0,NavMeshCollectGeometry.PhysicsColliders,0,markups,sources);
            var settings=surface.GetBuildSettings();settings.agentRadius=.34f;settings.agentHeight=1.75f;
            settings.agentClimb=.18f;settings.agentSlope=35;settings.overrideVoxelSize=true;settings.voxelSize=.05f;
            var data=NavMeshBuilder.BuildNavMeshData(settings,sources,bounds,Vector3.zero,Quaternion.identity);
            if(data==null)throw new InvalidOperationException("Transport NavMesh bake failed.");
            const string path="Assets/Data/TransportNavMesh.asset";
            var existing=AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            surface.RemoveData();
            if(existing!=null){EditorUtility.CopySerialized(data,existing);UnityEngine.Object.DestroyImmediate(data);data=existing;EditorUtility.SetDirty(data);}
            else AssetDatabase.CreateAsset(data,path);
            surface.navMeshData=data;surface.AddData();
            var portal=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Prototype/TransportPortal.mat");
            if(portal==null)
            {
                portal=new Material(Shader.Find("Universal Render Pipeline/Unlit"));portal.color=new Color(.025f,.035f,.04f);
                AssetDatabase.CreateAsset(portal,"Assets/Materials/Prototype/TransportPortal.mat");
            }
            foreach(var door in UnityEngine.Object.FindObjectsByType<DiagnosticDoorView>().Where(d=>!d.Laboratory))
            {
                var sequence=door.GetComponent<TransportDoorSequence>()??door.gameObject.AddComponent<TransportDoorSequence>();
                if(sequence.Leaf!=null)continue;
                var architecture=UnityEngine.Object.FindObjectsByType<Transform>().Single(t=>t.name==door.name+" architecture");
                var original=architecture.Find("Door leaf");
                var assembly=new GameObject("NPC sliding leaf").transform;assembly.SetParent(architecture,false);
                var backdrop=GameObject.CreatePrimitive(PrimitiveType.Cube);backdrop.name="Opaque diagnostic threshold";
                backdrop.transform.SetParent(architecture,false);backdrop.transform.localPosition=original.localPosition+Vector3.forward*.022f;
                backdrop.transform.localScale=new Vector3(original.localScale.x*.99f,original.localScale.y*.99f,.005f);
                UnityEngine.Object.DestroyImmediate(backdrop.GetComponent<Collider>());
                backdrop.GetComponent<Renderer>().sharedMaterial=portal;
                foreach(var child in architecture.Cast<Transform>().ToArray())
                    if(child.name=="Door leaf" || child.name=="Kick plate" || child.name=="Pull handle" || child.name=="Handle mounting" ||
                        child.name=="Frosted observation inset" || child.name=="Observation surround" ||
                        child.name=="Door identity plate" || child.name=="Access notice")
                    {
                        child.SetParent(assembly,true);
                        foreach(var renderer in child.GetComponentsInChildren<Renderer>())
                        {
                            GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,0);
                            renderer.lightmapIndex=-1;
                            renderer.lightProbeUsage=UnityEngine.Rendering.LightProbeUsage.BlendProbes;
                        }
                    }
                sequence.Leaf=assembly;
                EditorUtility.SetDirty(sequence);
            }
            foreach(var name in new[]{"PatientBay","Corridor"})
            {
                var scene=SceneManager.GetSceneByName(name);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();
            var routes=UnityEngine.Object.FindObjectsByType<DiagnosticDoorView>().Where(d=>!d.Laboratory).Select(d=>{
                var p=new NavMeshPath();
                bool complete=NavMesh.CalculatePath(director.BayStart,d.OutsideAnchor.position,NavMesh.AllAreas,p)&&p.status==NavMeshPathStatus.PathComplete;
                return new{destination=d.DestinationIds[0],complete,corners=p.corners.Length};
            }).ToArray();
            return new{sources=sources.Count,triangles=NavMesh.CalculateTriangulation().indices.Length,radius=settings.agentRadius,routes};
        }
    }
}
