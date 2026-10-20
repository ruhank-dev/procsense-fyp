"""Tests of the Ollama Cloud experiment adapter (tools/bench/cloud_eval.py), with a fake transport and NO network: a 401 is a blocker and nothing more is sent, `run` refuses without a successful smoke test for the same model, the call cap and the stop on a blocking status or three failures
are enforced, the model that answered is recorded, an output is only a candidate interpretation (the credit rule is rubric_eval's, reused unchanged and tested by tools/ci/test_rubric_eval.py), results stay under the git-ignored artifacts, and the frozen safety final set is neither read nor changed."""
import json
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "bench"))
import cloud_eval as C  # noqa: E402
import rubric_eval as R  # noqa: E402


class Resp:
    def __init__(self, status, body):
        self.status_code, self._b = status, body
        self.text = json.dumps(body)

    def json(self):
        return self._b


def fake(status=200, content="pong", model="gpt-oss:20b", calls=None):
    def _post(url, body, headers, timeout):
        if calls is not None:
            calls.append((url, body))
        return Resp(status, {"model": model, "message": {"content": content, "thinking": ""}, "done_reason": "stop", "total_duration": 1_000_000, "prompt_eval_count": 12, "eval_count": 3} if status < 400 else {"error": "x"})
    return _post


class Cloud(unittest.TestCase):
    def test_smoke_records_the_model_that_answered(self):
        r = C.smoke("gpt-oss:20b-cloud", "local", _post=fake())
        self.assertTrue(r["ok"])
        self.assertEqual(r["answered_by"], "gpt-oss:20b")
        self.assertEqual((r["prompt_tokens"], r["output_tokens"]), (12, 3))

    def test_a_401_is_a_blocker_and_not_ok(self):
        r = C.smoke("gpt-oss:20b-cloud", "local", _post=fake(status=401))
        self.assertFalse(r["ok"])
        self.assertIn("NOT SIGNED IN", r["blocker"])

    def test_run_refuses_without_a_good_smoke_test_for_the_same_model(self):
        with tempfile.TemporaryDirectory() as t:
            f = Path(t) / "smoke.json"
            calls = []
            self.assertFalse(C.run("dev", "x", "m", "local", 5, 1, 5.0, "low", 100, _post=fake(calls=calls), smoke_file=f)["started"])
            f.write_text(json.dumps({"ok": False, "model_requested": "m"}), encoding="utf-8")
            self.assertFalse(C.run("dev", "x", "m", "local", 5, 1, 5.0, "low", 100, _post=fake(calls=calls), smoke_file=f)["started"])
            f.write_text(json.dumps({"ok": True, "model_requested": "other"}), encoding="utf-8")
            self.assertFalse(C.run("dev", "x", "m", "local", 5, 1, 5.0, "low", 100, _post=fake(calls=calls), smoke_file=f)["started"])
            self.assertEqual(calls, [])

    def _ok_smoke(self, t):
        f = Path(t) / "smoke.json"
        f.write_text(json.dumps({"ok": True, "model_requested": "m"}), encoding="utf-8")
        return f

    def test_call_cap_and_blocking_status_and_failure_streak(self):
        with tempfile.TemporaryDirectory() as t:
            f = self._ok_smoke(t)
            old = C.OUT
            C.OUT = Path(t) / "out"
            try:
                calls = []
                r = C.run("dev", "cap", "m", "local", 4, 1, 5.0, "low", 100, _post=fake(content="{}", calls=calls), smoke_file=f)
                self.assertEqual((r["calls"], len(calls)), (4, 4))
                calls = []
                r = C.run("dev", "blk", "m", "local", 50, 1, 5.0, "low", 100, _post=fake(status=429, calls=calls), smoke_file=f)
                self.assertEqual((r["stopped"], len(calls)), ("BLOCKER_HTTP_429", 1))
                calls = []
                r = C.run("dev", "streak", "m", "local", 50, 1, 5.0, "low", 100, _post=fake(status=500, calls=calls), smoke_file=f)
                self.assertEqual((r["stopped"], len(calls)), ("THREE_FAILURES_IN_A_ROW", 3))
            finally:
                C.OUT = old

    def test_only_benchmark_sentences_and_definitions_are_sent_and_no_key_is_in_the_body(self):
        with tempfile.TemporaryDirectory() as t:
            f = self._ok_smoke(t)
            old = C.OUT
            C.OUT = Path(t) / "out"
            try:
                calls = []
                C.run("dev", "body", "m", "local", 2, 1, 5.0, "low", 100, _post=fake(content="{}", calls=calls), smoke_file=f)
            finally:
                C.OUT = old
            for url, body in calls:
                self.assertTrue(url.endswith("/api/chat"))
                text = json.dumps(body).lower()
                for banned in ("api_key", "authorization", "password", "patient name", "nhs"):
                    self.assertNotIn(banned, text)
                self.assertEqual({m["role"] for m in body["messages"]}, {"system", "user"})

    def test_no_billing_or_signin_action_exists_in_the_adapter(self):
        src = (ROOT / "tools" / "bench" / "cloud_eval.py").read_text(encoding="utf-8")
        for needle in ("signin", "billing", "purchase", "upgrade", "subprocess", "os.system"):
            self.assertNotIn(needle + "(", src)
        self.assertNotIn("print(os.environ", src)

    def test_the_frozen_safety_final_set_and_candidate_are_untouched(self):
        sys.path.insert(0, str(ROOT / "tools" / "bench"))
        import safety_frozen
        self.assertEqual(safety_frozen.check(), [])
        self.assertNotIn("final.jsonl", (ROOT / "tools" / "bench" / "cloud_eval.py").read_text(encoding="utf-8").split('"""')[2])


if __name__ == "__main__":
    unittest.main()
