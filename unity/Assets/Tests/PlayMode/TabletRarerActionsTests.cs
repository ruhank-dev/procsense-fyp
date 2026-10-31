using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Workflow.Steps;
using ProcSense.VR.Bridge;
using ProcSense.VR.Fallback;
using ProcSense.VR.UI.Debrief;
using ProcSense.VR.UI.Results;
using ProcSense.VR.Views;
using UnityEngine;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// The tablet's buttons and forms for the rarer engine actions no P0 correct path uses (DT-254): each makes exactly its action with the parameters its Core handler reads, the option lists that
    /// live only in Core code (the concern and anaesthetist topics) match Core, and the debrief's citation table is in content (DT-261).
    /// </summary>
    public class TabletRarerActionsTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();
        private SimulationHost _host;

        private T Make<T>(string name) where T : Component { var go = new GameObject(name); _made.Add(go); return go.AddComponent<T>(); }

        [TearDown]
        public void TearDown()
        {
            ModeGate.SessionProvider = () => SimulationHost.Instance != null ? SimulationHost.Instance.Session : null;
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
        }

        private TabletView Tablet()
        {
            TabletFallbackRegistry.Clear();
            if (SimulationHost.Instance != null) Object.DestroyImmediate(SimulationHost.Instance.gameObject);
            _host = Make<SimulationHost>("host");
            _host.AutoTick = false;
            _host.SaveLogOnDestroy = false;
            _host.StartSession("VAR-S1-A", 42, SessionMode.Practice);
            ModeGate.SessionProvider = () => _host.Session;
            var tablet = Make<TabletView>("tablet");
            foreach (var v in new[] { "introduce_self", "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.IsTrue(tablet.Press(v), v);
            return tablet;
        }

        private static readonly (string verb, Dictionary<string, string> p)[] Actions =
        {
            ("offer_chaperone", new Dictionary<string, string>()),
            ("request_fluids", new Dictionary<string, string> { ["type"] = "TR-IV-FLUIDS" }),
            ("request_oxygen", new Dictionary<string, string> { ["device"] = "nasal_cannula" }),
            ("request_iv_access", new Dictionary<string, string>()),
            ("hold_medicine", new Dictionary<string, string> { ["drug"] = "paracetamol" }),
            ("document_treatment", new Dictionary<string, string> { ["treatment"] = "TR-IV-FLUIDS" }),
            ("send_samples", new Dictionary<string, string> { ["via"] = "porter" }),
            ("request_interpreter", new Dictionary<string, string> { ["who"] = "trained" }),
            ("say_concern", new Dictionary<string, string> { ["topic"] = "deterioration" }),
            ("tell_anaesthetist", new Dictionary<string, string> { ["topic"] = "fasting" }),
        };

        [Test]
        public void Each_rarer_action_is_made_from_the_tablet_with_exactly_the_parameters_its_handler_reads()
        {
            var tablet = Tablet();
            foreach (var (verb, p) in Actions)
            {
                int before = _host.Session.Log.Count;
                Assert.IsTrue(tablet.PressAction(verb, p), "the tablet cannot make " + verb);
                Assert.AreEqual(before + 1, _host.Session.Log.Count, verb + ": one press, one action");
                var e = _host.Session.Log.Entries.Last();
                Assert.AreEqual(verb, e.Verb);
                CollectionAssert.AreEquivalent(p.Select(kv => kv.Key + "=" + kv.Value).ToList(), e.Params.Select(kv => kv.Key + "=" + kv.Value).ToList(), verb + ": exactly these parameters");
                Assert.IsFalse(e.Reject.StartsWith("MISSING_") || e.Reject.StartsWith("UNKNOWN_"), verb + ": Core read every parameter it needs (" + e.Reject + ")");
            }
        }

        [Test]
        public void The_topics_that_live_in_core_code_match_core()
        {
            CollectionAssert.AreEqual(SepsisSubsystem.Topics, LayoutText.Options("say_concern", "topic").Select(o => o.Value).ToArray());
            CollectionAssert.AreEqual(WardChecklistSubsystem.Topics, LayoutText.Options("tell_anaesthetist", "topic").Select(o => o.Value).ToArray());
        }

        [Test]
        public void The_citations_are_in_content_for_the_debrief()
        {
            Assert.Greater(Citations.Table.Count, 1000);
            StringAssert.Contains("NICE NG253", Citations.For("SR-2859"), "the sepsis lateness source");
            Assert.AreEqual("SR-9999", Citations.For("SR-9999"), "an unknown ID is shown as itself");
        }
    }
}
