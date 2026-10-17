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


def test_hold_a_medicine_is_read_back_but_has_no_engine_verb_yet():
    r = rb()
    h = r.hear("hold metformin")
    assert (h.intent, h.slots["label"].lower()) == ("HOLD_MED", "metformin") and h.read_back == "Confirm: hold metformin?"
    assert r.confirm("yes") is None and r.released == []              # DT-208


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
