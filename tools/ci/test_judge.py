"""The scenario quality judge (server/generation/judge.py, A9.c2): it returns scores from a fake model, returns None when the host is down or the answer breaks the schema, and the pipeline never reads it (advisory only)."""
import json
import sys
import unittest
from pathlib import Path

import httpx

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools"))
from composer import blocks as B  # noqa: E402
from generation import judge  # noqa: E402
from llm import client as C  # noqa: E402


def host(answer):
    return C.OllamaClient(transport=httpx.MockTransport(lambda r: httpx.Response(200, json={"models": []}) if r.url.path == "/api/tags" else httpx.Response(200, json={"message": {"content": json.dumps(answer)}})))


CASE = B.load_cases()[0]


class Judge(unittest.TestCase):
    def test_scores_come_back_and_the_lowest_is_found(self):
        s = judge.Judge(host({"coherence": 2, "plausibility": 4, "persona": 5, "reading": 4, "comment": "h.allergy contradicts the allergy fact"})).score(CASE)
        self.assertEqual((s.coherence, s.lowest()), (2, 2))

    def test_a_scale_breaking_or_missing_answer_gives_none(self):
        self.assertIsNone(judge.Judge(host({"coherence": 9, "plausibility": 4, "persona": 5, "reading": 4, "comment": "x"})).score(CASE))
        self.assertIsNone(judge.Judge(host({"coherence": 3})).score(CASE))

        def down(request):
            raise httpx.ConnectError("down")
        self.assertIsNone(judge.Judge(C.OllamaClient(transport=httpx.MockTransport(down))).score(CASE))

    def test_nothing_in_the_pipeline_reads_the_judge(self):
        text = "".join(p.read_text(encoding="utf-8") for p in (ROOT / "server" / "generation").glob("*.py") if p.name != "judge.py")
        self.assertNotIn("generation.judge", text)
        self.assertNotIn("import judge", text)
        self.assertNotIn("Judge(", text)                                # quality.evaluate only logs a judge dict it is given; nothing constructs one


if __name__ == "__main__":
    unittest.main()
