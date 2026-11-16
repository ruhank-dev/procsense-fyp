using System.Linq;
using UnityEditor;
using UnityEngine;
namespace ProcSense.EditorTools
{
    /// <summary>One-off probe: which mesh-LOD APIs does this Unity version have (Quest overhaul, Agent D)?</summary>
    public static class MeshLodProbe
    {
        public static void Run()
        {
            string Find(System.Type t) => string.Join(", ", t.GetMembers().Where(m => m.Name.ToLower().Contains("lod")).Select(m => m.Name).Distinct());
            var s = "ModelImporter: " + Find(typeof(ModelImporter)) + "\nMesh: " + Find(typeof(Mesh)) + "\nRenderer: " + Find(typeof(Renderer)) + "\nQualitySettings: " + Find(typeof(QualitySettings)) + "\nSkinnedMeshRenderer: " + Find(typeof(SkinnedMeshRenderer));
            System.IO.File.WriteAllText(@"D:\ProcSenseWork\reports\meshlod_probe.txt", s); Debug.Log(s); EditorApplication.Exit(0);
        }
    }
}
