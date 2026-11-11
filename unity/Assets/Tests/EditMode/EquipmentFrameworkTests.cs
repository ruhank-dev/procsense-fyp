using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ProcSense.Core;
using ProcSense.Core.Actions;
using ProcSense.Core.Content;
using ProcSense.Core.Workflow;
using ProcSense.VR.ActionEmitters;
using ProcSense.VR.Fallback;
using ProcSense.VR.Interaction;
using UnityEngine;

namespace ProcSense.Tests.EditMode
{
    /// <summary>
    /// The interaction framework (p0-vr-interaction-ui-s02), plan tests T-IN1, T-IN2, T-IN4 and T-IN5: items move through their states in order, Core can refuse and the item stays,
    /// one item per socket, and the tablet sends the same action as the gesture. T-IN3 (72 fps in the full theatre) needs the headset and is not tested here.
    /// </summary>
    public class EquipmentFrameworkTests
    {
        private static string Repo => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        private static IReadOnlyDictionary<string, EquipmentDefinition> Items() =>
            EquipmentDefinition.Parse(File.ReadAllText(Path.Combine(Repo, "content", "equipment", "equipment_states.json")));

        private static Session NewSession(out ContentPackage content)
        {
            content = ContentLoader.Load(new FileContentReader(Path.Combine(Repo, "content")), new[] { "VAR-S1-A" }, new[] { "VAR-S1-A" });
            return new Session(content.Cases["VAR-S1-A"], 42, SessionMode.Practice, Subsystems.Default(content));
        }

        private static ActionOutcome Accept(SimAction _) => new ActionOutcome();
        private static ActionOutcome Refuse(SimAction _) { var o = new ActionOutcome(); o.Reject("NOT_ALLOWED"); return o; }

        // ---- the data ----

        [Test]
        public void The_equipment_file_is_valid_and_every_verb_exists_in_Core()
        {
            var items = Items();
            Assert.That(items.Keys, Is.SupersetOf(new[] { "tympanic_thermometer", "spo2_probe", "bp_cuff", "iv_fluid_bag", "glucose_meter", "ecg_machine", "stethoscope" }));
            var session = NewSession(out _);
            foreach (var item in items.Values)
            {
                foreach (var t in item.Transitions.Where(t => !t.VisualOnly)) Assert.IsTrue(session.Verbs.IsKnown(t.Verb), item.Id + ": unknown Core verb " + t.Verb);
                foreach (var p in item.Panel) Assert.IsTrue(session.Verbs.IsKnown(p.Verb), item.Id + " panel " + p.Id + ": unknown Core verb " + p.Verb);
                foreach (var t in item.Transitions.Where(t => t.Trigger == Trigger.Socket)) Assert.IsTrue(SocketLibrary.IsKnown(t.Socket), item.Id + ": unknown socket " + t.Socket);
            }
        }

        [Test]
        public void A_broken_definition_is_refused_with_the_item_and_the_reason()
        {
            var bad = "{\"items\":[{\"id\":\"x\",\"initial\":\"A\",\"states\":[\"A\"],\"transitions\":[{\"from\":\"A\",\"to\":\"B\",\"trigger\":\"grab\"}]}]}";
            var e = Assert.Throws<ContentException>(() => EquipmentDefinition.Parse(bad));
            StringAssert.Contains("x", e.Message);
            StringAssert.Contains("unknown state", e.Message);
            Assert.Throws<ContentException>(() => EquipmentDefinition.Parse("{\"items\":[{\"id\":\"y\",\"initial\":\"A\",\"states\":[\"A\"],\"transitions\":[{\"from\":\"A\",\"to\":\"A\",\"trigger\":\"wave\"}]}]}"));
        }

        // ---- T-IN1: each equipment path, states in order ----

