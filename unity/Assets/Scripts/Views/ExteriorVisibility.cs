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
        // portal culling of the Quest variant's spatial chunks (P3-EXT-04): only chunks inside the view cone through a window or door of the camera's room are drawn
        public static bool PortalCullEnabled = true;
        public const float EyeOffset = 0.05f, RectPad = 0.04f;           // apex offset per side (half the interpupillary distance plus head movement within a frame) and extra border of the aperture's screen rectangle
        private readonly List<int> _chunk = new List<int>();                // indices into _city of the chunk renderers (those under "Quest variant")
        private readonly List<Bounds> _chunkBounds = new List<Bounds>();
        private readonly List<List<Bounds>> _rawApertures = new List<List<Bounds>>();   // per room: window glass and door openings without the safety margin
        private readonly List<Plane[]> _pyramids = new List<Plane[]>();
        private int _pyramidCount;
        private readonly List<bool> _off = new List<bool>();                // last forceRenderingOff written per city renderer (a property write is only made on change)
        private readonly List<bool> _isChunk = new List<bool>();
        public static bool CollectStats;                                   // diagnostic: triangles inside the camera frustum, with and without the portal test
        public static string StatsLine { get; private set; } = "";
        private readonly List<int> _chunkTris = new List<int>();
        public static int ChunksDrawn { get; private set; }
        public static int ChunksTotal { get; private set; }
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
            bool hide = ForceHidden || (Enabled && CityCannotBeSeen(cam));
            bool portal = !hide && Enabled && PortalCullEnabled && _chunk.Count > 0 && QuestExterior.Current == QuestExterior.Variant.Optimised;
            Apply(hide, portal);
            if (portal) PortalCull(cam);
        }

        /// <summary>Hides the chunks that no window or door of the camera's room can show. Conservative: the view cone is built from both eye positions (plus head movement) through each aperture's padded screen rectangle.</summary>
        private void PortalCull(Camera cam)
        {
            var pos = cam.transform.position; int room = -1;
            for (int i = 0; i < _rooms.Count; i++) if (_rooms[i] != null && _rooms[i].Contains(pos, 0f)) { room = i; break; }
            if (room < 0) { ChunksDrawn = ChunksTotal = _chunk.Count; return; }                       // between rooms: draw every chunk
            _pyramidCount = 0;
            var right = cam.transform.right;
            for (int e = -1; e <= 1; e += 2)
            {
                var apex = pos + right * (EyeOffset * e);
                var proj = Matrix4x4.Perspective(VerticalFov, Aspect, 0.05f, 3000f);
                var viewM = Matrix4x4.TRS(apex, cam.transform.rotation, new Vector3(1, 1, -1)).inverse;
                var vp = proj * viewM;
                float ux0 = 1f, ux1 = -1f, uy0 = 1f, uy1 = -1f;                                           // union of every aperture's screen rectangle: one view cone per eye keeps the per-chunk test to two plane sets
                for (int k = 0; k < _rawApertures[room].Count; k++)
                {
                    var b = _rawApertures[room][k]; float x0 = 1f, x1 = -1f, y0 = 1f, y1 = -1f; int inFront = 0;
                    for (int c = 0; c < 8; c++)
                    {
                        var qa = Clip(vp, b, c);
                        if (qa.w > NearW) { inFront++; Grow(qa, ref x0, ref x1, ref y0, ref y1); }
                        for (int bit = 1; bit <= 4; bit <<= 1)                                               // the 12 edges: the point where an edge crosses the eye plane is part of the visible outline
                        {
                            if ((c & bit) != 0) continue;
                            var qb = Clip(vp, b, c | bit);
                            if ((qa.w > NearW) == (qb.w > NearW)) continue;
                            float t = (NearW - qa.w) / (qb.w - qa.w); Grow(Vector4.Lerp(qa, qb, t), ref x0, ref x1, ref y0, ref y1);
                        }
                    }
                    if (inFront == 0) continue;                                                               // the aperture is entirely behind the eye: nothing can be seen through it
                    x0 = Mathf.Max(-1f, x0 - RectPad); x1 = Mathf.Min(1f, x1 + RectPad); y0 = Mathf.Max(-1f, y0 - RectPad); y1 = Mathf.Min(1f, y1 + RectPad);
                    if (x0 >= x1 || y0 >= y1) continue;                                                       // fully outside the wide cone
                    ux0 = Mathf.Min(ux0, x0); ux1 = Mathf.Max(ux1, x1); uy0 = Mathf.Min(uy0, y0); uy1 = Mathf.Max(uy1, y1);
                }
                if (ux0 < ux1 && uy0 < uy1)
                {
                    float x0 = ux0, x1 = ux1, y0 = uy0, y1 = uy1;
                    var s = Matrix4x4.identity; s.m00 = 2f / (x1 - x0); s.m02 = 0f; s.m03 = -(x1 + x0) / (x1 - x0); s.m11 = 2f / (y1 - y0); s.m13 = -(y1 + y0) / (y1 - y0);
                    if (_pyramidCount >= _pyramids.Count) _pyramids.Add(new Plane[6]);
                    GeometryUtility.CalculateFrustumPlanes(s * vp, _pyramids[_pyramidCount++]);
                }
            }
            int drawn = 0; long trisAll = 0, trisFrustum = 0, trisPortal = 0;
            if (CollectStats) GeometryUtility.CalculateFrustumPlanes(cam, _planes);
            for (int i = 0; i < _chunk.Count; i++)
            {
                var r = _city[_chunk[i]]; if (r == null) continue;
                bool visible = false;
                for (int p = 0; p < _pyramidCount && !visible; p++) visible = GeometryUtility.TestPlanesAABB(_pyramids[p], _chunkBounds[i]);
                if (CollectStats) { trisAll += _chunkTris[i]; bool inF = GeometryUtility.TestPlanesAABB(_planes, _chunkBounds[i]); if (inF) trisFrustum += _chunkTris[i]; if (inF && visible) trisPortal += _chunkTris[i]; }
                if (visible) drawn++;
                Set(_chunk[i], !visible || _authoredOff[_chunk[i]]);
            }
            if (CollectStats) StatsLine = "chunk tris all " + trisAll + " in camera frustum " + trisFrustum + " frustum and portal " + trisPortal + " pyramids " + _pyramidCount;
            ChunksDrawn = drawn; ChunksTotal = _chunk.Count;
        }

        private void Apply(bool hide, bool chunksHandledByPortal = false)
        {
            for (int i = 0; i < _city.Count; i++) if (_city[i] != null && !(chunksHandledByPortal && _isChunk[i])) Set(i, hide || _authoredOff[i]);
            CityHiddenNow = hide;
        }

        // reads before it writes (a write is the expensive call); the flag is read back rather than cached because Unity may reset it when a renderer is enabled or disabled
        private void Set(int i, bool off) { var r = _city[i]; if (r.forceRenderingOff != off) r.forceRenderingOff = off; }

        private const float NearW = 0.02f;
        private static Vector4 Clip(Matrix4x4 vp, Bounds b, int corner)
        {
            var p = b.center + Vector3.Scale(b.extents, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
            return vp * new Vector4(p.x, p.y, p.z, 1f);
        }
        private static void Grow(Vector4 q, ref float x0, ref float x1, ref float y0, ref float y1)
        {
            float nx = q.x / q.w, ny = q.y / q.w; x0 = Mathf.Min(x0, nx); x1 = Mathf.Max(x1, nx); y0 = Mathf.Min(y0, ny); y1 = Mathf.Max(y1, ny);
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
            int signature = SceneManager.sceneCount;                                                      // the handles, not only the count: a reload of the same scenes (Boot, test runs) must index again
            for (int i = 0; i < SceneManager.sceneCount; i++) signature = signature * 31 + SceneManager.GetSceneAt(i).handle.GetHashCode();
            if (signature == _indexedScenes) return;
            _indexedScenes = signature;
            Apply(false); _city.Clear(); _authoredOff.Clear(); _rooms.Clear(); _apertures.Clear(); _chunk.Clear(); _chunkBounds.Clear(); _chunkTris.Clear(); _off.Clear(); _isChunk.Clear(); _rawApertures.Clear();
            _rooms.AddRange(FindObjectsByType<RoomLayout>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
            foreach (var _ in _rooms) { _apertures.Add(new List<Bounds>()); _rawApertures.Add(new List<Bounds>()); }
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
                if (UnderCity(r.transform)) { if (IsChunk(r.transform)) { _chunk.Add(_city.Count); _chunkBounds.Add(WorldBounds(r)); var cmf = r.GetComponent<MeshFilter>(); _chunkTris.Add(cmf != null && cmf.sharedMesh != null ? (int)(cmf.sharedMesh.GetIndexCount(0) / 3) : 0); } _isChunk.Add(IsChunk(r.transform)); _off.Add(r.forceRenderingOff); _city.Add(r); _authoredOff.Add(r.forceRenderingOff); continue; }
                if (!r.gameObject.activeInHierarchy || !IsWindowGlass(r)) continue;
                int room = RoomOf(r.bounds.center); if (room < 0) continue;
                var b = r.bounds; var raw = b; raw.Expand(0.1f); _rawApertures[room].Add(raw); b.Expand(Margin * 2f); _apertures[room].Add(b);
            }
            Report = string.Join("; ", _rooms.Select((r, i) => r.gameObject.scene.name + " apertures " + _apertures[i].Count));
            foreach (var d in FindObjectsByType<SwingDoor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var c = d.Leaf != null ? d.Leaf.bounds.center : d.transform.position;
                var b = new Bounds(c, new Vector3(2f * (DoorReachXZ + DoorMargin), 2f * (DoorReachY + DoorMargin), 2f * (DoorReachXZ + DoorMargin)));   // the opening plus the swing of the leaf
                // the opening seen by the portal test is the closed leaf's plane (indexed while the door is shut); the cubic reach box above is only for the "can the city be seen at all" decision
                var opening = d.Leaf != null && Mathf.Abs(d.Angle) < 1f ? d.Leaf.bounds : b; if (d.Leaf != null && Mathf.Abs(d.Angle) < 1f) opening.Expand(0.3f);
                // a door belongs to both rooms it connects: the leaf's own position may lie inside either layout's inset
                Report += " | door " + d.Connection + " " + d.RoomA + "-" + d.RoomB;
                for (int i = 0; i < _rooms.Count; i++) if (_rooms[i].gameObject.scene.name == d.RoomA || _rooms[i].gameObject.scene.name == d.RoomB) { _apertures[i].Add(b); _rawApertures[i].Add(opening); }
            }
        }

        /// <summary>World bounds from the mesh's local bounds; Renderer.bounds is not reliable for a renderer that is switched off, and the Quest variant's chunks are off while the original city is shown.</summary>
        private static Bounds WorldBounds(Renderer r)
        {
            var lb = r.localBounds; var m = r.transform.localToWorldMatrix; var b = new Bounds(m.MultiplyPoint3x4(lb.center), Vector3.zero);
            for (int c = 0; c < 8; c++) b.Encapsulate(m.MultiplyPoint3x4(lb.center + Vector3.Scale(lb.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1))));
            return b;
        }

        private static bool IsChunk(Transform t) { for (; t != null; t = t.parent) if (t.name == "Quest variant") return true; return false; }

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
