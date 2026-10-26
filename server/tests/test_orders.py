"""Spoken orders with read-back (server/orders/grammar.py; p1-voice-input-s03, plan tests T-VO1 to T-VO4): nothing reaches the engine without a confirmation."""
from __future__ import annotations

import pytest

from server.orders.grammar import ReadBack, clean, vocabulary

VOCAB = vocabulary()


def rb():
    return ReadBack(VOCAB)


def test_T_VO1_a_clear_order_is_read_back_and_released_only_after_a_yes():
    r = rb()
    h = r.hear("Could I get a full blood count please")
    assert (h.status, h.intent, h.slots["target"], h.read_back) == ("confirm", "ORDER_TEST", "fbc", "Confirm: order full blood count?")
    assert r.released == []                                          # nothing yet
    assert r.confirm("yes") == {"verb": "order_investigation", "params": {"test": "fbc"}}
    assert r.released == [{"verb": "order_investigation", "params": {"test": "fbc"}}]


def test_a_misheard_word_is_matched_by_fuzzy_comparison_and_a_brand_name_by_the_synonym_table():
    r = rb()
    assert r.hear("order a fool blood count").slots["target"] == "fbc"
    r = rb()
    h = r.hear("give some calpol")
    assert (h.intent, h.slots["target"]) == ("REQUEST_MEDICATION", "TR-PARACETAMOL")
    r = rb()
    assert r.hear("could I have a you and e").slots["target"] == "u_and_e"


def test_T_VO3_a_spoken_dose_is_removed_and_never_carried_into_the_order():
    text, removed = clean("give 500 mg of paracetamol")
    assert removed and "500" not in text and "mg" not in text
    h = rb().hear("give 1 g of paracetamol")
    assert h.dose_removed and h.status == "removed_dose_confirm" and "1 g" not in h.read_back and "left out the dose" in h.read_back
    assert "dose" not in str(h.action)


def test_T_VO2_two_things_equally_close_ask_a_clarifying_question_and_release_nothing():
    from server.orders.grammar import Entry
    r = ReadBack(VOCAB + [Entry("morphine", "medicine", "TR-OPIOID-A", "Opioid A"), Entry("morphine", "medicine", "TR-OPIOID-B", "Opioid B")])
    h = r.hear("give morphine")
    assert h.status == "clarify" and h.read_back.startswith("Which one:") and len(h.options) >= 2
    assert r.pending is None and r.confirm("yes") is None and r.released == []


def test_T_VO4_no_match_offers_the_menu():
    h = rb().hear("what a lovely morning")
    assert (h.status, h.intent) == ("menu", None) and "menu" in h.read_back.lower() and h.options


def test_no_cancels_and_anything_else_leaves_the_question_open():
    r = rb()
    r.hear("order a full blood count")
    assert r.confirm("hmm maybe") is None and r.pending is not None
    assert r.confirm("no") is None and r.pending is None and r.released == []
    assert r.confirm("yes") is None                                   # nothing is waiting: a stray yes releases nothing


def test_hold_a_home_medicine_is_read_back_and_released_to_the_hold_verb():
    r = rb()
    h = r.hear("hold metformin")
    assert (h.intent, h.slots["label"].lower()) == ("HOLD_MED", "metformin") and h.read_back == "Confirm: hold metformin?"
    assert r.confirm("yes") == {"verb": "hold_medicine", "params": {"drug": "metformin"}}      # DT-208: the engine checks the patient named it


def test_calling_staff_and_transport_map_to_engine_verbs():
    r = rb()
    assert r.hear("call the anaesthetist").intent == "CALL_STAFF"
    assert r.confirm("yes") == {"verb": "request_anaesthetic_review", "params": {}}
    r = rb()
    h = r.hear("transport the patient to the x ray")
    assert h.intent == "REQUEST_TRANSPORT"
    assert r.confirm("confirm")["verb"] == "request_transport"


@pytest.mark.parametrize("phrase", ["order a full blood count", "give calpol", "hold metformin", "call the nurse", "transport to theatre", "rubbish words here", "order the liver"])
def test_the_log_never_shows_an_action_released_without_a_confirmation(phrase):
    r = rb()
    r.hear(phrase)
    assert not r.released
    for i, e in enumerate(r.log):
        if e["event"] == "confirmed":
            assert any(x["event"] == "heard" and x["status"] in ("confirm", "removed_dose_confirm") for x in r.log[:i])


@pytest.mark.parametrize("phrase,place", [("take her along to the CT scanner", "ct"), ("send him down to the CT department", "ct"), ("wheel the patient to the ultrasound department", "ultrasound"), ("move her up to the operating theatre", "theatre")])
def test_moving_the_patient_to_a_scanner_is_a_transport_not_a_test(phrase, place):
    """Found on the frozen second order set (18 October 2026): 'send him down to the CT department' was read back as ordering a CT scan."""
    h = rb().hear(phrase)
    assert h.intent == "REQUEST_TRANSPORT" and h.slots.get("target") == place


def test_sending_a_test_is_still_a_test_order():
    assert rb().hear("send a full blood count").intent == "ORDER_TEST"


def test_each_kind_of_treatment_maps_to_the_engine_verb_and_parameter_that_reads_it():
    from server.orders.grammar import Entry, action_for
    assert action_for("REQUEST_MEDICATION", Entry("calpol", "medicine", "TR-PARACETAMOL", "Paracetamol")) == {"verb": "request_medication", "params": {"class": "TR-PARACETAMOL"}}
    assert action_for("REQUEST_MEDICATION", Entry("fluids", "medicine", "TR-IV-FLUIDS", "IV fluids")) == {"verb": "request_fluids", "params": {"type": "TR-IV-FLUIDS"}}
    assert action_for("REQUEST_MEDICATION", Entry("oxygen", "medicine", "TR-OXYGEN", "Oxygen")) == {"verb": "request_oxygen", "params": {}}       # the device is chosen before the confirmation
    assert action_for("CALL_STAFF", Entry("senior", "staff", "senior", "senior")) == {"verb": "escalate", "params": {"role": "senior"}}          # the engine's parameter is `role`
    assert action_for("CALL_STAFF", Entry("surgeon", "staff", "surgeon", "surgeon")) is None                                                   # the engine escalates to the senior and the anaesthetist only


def test_missing_details_are_closed_lists_and_only_those_values_complete_an_action():
    from server.orders import details
    need = details.needs_for({"verb": "request_oxygen", "params": {}})
    assert [n["param"] for n in need] == ["device"] and {o["id"] for o in need[0]["options"]} == {"nasal_cannula", "simple_mask", "non_rebreather_mask"}
    assert details.needs_for({"verb": "request_oxygen", "params": {"device": "simple_mask"}}) == [] and details.needs_for({"verb": "order_investigation", "params": {"test": "crp"}}) == []
    assert {o["id"] for o in details.needs_for({"verb": "request_transport", "params": {"to": "ct"}})[0]["options"]} == {"walk", "wheelchair", "trolley"}
    done, why = details.apply_choices({"verb": "request_transport", "params": {"to": "ct"}}, {"mode": "trolley"})
    assert done == {"verb": "request_transport", "params": {"mode": "trolley", "to": "ct"}} and why == ""
    assert details.apply_choices({"verb": "request_oxygen", "params": {}}, {"device": "jet_ventilator"})[0] is None            # not in the closed list
    assert details.apply_choices({"verb": "request_oxygen", "params": {}}, {"device": "simple_mask", "dose": "15"})[0] is None   # nothing else can be added
