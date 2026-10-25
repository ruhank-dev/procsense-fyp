using System.Collections.Generic;
using System.Linq;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.Core.Workflow.Steps;
using ProcSense.VR.Bridge;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProcSense.VR.Views
{
    /// <summary>
    /// The recovery nurse and the handover panel (plan p1-theatre-extension-s03, Unity half): a nurse figure in the reused patient bay and one button for each item the recovery handover needs for
    /// this scenario; pressing one applies `recovery_handover` for that item, "Finish handover" applies `finish_recovery_handover`. The view decides nothing: the engine accepts or refuses, and the
    /// refusal is shown in plain words. Practice mode shows the nurse's questions about what is missing (through <see cref="ModeGate"/>); assessment mode shows none.
    /// </summary>
    public sealed class RecoveryHandoverView : MonoBehaviour
    {
        public Canvas Canvas { get; private set; }
        public GameObject Nurse { get; private set; }
        public string Status => _status != null ? _status.text : "";
        public IReadOnlyList<Button> ItemButtons => _buttons;
        public IReadOnlyList<string> ItemIds => _ids;
        public Button FinishButton { get; private set; }

        private readonly List<Button> _buttons = new List<Button>();
        private readonly List<string> _ids = new List<string>();
        private TMP_Text _status;
        private Session _session;

        private static readonly Dictionary<string, string> Words = new Dictionary<string, string>
        {
            { "IDENTITY_NOT_CONFIRMED", "Confirm the patient's identity first." },
            { RecoveryHandoverSubsystem.SignOutNotComplete, "Finish the sign out first." },
            { RecoveryHandoverSubsystem.UnknownFact, "You do not know that yet." },
            { RecoveryHandoverSubsystem.AlreadyHandedOver, "The handover is already done." },
            { RecoveryHandoverSubsystem.NothingHandedOver, "Hand something over before you finish." },
            { RecoveryHandoverSubsystem.MissingItems, "Choose what to hand over." },
            { RecoveryHandoverSubsystem.WrongSection, "That item belongs to a different section." },
        };

        /// <summary>For tests and previews with no running host: act on this session instead of the host's.</summary>
        public void Use(Session session) { _session = session; Build(); }

        private Session Current => SimulationHost.Instance != null && SimulationHost.Instance.Session != null ? SimulationHost.Instance.Session : _session;

        private void Awake() => Build();

        private void Update() { if (Canvas == null) Build(); }          // the scene is loaded before the session exists

        public void Build()
        {
            var session = Current;
            if (Canvas != null || session?.RecoveryHandover == null) return;
            Nurse = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Nurse.name = "Recovery nurse";
            var collider = Nurse.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            Nurse.transform.SetParent(transform, false);
            Nurse.transform.localPosition = new Vector3(-0.9f, 0.9f, 0f);
            Nurse.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", new Color(0.2f, 0.45f, 0.6f)); block.SetColor("_Color", new Color(0.2f, 0.45f, 0.6f));
            Nurse.GetComponent<Renderer>().SetPropertyBlock(block);

            var items = session.RecoveryHandover.Required();
            int rows = Mathf.Max(1, items.Count);
            Canvas = UiKit.WorldCanvas("Recovery handover", transform, new Vector2(820, 190 + rows * 64), 0.0006f);
            Canvas.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            UiKit.Label(Canvas.transform, "Title", "Recovery handover to the nurse", new Vector2(20, 12), new Vector2(780, 40), 30);
            float y = 62;
            foreach (var item in items)
            {
                var button = UiKit.MakeButton(Canvas.transform, item.Label, new Vector2(20, y), new Vector2(780, 56), 22f);
                string id = item.Id, section = item.Section;
                button.onClick.AddListener(() => Give(section, id));
                _buttons.Add(button); _ids.Add(id);
                y += 64;
            }
            FinishButton = UiKit.MakeButton(Canvas.transform, "Finish handover", new Vector2(20, y + 4), new Vector2(380, 56), 24f);
            FinishButton.onClick.AddListener(() => Finish());
            _status = UiKit.Label(Canvas.transform, "Status", "", new Vector2(20, y + 70), new Vector2(780, 90), 22, TextAlignmentOptions.TopLeft, UiKit.WarnColor);
        }

        public ActionOutcome Give(string section, string id) => Apply(new SimAction(RecoveryHandoverSubsystem.HandoverVerb, "nurse", Channel.Cli, new Dictionary<string, string> { { "section", section }, { "items", id } }), "Handed over.");

        public ActionOutcome Finish() => Apply(new SimAction(RecoveryHandoverSubsystem.FinishVerb, "nurse", Channel.Cli, new Dictionary<string, string>()), "Handover finished.");

        private ActionOutcome Apply(SimAction action, string accepted)
        {
            var host = SimulationHost.Instance;
            var outcome = host != null && host.Session != null ? host.Apply(action) : _session.Apply(action);
            if (!outcome.Accepted) { SetStatus(Words.TryGetValue(outcome.RejectReason, out var w) ? w : "Not accepted: " + outcome.RejectReason); return outcome; }
            var asks = outcome.Effects.Where(e => e.StartsWith("recovery_nurse_asks:")).Select(e => Label(e.Substring("recovery_nurse_asks:".Length))).ToList();
            string text = outcome.Effects.Contains("recovery_handover_complete") ? "Handover complete. Thank you." : accepted;
            if (asks.Count > 0) text += ModeGate.Practice("\nThe nurse asks about: " + string.Join(", ", asks));
            SetStatus(text);
            return outcome;
        }

        private string Label(string id)
        {
            var item = Current.RecoveryHandover.Required().FirstOrDefault(i => i.Id == id);
            return item != null ? item.Label : id;
        }

        private void SetStatus(string text) { if (_status != null) _status.text = text; }
    }
}
