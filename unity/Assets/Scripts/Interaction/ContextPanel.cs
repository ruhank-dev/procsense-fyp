using System;
using System.Collections.Generic;
using System.Linq;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.VR.ActionEmitters;
using ProcSense.VR.Bridge;
using ProcSense.VR.Settings;
using ProcSense.VR.UI.Kit;
using ProcSense.VR.UI.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.Interaction
{
    /// <summary>A contextual-panel button: the gateway tags its actions as menu actions (P3-SES-04); the request itself is the tablet's (same verb, parameters and target).</summary>
    public sealed class ContextButtonEmitter : ActionEmitter
    {
        protected override ActionSource Source => ActionSource.Menu;
    }

    /// <summary>
    /// P3-CTX-05: where a contextual panel goes. Beside the entity's anchor (0.15 m to the side facing the player), turned to the player, its height clamped to 1.1-1.6 m and its distance from the
    /// player's head to 0.6-1.2 m, and pulled in front of any wall between the head and that point. Pure, so the placement sweep can test it without a headset.
    /// </summary>
    public static class PanelPlacement
    {
        public const float Beside = .15f, MinHeight = 1.1f, MaxHeight = 1.6f, MinDistance = .6f, MaxDistance = 1.2f, WallGap = .05f, FollowAfter = 1.5f;

        /// <param name="wall">Distance along a ray from the head to the first wall, or null when nothing is hit (Physics.Raycast in the game).</param>
        public static (Vector3 position, Quaternion rotation) Place(Vector3 anchor, Vector3 head, Func<Vector3, Vector3, float, float?> wall = null)
        {
            var toAnchor = Vector3.ProjectOnPlane(anchor - head, Vector3.up);
            if (toAnchor.sqrMagnitude < 1e-4f) toAnchor = Vector3.forward;
            var dir = toAnchor.normalized;
            var side = Vector3.Cross(Vector3.up, dir);                                        // the player's right as they face the entity
            var p = anchor + side * Beside;
            p.y = Mathf.Clamp(p.y, MinHeight, MaxHeight);
            var flat = Vector3.ProjectOnPlane(p - head, Vector3.up);
            float d = Mathf.Clamp(flat.magnitude, MinDistance, MaxDistance);
            if (flat.sqrMagnitude < 1e-4f) flat = dir;
            p = new Vector3(head.x, p.y, head.z) + flat.normalized * d;
            var ray = p - head;
            float? hit = wall?.Invoke(head, ray.normalized, ray.magnitude);
            if (hit.HasValue && hit.Value < ray.magnitude) p = head + ray.normalized * Mathf.Max(.2f, hit.Value - WallGap);
            var face = Vector3.ProjectOnPlane(p - head, Vector3.up);
            return (p, Quaternion.LookRotation(face.sqrMagnitude < 1e-4f ? dir : face.normalized, Vector3.up));
        }

        public static float? PhysicsWall(Vector3 from, Vector3 dir, float max) =>
            Physics.Raycast(from, dir, out var h, max, ~0, QueryTriggerInteraction.Ignore) ? h.distance : (float?)null;
    }

    /// <summary>One action row as the panel shows it.</summary>
    public sealed class ContextRow
    {
        public CatalogueEntry Entry;
        public bool Available = true;
        public string Reason = "";
        public Button Button;
    }

    /// <summary>
    /// The contextual panel (P3-CTX-04): a glass card beside the entity the player selected, with its name and role, its groups as a segmented choice, and its actions with icons. In training the
    /// availability comes from the engine's Check (greyed, with the reason in words); in the examination nothing is greyed and no reason is given (P3-EXM-07: the order is not revealed). An action
    /// that needs parameters opens the tablet's own form; one flagged "confirm" asks first. Every action goes through the same emitter path as the tablet, tagged as a menu action.
    /// In voice mode (P3-VOX-03/04) the panel is a reference card instead: "You can ask…" with the catalogue's voice examples, and no action buttons except the exceptions (Submit patient to OT).
    /// For the patient a large Submit patient to OT button (P3-EXM-04) opens a confirmation and then submits through the game flow, once.
    /// </summary>
    public sealed class ContextPanel : MonoBehaviour
    {
        public const float Width = 640, Height = 760, Distance = .9f;
        public const int PerPage = 6;

        public static ContextPanel Open { get; private set; }

        public Interactable Entity { get; private set; }
        public int Group { get; private set; }
        public int Page { get; private set; }
        public bool ReferenceCard => InteractionMode.Voice;
        public IReadOnlyList<ContextRow> Rows => _rows;
        public IReadOnlyList<string> Examples => _examples;
        public Button SubmitButton { get; private set; }
        public KitDialog Confirmation { get; private set; }
        public string Status => _status != null ? _status.text : "";
        public Vector3 OpenedFrom { get; private set; }

        /// <summary>The game flow that submits the patient (MenuFlowInstaller's; tests set it).</summary>
        public static Func<ProcSense.VR.Flow.MenuFlow> FlowSource = () => FindAnyObjectByType<ProcSense.VR.Flow.MenuFlowInstaller>()?.Flow;
        /// <summary>The running session's host, whose engine Check greys the rows (the app's host unless set; tests set their own).</summary>
        public static Func<SimulationHost> HostSource = () => SimulationHost.Instance;
        /// <summary>The player's eyes (the main camera unless set).</summary>
        public static Func<Transform> HeadSource = () => Camera.main != null ? Camera.main.transform : null;

        private Canvas _canvas;
        private RectTransform _body;
        private TMP_Text _status;
        private readonly List<ContextRow> _rows = new List<ContextRow>();
        private readonly List<string> _examples = new List<string>();
        private InteractionCatalogue _catalogue;

        /// <summary>Opens the panel for an entity, replacing any open one; the same entity again closes it (the second trigger).</summary>
        public static ContextPanel Toggle(Interactable entity, InteractionCatalogue catalogue = null)
        {
            if (Open != null && Open.Entity == entity) { Open.Close(); return null; }
            if (Open != null) Open.Close();
            if (entity == null) return null;
            var go = new GameObject("Contextual panel " + entity.EntityType);
            var p = go.AddComponent<ContextPanel>();
            p._catalogue = catalogue ?? InteractionCatalogue.Current;
            p.Entity = entity;
            Open = p;
            p.Build();
            p.Place();
            p.React(ProcSense.VR.Views.CharacterReactions.State.Addressed, 3f);   // the person turns to the trainee (P3-ANI-04)
            return p;
        }

        public void Close()
        {
            if (Open == this) Open = null;
            if (this != null) Destroy(gameObject);
        }

        private void OnDestroy() { if (Open == this) Open = null; }

        public void Place()
        {
            var head = HeadSource?.Invoke();
            if (head == null || Entity == null) return;
            var (pos, rot) = PanelPlacement.Place(Entity.PanelAnchor.position, head.position, Application.isPlaying ? PanelPlacement.PhysicsWall : (Func<Vector3, Vector3, float, float?>)null);
            transform.SetPositionAndRotation(pos, rot);
            OpenedFrom = head.position;
        }

        private void Update()
        {
            if (Entity == null || !Entity.isActiveAndEnabled) { Close(); return; }   // the entity left (another room, the patient taken to CT)
            var head = HeadSource?.Invoke();
            if (head != null && Vector3.ProjectOnPlane(head.position - OpenedFrom, Vector3.up).magnitude > PanelPlacement.FollowAfter) Place();
        }

        private void Build()
        {
            _canvas = Kit.Canvas("Contextual panel", transform, new Vector2(Width, Height), Distance);
            var t = _canvas.transform;
            Kit.Panel(t, "Background", Vector2.zero, new Vector2(Width, Height));
            var entity = _catalogue.Entities.TryGetValue(Entity.EntityType, out var e) ? e : null;
            Kit.Header(t, entity != null ? entity.Label : Entity.name, ReferenceCard ? "You can ask by voice" : "Say or choose what to do", new Vector2(24, 20), Width - 48, null);
            Kit.Button(t, null, new Vector2(Width - 24 - 64, 20), new Vector2(64, 64), ButtonKind.Icon, UiIcons.Cancel, Close).name = "Button Close";
            var go = new GameObject("Body", typeof(RectTransform));
            go.transform.SetParent(t, false);
            _body = (RectTransform)go.transform;
            _body.anchorMin = _body.anchorMax = _body.pivot = new Vector2(0, 1);
            _body.anchoredPosition = Vector2.zero;
            _body.sizeDelta = new Vector2(Width, Height);
            _status = Kit.Text(t, "Status", "", new Vector2(24, Height - 56), new Vector2(Width - 48, 40), TextStyle.Caption, UiTheme.Current.HintText);
            Redraw();
        }

        public void ShowGroup(int group) { Group = group; Page = 0; Redraw(); }
        public void NextPage() { Page++; Redraw(); }

        /// <summary>Redraws the body: the reference card in voice mode, else the chosen group's rows with their availability.</summary>
        public void Redraw()
        {
            for (int i = _body.childCount - 1; i >= 0; i--) DestroyImmediate(_body.GetChild(i).gameObject);
            _rows.Clear(); _examples.Clear(); SubmitButton = null;
            var entity = _catalogue.Entities.TryGetValue(Entity.EntityType, out var e) ? e : null;
            float y = 112;
            if (ReferenceCard) y = DrawReference(y);
            else if (entity != null && entity.Groups.Count > 0)
            {
                Group = Mathf.Clamp(Group, 0, entity.Groups.Count - 1);
                if (entity.Groups.Count > 1) { Kit.Segmented(_body, entity.Groups.Select(g => g.Label).ToArray(), new Vector2(24, y), new Vector2(Width - 48, 64), Group, ShowGroup); y += 80; }
                var ids = entity.Groups[Group].Entries.Where(_catalogue.Entries.ContainsKey).ToList();
                int pages = Mathf.Max(1, (ids.Count + PerPage - 1) / PerPage);
                Page = ((Page % pages) + pages) % pages;
                foreach (var id in ids.Skip(Page * PerPage).Take(PerPage)) { _rows.Add(Row(_catalogue.Entries[id], y)); y += 72; }
                if (pages > 1) { Kit.Button(_body, "More (" + (Page + 1) + " of " + pages + ")", new Vector2(24, y), new Vector2(Width - 48, 56), ButtonKind.Secondary, null, NextPage).name = "Button More"; y += 64; }
            }
            if (Entity.EntityType == "patient") DrawSubmit();
        }

        private float DrawReference(float y)
        {
            var t = UiTheme.Current;
            foreach (var entry in _catalogue.EntriesOf(Entity.EntityType).Where(x => x.Spoken))
                foreach (var ex in entry.VoiceExamples.Take(1)) if (!_examples.Contains(ex)) _examples.Add(ex);
            var name = Entity.VoiceName;
            var lines = _examples.Take(7).Select(x => "“" + (Entity.EntityType == "patient" ? x : Cap(name) + ", " + Lower(x)) + "”");
            Kit.Text(_body, "Ask", "You can ask…", new Vector2(24, y), new Vector2(Width - 48, 36), TextStyle.Heading);
            string body = _examples.Count > 0 ? string.Join("\n", lines)
                : "Nothing here can be started by voice yet. You can still talk to " + (AddresseeTarget.RouteName(Entity.EntityType) != null ? "them" : "the team") + "; to act on it, switch to menu mode in Settings.";
            Kit.Text(_body, "Examples", body, new Vector2(24, y + 44), new Vector2(Width - 48, 330), TextStyle.Body, t.TextSecondary);
            return y + 380;
        }

        private static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
        private static string Lower(string s) => string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s.Substring(1);

        private ContextRow Row(CatalogueEntry entry, float y)
        {
            var row = new ContextRow { Entry = entry };
            var host = HostSource?.Invoke();
            if (!entry.HasForm && ModeGate.PracticeAids && host != null && host.Session != null)
            {
                try
                {
                    var check = host.Session.Check(new SimAction(entry.Verb, "patient", Channel.Vr, new Dictionary<string, string>(entry.Params)));
                    row.Available = check.Accepted;
                    row.Reason = check.Accepted ? "" : HintTexts.For(check.Reason);
                }
                catch (InvalidOperationException) { }   // no checker in this session: leave it available, the engine still decides on press
            }
            string icon = UiIcons.Names.Contains(entry.Icon) ? entry.Icon : null;
            string label = entry.Label + (entry.HasForm ? " …" : "") + (row.Available || string.IsNullOrEmpty(row.Reason) ? "" : "\n<size=16>" + row.Reason + "</size>");
            row.Button = Kit.Button(_body, label, new Vector2(24, y), new Vector2(Width - 48, 64), ButtonKind.Secondary, icon, () => Press(row));
            row.Button.name = "Button " + entry.Id;
            row.Button.interactable = row.Available;
            if (!entry.HasForm) row.Button.gameObject.AddComponent<ContextButtonEmitter>().ConfigureParams(entry.Verb, entry.Params);
            return row;
        }

        /// <summary>Presses a row as the trainee would: a form opens the tablet's form; a confirm entry asks first; otherwise the action is sent at once.</summary>
        public void Press(ContextRow row)
        {
            if (row == null || !row.Available) return;
            if (row.Entry.HasForm)
            {
                var tablet = FindAnyObjectByType<ProcSense.VR.Views.TabletView>();
                var wrist = FindAnyObjectByType<WristMenu>();
                if (tablet != null && tablet.OpenFormFor(row.Entry.Tab, row.Entry.Verb))
                {
                    if (wrist != null && !wrist.TabletSummoned) wrist.ToggleTablet();
                    Say("The form is open on the tablet.", false);
                }
                return;
            }
            if (row.Entry.Confirm) { Ask(row.Entry.Label + "?", "This cannot be undone.", () => Send(row)); return; }
            Send(row);
        }

        private void Send(ContextRow row)
        {
            var emitter = row.Button != null ? row.Button.GetComponent<ContextButtonEmitter>() : null;
            var outcome = emitter != null ? emitter.Fire() : null;
            if (outcome == null) return;
            React(outcome.Accepted ? ProcSense.VR.Views.CharacterReactions.State.Acknowledge : ProcSense.VR.Views.CharacterReactions.State.Refuse);   // P3-CTX-06 / P3-ANI-04
            if (outcome.Accepted) { Say("", false); if (row.Button != null) KitFeedback.Draw(KitFeedback.Tick(row.Button.transform, new Vector2(Width - 48 - 56, 12), 40)); }
            else
            {
                Say(ModeGate.PracticeAids ? HintTexts.For(outcome.RejectReason) : "Not now.", true);
                if (row.Button != null) KitFeedback.Error((RectTransform)row.Button.transform, _status);
            }
            if (this != null && Open == this) Invoke(nameof(Redraw), 0f);
        }

        /// <summary>A visible reaction of the person this panel is for (nothing for equipment and records).</summary>
        public void React(ProcSense.VR.Views.CharacterReactions.State state, float seconds = 2f)
        {
            if (Entity == null || AddresseeTarget.RouteName(Entity.EntityType) == null && Entity.EntityType != "porter" && Entity.EntityType != "doctor" && Entity.EntityType != "theatre_team") return;
            ProcSense.VR.Views.CharacterReactions.For(Entity)?.Trigger(state, seconds);
        }

        private void Say(string text, bool error)
        {
            if (_status == null) return;
            _status.text = text;
            _status.color = error ? UiTheme.Current.DangerText : UiTheme.Current.HintText;
        }

        private void Ask(string title, string body, Action yes, bool danger = false)
        {
            if (Confirmation != null) Destroy(Confirmation.gameObject);
            Confirmation = Kit.Dialog(_canvas.transform, title, body, danger ? "Submit" : "Yes", "Cancel", new Vector2(0, 200), danger, yes, null, Width);
            Confirmation.Open();
        }

        // ------------------------------------------------------------- P3-EXM-04

        private void DrawSubmit()
        {
            var flow = FlowSource?.Invoke();
            if (flow == null || flow.Phase != ProcSense.VR.Flow.MenuPhase.Playing) return;
            SubmitButton = Kit.Button(_body, "Submit patient to OT", new Vector2(24, Height - 56 - 96), new Vector2(Width - 48, 88), ButtonKind.Danger, UiIcons.Submit, PressSubmit);
            SubmitButton.name = "Button Submit patient to OT";
        }

        /// <summary>The big button: a confirmation, then one submission through the game flow (which arms and confirms its own submit); a second confirmation finds the case closed.</summary>
        public void PressSubmit()
        {
            Ask("Submit the patient to theatre?", "The case closes and the results are shown. You cannot change anything afterwards.", () =>
            {
                var flow = FlowSource?.Invoke();
                if (flow == null || flow.Phase != ProcSense.VR.Flow.MenuPhase.Playing) return;   // already submitted: nothing happens
                if (!flow.SubmitArmed) flow.Submit();
                var outcome = flow.Submit();
                if (outcome != null && outcome.Accepted) Close();
                else Say(flow.Message, true);
            }, true);
        }
    }
}
