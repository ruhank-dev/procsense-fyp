"""The semantic order interpreter (server/orders/interpreter.py; features B23, C32; ADR-0028) with a fake model host: the model's reading is only a proposal, every proposal is checked against the vocabulary, and nothing is released before the trainee's yes."""
from __future__ import annotations

import json

import httpx

from server.llm import client as client_mod
from server.orders.grammar import ReadBack, vocabulary
from server.orders.interpreter import OrderInterpreter


def host(answer, calls=None):
    def h(request):
        if request.url.path == "/api/tags":
            return httpx.Response(200, json={"models": []})
        if calls is not None:
            calls.append(json.loads(request.content))
        return httpx.Response(200, json={"message": {"content": json.dumps(answer)}})
    return client_mod.OllamaClient(transport=httpx.MockTransport(h))


def hear(answer, text="could I get a dose of morphine for her pain", context=None):
    return ReadBack(vocabulary(), None, OrderInterpreter(host(answer))).hear(text, context)


def test_a_can_i_question_is_not_rejected_by_a_word_list_the_model_decides():
    h = hear({"kind": "order", "intent": "ORDER_TEST", "target": "test:crp", "confidence": 0.9}, "Can I get a CRP on him?")
    assert (h.status, h.source, h.slots["target"], h.interpretation) == ("confirm", "model", "crp", "order")
    h = hear({"kind": "question", "intent": "NONE", "target": "NONE", "confidence": 0.9}, "Can I ask what a CRP is?")
    assert (h.status, h.interpretation) == ("menu", "question")


def test_every_kind_that_is_not_an_order_proposes_nothing():
    for kind in ("question", "hypothetical", "negated", "unsupported", "other"):
        h = hear({"kind": kind, "intent": "NONE", "target": "NONE", "confidence": 0.9})
        assert h.status == "menu" and h.action is None, kind


def test_an_ambiguous_or_incomplete_sentence_asks_and_never_chooses():
    h = hear({"kind": "ambiguous", "intent": "NONE", "target": "NONE", "alternatives": ["test:ct", "test:mri"], "confidence": 0.8, "clarification": "CT or MRI?"})
    assert (h.status, h.action, h.read_back) == ("clarify", None, "CT or MRI?")
    h = hear({"kind": "incomplete", "intent": "NONE", "target": "NONE", "confidence": 0.8})
    assert h.status == "clarify" and h.action is None


def test_an_order_the_model_is_not_sure_of_or_names_other_readings_for_becomes_a_question():
    assert hear({"kind": "order", "intent": "ORDER_TEST", "target": "test:ct", "confidence": 0.3}).status == "clarify"
    assert hear({"kind": "order", "intent": "ORDER_TEST", "target": "test:ct", "alternatives": ["test:mri"], "confidence": 0.9}).status == "clarify"


def test_an_item_that_does_not_fit_the_intent_or_the_catalogue_is_never_proposed():
    h = hear({"kind": "order", "intent": "CALL_STAFF", "target": "test:ct", "confidence": 0.99})          # a test is not a person
    assert (h.status, h.action, h.interpretation) == ("menu", None, "unsupported")
    it = OrderInterpreter.check({"kind": "order", "intent": "ORDER_TEST", "target": "test:invented", "confidence": 0.99}, {})
    assert it.kind == "unsupported"
    assert OrderInterpreter.check({"kind": "bogus", "intent": "NONE", "target": "NONE", "confidence": 1}, {}) is None


def test_a_proposal_is_released_only_by_the_trainees_own_yes():
    rb = ReadBack(vocabulary(), None, OrderInterpreter(host({"kind": "order", "intent": "REQUEST_MEDICATION", "target": "medicine:TR-ADRENALINE", "confidence": 0.95})))
    h = rb.hear("give the adrenaline")
    assert h.status == "confirm" and h.action["verb"] == "request_medication"
    for spoken in ("", "maybe", "what", "tell me more"):
        assert rb.confirm(spoken) is None or spoken == "", spoken
        rb.pending = h
    assert rb.confirm("yes") == h.action


def test_a_hold_of_a_home_medicine_goes_to_the_hold_verb():
    h = hear({"kind": "order", "intent": "HOLD_MED", "target": "home_medicine:metformin", "confidence": 0.9}, "do not give him his metformin today")
    assert h.action == {"verb": "hold_medicine", "params": {"drug": "metformin"}}


def test_the_dose_is_removed_before_anything_is_read_back_and_the_context_reaches_the_model():
    calls = []
    rb = ReadBack(vocabulary(), None, OrderInterpreter(host({"kind": "order", "intent": "REQUEST_MEDICATION", "target": "medicine:TR-OPIOID", "confidence": 0.9}, calls)))
    h = rb.hear("give 10 mg of morphine", "Recovery; drowsy")
    assert h.status == "removed_dose_confirm" and h.dose_removed
    assert "Recovery; drowsy" in json.dumps(calls[0])


def test_when_the_model_is_down_the_grammar_with_its_question_guard_still_answers():
    def down(request):
        if request.url.path == "/api/tags":
            return httpx.Response(200, json={"models": []})
        return httpx.Response(500)
    rb = ReadBack(vocabulary(), None, OrderInterpreter(client_mod.OllamaClient(transport=httpx.MockTransport(down))))
    h = rb.hear("order a CRP")
    assert (h.status, h.source) == ("confirm", "grammar")
    assert rb.hear("can i have a crp").status == "menu"            # offline safety net only
