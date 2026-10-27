using System.Collections.Generic;
using System.Linq;
using ProcSense.Core.Actions;
using ProcSense.Core.Records;
using ProcSense.VR.ActionEmitters;
using ProcSense.VR.Bridge;
using ProcSense.VR.UI.Results;
using ProcSense.VR.UI.Tablet;
using ProcSense.VR.Views;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ProcSense.VR.Records.Folder
{
    /// <summary>
    /// The physical folder's pages (p0-patient-records-emr-s02), on the notes folder next to <see cref="NotesFolderInteraction"/> (which opens the cover). Page 1 is the identifiers page; after it come Core's folder
    /// documents (<see cref="FolderPages"/>), a long one over several pages. A page turns by dragging the page edge sideways past a threshold, or with the arrow buttons under the page (the accessible way); one
    /// page turns at a time, over a fixed time that does not depend on the frame rate, and a turn asked for during another is refused. Reaching a document's page opens it in Core (open_document, view folder), so
    /// the reading is logged. Dropping a printout into the report pocket (SOCKET_Report) files it (file_document); it then becomes a page. Held, the folder tilts towards the reader.
    /// </summary>
    [RequireComponent(typeof(NotesFolderInteraction))]
    public sealed class FolderController : MonoBehaviour
    {
        public const float TurnSeconds = 0.4f, DragThreshold = 0.06f;
        private const float PageWidth = 0.189f, PageDepth = 0.27f, Spine = -0.0915f, Surface = 0.0089f;

        public int PageIndex { get; private set; }
        public int PageCount => 1 + _pages.Count;
        public bool Turning { get; private set; }
        public float TurnProgress { get; private set; }
        public IReadOnlyList<FolderPage> Pages => _pages;
        public string HeadingText => _heading != null ? _heading.text : "";
        public string PageText => _body != null ? _body.text : "";
        public string FooterText => _footer != null ? _footer.text : "";
        public ReportPocket Pocket { get; private set; }
        public Canvas PageCanvas => _page;
        public string LastRejection { get; private set; } = "";

        private readonly List<FolderPage> _pages = new List<FolderPage>();
        private NotesFolderInteraction _notes;
        private FormSendEmitter _send;
        private Canvas _page;
        private TMP_Text _heading, _body, _footer;
        private Transform _leafHinge;
        private int _direction, _target;
        private string _lastOpened = "";
        private object _shown;
        private Vector3 _dragStart;
        private bool _dragging;
        private UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor _dragger;

        private void Awake()
        {
            _notes = GetComponent<NotesFolderInteraction>();
            _send = gameObject.AddComponent<FormSendEmitter>();

            _page = UiKit.WorldCanvas("Folder document page", transform, new Vector2(630, 900), 0.0003f);
            _page.transform.localPosition = new Vector3(0.003f, Surface, 0);
            _page.transform.localRotation = Quaternion.Euler(90, 0, 0);
            _page.GetComponentInChildren<UnityEngine.UI.Image>().color = new Color(0.96f, 0.96f, 0.93f);
            _heading = UiKit.Label(_page.transform, "Heading", "", new Vector2(35, 35), new Vector2(560, 70), 34, TextAlignmentOptions.TopLeft, Color.black);
            _body = UiKit.Label(_page.transform, "Text", "", new Vector2(35, 120), new Vector2(560, 680), 27, TextAlignmentOptions.TopLeft, Color.black);
            _footer = UiKit.Label(_page.transform, "Footer", "", new Vector2(35, 820), new Vector2(560, 50), 22, TextAlignmentOptions.Center, Color.black);
            _page.gameObject.SetActive(false);

            var controls = UiKit.WorldCanvas("Page buttons", transform, new Vector2(630, 120), 0.0003f);
            controls.transform.localPosition = new Vector3(0.003f, Surface, -PageDepth / 2f - 0.022f);
            controls.transform.localRotation = Quaternion.Euler(90, 0, 0);
            UiKit.MakeButton(controls.transform, "<", new Vector2(20, 15), new Vector2(180, 90), 48f).onClick.AddListener(() => TurnPrevious());
            UiKit.MakeButton(controls.transform, ">", new Vector2(430, 15), new Vector2(180, 90), 48f).onClick.AddListener(() => TurnNext());
            controls.gameObject.SetActive(false);
            _controls = controls.gameObject;

            _leafHinge = new GameObject("Page leaf hinge").transform;
            _leafHinge.SetParent(transform, false);
            _leafHinge.localPosition = new Vector3(Spine, Surface + 0.0004f, 0);
            var leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leaf.name = "Turning leaf";
            DestroyImmediate(leaf.GetComponent<BoxCollider>());           // now, so the folder's grab never holds it
            leaf.transform.SetParent(_leafHinge, false);
            leaf.transform.localPosition = new Vector3(PageWidth / 2f, 0, 0);
            leaf.transform.localScale = new Vector3(PageWidth, 0.0006f, PageDepth);
            leaf.GetComponent<Renderer>().material.color = new Color(0.96f, 0.96f, 0.93f);
            _leafHinge.gameObject.SetActive(false);

            var edge = new GameObject("Page edge");
            edge.transform.SetParent(transform, false);
            edge.transform.localPosition = new Vector3(Spine + PageWidth - 0.02f, Surface + 0.01f, 0);
            var edgeBox = edge.AddComponent<BoxCollider>();
            edgeBox.size = new Vector3(0.04f, 0.02f, PageDepth);
            edgeBox.isTrigger = true;
            var handle = edge.AddComponent<XRSimpleInteractable>();
            handle.selectEntered.AddListener(a => { _dragger = a.interactorObject; BeginDrag(a.interactorObject.transform.position); });
            handle.selectExited.AddListener(_ => { _dragger = null; EndDrag(); });

            var pocket = new GameObject("SOCKET_Report");
            pocket.transform.SetParent(transform, false);
            pocket.transform.localPosition = new Vector3(0.003f, Surface + 0.02f, 0);
            var pocketBox = pocket.AddComponent<BoxCollider>();
            pocketBox.size = new Vector3(PageWidth, 0.04f, PageDepth);
            pocketBox.isTrigger = true;
            Pocket = pocket.AddComponent<ReportPocket>();
            Pocket.showInteractableHoverMeshes = false;
            Pocket.selectEntered.AddListener(a => { var p = a.interactableObject.transform.GetComponent<Printout>(); if (p != null) File(p); });

            var grab = GetComponent<XRGrabInteractable>();
            if (grab != null && grab.attachTransform == null)
            {
                var pose = new GameObject("Reading pose").transform;     // held, the folder tilts its pages towards the reader (checked in the headset, UX-P0-EMR)
                pose.SetParent(transform, false);
                pose.localRotation = Quaternion.Euler(-35f, 0, 0);
                grab.attachTransform = pose;
            }
        }

        private GameObject _controls;

        private void Update()
        {
            if (_dragger != null) Drag(_dragger.transform.position);
            Advance(Time.unscaledDeltaTime);
            var host = SimulationHost.Instance;
            if (host != null && host.Observable != _shown) Refresh();
        }

        /// <summary>Starts turning to the next page. False while the folder is shut, a page is turning, or this is the last page.</summary>
        public bool TurnNext() => Turn(+1);

        public bool TurnPrevious() => Turn(-1);

        private bool Turn(int direction)
        {
            if (!_notes.IsOpen || Turning) return false;
            int target = PageIndex + direction;
            if (target < 0 || target >= PageCount) return false;
            Turning = true;
            TurnProgress = 0f;
            _direction = direction;
            _target = target;
            _leafHinge.gameObject.SetActive(true);
            Pose(0f);
            return true;
        }

        /// <summary>Moves the turning page on by this much time: the turn always takes <see cref="TurnSeconds"/>, whatever the frame rate. The page lands, and its document opens in Core, at the end.</summary>
        public void Advance(float seconds)
        {
            if (!Turning || seconds <= 0f) return;
            TurnProgress = Mathf.Min(1f, TurnProgress + seconds / TurnSeconds);
            Pose(TurnProgress);
            if (TurnProgress < 1f) return;
            Turning = false;
            _leafHinge.gameObject.SetActive(false);
            PageIndex = _target;
            Refresh();
            OpenShownDocument();
        }

        // forward, the leaf lifts from the right page over the spine; back, it comes the other way
        private void Pose(float t) => _leafHinge.localRotation = Quaternion.Euler(0, 0, (_direction > 0 ? t : 1f - t) * 175f);

        /// <summary>A drag on the page edge (folder space): far enough towards the spine turns forward, away from it turns back. One turn per drag.</summary>
        public void BeginDrag(Vector3 worldPosition)
        {
            _dragging = true;
            _dragStart = transform.InverseTransformPoint(worldPosition);
        }

        public void Drag(Vector3 worldPosition)
        {
            if (!_dragging) return;
            float dx = transform.InverseTransformPoint(worldPosition).x - _dragStart.x;
            if (dx <= -DragThreshold && TurnNext()) _dragging = false;
            else if (dx >= DragThreshold && TurnPrevious()) _dragging = false;
        }

        public void EndDrag() => _dragging = false;

        /// <summary>Files a printout dropped into the pocket (file_document). Filed, the sheet goes and the document is a page of the folder; refused, the sheet is let go and the reason kept.</summary>
        public ActionOutcome File(Printout printout)
        {
            var outcome = PanelSend.Send(_send, RecordsSubsystem.FileVerb, new Dictionary<string, string> { ["id"] = printout.DocumentId }, false);
            if (outcome == null) return null;
            LastRejection = outcome.Accepted ? "" : outcome.RejectReason;
            var grab = printout.GetComponent<XRGrabInteractable>();
            if (grab != null && grab.isSelected && Pocket.interactionManager != null && grab.interactorsSelecting.Contains(Pocket))
                Pocket.interactionManager.SelectExit(Pocket, (IXRSelectInteractable)grab);
            if (outcome.Accepted) Destroy(printout.gameObject);
            Refresh();
            return outcome;
        }

        private void OpenShownDocument()
        {
            if (PageIndex == 0) { _lastOpened = ""; return; }
            var page = _pages[PageIndex - 1];
            if (page.DocumentId == _lastOpened) return;           // the next part of the same document is not a new reading
            _lastOpened = page.DocumentId;
            PanelSend.Send(_send, RecordsSubsystem.OpenVerb, new Dictionary<string, string> { ["id"] = page.DocumentId, ["view"] = RecordsSubsystem.FolderView }, false);
        }

        public void Refresh()
        {
            var host = SimulationHost.Instance;
            _shown = host != null ? host.Observable : null;
            _pages.Clear();
            if (host?.Session != null) _pages.AddRange(FolderPages.Build(host.Session, host.Content));
            if (PageIndex >= PageCount) PageIndex = PageCount - 1;
            bool open = _notes.IsOpen;
            _controls.SetActive(open);
            if (_notes.Page != null && open) _notes.Page.SetActive(PageIndex == 0);
            _page.gameObject.SetActive(open && PageIndex > 0);
            if (PageIndex > 0)
            {
                var p = _pages[PageIndex - 1];
                _heading.text = p.Heading;
                _body.text = p.Text + (p.ImageId.Length > 0 ? "\n\n[printed image " + p.ImageId + "]" : "");
            }
            else { _heading.text = ""; _body.text = ""; }
            _footer.text = "Page " + (PageIndex + 1) + " of " + PageCount;
        }

        private void LateUpdate()
        {
            // the notes folder shows its identifiers page whenever it opens; keep the page the trainee turned to
            if (_notes.IsOpen && _notes.Page != null && _notes.Page.activeSelf != (PageIndex == 0)) Refresh();
            if (!_notes.IsOpen && (_page.gameObject.activeSelf || _controls.activeSelf)) Refresh();
        }
    }
}
