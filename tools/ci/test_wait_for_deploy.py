"""The deploy checker (tools/ci/wait_for_deploy.py): it waits for the new commit, tolerates a service that is asleep or still starting, and fails with a plain sentence when the time runs out or the database does not answer."""
import json
import sys
import threading
import unittest
from http.server import BaseHTTPRequestHandler, HTTPServer
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import wait_for_deploy  # noqa: E402

SHA = "304d1f1db9b3f6565021592352a82512264d0621"


class Script:
    """Answers each call in turn from a list of (status, body); the last answer repeats."""
    def __init__(self, health, db=None):
        self.health, self.db, self.calls = list(health), db or (200, {"status": "ok", "latency_ms": 11.5}), []

    def __call__(self, url, timeout=30.0):
        self.calls.append(url)
        if url.endswith("/health/db"):
            return self.db
        return self.health.pop(0) if len(self.health) > 1 else self.health[0]


class Clock:
    def __init__(self):
        self.now = 0.0

    def __call__(self):
        return self.now

    def sleep(self, seconds):
        self.now += seconds


def run(script, timeout=100.0, interval=10.0):
    clock, said = Clock(), []
    result = wait_for_deploy.wait("https://svc.test/", SHA, timeout, interval, say=said.append, sleep=clock.sleep, clock=clock, fetch=script)
    return result, said, clock.now


class WaitForDeploy(unittest.TestCase):
    def test_it_waits_through_no_answer_and_the_old_commit_and_then_succeeds(self):
        old = (200, {"status": "ok", "git_sha": "0a25a3f" + "0" * 33})
        script = Script([(0, {}), (0, {}), (503, {}), old, (200, {"status": "ok", "git_sha": SHA})])
        (ok, message), said, elapsed = run(script)
        self.assertTrue(ok, message)
        self.assertIn("database answers", message)
        self.assertEqual(elapsed, 40.0)   # four waits of ten seconds before the fifth answer
        self.assertEqual(script.calls[-1], "https://svc.test/health/db")   # the database is asked once, after the commit is right
        self.assertEqual(script.calls.count("https://svc.test/health/db"), 1)

    def test_a_short_commit_id_is_enough(self):
        (ok, _), _, _ = run(Script([(200, {"git_sha": SHA})]))
        self.assertTrue(ok)
        clock = Clock()
        ok, _ = wait_for_deploy.wait("https://svc.test", SHA[:7], 10, 1, say=lambda s: None, sleep=clock.sleep, clock=clock, fetch=Script([(200, {"git_sha": SHA})]))
        self.assertTrue(ok)

    def test_it_gives_up_with_a_plain_sentence_when_the_new_commit_never_appears(self):
        (ok, message), said, elapsed = run(Script([(200, {"status": "ok", "git_sha": "abcdef" + "0" * 34})]), timeout=35, interval=10)
        self.assertFalse(ok)
        self.assertIn("did not start running 304d1f1db9b3", message)
        self.assertIn("running abcdef000000", message)
        self.assertLessEqual(elapsed, 35)

    def test_it_reports_no_answer_at_all(self):
        (ok, message), _, _ = run(Script([(0, {})]), timeout=25, interval=10)
        self.assertFalse(ok)
        self.assertIn("no answer", message)

    def test_a_database_that_does_not_answer_is_a_failure_even_when_the_commit_is_right(self):
        (ok, message), _, _ = run(Script([(200, {"git_sha": SHA})], db=(503, {"status": "error", "reason": "timeout"})))
        self.assertFalse(ok)
        self.assertIn("database does not answer", message)
        self.assertIn("timeout", message)

    def test_a_real_server_old_then_new(self):
        state = {"calls": 0}

        class Handler(BaseHTTPRequestHandler):
            def do_GET(self):
                if self.path == "/health":
                    state["calls"] += 1
                    body = {"status": "ok", "git_sha": SHA if state["calls"] >= 3 else "old"}
                elif self.path == "/health/db":
                    body = {"status": "ok", "latency_ms": 3}
                else:
                    self.send_response(404); self.end_headers(); return
                data = json.dumps(body).encode()
                self.send_response(200); self.send_header("Content-Type", "application/json"); self.end_headers(); self.wfile.write(data)

            def log_message(self, *args):
                pass

        server = HTTPServer(("127.0.0.1", 0), Handler)
        threading.Thread(target=server.serve_forever, daemon=True).start()
        try:
            ok, message = wait_for_deploy.wait(f"http://127.0.0.1:{server.server_port}", SHA, timeout=20, interval=0.05, say=lambda s: None)
            self.assertTrue(ok, message)
            self.assertGreaterEqual(state["calls"], 3)
            self.assertEqual(wait_for_deploy.get_json("http://127.0.0.1:1/health")[0], 0)   # nothing listens: "no answer", not a crash
        finally:
            server.shutdown()
            server.server_close()


if __name__ == "__main__":
    unittest.main(verbosity=1)
