"""The language-model fallback of the spoken-order grammar and the read-back ticket (server/orders/llm_fallback.py, ticket.py; features C32, C35, B23): the model only chooses from a short list built from the content, its
suggestion is read back like any other, nothing is released without a yes, a model that is down or unsure leaves the grammar's answer, and a ticket cannot be forged, moved, replayed or used late. A fake model host is used; no service is called."""
import json
import sys
import time
import unittest
from pathlib import Path

import httpx

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT))
from llm import client as client_mod  # noqa: E402
from orders import llm_fallback as LF, ticket as TK  # noqa: E402
from orders.grammar import ReadBack, vocabulary  # noqa: E402

VOCAB = vocabulary()


def host(answer, seen=None):
    def h(request):
        if request.url.path == "/api/tags":
            return httpx.Response(200, json={"models": []})
        if seen is not None:
            seen.append(json.loads(request.content))
        return httpx.Response(200, json={"message": {"content": json.dumps(answer)}})
    return client_mod.OllamaClient(transport=httpx.MockTransport(h))


def down():
    def h(request):
        raise httpx.ConnectError("down")
    return client_mod.OllamaClient(transport=httpx.MockTransport(h))


class Mapper(unittest.TestCase):
    def test_a_described_test_becomes_a_read_back_and_nothing_is_released_before_a_yes(self):
        rb = ReadBack(VOCAB, LF.OrderMapper(host({"target": "test:ecg", "confidence": 0.9})))
        h = rb.hear("get a trace of his heart")
        self.assertEqual((h.status, h.intent, h.slots["target"], h.source), ("confirm", "ORDER_TEST", "ecg", "model"))
        self.assertTrue(h.read_back.startswith("Confirm: order ecg"))
        self.assertEqual(rb.released, [])
        self.assertEqual(rb.confirm("yes"), {"verb": "order_investigation", "params": {"test": "ecg"}})

    def test_a_no_cancels_and_an_unclear_answer_leaves_the_question_open(self):
        rb = ReadBack(VOCAB, LF.OrderMapper(host({"target": "test:ecg", "confidence": 0.9})))
        rb.hear("get a trace of his heart")
        self.assertIsNone(rb.confirm("hmm maybe"))
        self.assertIsNotNone(rb.pending)
        self.assertIsNone(rb.confirm("no"))
        self.assertEqual((rb.pending, rb.released), (None, []))

    def test_none_low_confidence_a_down_host_and_a_missing_mapper_leave_the_menu(self):
        for client in (host({"target": "NONE", "confidence": 0.9}), host({"target": "test:ecg", "confidence": 0.3}), down(), None):
            rb = ReadBack(VOCAB, LF.OrderMapper(client) if client is not None else None)
            h = rb.hear("get a trace of his heart")
            self.assertEqual((h.status, h.source), ("menu", "grammar"))
            self.assertEqual(rb.released, [])

    def test_the_model_cannot_name_an_item_that_is_not_on_the_list(self):
        rb = ReadBack(VOCAB, LF.OrderMapper(host({"target": "test:invented", "confidence": 1.0})))
        self.assertEqual(rb.hear("get a trace of his heart").status, "menu")        # the schema refuses it, and the check refuses it again

    def test_the_grammar_is_tried_first_and_the_model_is_not_called_for_a_clear_order(self):
        seen = []
        rb = ReadBack(VOCAB, LF.OrderMapper(host({"target": "test:ecg", "confidence": 1.0}, seen)))
        h = rb.hear("could I get a full blood count please")
        self.assertEqual((h.status, h.slots["target"], h.source), ("confirm", "fbc", "grammar"))
        self.assertEqual(seen, [])

    def test_a_dose_never_reaches_the_model_or_the_order(self):
        seen = []
        rb = ReadBack(VOCAB, LF.OrderMapper(host({"target": "medicine:TR-PARACETAMOL", "confidence": 0.9}, seen)))
        h = rb.hear("something simple for the pain, five hundred and then 1 g")
        self.assertEqual(h.source, "model")
        self.assertNotIn(" 1 g", json.dumps(seen))
        self.assertNotIn("dose", str(h.action))

    def test_a_medicine_named_with_a_hold_word_is_a_hold_and_has_no_engine_verb(self):
        rb = ReadBack(VOCAB, LF.OrderMapper(host({"target": "home_medicine:metformin", "confidence": 0.9})))
        h = rb.hear("leave out the sugar tablet today")
        self.assertEqual((h.intent, h.action), ("HOLD_MED", None))
        self.assertIsNone(rb.confirm("yes"))
        self.assertEqual(rb.released, [])

    def test_the_shortlist_is_short_and_comes_from_the_content(self):
        cands = LF.shortlist("get a trace of his heart", VOCAB)
        self.assertLessEqual(len(cands), LF.LIST_SIZE)
        known = {(e.kind, e.target) for e in VOCAB}
        self.assertTrue(all((e.kind, e.target) in known for e in cands))

    def test_the_prompt_is_registered_live_and_the_log_says_where_a_hearing_came_from(self):
        from llm import prompts
        self.assertEqual(prompts.get("orders.map").role, "live")
        rb = ReadBack(VOCAB, LF.OrderMapper(host({"target": "test:ecg", "confidence": 0.9})))
        rb.hear("get a trace of his heart")
        self.assertEqual(rb.log[-1]["source"], "model")


class Tickets(unittest.TestCase):
    def test_a_ticket_round_trips_and_carries_the_action(self):
        t = TK.issue("u1", "ORDER_TEST", {"target": "fbc"}, {"verb": "order_investigation", "params": {"test": "fbc"}})
        c = TK.open_ticket(t, "u1")
        self.assertEqual((c["i"], c["a"]["params"]["test"]), ("ORDER_TEST", "fbc"))

    def test_a_forged_moved_expired_or_replayed_ticket_is_refused(self):
        t = TK.issue("u1", "ORDER_TEST", {"target": "fbc"}, {"verb": "order_investigation", "params": {"test": "fbc"}})
        body, sig = t.split(".")
        import base64
        forged_body = base64.urlsafe_b64encode(base64.urlsafe_b64decode(body + "==").replace(b"fbc", b"crp")).decode().rstrip("=")
        for bad, user, why in ((forged_body + "." + sig, "u1", "TICKET_FORGED"), (t, "u2", "TICKET_NOT_YOURS"), ("garbage", "u1", "TICKET_MALFORMED")):
            with self.assertRaises(TK.TicketError) as e:
                TK.open_ticket(bad, user)
            self.assertEqual(str(e.exception), why)
        with self.assertRaises(TK.TicketError) as e:
            TK.open_ticket(t, "u1", now=time.time() + TK.TTL + 5)
        self.assertEqual(str(e.exception), "TICKET_EXPIRED")
        TK.decide(TK.open_ticket(t, "u1"))
        with self.assertRaises(TK.TicketError) as e:
            TK.open_ticket(t, "u1")
        self.assertEqual(str(e.exception), "TICKET_ALREADY_DECIDED")


if __name__ == "__main__":
    unittest.main(verbosity=1)
