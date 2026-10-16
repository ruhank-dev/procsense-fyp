"""Tests of the model benchmark (tools/ai/benchmark.py) against a fake Ollama: no model, no GPU, no network.

    uv run --project tools tools/ai/test_benchmark.py
"""
from __future__ import annotations

import io
import json
import sys
import tempfile
import threading
import unittest
from contextlib import redirect_stdout
from http.server import BaseHTTPRequestHandler, HTTPServer
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import benchmark as bm  # noqa: E402

LEX = bm.load_json("content/ai/lexicon.json")
INTENTS = bm.load_json("content/ai/intents.json")["roles"]["patient"]
SEEN: list[dict] = []
REPLY = {"text": json.dumps({"intent": "answer_question", "text": "I am not sure about that.", "fact_ids": []})}


class FakeOllama(BaseHTTPRequestHandler):
    def log_message(self, *a):  # silence
        pass

    def _send(self, body: dict):
        data = json.dumps(body).encode()
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def do_GET(self):
        if self.path == "/api/tags":
            self._send({"models": [{"name": "fake:1b", "model": "fake:1b", "digest": "abc123def456789", "size": 1_000_000_000}]})
        else:
            self._send({"models": [{"name": "fake:1b", "size": 2_000_000_000, "size_vram": 1_500_000_000, "context_length": 4096}]})

    def do_POST(self):
        body = json.loads(self.rfile.read(int(self.headers["Content-Length"])))
        SEEN.append(body)
        self._send({"response": REPLY["text"], "thinking": "", "load_duration": 1_000_000_000, "prompt_eval_count": 100, "prompt_eval_duration": 100_000_000,
                    "eval_count": 20, "eval_duration": 400_000_000, "done_reason": "stop"})


