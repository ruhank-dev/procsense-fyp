using System.Collections.Generic;
using System.Linq;
using ProcSense.VR.Boot;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// Look-neutral culling of the exterior city (P3-PRF-21, owner decision D7). The city ("City exterior" in the corridor scene, "Window exterior backdrop" in the bay) is only ever seen through
    /// a window or through a door, yet its meshes sit in the same visibility space as the hospital and neither frustum culling nor baked occlusion rejects them: about 390,000 triangles were
    /// drawn in the theatre and behind every solid wall (desktop benchmark, docs/qa/quest2_performance_results.md).
    /// Rule, per camera, before it renders: find the room the camera is in (RoomLayout). The city can only be seen from there through that room's window glass or through one of that room's doors
    /// (open or closed: a closed door has a vision pane and the opening shows the next room). If none of those apertures can be inside the camera's view, the city renderers are switched off for
    /// this camera's frame, otherwise they are drawn exactly as authored. The view test is deliberately wider than the headset's (112 deg vertical, aspect 1.15) and the apertures are expanded by
    /// <see cref="Margin"/>. A camera outside every room (a doorway transition) always shows the city.
    /// Nothing is deleted, moved or re-materialled; the authored forceRenderingOff state is remembered and restored; <see cref="Enabled"/> = false (or disabling the component) restores everything.
    /// </summary>
    public sealed class ExteriorVisibility : MonoBehaviour
    {
        private static bool? _enabled;
        /// <summary>On by default on Android only (the Quest tier); desktop and Editor draw the city unconditionally so QA captures and the approved desktop look stay untouched unless a test or the diagnostic build switches it on.</summary>
        public static bool Enabled { get => _enabled ?? Application.isMobilePlatform; set => _enabled = value; }
        public static void ResetToDefault() { _enabled = null; ForceHidden = false; }
        /// <summary>Diagnostic only (PerfToggles, configuration B8): keeps the city off whatever the rule says.</summary>
        public static bool ForceHidden;
        public const float Margin = 0.5f, VerticalFov = 112f, Aspect = 1.15f, DoorReachXZ = 0.9f, DoorReachY = 1.15f, DoorMargin = 0.15f;
        private static readonly string[] CityRoots = { "City exterior", "Window exterior backdrop" };
        private static readonly string[] WindowMaterials = { "window glass", "glass wall" };   // "Corridor window glass", "PatientBay window glass", "Corridor glass wall"

        private readonly List<Renderer> _city = new List<Renderer>();
        private readonly List<bool> _authoredOff = new List<bool>();
        private readonly List<RoomLayout> _rooms = new List<RoomLayout>();
        private readonly List<List<Bounds>> _apertures = new List<List<Bounds>>();     // per room: window glass and door openings
        private readonly Plane[] _planes = new Plane[6];
        private int _indexedScenes = -1;
        public static bool CityHiddenNow { get; private set; }
        public static string Report { get; private set; } = "";
        public static string LastDecision { get; private set; } = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindAnyObjectByType<ExteriorVisibility>() != null) return;
            var go = new GameObject("Exterior visibility") { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<ExteriorVisibility>();
        }

        private void OnEnable() => RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        private void OnDisable() { RenderPipelineManager.beginCameraRendering -= OnBeginCamera; Apply(false); }

        private void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.VR) return;
            Index();
            if (_city.Count == 0) return;
            Apply(ForceHidden || (Enabled && CityCannotBeSeen(cam)));
        }

        private void Apply(bool hide)
        {
            for (int i = 0; i < _city.Count; i++) if (_city[i] != null) _city[i].forceRenderingOff = hide || _authoredOff[i];
            CityHiddenNow = hide;
        }

        private bool CityCannotBeSeen(Camera cam)
        {
            var pos = cam.transform.position; int room = -1;
            for (int i = 0; i < _rooms.Count; i++) if (_rooms[i] != null && _rooms[i].Contains(pos, 0f)) { room = i; break; }
            if (room < 0) { LastDecision = "no room"; return false; }                      // between rooms: draw everything
            var proj = Matrix4x4.Perspective(VerticalFov, Aspect, 0.05f, 3000f);
            var view = Matrix4x4.TRS(pos, cam.transform.rotation, new Vector3(1, 1, -1)).inverse;
            GeometryUtility.CalculateFrustumPlanes(proj * view, _planes);
            for (int k = 0; k < _apertures[room].Count; k++) if (GeometryUtility.TestPlanesAABB(_planes, _apertures[room][k])) { LastDecision = _rooms[room].gameObject.scene.name + " aperture " + k + " " + _apertures[room][k]; return false; }
            LastDecision = _rooms[room].gameObject.scene.name + " none"; return true;
        }

        /// <summary>Finds the city renderers, rooms, window glass and doors again whenever scenes were loaded or unloaded.</summary>
        private void Index()
        {
            if (SceneManager.sceneCount == _indexedScenes) return;
            _indexedScenes = SceneManager.sceneCount;
            Apply(false); _city.Clear(); _authoredOff.Clear(); _rooms.Clear(); _apertures.Clear();
            _rooms.AddRange(FindObjectsByType<RoomLayout>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
            foreach (var _ in _rooms) _apertures.Add(new List<Bounds>());
            // the room whose floor plan holds the point deepest (glass in a shared wall sits on the boundary of two plans: the deeper one is the room it belongs to)
            int RoomOf(Vector3 p)
            {
                int best = -1; float bestDepth = -1.5f;
                for (int i = 0; i < _rooms.Count; i++)
                {
                    var l = _rooms[i].transform.InverseTransformPoint(p);
                    float depth = Mathf.Min(_rooms[i].Size.x / 2 - Mathf.Abs(l.x), _rooms[i].Size.y / 2 - Mathf.Abs(l.z));
                    if (depth > bestDepth && l.y > -.5f && l.y < 3.5f) { best = i; bestDepth = depth; }
                }
                return best;
            }
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (r is ParticleSystemRenderer) continue;
                if (UnderCity(r.transform)) { _city.Add(r); _authoredOff.Add(r.forceRenderingOff); continue; }
                if (!r.gameObject.activeInHierarchy || !IsWindowGlass(r)) continue;
                int room = RoomOf(r.bounds.center); if (room < 0) continue;
                var b = r.bounds; b.Expand(Margin * 2f); _apertures[room].Add(b);
            }
            Report = string.Join("; ", _rooms.Select((r, i) => r.gameObject.scene.name + " apertures " + _apertures[i].Count));
            foreach (var d in FindObjectsByType<SwingDoor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var c = d.Leaf != null ? d.Leaf.bounds.center : d.transform.position;
                var b = new Bounds(c, new Vector3(2f * (DoorReachXZ + DoorMargin), 2f * (DoorReachY + DoorMargin), 2f * (DoorReachXZ + DoorMargin)));   // the opening plus the swing of the leaf
                // a door belongs to both rooms it connects: the leaf's own position may lie inside either layout's inset
                Report += " | door " + d.Connection + " " + d.RoomA + "-" + d.RoomB;
                for (int i = 0; i < _rooms.Count; i++) if (_rooms[i].gameObject.scene.name == d.RoomA || _rooms[i].gameObject.scene.name == d.RoomB) _apertures[i].Add(b);
            }
        }

        private static bool UnderCity(Transform t)
        {
            for (; t != null; t = t.parent) foreach (var n in CityRoots) if (t.name == n) return true;
            return false;
        }

        private static bool IsWindowGlass(Renderer r)
        {
            foreach (var m in r.sharedMaterials)
            {
                if (m == null) continue;
                string n = m.name.ToLowerInvariant();
                foreach (var key in WindowMaterials) if (n.Contains(key)) return true;
            }
            return false;
        }
    }
}
