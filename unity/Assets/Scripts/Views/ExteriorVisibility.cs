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
        private static readonly string[] WindowMaterials = { "window glass", "glass wall", "vision glass" };   // "Corridor window glass", "PatientBay window glass", "Corridor glass wall"

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

        // CPU fast path (round 3): the last forceRenderingOff written per renderer is cached and a write is made only on change; the real flags are re-read every ResyncEvery frames and whenever the
        // index, the exterior variant or the mode changes (Unity may reset a flag when a renderer is enabled or disabled). Chunk bounds are tested in managed code (no native call per chunk) and a
        // frame whose pose, room and pads equal the previous one costs nothing. FastPath = false restores the original per-frame behaviour (A/B for CullingCostTests).
        private static bool _fast = true;
        /// <summary>Benchmark hook (null in every build): when set, only this camera runs the exterior and room culling. The desktop two-eye benchmark renders two cameras per frame; the headset renders one multiview camera and culls once, so the benchmark sets this to the left-eye camera to measure the same number of culls.</summary>
        public static Camera OnlyCamera;
        public static bool FastPath { get => _fast; set { _fast = value; _dirtyAll = true; } }
        private const int ResyncEvery = 30;
        private static bool _dirtyAll = true;
        /// <summary>Called when something else may have changed renderer flags (variant switch): the next frame re-reads every flag.</summary>
        public static void MarkDirty() { _dirtyAll = true; }
        private int _applied = -1, _sinceSync;
        private bool _pcValid; private int _pcRoom; private Vector3 _pcPos; private Quaternion _pcRot; private float _pcPad, _pcApex;
        private Vector3[] _cCenter = new Vector3[0], _cExt = new Vector3[0];
        public static bool CityHiddenNow { get; private set; }
        public static string Report { get; private set; } = "";
        public static string LastDecision { get; private set; } = "";

        // Head motion allowance: the pose the headset renders with is predicted and can be a frame or two ahead of the transform read here, so the apex and the aperture rectangles are widened by the
        // rotation and travel measured since the previous frame (capped; a snap turn or teleport is a jump, not a prediction error, so it is capped too). Look-neutral: it only draws more.
        public static bool MotionPadEnabled = true;
        public const float MotionPadMax = 0.15f, MotionApexMax = 0.1f, MotionRectPerRadian = 1.05f;
        private struct PoseTrail { public int Frame; public Vector3 PrevPos, CurPos; public Quaternion PrevRot, CurRot; }
        private static readonly Dictionary<Camera, PoseTrail> Trails = new Dictionary<Camera, PoseTrail>();
        /// <summary>Extra NDC border and extra apex travel for this camera, from its pose change since the previous frame. Idempotent within a frame (several renders of one frame give the same answer).</summary>
        internal static void MotionPad(Camera cam, out float rectPad, out float apexExtra)
        {
            rectPad = 0f; apexExtra = 0f; if (!MotionPadEnabled) return;
            var id = cam; Trails.TryGetValue(id, out var t);
            var pos = cam.transform.position; var rot = cam.transform.rotation;
            if (t.Frame == 0) { t.PrevPos = t.CurPos = pos; t.PrevRot = t.CurRot = rot; }
            else if (t.Frame != Time.frameCount) { t.PrevPos = t.CurPos; t.PrevRot = t.CurRot; }
            t.Frame = Time.frameCount == 0 ? 1 : Time.frameCount; t.CurPos = pos; t.CurRot = rot; Trails[id] = t;
            float ang = Quaternion.Angle(t.PrevRot, t.CurRot) * Mathf.Deg2Rad;
            rectPad = Mathf.Min(ang * MotionRectPerRadian * 1.5f, MotionPadMax);
            apexExtra = Mathf.Min((t.CurPos - t.PrevPos).magnitude * 1.5f, MotionApexMax);
        }

        /// <summary>The instance that indexes rooms and apertures; RoomVisibility reads the same lists so there is one source of truth.</summary>
        public static ExteriorVisibility Instance { get; private set; }
        /// <summary>Incremented every time the room/aperture index is rebuilt.</summary>
        public int IndexVersion { get; private set; }
        public IReadOnlyList<RoomLayout> Rooms => _rooms;
        /// <summary>Window glass, vision glass and door openings of a room, without the large safety margin (the portal-test apertures).</summary>
        public IReadOnlyList<Bounds> RawAperturesOf(int room) => _rawApertures[room];
        /// <summary>Makes sure the index matches the loaded scenes (cheap when nothing changed).</summary>
        public void EnsureIndexed() => Index();
        /// <summary>Index of the room whose footprint holds the point (strict, no inset), or -1 between rooms.</summary>
        public int RoomIndexAt(Vector3 pos)
        {
            for (int i = 0; i < _rooms.Count; i++) if (_rooms[i] != null && _rooms[i].Contains(pos, 0f)) return i;
            return -1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindAnyObjectByType<ExteriorVisibility>() != null) return;
            var go = new GameObject("Exterior visibility") { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<ExteriorVisibility>();
        }

        private bool _sceneDirty = true; private int _idxTick;
        private void MarkScenes(Scene s, LoadSceneMode m) { _sceneDirty = true; }
        private void MarkScenes(Scene s) { _sceneDirty = true; }
        private static readonly Matrix4x4 WideProj = Matrix4x4.Perspective(VerticalFov, Aspect, 0.05f, 3000f);
        private void OnEnable() { Instance = this; _sceneDirty = true; SceneManager.sceneLoaded += MarkScenes; SceneManager.sceneUnloaded += MarkScenes; RenderPipelineManager.beginCameraRendering += OnBeginCamera; }
        private void OnDisable() { SceneManager.sceneLoaded -= MarkScenes; SceneManager.sceneUnloaded -= MarkScenes; RenderPipelineManager.beginCameraRendering -= OnBeginCamera; if (Instance == this) Instance = null; _applied = -1; _dirtyAll = true; Apply(false); }

        private void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (OnlyCamera != null && cam != OnlyCamera) return;   // desktop two-eye benchmark: the headset culls once per frame for both eyes, so only the first eye camera runs the culling
            if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.VR) return;
            long t0 = CullCost.Begin(); OnBeginCameraCore(cam); CullCost.End(CullCost.ExtCamera, t0);
        }

        private void OnBeginCameraCore(Camera cam)
        {
            Index();
            if (_city.Count == 0) return;
            if (FastPath && (_dirtyAll || ++_sinceSync >= ResyncEvery)) Resync();
            bool hide = ForceHidden || (Enabled && CityCannotBeSeen(cam));
            bool portal = !hide && Enabled && PortalCullEnabled && _chunk.Count > 0 && QuestExterior.Current == QuestExterior.Variant.Optimised;
            Apply(hide, portal);
            if (portal) { long t1 = CullCost.Begin(); PortalCull(cam); CullCost.End(CullCost.ExtPortal, t1); }
        }

        /// <summary>Hides the chunks that no window or door of the camera's room can show. Conservative: the view cone is built from both eye positions (plus head movement) through each aperture's padded screen rectangle.</summary>
        private void PortalCull(Camera cam)
        {
            var pos = cam.transform.position; int room = -1;
            for (int i = 0; i < _rooms.Count; i++) if (_rooms[i] != null && _rooms[i].Contains(pos, 0f)) { room = i; break; }
            if (room < 0)                                                                              // between rooms: draw every chunk (they must be switched back on: the previous frame's cone may have hidden them)
            {
                for (int i = 0; i < _chunk.Count; i++) if (_city[_chunk[i]] != null) Set(_chunk[i], _authoredOff[_chunk[i]]);
                ChunksDrawn = ChunksTotal = _chunk.Count; return;
            }
            _pyramidCount = 0;
            var right = cam.transform.right;
            MotionPad(cam, out float motionPad, out float apexExtra);
            float pad = RectPad + motionPad;
            var rotNow = cam.transform.rotation;
            if (FastPath && !CollectStats && _pcValid && _pcRoom == room && _pcPos == pos && _pcRot == rotNow && _pcPad == pad && _pcApex == apexExtra) return;   // identical pose: the chunk states already hold
            _pcValid = FastPath && !CollectStats; _pcRoom = room; _pcPos = pos; _pcRot = rotNow; _pcPad = pad; _pcApex = apexExtra;
            for (int e = -1; e <= 1; e += 2)
            {
                var apex = pos + right * ((EyeOffset + apexExtra) * e) - cam.transform.forward * apexExtra;   // also step back along the view: a cone from further behind contains the cone from the true apex
                var proj = WideProj;
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
                    x0 = Mathf.Max(-1f, x0 - pad); x1 = Mathf.Min(1f, x1 + pad); y0 = Mathf.Max(-1f, y0 - pad); y1 = Mathf.Min(1f, y1 + pad);
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
                if (FastPath) { for (int p = 0; p < _pyramidCount && !visible; p++) visible = InsidePlanes(_pyramids[p], _cCenter[i], _cExt[i]); }
                else for (int p = 0; p < _pyramidCount && !visible; p++) visible = GeometryUtility.TestPlanesAABB(_pyramids[p], _chunkBounds[i]);
                if (CollectStats) { trisAll += _chunkTris[i]; bool inF = GeometryUtility.TestPlanesAABB(_planes, _chunkBounds[i]); if (inF) trisFrustum += _chunkTris[i]; if (inF && visible) trisPortal += _chunkTris[i]; }
                if (visible) drawn++;
                Set(_chunk[i], !visible || _authoredOff[_chunk[i]]);
            }
            if (CollectStats) StatsLine = "chunk tris all " + trisAll + " in camera frustum " + trisFrustum + " frustum and portal " + trisPortal + " pyramids " + _pyramidCount;
            ChunksDrawn = drawn; ChunksTotal = _chunk.Count;
        }

        private void Resync()
        {
            _dirtyAll = false; _sinceSync = 0; _pcValid = false; _applied = -1;   // another script (PerfToggles resets forceRenderingOff on every configuration change) may have rewritten the flags: apply the hide state again instead of trusting the cached state
            for (int i = 0; i < _city.Count; i++) if (_city[i] != null) _off[i] = _city[i].forceRenderingOff;
            if (CullCost.On) CullCost.Reads += _city.Count;
        }

        private void Apply(bool hide, bool chunksHandledByPortal = false)
        {
            if (FastPath && _dirtyAll) Resync();
            int code = (hide ? 1 : 0) | (chunksHandledByPortal ? 2 : 0);
            if (FastPath && code == _applied) { CityHiddenNow = hide; return; }                               // nothing to change: the previous frame already wrote this state
            _applied = code; _pcValid = false;
            for (int i = 0; i < _city.Count; i++) if (_city[i] != null && !(chunksHandledByPortal && _isChunk[i])) Set(i, hide || _authoredOff[i]);
            CityHiddenNow = hide;
        }

        // reads before it writes (a write is the expensive call); the flag is read back rather than cached because Unity may reset it when a renderer is enabled or disabled
        private void Set(int i, bool off)
        {
            var r = _city[i];
            if (FastPath) { if (_off[i] != off) { r.forceRenderingOff = off; _off[i] = off; if (CullCost.On) CullCost.Writes++; } return; }
            if (CullCost.On) CullCost.Reads++; if (r.forceRenderingOff != off) { r.forceRenderingOff = off; if (CullCost.On) CullCost.Writes++; }
        }

        /// <summary>Managed equivalent of GeometryUtility.TestPlanesAABB (no native call): false only when the box lies completely outside one plane.</summary>
        internal static bool InsidePlanes(Plane[] pl, Vector3 c, Vector3 e)
        {
            for (int i = 0; i < 6; i++)
            {
                var n = pl[i].normal; float r = e.x * Mathf.Abs(n.x) + e.y * Mathf.Abs(n.y) + e.z * Mathf.Abs(n.z);
                if (n.x * c.x + n.y * c.y + n.z * c.z + pl[i].distance < -r) return false;
            }
            return true;
        }

        internal const float NearW = 0.02f;
        internal static Vector4 Clip(Matrix4x4 vp, Bounds b, int corner)
        {
            var p = b.center + Vector3.Scale(b.extents, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
            return vp * new Vector4(p.x, p.y, p.z, 1f);
        }
        internal static void Grow(Vector4 q, ref float x0, ref float x1, ref float y0, ref float y1)
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
            if (FastPath && !_sceneDirty && ++_idxTick < 300) return;                                   // scene events mark the index stale; the signature is still re-checked every 300 renders as a safety net
            _sceneDirty = false; _idxTick = 0;
            int signature = SceneManager.sceneCount;                                                      // the handles, not only the count: a reload of the same scenes (Boot, test runs) must index again
            for (int i = 0; i < SceneManager.sceneCount; i++) signature = signature * 31 + SceneManager.GetSceneAt(i).handle.GetHashCode();
            if (signature == _indexedScenes) return;
            _indexedScenes = signature; IndexVersion++;
            Apply(false); _applied = -1; _pcValid = false; _dirtyAll = true; _city.Clear(); _authoredOff.Clear(); _rooms.Clear(); _apertures.Clear(); _chunk.Clear(); _chunkBounds.Clear(); _chunkTris.Clear(); _off.Clear(); _isChunk.Clear(); _rawApertures.Clear();
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
            _cCenter = new Vector3[_chunkBounds.Count]; _cExt = new Vector3[_chunkBounds.Count];
            for (int i = 0; i < _chunkBounds.Count; i++) { _cCenter[i] = _chunkBounds[i].center; _cExt[i] = _chunkBounds[i].extents; }
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

        internal static bool UnderCity(Transform t)
        {
            for (; t != null; t = t.parent) foreach (var n in CityRoots) if (t.name == n) return true;
            return false;
        }

        internal static bool IsWindowGlass(Renderer r)
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
