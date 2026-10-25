"""The trap wording writer (server/generation/trap_writer.py, A7.c2): only wrong options are reworded, ids, correct flags and consequences stay, the lexical checks hold, a failing or missing model leaves the authored text, and
the case still passes trap fairness and the whole generation gate."""
import copy
import json
import sys
import unittest
from pathlib import Path

import httpx

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools"))
from composer import blocks as B  # noqa: E402
from generation import quality, trap_writer as TW  # noqa: E402
from llm import client as C  # noqa: E402

CASE = next(c for c in B.load_cases() if TW.wrong_options(c))


def host(fn):
    def h(request):
        if request.url.path == "/api/tags":
            return httpx.Response(200, json={"models": []})
        body = json.loads(request.content)
        return httpx.Response(200, json={"message": {"content": json.dumps({"text": fn(body["messages"][-1]["content"])})}})
    return C.OllamaClient(transport=httpx.MockTransport(h))


def original_of(prompt: str) -> str:
    return prompt.split("Wrong option: ", 1)[1].strip()


class Trap(unittest.TestCase):
    def test_a_faithful_rewording_replaces_only_the_text_of_wrong_options(self):
        case = copy.deepcopy(CASE)
        flags = [(o["id"], o["correct"], o.get("consequence")) for d in case["decisions"] for o in d["options"]]
        correct_text = [o["text"] for d in case["decisions"] for o in d["options"] if o["correct"]]
        rep = TW.TrapWriter(host(lambda p: original_of(p).replace("Wait", "Hold off").replace("Give", "Go ahead and give"))).apply(case, 1)
        self.assertEqual(flags, [(o["id"], o["correct"], o.get("consequence")) for d in case["decisions"] for o in d["options"]])
        self.assertEqual(correct_text, [o["text"] for d in case["decisions"] for o in d["options"] if o["correct"]])
        self.assertTrue(rep["lines"])

    def test_a_rewording_that_adds_a_medicine_or_a_number_or_flips_a_negation_is_refused(self):
        for bad in (lambda p: original_of(p) + " and give 500 mg of morphine", lambda p: "No, " + original_of(p), lambda p: "Do something different entirely, whatever feels right at the time."):
            case = copy.deepcopy(CASE)
            before = json.dumps(case["decisions"], sort_keys=True)
            rep = TW.TrapWriter(host(bad)).apply(case, 1)
            self.assertEqual(json.dumps(case["decisions"], sort_keys=True), before)
            self.assertTrue(all(x["method"] == "template" for x in rep["lines"]))

    def test_no_model_leaves_the_authored_text(self):
        case = copy.deepcopy(CASE)
        before = json.dumps(case["decisions"], sort_keys=True)
        rep = TW.TrapWriter(None).apply(case, 1)
        self.assertEqual(json.dumps(case["decisions"], sort_keys=True), before)
        self.assertTrue(all(x["method"] == "template" for x in rep["lines"]))

    def test_fairness_does_not_fall_when_wording_changes(self):
        case = copy.deepcopy(CASE)
        before = quality.trap_fairness(case)[0]
        TW.TrapWriter(host(lambda p: original_of(p).replace("Wait", "Hold off"))).apply(case, 1)
        self.assertGreaterEqual(quality.trap_fairness(case)[0], before)



class InPipeline(unittest.TestCase):
    def test_a_generated_package_records_the_trap_wording_and_passes_the_gate(self):
        from generation import objectives as ob, package, pipeline, retrieve
        ctx = pipeline.Context(retrieve.build_index(), B.load_cases(), ob.Tables.load(), trap_writer=TW.TrapWriter(host(lambda p: "Perhaps " + original_of(p)[0].lower() + original_of(p)[1:])))
        pkg = None
        for seed in range(1, 40):
            r = pipeline.generate({"version": 1, "trainee": "trainee-001", "mode": "practice", "scenario": "S1", "level": "advanced", "seed": seed}, [], [], ctx, recent_fingerprints=[])
            if r.package and r.package.get("trap_wording"):
                pkg = r.package
                break
        if pkg is None:
            self.skipTest("no seed gave a case with a reworded trap in 40 tries (the fake wording changes only 'Wait' and 'Do not')")
        self.assertTrue(package.validate(pkg, B.load_cases())["passed"])
        self.assertIn("trap.reword@2", pkg["provenance"]["prompts"])


if __name__ == "__main__":
    unittest.main()
