using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.Core.Replay;
using ProcSense.VR.Bridge;
using ProcSense.VR.Fallback;
using ProcSense.VR.UI.Results;
using UnityEngine;

namespace ProcSense.Tests.PlayMode
{
    /// <summary>
    /// The results viewer and the critical-result phone (p0-vr-interaction-ui-s05): VAR-S1-J shows the haemolysed potassium 6.4 with the lab's comment and then the repeat 4.6; a critical potassium (STK-S1-C, 6.6) rings
    /// the phone, and its result cannot be acknowledged until the call has been answered, the patient confirmed and the values read back exactly.
    /// </summary>
    public class ResultsPhoneTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();
        private SimulationHost _host;

        private static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        private T Make<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            _made.Add(go);
            return go.AddComponent<T>();
        }

        private void Host(string caseId)
        {
            TabletFallbackRegistry.Clear();
            if (SimulationHost.Instance != null) Object.DestroyImmediate(SimulationHost.Instance.gameObject);   // a host another test left behind would be the one the views read
            _host = Make<SimulationHost>("host");
            Assert.AreSame(_host, SimulationHost.Instance);
            _host.AutoTick = false;
            _host.SaveLogOnDestroy = false;
            _host.StartSession(caseId, 42, SessionMode.Practice);
            ModeGate.SessionProvider = () => _host.Session;
        }

        private ActionOutcome Do(string verb, params (string k, string v)[] p) => _host.Apply(new SimAction(verb, "patient", Channel.Vr, p.ToDictionary(x => x.k, x => x.v)));

        [TearDown]
        public void TearDown()
        {
            ModeGate.SessionProvider = () => SimulationHost.Instance != null ? SimulationHost.Instance.Session : null;
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
        }

        [Test]
        public void VAR_S1_J_shows_the_haemolysed_potassium_with_the_lab_comment_then_the_repeat()
        {
            Host("VAR-S1-J");
            var results = Make<ResultsView>("results");
            var script = ActionScript.Parse(File.ReadAllText(Path.Combine(Repo, "tests", "golden", "VAR-S1-J_correct.actions.json")));
            int views = 0;
            foreach (var step in script.TakeWhile(s => s.T <= 9476.0))
            {
                _host.Tick(step.T - _host.Session.Clock.SimSeconds);
                if (step.Action.Verb == "view_result" && step.Action.Params["test"] == "u_and_e")
                {
                    Assert.IsTrue(results.Select("u_and_e"));
                    Assert.IsTrue(results.ViewEnabled);
                    Assert.IsTrue(results.View().Accepted);
                    views++;
                    if (views == 1)
                    {
                        Assert.AreEqual(ResultsText.Table, results.Mode);
                        StringAssert.Contains("Potassium: 6.4 mmol/L  H   (3.5-5.3)", results.BodyText, "the value, the unit as content wrote it, the flag and the range");
                        StringAssert.Contains("Haemolysed sample: the cells burst in the tube, so the potassium looks falsely high. Repeat.", results.BodyText, "the lab comment verbatim");
                        Assert.IsFalse(results.CriticalOpen("u_and_e"), "6.4 is under the lab's phone limit");
                        CollectionAssert.Contains(results.Picker.Options.Select(o => o.Value).ToList(), "artefact");
                        Assert.IsTrue(results.Picker.Choose("artefact").Accepted);
                        StringAssert.Contains("bear this out", results.Picker.CardText, "practice says whether the results bear the decision out");
                    }
                    continue;
                }
                _host.Apply(new SimAction(step.Action.Verb, "patient", Channel.Vr, step.Action.Params.ToDictionary(kv => kv.Key, kv => kv.Value)));
            }
            Assert.AreEqual(2, views);
            Snapshots.Photograph(results.Canvas, Snapshots.Out("results-repeat.png"));
            var body = results.BodyText;
            StringAssert.Contains("Potassium (repeat): 4.6 mmol/L", body);
            Assert.Less(body.IndexOf("6.4"), body.IndexOf("4.6"), "the first sample above, the repeat below");
            Assert.IsTrue(results.AcknowledgeEnabled);
            Assert.IsTrue(results.Acknowledge().Accepted, "a result that is not critical is acknowledged without an action or a note");
        }

        [Test]
        public void A_critical_result_rings_the_phone_and_cannot_be_acknowledged_until_it_is_read_back()
        {
            Host("STK-S1-C");
            var results = Make<ResultsView>("results");
            var phone = Make<PhoneView>("phone");
            phone.transform.position = new Vector3(3f, 0f, 0f);                     // beside the viewer, so their pictures do not overlap
            foreach (var verb in new[] { "introduce_self", "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.IsTrue(Do(verb).Accepted, verb);
            Assert.IsTrue(Do("order_investigation", ("test", "u_and_e")).Accepted);
            Assert.IsTrue(Do("take_sample", ("test", "u_and_e"), ("tube", "serum_gold")).Accepted);
            Assert.IsFalse(phone.Ringing);
            _host.Tick(60 * 60);                                                    // the result comes with its slowest analyte (creatinine, 45 minutes)
            phone.RenderNow();
            Assert.IsTrue(phone.Ringing, "the lab phones the critical potassium");
            Assert.IsTrue(phone.AnswerEnabled);
            Assert.IsFalse(phone.ConfirmEnabled);

            Assert.IsTrue(results.Select("u_and_e"));
            Assert.IsTrue(results.View().Accepted);
            StringAssert.Contains("6.6", results.BodyText);
            Assert.IsTrue(results.CriticalOpen("u_and_e"));
            Assert.IsFalse(results.AcknowledgeEnabled, "blocked on the viewer while the call is open");
            Assert.AreEqual("CALL_NOT_ANSWERED", results.Acknowledge("treat_and_repeat", "phoned").RejectReason, "and refused by Core");

            Assert.IsTrue(phone.Answer().Accepted);
            StringAssert.Contains("Helena Novak", phone.MessageText, "the caller names the patient");
            StringAssert.Contains("6.6", phone.MessageText);
            Assert.IsTrue(phone.ConfirmEnabled);
            Assert.IsTrue(phone.ConfirmIdentity().Accepted);
            Assert.AreEqual("IDENTITY_CONFIRMED", phone.State);
            Assert.IsTrue(phone.ReadBackEnabled);

            var choices = phone.Choices.ToList();
            Assert.AreEqual(3, choices.Count);
            var wrong = phone.ReadBack(choices.FindIndex(c => !c.Exact));
            CollectionAssert.Contains(wrong.Effects, "readback:wrong");
            Assert.AreEqual("IDENTITY_CONFIRMED", phone.State, "a wrong read-back does not move the call on");
            results.RenderNow();
            Assert.IsFalse(results.AcknowledgeEnabled);

            Snapshots.Photograph(phone.Canvas, Snapshots.Out("phone-read-back.png"));
            CollectionAssert.Contains(phone.ReadBack(choices.FindIndex(c => c.Exact)).Effects, "readback:correct");
            Assert.AreEqual("READ_BACK", phone.State);
            results.RenderNow();
            Assert.IsTrue(results.AcknowledgeEnabled);
            Assert.AreEqual("MISSING_ACTION", results.Acknowledge().RejectReason, "a critical result needs an action");
            CollectionAssert.AreEquivalent(_host.Content.Investigations.Critical.Actions.ToList(), LayoutText.Options("acknowledge_result", "action").Select(o => o.Value).ToList(), "the actions offered are the content's");
            Assert.IsTrue(results.Acknowledge("treat_and_repeat", "Potassium 6.6 phoned and read back; treating and repeating").Accepted);
            Assert.AreEqual("DONE", _host.Session.Patient.Clinical.CriticalCalls.Single().State);
            phone.RenderNow();
            Assert.AreEqual("", phone.State);
            Assert.AreEqual("No call", phone.StatusText);
        }
    }
}