        [Test]
        public void T_IN1_The_fluid_bag_goes_through_its_states_in_order()
        {
            var m = new EquipmentStateMachine(Items()["iv_fluid_bag"]);
            var seen = new List<string> { m.State };
            m.StateChanged += (_, to) => seen.Add(to);
            Assert.AreEqual(StepResult.Moved, m.Step(Trigger.Socket, Accept, SocketLibrary.GivingSet));
            Assert.AreEqual(StepResult.Moved, m.Step(Trigger.Socket, Accept, SocketLibrary.Stand));
            Assert.AreEqual(StepResult.Accepted, m.Step(Trigger.Socket, Accept, SocketLibrary.Arm));
            Assert.AreEqual(StepResult.Accepted, m.Step(Trigger.Ui, Accept));
            CollectionAssert.AreEqual(new[] { "SEALED", "SPIKED", "HUNG", "CONNECTED", "RUNNING" }, seen);
        }

        [Test]
        public void T_IN1_The_thermometer_measures_in_the_real_engine_and_the_item_follows_Core()
        {
            var session = NewSession(out _);
            var m = new EquipmentStateMachine(Items()["tympanic_thermometer"]);
            Assert.AreEqual(StepResult.Accepted, m.Step(Trigger.Activate, session.Apply, SocketLibrary.Ear));
            Assert.AreEqual("MEASURED", m.State);
            Assert.AreEqual(1, session.Log.Count, "one gesture sends exactly one action");
            Assert.AreEqual("measure_temperature", session.Log.Entries[0].Verb);
        }

        // ---- T-IN2: an invalid transition: Core rejects and the visual reverts ----

        [Test]
        public void T_IN2_When_Core_refuses_the_item_stays_in_its_state()
        {
            var m = new EquipmentStateMachine(Items()["spo2_probe"]);
            Assert.AreEqual(StepResult.Rejected, m.Step(Trigger.Socket, Refuse, SocketLibrary.Finger));
            Assert.AreEqual("DETACHED", m.State);
            Assert.AreEqual("NOT_ALLOWED", m.LastReject);
            Assert.AreEqual(StepResult.Accepted, m.Step(Trigger.Socket, Accept, SocketLibrary.Finger));
            Assert.AreEqual("", m.LastReject);
        }

        [Test]
        public void T_IN2_A_request_the_real_engine_refuses_leaves_the_bag_hung()
        {
            var session = NewSession(out _);
            var m = new EquipmentStateMachine(Items()["iv_fluid_bag"]);
            m.Step(Trigger.Socket, session.Apply, SocketLibrary.GivingSet);
            m.Step(Trigger.Socket, session.Apply, SocketLibrary.Stand);
            Assert.AreEqual("HUNG", m.State);
            // No device named and identity not yet confirmed: Core will not accept an IV access request.
            Assert.AreEqual(StepResult.Rejected, m.Step(Trigger.Socket, session.Apply, SocketLibrary.Arm));
            Assert.AreEqual("HUNG", m.State);
            Assert.That(m.LastReject, Is.Not.Empty);
        }

        [Test]
        public void T_IN2_A_trigger_with_no_transition_sends_nothing_and_changes_nothing()
        {
            var m = new EquipmentStateMachine(Items()["bp_cuff"]);
            int sent = 0;
            // The cuff is DETACHED: a grab has no transition, and a socket the cuff does not use is not its socket.
            Assert.AreEqual(StepResult.NoTransition, m.Step(Trigger.Grab, a => { sent++; return Accept(a); }));
            Assert.AreEqual(StepResult.NoTransition, m.Step(Trigger.Socket, a => { sent++; return Accept(a); }, SocketLibrary.Finger));
            Assert.AreEqual(0, sent);
            Assert.AreEqual("DETACHED", m.State);
        }

        [Test]
        public void T_IN2_No_session_or_a_swallowed_press_changes_nothing()
        {
            var m = new EquipmentStateMachine(Items()["spo2_probe"]);
            Assert.AreEqual(StepResult.Rejected, m.Step(Trigger.Socket, _ => null, SocketLibrary.Finger));
            Assert.AreEqual("DETACHED", m.State);
        }

        // ---- T-IN4: two items offered to one socket, only the first snaps ----

