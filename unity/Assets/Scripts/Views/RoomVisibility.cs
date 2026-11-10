using System.Collections.Generic;
using ProcSense.VR.Boot;
using UnityEngine;
using UnityEngine.Rendering;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// Look-neutral room-level portal culling, the deterministic replacement for the baked occlusion data across rooms. The three rooms (PatientBay, Corridor, OT) are loaded together in one world space,
    /// so frustum culling still draws a room that is behind a wall. Rule, per camera, before it renders: find the camera's room (RoomLayout, the same lookup as ExteriorVisibility). The static scenery
    /// of every OTHER room is split into cells (room x 3 m grid); a cell is drawn only when it can be inside the view cone through at least one aperture of the camera's room (window glass, door
    /// vision glass, door opening, open or closed: ExteriorVisibility's aperture list plus the cosmetic transport doors), one cone per eye through each aperture's padded screen rectangle. Everything else
    /// is switched off with Renderer.forceRenderingOff (read before write, authored state remembered and restored). It never touches: the camera's own room, the city, skinned/animated/physics/interactable
    /// objects, doors, glass, particles, UI, anything within <see cref="ShellInset"/> of a room's boundary (walls and floors shared with the next room) or anything outside every room's plan.
    /// A camera between rooms (doorway) shows everything. Lights are not affected, so per-object light selection stays exactly as without this component.
    /// Effective only when <see cref="Enabled"/> and ExteriorVisibility.Enabled are both true (a test that switches the culling off with ExteriorVisibility.Enabled gets the unculled reference).
    /// </summary>
    public sealed class RoomVisibility : MonoBehaviour
    {
        private static bool? _enabled;
        /// <summary>On by default on Android only (the Quest tier), like ExteriorVisibility; desktop and Editor draw every room unless a test or diagnostic switches it on.</summary>
        public static bool Enabled { get => _enabled ?? Application.isMobilePlatform; set => _enabled = value; }
        public static void ResetToDefault() { _enabled = null; }
        public const float ShellInset = 0.35f, CellSize = 3f, GroupMargin = 0.25f, RectPad = 0.06f, ExtraDoorReach = 0.8f;
        private const int ResyncEvery = 30;

        public static int GroupsHidden { get; private set; }
        public static int GroupsTotal { get; private set; }
        public static int RenderersHidden { get; private set; }
        public static string LastDecision { get; private set; } = "";

        private sealed class Group { public int Room; public Bounds Bounds; public bool HasBounds; public readonly List<Renderer> Rs = new List<Renderer>(); public readonly List<bool> Authored = new List<bool>(); public bool Hidden; public Vector3 Center, Ext; }
        private readonly List<Group> _groups = new List<Group>();
        private List<Bounds>[] _extra = new List<Bounds>[0];                 // per room: cosmetic transport door openings (not SwingDoors)
        private readonly List<Plane[]> _pyramids = new List<Plane[]>();
        private int _pyramidCount, _indexedVersion = -1, _roomCount = -1;
        private bool _anyHidden;
        /// <summary>CPU fast path (round 3): managed cell-versus-cone tests and no work for a frame whose pose equals the previous one. false = the original per-frame behaviour (A/B in CullingCostTests).</summary>
        private static bool _fast = true; private static int _ver;
        public static bool FastPath { get => _fast; set { _fast = value; _ver++; } }
        /// <summary>Forgets the cached pose so the next frame recomputes every cell (call after changing renderer flags from outside).</summary>
        public static void InvalidateCache() { _ver++; }
        private int _seenVer;
        private static readonly Matrix4x4 Proj = Matrix4x4.Perspective(ExteriorVisibility.VerticalFov, ExteriorVisibility.Aspect, 0.05f, 3000f);
        private bool _pcValid; private int _pcRoom; private Vector3 _pcPos; private Quaternion _pcRot; private float _pcPad, _pcApex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindAnyObjectByType<RoomVisibility>() != null) return;
            var go = new GameObject("Room visibility") { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<RoomVisibility>();
        }

        private void OnEnable() => RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        private void OnDisable() { RenderPipelineManager.beginCameraRendering -= OnBeginCamera; ShowAll(); }

        private static bool Active => Enabled && ExteriorVisibility.Enabled;

        private void OnBeginCamera(ScriptableRenderContext ctx, Camera cam)
        {
            if (ExteriorVisibility.OnlyCamera != null && cam != ExteriorVisibility.OnlyCamera) return;
            if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.VR) return;
            long t0 = CullCost.Begin(); OnBeginCameraCore(cam); CullCost.End(CullCost.RoomCamera, t0);
        }

        private void OnBeginCameraCore(Camera cam)
        {
            var ex = ExteriorVisibility.Instance;
            if (!Active || ex == null) { ShowAll(); return; }
            ex.EnsureIndexed();
            if (ex.IndexVersion != _indexedVersion || ex.Rooms.Count != _roomCount) Build(ex);
            var pos = cam.transform.position; int room = ex.RoomIndexAt(pos);
            if (room < 0) { LastDecision = "no room"; ShowAll(); return; }                                // between rooms: draw everything

            if (_seenVer != _ver) { _seenVer = _ver; _pcValid = false; }
            ExteriorVisibility.MotionPad(cam, out float motionPad, out float apexExtra);
            var rotNow = cam.transform.rotation;
            if (FastPath && _pcValid && _pcRoom == room && _pcPos == pos && _pcRot == rotNow && _pcPad == motionPad && _pcApex == apexExtra) { ResyncSome(); return; }   // identical pose: the cell states already hold
            _pcValid = FastPath; _pcRoom = room; _pcPos = pos; _pcRot = rotNow; _pcPad = motionPad; _pcApex = apexExtra;
            BuildPyramids(ex, room, cam, pos, motionPad, apexExtra);
            int hidden = 0, hiddenRs = 0;
            for (int i = 0; i < _groups.Count; i++)
            {
                var g = _groups[i]; bool visible = g.Room == room;
                if (!visible)
                {
                    if (FastPath) { for (int p = 0; p < _pyramidCount && !visible; p++) visible = ExteriorVisibility.InsidePlanes(_pyramids[p], g.Center, g.Ext); }
                    else { var b = g.Bounds; b.Expand(GroupMargin * 2f); for (int p = 0; p < _pyramidCount && !visible; p++) visible = GeometryUtility.TestPlanesAABB(_pyramids[p], b); }
                }
                if (visible == !g.Hidden) { if (g.Hidden && (Time.frameCount + i) % ResyncEvery == 0) Write(g, true); }   // unchanged; a hidden cell re-asserts its flags now and then in case Unity reset them
                else Write(g, !visible);
                if (g.Hidden) { hidden++; hiddenRs += g.Rs.Count; }
            }
            _anyHidden = hidden > 0; GroupsHidden = hidden; GroupsTotal = _groups.Count; RenderersHidden = hiddenRs;
            LastDecision = ex.Rooms[room].gameObject.scene.name + " hidden cells " + hidden + "/" + _groups.Count;
        }

        /// <summary>One view cone per eye per aperture of the camera's room: apex at the eye, base is the aperture's padded screen rectangle in a wider-than-headset frustum (the same construction as ExteriorVisibility's chunk cull).</summary>
        private void ResyncSome()
        {
            if (!_anyHidden) return;
            for (int i = 0; i < _groups.Count; i++) if (_groups[i].Hidden && (Time.frameCount + i) % ResyncEvery == 0) Write(_groups[i], true);
        }

        private void BuildPyramids(ExteriorVisibility ex, int room, Camera cam, Vector3 pos, float motionPad, float apexExtra)
        {
            _pyramidCount = 0;
            float pad = RectPad + motionPad; var right = cam.transform.right; var fwd = cam.transform.forward;
            var proj = Proj;
            var raw = ex.RawAperturesOf(room); var extra = room < _extra.Length ? _extra[room] : null;
            int count = raw.Count + (extra != null ? extra.Count : 0);
            for (int e = -1; e <= 1; e += 2)
            {
                var apex = pos + right * ((ExteriorVisibility.EyeOffset + apexExtra) * e) - fwd * apexExtra;
                var vp = proj * Matrix4x4.TRS(apex, cam.transform.rotation, new Vector3(1, 1, -1)).inverse;
                for (int k = 0; k < count; k++)
                {
                    var b = k < raw.Count ? raw[k] : extra[k - raw.Count];
                    if (!ApertureRect(vp, b, pad, out float x0, out float x1, out float y0, out float y1)) continue;
                    var s = Matrix4x4.identity; s.m00 = 2f / (x1 - x0); s.m03 = -(x1 + x0) / (x1 - x0); s.m11 = 2f / (y1 - y0); s.m13 = -(y1 + y0) / (y1 - y0);
                    if (_pyramidCount >= _pyramids.Count) _pyramids.Add(new Plane[6]);
                    GeometryUtility.CalculateFrustumPlanes(s * vp, _pyramids[_pyramidCount++]);
                }
            }
        }

        private static bool ApertureRect(Matrix4x4 vp, Bounds b, float pad, out float x0, out float x1, out float y0, out float y1)
        {
            x0 = 1f; x1 = -1f; y0 = 1f; y1 = -1f; int inFront = 0; const float nearW = ExteriorVisibility.NearW;
            for (int c = 0; c < 8; c++)
            {
                var qa = ExteriorVisibility.Clip(vp, b, c);
                if (qa.w > nearW) { inFront++; ExteriorVisibility.Grow(qa, ref x0, ref x1, ref y0, ref y1); }
                for (int bit = 1; bit <= 4; bit <<= 1)
                {
                    if ((c & bit) != 0) continue;
                    var qb = ExteriorVisibility.Clip(vp, b, c | bit);
                    if ((qa.w > nearW) == (qb.w > nearW)) continue;
                    float t = (nearW - qa.w) / (qb.w - qa.w); ExteriorVisibility.Grow(Vector4.Lerp(qa, qb, t), ref x0, ref x1, ref y0, ref y1);
                }
            }
            if (inFront == 0) return false;
            x0 = Mathf.Max(-1f, x0 - pad); x1 = Mathf.Min(1f, x1 + pad); y0 = Mathf.Max(-1f, y0 - pad); y1 = Mathf.Min(1f, y1 + pad);
            return x0 < x1 && y0 < y1;
        }

        // reads before it writes (a property write is the expensive call)
        private static void Write(Group g, bool hide)
        {
            g.Hidden = hide;
            for (int i = 0; i < g.Rs.Count; i++) { var r = g.Rs[i]; if (r == null) continue; bool off = hide || g.Authored[i]; if (CullCost.On) CullCost.Reads++; if (r.forceRenderingOff != off) { r.forceRenderingOff = off; if (CullCost.On) CullCost.Writes++; } }
        }

        private void ShowAll()
        {
            _pcValid = false;
            if (!_anyHidden) return;
            foreach (var g in _groups) if (g.Hidden) Write(g, false);
            _anyHidden = false; GroupsHidden = RenderersHidden = 0;
        }

        /// <summary>Collects the hideable static scenery of each room into cells, and the cosmetic transport doors as extra apertures.</summary>
        private void Build(ExteriorVisibility ex)
        {
            ShowAll(); _groups.Clear(); _anyHidden = false;
            _indexedVersion = ex.IndexVersion; var rooms = ex.Rooms; _roomCount = rooms.Count;
            _extra = new List<Bounds>[rooms.Count]; for (int i = 0; i < _extra.Length; i++) _extra[i] = new List<Bounds>();
            foreach (var t in FindObjectsByType<TransportDoorSequence>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                bool any = false; Bounds b = default;
                if (t.Leaf != null) foreach (var r in t.Leaf.GetComponentsInChildren<Renderer>(true)) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
                if (!any) b = new Bounds(t.transform.position, Vector3.one);
                b.Expand(ExtraDoorReach);
                for (int i = 0; i < rooms.Count; i++) if (rooms[i] != null && NearFootprint(rooms[i], b.center, ExtraDoorReach)) _extra[i].Add(b);
            }
            var cells = new Dictionary<long, Group>();
            foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (r == null || ExteriorVisibility.UnderCity(r.transform) || ExteriorVisibility.IsWindowGlass(r) || Unsafe(r.transform)) continue;
                int room = -1; var scene = r.gameObject.scene;
                for (int i = 0; i < rooms.Count; i++) if (rooms[i] != null && rooms[i].gameObject.scene == scene) { room = i; break; }
                if (room < 0) continue;
                var wb = WorldBounds(r);
                if (!InsideShrunk(rooms[room], wb)) continue;                                            // near a boundary or outside the plan: shared shell, never hidden
                long key = ((long)room << 40) ^ (((long)Mathf.FloorToInt(wb.center.x / CellSize) & 0xFFFFF) << 20) ^ ((long)Mathf.FloorToInt(wb.center.z / CellSize) & 0xFFFFF);
                if (!cells.TryGetValue(key, out var g)) { g = new Group { Room = room }; cells[key] = g; _groups.Add(g); }
                if (!g.HasBounds) { g.Bounds = wb; g.HasBounds = true; } else g.Bounds.Encapsulate(wb);
                g.Rs.Add(r); g.Authored.Add(r.forceRenderingOff);
            }
            foreach (var g in _groups) { g.Center = g.Bounds.center; g.Ext = g.Bounds.extents + Vector3.one * GroupMargin; }
            GroupsTotal = _groups.Count;
        }

        // Anything that can move, deform, be grabbed, straddle a doorway or belong to UI stays out of the candidate set.
        private static bool Unsafe(Transform t) =>
            t.GetComponentInParent<Rigidbody>(true) != null || t.GetComponentInParent<Animator>(true) != null || t.GetComponentInParent<SwingDoor>(true) != null
            || t.GetComponentInParent<RoomDoor>(true) != null || t.GetComponentInParent<TransportDoorSequence>(true) != null || t.GetComponentInParent<TransportActor>(true) != null
            || t.GetComponentInParent<CharacterController>(true) != null || t.GetComponentInParent<Canvas>(true) != null || t.GetComponentInParent<SkinnedMeshRenderer>(true) != null
            || t.GetComponentInParent<LODGroup>(true) != null;

        private static bool NearFootprint(RoomLayout room, Vector3 world, float reach)
        {
            var l = room.transform.InverseTransformPoint(world);
            return Mathf.Abs(l.x) < room.Size.x / 2 + reach && Mathf.Abs(l.z) < room.Size.y / 2 + reach;
        }

        private static bool InsideShrunk(RoomLayout room, Bounds b)
        {
            for (int c = 0; c < 8; c++)
            {
                var p = b.center + Vector3.Scale(b.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                var l = room.transform.InverseTransformPoint(p);
                if (Mathf.Abs(l.x) > room.Size.x / 2 - ShellInset || Mathf.Abs(l.z) > room.Size.y / 2 - ShellInset) return false;
            }
            return true;
        }

        private static Bounds WorldBounds(Renderer r)
        {
            var lb = r.localBounds; var m = r.transform.localToWorldMatrix; var b = new Bounds(m.MultiplyPoint3x4(lb.center), Vector3.zero);
            for (int c = 0; c < 8; c++) b.Encapsulate(m.MultiplyPoint3x4(lb.center + Vector3.Scale(lb.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1))));
            return b;
        }
    }
}
