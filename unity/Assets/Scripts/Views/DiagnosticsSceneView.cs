using System.Collections.Generic;
using ProcSense.Core;
using ProcSense.VR.Bridge;
using TMPro;
using UnityEngine;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// The lab-sample tube, the tube station, the porter and the printed report as simple props (greybox, like the rest of the project), driven by <see cref="DiagnosticsSceneText"/> from the
    /// Core diagnostics loop. It is a presentation only: it never applies an action. Tubes sit on three marked spots (bedside tray, tube station, laboratory shelf); the porter is a capsule that is
    /// visible only while a porter carries a sample; the report lies on the desk once delivered and the folder page lists it. Anchors can be assigned to real scene objects; otherwise they are
    /// created beside this object so a scene or a test needs no hand-wiring. The physical placement and the headset check (UX-P0-DIAG) stay with Sannan.
    /// </summary>
    public sealed class DiagnosticsSceneView : MonoBehaviour
    {
        public Transform BedsideTray, TubeStation, LaboratoryShelf, PorterSpot, ReportDesk;
        public IReadOnlyList<DiagnosticsLine> Lines => _lines;
        public string FolderPage => DiagnosticsSceneText.Summary(_lines);

        private readonly List<GameObject> _tubes = new List<GameObject>();
        private List<DiagnosticsLine> _lines = new List<DiagnosticsLine>();
        private GameObject _porter, _report;
        private TMP_Text _folder;
        private Session _session;

        /// <summary>For tests and previews with no running host: read this session instead of the host's.</summary>
        public void Use(Session session) { _session = session; Refresh(); }

        private void Awake() => EnsureProps();

        private void Update() => Refresh();

        public GameObject Porter => _porter;
        public GameObject Report => _report;
        public int VisibleTubes { get { int n = 0; foreach (var t in _tubes) if (t.activeSelf) n++; return n; } }
        public IReadOnlyList<GameObject> Tubes => _tubes;

        public void EnsureProps()
        {
            if (_porter != null) return;
            BedsideTray = BedsideTray != null ? BedsideTray : Spot("Bedside tray", new Vector3(-0.3f, 0.9f, 0f));
            TubeStation = TubeStation != null ? TubeStation : Spot("Tube station", new Vector3(1.8f, 1.1f, 0f));
            LaboratoryShelf = LaboratoryShelf != null ? LaboratoryShelf : Spot("Laboratory shelf", new Vector3(2.6f, 1.0f, 0f));
            PorterSpot = PorterSpot != null ? PorterSpot : Spot("Porter spot", new Vector3(1.0f, 0f, 0.6f));
            ReportDesk = ReportDesk != null ? ReportDesk : Spot("Report desk", new Vector3(0.4f, 0.9f, 0.5f));
            _porter = Prop(PrimitiveType.Capsule, "Porter", PorterSpot, new Vector3(0.5f, 0.9f, 0.5f), new Color(0.25f, 0.5f, 0.35f));
            _report = Prop(PrimitiveType.Cube, "Printed report", ReportDesk, new Vector3(0.21f, 0.004f, 0.297f), new Color(0.95f, 0.95f, 0.9f));
            _porter.SetActive(false);
            _report.SetActive(false);
            var canvas = UiKit.WorldCanvas("Folder page", ReportDesk, new Vector2(500, 260), 0.0006f);
            canvas.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            _folder = UiKit.Label(canvas.transform, "Text", "", new Vector2(12, 10), new Vector2(476, 240), 20);
        }

        private Transform Spot(string name, Vector3 local)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = local;
            return go.transform;
        }

        private static GameObject Prop(PrimitiveType type, string name, Transform parent, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var collider = go.GetComponent<Collider>();
            if (Application.isPlaying) Object.Destroy(collider); else Object.DestroyImmediate(collider);      // props are scenery: nothing to grab or collide with
            go.transform.SetParent(parent, false);
            go.transform.localScale = scale;
            var block = new MaterialPropertyBlock();                                                  // a property block, so no material is instantiated (edit-mode tests, no leaks)
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            go.GetComponent<Renderer>().SetPropertyBlock(block);
            return go;
        }

        public void Refresh()
        {
            var session = SimulationHost.Instance != null ? SimulationHost.Instance.Session : _session;
            if (session?.Diagnostics == null) return;
            EnsureProps();
            _lines = DiagnosticsSceneText.Build(session.Diagnostics.Trips);
            int slot = 0;
            bool porter = false, report = false;
            foreach (var l in _lines)
            {
                porter |= l.PorterVisible;
                report |= l.ReportOnDesk;
                var site = l.Tube == TubeSite.Bedside ? BedsideTray : l.Tube == TubeSite.TubeStation ? TubeStation : l.Tube == TubeSite.Porter ? PorterSpot : l.Tube == TubeSite.Laboratory ? LaboratoryShelf : null;
                if (site == null) continue;
                while (_tubes.Count <= slot) _tubes.Add(Prop(PrimitiveType.Cylinder, "Sample tube " + (_tubes.Count + 1), transform, new Vector3(0.02f, 0.05f, 0.02f), new Color(0.8f, 0.15f, 0.2f)));
                var tube = _tubes[slot++];
                tube.transform.SetParent(site, false);
                tube.transform.localPosition = new Vector3(0.03f * slot, 0.05f, 0f);
            }
            for (int i = 0; i < _tubes.Count; i++) _tubes[i].SetActive(i < slot);
            _porter.SetActive(porter);
            _report.SetActive(report);
            if (_folder != null) _folder.text = FolderPage;
        }
    }
}