        [Test]
        public void T_IN4_Only_the_first_item_snaps_into_a_socket()
        {
            var slot = new SocketSlot(SocketLibrary.Finger);
            var snapped = new List<string>();
            slot.Snapped += snapped.Add;
            Assert.IsTrue(slot.TryOccupy("spo2_probe"));
            Assert.IsFalse(slot.TryOccupy("glucose_meter"));
            Assert.AreEqual("spo2_probe", slot.Occupant);
            CollectionAssert.AreEqual(new[] { "spo2_probe" }, snapped);
            slot.Release("glucose_meter");
            Assert.AreEqual("spo2_probe", slot.Occupant, "an item that is not in the socket cannot release it");
            slot.Release("spo2_probe");
            Assert.IsTrue(slot.TryOccupy("glucose_meter"));
        }

        [Test]
        public void Every_socket_has_its_own_layer_and_blender_point_name()
        {
            var layers = SocketLibrary.Names.Select(SocketLibrary.LayerOf).ToList();
            Assert.AreEqual(layers.Count, layers.Distinct().Count());
            Assert.AreEqual("SOCKET_Finger", SocketLibrary.PointName(SocketLibrary.Finger));
            Assert.Throws<System.ArgumentException>(() => new SocketSlot("elbow"));
        }

        // ---- T-IN5: the tablet sends the same action as the gesture ----