class Bench(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.server = HTTPServer(("127.0.0.1", 0), FakeOllama)
        cls.url = f"http://127.0.0.1:{cls.server.server_port}"
        threading.Thread(target=cls.server.serve_forever, daemon=True).start()
        cls.tmp = tempfile.TemporaryDirectory()
        bm.OUT_DIR = Path(cls.tmp.name)                       # results go to a temporary folder, never the repository

    @classmethod
    def tearDownClass(cls):
        cls.server.shutdown()
        cls.tmp.cleanup()

    def run_cli(self, *argv: str) -> str:
        out = io.StringIO()
        with redirect_stdout(out):
            code = bm.main(["--url", self.url, *argv])
        self.assertEqual(code, 0)
        return out.getvalue()

    def test_percentile(self):
        self.assertEqual(bm.percentile([], 95), 0.0)
        self.assertEqual(bm.percentile([5], 95), 5)
        self.assertAlmostEqual(bm.percentile([1, 2, 3, 4, 5], 50), 3)
        self.assertAlmostEqual(bm.percentile([1, 2, 3, 4, 5], 95), 4.8)

    def test_the_model_is_never_shown_a_fact_the_patient_has_not_earned(self):
        for case in bm.p0_cases():
            hidden = [h for h in case["history"] if h.get("variant") is None and not h["volunteered"]]
            asked = hidden[0]
            earned_ctx = bm.patient_context(case, asked["topic"], LEX, INTENTS)
            self.assertIn(asked["answer"], bm.build_prompt(earned_ctx, asked["topic"]))
            withheld_ctx = bm.patient_context(case, "", LEX, INTENTS)
            prompt = bm.build_prompt(withheld_ctx, asked["topic"])
            for h in hidden:
                self.assertNotIn(h["answer"], prompt, f"{case['id']} {h['topic']} leaked into the prompt")
            self.assertTrue(all(f["disclosable_now"] for f in withheld_ctx["shown"]))
            self.assertTrue(any(not f["disclosable_now"] for f in withheld_ctx["facts"]))     # the validator still knows the hidden ones

    def test_the_schema_uses_the_real_patient_intents(self):
        s = bm.schema_for(INTENTS)
        self.assertEqual(s["properties"]["intent"]["enum"], INTENTS)
        self.assertGreaterEqual(len(INTENTS), 5)

    def test_the_schema_limits_cited_fact_ids_to_the_facts_shown(self):
        self.assertEqual(bm.schema_for(INTENTS, ["h.a", "h.b"])["properties"]["fact_ids"]["items"]["enum"], ["h.a", "h.b"])
        self.assertEqual(bm.schema_for(INTENTS, [])["properties"]["fact_ids"]["maxItems"], 0)
        case = bm.p0_cases()[0]
        topic = next(h["topic"] for h in case["history"] if not h["volunteered"])
        SEEN.clear()
        ctx = bm.patient_context(case, topic, LEX, INTENTS, bm.question_for(topic))
        bm.generate(self.url, "fake:1b", bm.build_prompt(ctx, topic), think=False, ctx=4096, schema=bm.schema_for(INTENTS, [f["id"] for f in ctx["shown"]]), seed=1, num_predict=50)
        self.assertIn("h." + topic, SEEN[0]["format"]["properties"]["fact_ids"]["items"]["enum"])

    def test_a_word_in_the_nurses_question_may_be_repeated_by_the_patient(self):
        case = bm.p0_cases()[0]
        without = bm.patient_context(case, "", LEX, INTENTS)
        withq = bm.patient_context(case, "", LEX, INTENTS, "Could you be pregnant? Any penicillin or sepsis?")
        for term in ("pregnancy", "penicillin", "sepsis"):
            self.assertLessEqual(len([e for e in without["allowed_entities"] if e == term]), len([e for e in withq["allowed_entities"] if e == term]))
        self.assertTrue(any(e in withq["allowed_entities"] for e in ("penicillin", "sepsis", "pregnancy")))

    def test_scoring_catches_an_invented_number_a_wrong_intent_and_bad_json(self):
        case = bm.p0_cases()[0]
        ctx = bm.patient_context(case, "", LEX, INTENTS)
        good = json.dumps({"intent": "answer_question", "text": "I am not sure.", "fact_ids": []})
        self.assertTrue(bm.score_reply(good, ctx, LEX)["ok"])
        invented = json.dumps({"intent": "answer_question", "text": "My blood sugar was 977.", "fact_ids": []})
        self.assertIn("UNGROUNDED_NUMBER", bm.score_reply(invented, ctx, LEX)["codes"])
        wrong = json.dumps({"intent": "dance", "text": "Hello.", "fact_ids": []})
        self.assertIn("UNKNOWN_INTENT", bm.score_reply(wrong, ctx, LEX)["codes"])
        self.assertEqual(bm.score_reply("not json", ctx, LEX)["codes"], ["PARSE"])
        self.assertEqual(bm.score_reply("[1]", ctx, LEX)["codes"], ["PARSE"])

    def test_a_hidden_fact_told_back_is_caught_by_the_validator(self):
        case = bm.p0_cases()[0]
        hidden = next(h for h in case["history"] if h.get("variant") is None and not h["volunteered"] and len(h["answer"]) > 30)
        ctx = bm.patient_context(case, "", LEX, INTENTS)
        leak = json.dumps({"intent": "answer_question", "text": hidden["answer"][:390], "fact_ids": []})
        self.assertIn("DISCLOSURE_LEAK", bm.score_reply(leak, ctx, LEX)["codes"])

    def test_b1_counts_valid_replies_and_writes_one_line_per_call(self):
        SEEN.clear()
        text = self.run_cli("b1", "--model", "fake:1b", "--n", "6")
        self.assertIn("6/6 valid", text)
        self.assertEqual(len(SEEN), 6)
        self.assertEqual(SEEN[0]["think"], False)
        self.assertEqual(SEEN[0]["format"]["properties"]["intent"]["enum"], INTENTS)
        out = sorted(bm.OUT_DIR.glob("*-b1-*.jsonl"))[-1]
        rows = [json.loads(x) for x in out.read_text(encoding="utf-8").splitlines()]
        self.assertEqual(rows[0]["record"], "run")
        self.assertEqual(rows[0]["digest"], "abc123def456")
        self.assertEqual(len(rows), 7)

    def test_b1_reports_an_invalid_reply(self):
        old = REPLY["text"]
        REPLY["text"] = "this is not json"
        try:
            self.assertIn("0/3 valid", self.run_cli("b1", "--model", "fake:1b", "--n", "3"))
        finally:
            REPLY["text"] = old

    def test_b0_reports_speed_and_the_share_on_the_gpu(self):
        text = self.run_cli("b0", "--model", "fake:1b", "--runs", "4", "--think", "on")
        self.assertIn("75% on the GPU", text)
        self.assertIn("generation 50.0 tok/s", text)                    # 20 tokens in 0.4 s

    def test_b2_asks_each_topic_earned_and_withheld(self):
        SEEN.clear()
        text = self.run_cli("b2", "--model", "fake:1b", "--limit", "4")
        self.assertEqual(len(SEEN), 8)
        self.assertIn("earned", text)
        self.assertIn("withheld", text)

    def test_blind_sheet_hides_the_model_and_the_key_maps_it_back(self):
        self.run_cli("b2", "--model", "fake:1b", "--limit", "3")
        files = [str(p) for p in bm.OUT_DIR.glob("*-b2-*.jsonl")]
        self.run_cli("blind", *files)
        sheet = (bm.OUT_DIR / "blind-sheet.csv").read_text(encoding="utf-8")
        self.assertNotIn("fake:1b", sheet)
        key = json.loads((bm.OUT_DIR / "blind-key.json").read_text(encoding="utf-8"))
        self.assertTrue(all(v["model"] == "fake:1b" for v in key.values()))
        self.assertEqual(len(key), sheet.count("\n") - 1 if sheet.count("\n") > 1 else 0)

    def test_unreachable_ollama_exits_with_a_clear_code(self):
        out = io.StringIO()
        with redirect_stdout(out):
            code = bm.main(["--url", "http://127.0.0.1:9", "inventory"])
        self.assertEqual(code, 2)
        self.assertIn("ollama serve", out.getvalue())


if __name__ == "__main__":
    unittest.main(verbosity=1)