        [Test]
        public void T_IN5_The_tablet_action_equals_the_gesture_action_for_every_action_transition()
        {
            var go = new GameObject("equipment under test");
            try
            {
                foreach (var def in Items().Values)
                {
                    var controller = go.AddComponent<EquipmentController>();
                    controller.Bind(def);
                    var emitters = go.GetComponents<TransitionEmitter>();
                    foreach (var t in def.Transitions.Where(t => !t.VisualOnly))
                    {
                        var gesture = EquipmentStateMachine.BuildAction(t);
                        Assert.IsTrue(emitters.Any(e => TabletFallbackRegistry.SameAction(e.BuildAction(), gesture)), def.Id + ": no emitter sends the action of " + t.From + " to " + t.To);
                    }
                    Object.DestroyImmediate(controller);
                    foreach (var e in go.GetComponents<TransitionEmitter>()) Object.DestroyImmediate(e);
                }
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void An_answer_that_arrives_from_the_tablet_moves_the_item_too()
        {
            var m = new EquipmentStateMachine(Items()["spo2_probe"]);
            var t = m.Find(Trigger.Socket, SocketLibrary.Finger);
            Assert.AreEqual(StepResult.Rejected, m.Resolve(t, Refuse(null)));
            Assert.AreEqual("DETACHED", m.State);
            Assert.AreEqual(StepResult.Accepted, m.Resolve(t, Accept(null)));
            Assert.AreEqual("ATTACHED", m.State);
            Assert.AreEqual(StepResult.NoTransition, m.Resolve(t, Accept(null)), "the same answer cannot be taken twice from a different state");
        }

        // ---- bedside monitoring rows (p0-vr-interaction-ui-s06, spec rows 13 to 15) against the real engine ----

        private static Session Verified()
        {
            var s = NewSession(out _);
            foreach (var v in new[] { "introduce_self", "ask_identity", "read_wristband", "open_notes", "confirm_identity" }) Assert.IsTrue(s.Apply(new SimAction(v)).Accepted, v);
            return s;
        }

        private static SimAction Order(string test) => new SimAction("order_investigation", "", Channel.Vr, new Dictionary<string, string> { { "test", test } });

        [TestCase("glucose_meter", "glucose_fingerprick", "IDLE", "READ")]
        public void A_bedside_test_is_refused_until_it_is_ordered_then_the_meter_reads(string item, string test, string before, string after)
        {
            var s = Verified();
            var m = new EquipmentStateMachine(Items()[item]);
            Assert.AreEqual(StepResult.Rejected, m.Step(Trigger.Activate, s.Apply, SocketLibrary.Finger), "not ordered yet");
            Assert.AreEqual(before, m.State);
            Assert.IsTrue(s.Apply(Order(test)).Accepted, "ordering " + test);
            Assert.AreEqual(StepResult.Accepted, m.Step(Trigger.Activate, s.Apply, SocketLibrary.Finger));
            Assert.AreEqual(after, m.State);
        }

        [Test]
        public void The_ecg_needs_its_electrodes_then_an_order_and_follows_Core()
        {
            var s = Verified();
            var m = new EquipmentStateMachine(Items()["ecg_machine"]);
            Assert.AreEqual(StepResult.NoTransition, m.Step(Trigger.Ui, s.Apply), "no recording without electrodes");
            Assert.AreEqual(StepResult.Moved, m.Step(Trigger.Socket, s.Apply, SocketLibrary.Chest));
            Assert.AreEqual(StepResult.Rejected, m.Step(Trigger.Ui, s.Apply), "not ordered yet");
            Assert.AreEqual("ELECTRODES_ON", m.State);
            Assert.IsTrue(s.Apply(Order("ecg")).Accepted);
            Assert.AreEqual(StepResult.Accepted, m.Step(Trigger.Ui, s.Apply));
            Assert.AreEqual("RECORDED", m.State);
        }

        [Test]
        public void The_stethoscope_follows_whatever_Core_says_about_examining_the_chest()
        {
            var s = Verified();
            var m = new EquipmentStateMachine(Items()["stethoscope"]);
            var result = m.Step(Trigger.Socket, s.Apply, SocketLibrary.Chest);
            Assert.AreEqual(result == StepResult.Accepted ? "ON_CHEST" : "HUNG", m.State, "the item shows Core's answer and nothing else");
            Assert.AreEqual("examine", s.Log.Entries.Last().Verb);
        }

        // ---- sampling, access and oxygen rows (spec rows 16 to 19, 25, 28) ----

        private static SimAction Act(string verb, params string[] kv)
        {
            var p = new Dictionary<string, string>();
            for (int i = 0; i < kv.Length; i += 2) p[kv[i]] = kv[i + 1];
            return new SimAction(verb, "patient", Channel.Vr, p);
        }

        [Test]
        public void The_sample_is_kept_at_the_bedside_until_it_is_sent_and_the_tube_station_refuses_an_empty_send()
        {
            var s = Verified();
            var bottles = new EquipmentStateMachine(Items()["blood_culture_bottles"]);
            var station = new EquipmentStateMachine(Items()["tube_station"]);
            Assert.AreEqual(StepResult.Rejected, station.Step(Trigger.Ui, s.Apply), "nothing waits yet");
            Assert.AreEqual(StepResult.Rejected, bottles.Step(Trigger.Activate, s.Apply, SocketLibrary.Arm), "not ordered");
            Assert.IsTrue(s.Apply(Order("lactate_cultures")).Accepted);
            Assert.AreEqual(StepResult.Accepted, bottles.Step(Trigger.Activate, s.Apply, SocketLibrary.Arm));
            Assert.AreEqual("FILLED", bottles.State);
            Assert.AreEqual(StepResult.Accepted, station.Step(Trigger.Ui, s.Apply));
            Assert.AreEqual("SENT", station.State);
        }

        [Test]
        public void The_urine_kit_needs_an_order_and_the_pregnancy_pad_sends_its_own_test()
        {
            var s = Verified();
            var kit = new EquipmentStateMachine(Items()["urine_kit"]);
            Assert.AreEqual(StepResult.Rejected, kit.Step(Trigger.Ui, s.Apply));
            Assert.IsTrue(s.Apply(Order("urine_dipstick")).Accepted);
            Assert.AreEqual(StepResult.Accepted, kit.Step(Trigger.Ui, s.Apply));
            Assert.AreEqual("DIPPED", kit.State);
            var pad = kit.PanelNow().Single(p => p.Id == "pregnancy_test");
            Assert.AreEqual("pregnancy", pad.Params["test"]);
        }

        [TestCase("iv_cannula_kit", SocketLibrary.Arm, "request_iv_access")]
        [TestCase("oxygen_nasal_cannula", SocketLibrary.Face, "request_oxygen")]
        [TestCase("oxygen_simple_mask", SocketLibrary.Face, "request_oxygen")]
        [TestCase("oxygen_non_rebreather_mask", SocketLibrary.Face, "request_oxygen")]
        public void A_request_item_shows_Core_s_answer_whichever_it_is(string item, string socket, string verb)
        {
            var s = Verified();
            var m = new EquipmentStateMachine(Items()[item]);
            var from = m.State;
            var t = m.Find(Trigger.Socket, socket);
            var result = m.Step(Trigger.Socket, s.Apply, socket);
            Assert.AreEqual(verb, s.Log.Entries.Last().Verb);
            Assert.AreEqual(result == StepResult.Accepted ? t.To : from, m.State);
            Assert.AreEqual(result == StepResult.Accepted, s.Log.Entries.Last().Accepted);
        }

        [Test]
        public void The_oxygen_request_names_a_device_Core_knows_and_the_fluid_names_its_treatment()
        {
            var s = Verified();
            foreach (var id in new[] { "oxygen_nasal_cannula", "oxygen_simple_mask", "oxygen_non_rebreather_mask" })
            {
                var t = Items()[id].Transitions.First(x => !x.VisualOnly);
                Assert.AreNotEqual("UNKNOWN_DEVICE", s.Apply(EquipmentStateMachine.BuildAction(t)).RejectReason, id);
                Assert.AreNotEqual("MISSING_DEVICE", s.Apply(EquipmentStateMachine.BuildAction(t)).RejectReason, id);
            }
            var fluid = Items()["iv_fluid_bag"].Transitions.Single(x => x.Verb == "request_fluids");
            Assert.AreNotEqual("MISSING_TREATMENT", s.Apply(EquipmentStateMachine.BuildAction(fluid)).RejectReason);
            Assert.AreNotEqual("UNKNOWN_TREATMENT", s.Apply(EquipmentStateMachine.BuildAction(fluid)).RejectReason);
        }

        [Test]
        public void The_clippers_and_stockings_follow_Core_and_the_razor_is_recorded_as_an_infection_risk()
        {
            var s = Verified();
            var clippers = new EquipmentStateMachine(Items()["hair_clippers"]);
            Assert.AreEqual(StepResult.Accepted, clippers.Step(Trigger.Activate, s.Apply));
            Assert.AreEqual("USED", clippers.State);
            var razor = new EquipmentStateMachine(Items()["razor"]);
            var o = (ActionOutcome)null;
            Assert.AreEqual(StepResult.Accepted, razor.Step(Trigger.Activate, a => o = s.Apply(a)));
            CollectionAssert.Contains(o.Effects, "infection_risk:razor");
            var socks = new EquipmentStateMachine(Items()["stockings"]);
            Assert.AreEqual(StepResult.Accepted, socks.Step(Trigger.Socket, s.Apply, SocketLibrary.Leg));
            Assert.AreEqual("APPLIED", socks.State);
        }

        [Test]
        public void The_consent_form_stays_blank_until_Core_lets_it_be_signed()
        {
            var s = Verified();
            var m = new EquipmentStateMachine(Items()["consent_form"]);
            Assert.AreEqual(StepResult.Rejected, m.Step(Trigger.Ui, s.Apply), "no discussion yet");
            Assert.AreEqual("BLANK", m.State);
        }

        // ---- panel and faults ----

        [Test]
        public void The_panel_shows_an_entry_only_in_the_states_it_lists()
        {
            var m = new EquipmentStateMachine(Items()["bp_cuff"]);
            Assert.IsEmpty(m.PanelNow());
            m.Step(Trigger.Socket, Accept, SocketLibrary.Arm);
            Assert.AreEqual(new[] { "start_cycle" }, m.PanelNow().Select(p => p.Id).ToArray());
        }

        [Test]
        public void A_fault_appears_and_clears_only_when_Core_reports_it()
        {
            var faults = new FaultState();
            var events = new List<string>();
            faults.Changed += (id, on) => events.Add(id + (on ? ":on" : ":off"));
            faults.Update(new string[0]);
            Assert.IsEmpty(events, "nothing reported, nothing shown");
            faults.Update(new[] { "suction_blocked" });
            faults.Update(new[] { "suction_blocked" });
            faults.Update(new string[0]);
            CollectionAssert.AreEqual(new[] { "suction_blocked:on", "suction_blocked:off" }, events);
        }
    }
}
