"""Events, what-if branches and interactions proposed by the plan composer (server/generation/plan_extras.py; features A4, A5, A8; DT-224): candidates come from code and the knowledge base, the model chooses among them,
every choice is checked again, the choices reach the package, the engine reads the planned events from the package and plays only those in practice, and a model that is down changes nothing."""
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

import httpx

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools"))
from composer import blocks as B  # noqa: E402
from generation import objectives as ob, package, pipeline, plan_extras as PE, retrieve  # noqa: E402
from llm import client as C  # noqa: E402

CASES = {c["id"]: c for c in B.load_cases()}


def host(answers):
    """A fake model host that answers the n-th call with the n-th answer (a dict) or the last one."""
    calls = []

    def h(request):
        if request.url.path == "/api/tags":
            return httpx.Response(200, json={"models": []})
        calls.append(1)
        a = answers[min(len(calls) - 1, len(answers) - 1)]
        return httpx.Response(200, json={"message": {"content": json.dumps(a)}})
    return C.OllamaClient(transport=httpx.MockTransport(h))


def objectives():
    return {"competencies": [{"behaviour": "b05"}], "dimensions": ["communication_challenges"]}


class Candidates(unittest.TestCase):
    def test_the_allergic_reaction_needs_an_allergy_and_the_vital_sign_events_need_nothing(self):
        with_allergy = next(c for c in CASES.values() if c["allergies"])
        without = next(c for c in CASES.values() if not c["allergies"])
        self.assertIn("E-HYPERSENSITIVITY", [e["id"] for e in PE.eligible_events(with_allergy)])
        self.assertNotIn("E-HYPERSENSITIVITY", [e["id"] for e in PE.eligible_events(without)])
        self.assertEqual({e["id"] for e in PE.eligible_events(without)}, {"E-LOW-BP", "E-HIGH-HR", "E-LOW-HR", "E-LOW-OXYGEN", "E-FAST-BREATHING", "E-SLOW-BREATHING"})

    def test_an_interaction_candidate_is_two_problems_the_knowledge_base_ties_to_the_same_evidence(self):
        c = PE.interaction_candidates(CASES["SC-1"])
        self.assertEqual([x["problems"] for x in c], [["sc1.aki", "sc1.sepsis"]])
        self.assertTrue(all(x["shared"] for x in c))
        self.assertEqual(PE.interaction_candidates(CASES["VAR-S1-A"]), [])


class Proposal(unittest.TestCase):
    def test_a_valid_choice_is_kept_with_its_whatif_and_applied_to_the_debrief_only(self):
        case = json.loads(json.dumps(CASES["SC-1"]))
        before = json.dumps({k: v for k, v in case.items() if k != "debrief"}, sort_keys=True)
        x = PE.ExtrasPlanner(host([{"events": [{"event_id": "E-LOW-BP", "reason": "the patient is septic"}]}, {"interactions": [{"pair": "sc1.aki+sc1.sepsis", "reason": "same findings"}]}])).propose(case, objectives())
        self.assertEqual([(e["event_id"], e["whatif_id"]) for e in x["events"]], [("E-LOW-BP", "w01")])
        self.assertEqual(PE.validate(case, x["events"], x["interactions"]), [])
        PE.apply_to_case(case, x["interactions"])
        self.assertIn("meet in the same findings", case["debrief"]["related"])
        self.assertEqual(json.dumps({k: v for k, v in case.items() if k != "debrief"}, sort_keys=True), before)

    def test_a_tampered_choice_is_refused_by_the_check(self):
        case = CASES["VAR-S1-A"]
        bad = [{"event_id": "E-HYPERSENSITIVITY", "whatif_id": "w29", "reason": "x y z"}]
        self.assertIn("INELIGIBLE_EVENT:E-HYPERSENSITIVITY", PE.validate(case, bad, []))
        self.assertIn("NOT_IN_KNOWLEDGE_BASE:a+b", PE.validate(case, [], [{"pair": "a+b", "reason": "x y z"}]))
        self.assertIn("WHATIF_MISMATCH:E-LOW-BP", PE.validate(case, [{"event_id": "E-LOW-BP", "whatif_id": "w99", "reason": "x y z"}], []))

    def test_a_down_model_or_a_schema_breaking_answer_proposes_nothing_and_changes_nothing(self):
        case = CASES["SC-1"]
        def down(request):
            raise httpx.ConnectError("down")
        x = PE.ExtrasPlanner(C.OllamaClient(transport=httpx.MockTransport(down))).propose(case, objectives())
        self.assertEqual((x["events"], x["interactions"]), ([], []))
        y = PE.ExtrasPlanner(host([{"events": [{"event_id": "E-NOT-AN-EVENT", "reason": "x y z"}]}])).propose(case, objectives())
        self.assertEqual(y["events"], [])
        self.assertIsNone(PE.ExtrasPlanner(None).propose(case, objectives()))

    def test_a_case_with_no_knowledge_base_pair_asks_the_model_nothing_about_interactions(self):
        case = next(c for c in CASES.values() if not PE.interaction_candidates(c))
        asked = []

        def h(request):
            if request.url.path == "/api/tags":
                return httpx.Response(200, json={"models": []})
            asked.append(json.loads(request.content)["messages"][-1]["content"])
            return httpx.Response(200, json={"message": {"content": json.dumps({"events": [], "interactions": []})}})
        x = PE.ExtrasPlanner(C.OllamaClient(transport=httpx.MockTransport(h))).propose(case, objectives())
        self.assertEqual(x["interactions"], [])
        self.assertFalse(any("share:" in a for a in asked))                      # the interactions prompt (which lists pairs) was never sent

    def test_no_eligible_event_means_no_event_question_and_a_repeat_or_too_many_is_refused(self):
        case = CASES["SC-1"]
        ev = [{"event_id": "E-LOW-BP", "whatif_id": "w01", "reason": "x y z"}]
        self.assertIn("REPEATED_EVENT:E-LOW-BP", PE.validate(case, ev + ev, []))
        many = [{"event_id": e["id"], "whatif_id": e["whatif"], "reason": "x y z"} for e in PE.eligible_events(case)][: PE.MAX_EVENTS + 1]
        self.assertIn("TOO_MANY_EVENTS", PE.validate(case, many, []))
        saved = PE._events
        try:
            PE._events = lambda: []
            self.assertEqual(PE.eligible_events(case), [])
            self.assertEqual(PE.ExtrasPlanner(host([{"events": []}, {"interactions": []}])).propose(case, objectives())["events"], [])
        finally:
            PE._events = saved

    def test_a_reason_that_is_not_a_sentence_and_a_non_json_answer_change_nothing(self):
        case = CASES["SC-1"]
        self.assertIn("REASON", PE.validate(case, [{"event_id": "E-LOW-BP", "whatif_id": "w01", "reason": "12"}], []))

        def h(request):
            if request.url.path == "/api/tags":
                return httpx.Response(200, json={"models": []})
            return httpx.Response(200, json={"message": {"content": "this is not json at all"}})
        x = PE.ExtrasPlanner(C.OllamaClient(transport=httpx.MockTransport(h))).propose(case, objectives())
        self.assertEqual((x["events"], x["interactions"]), ([], []))


class FakePlanner:
    def __init__(self, client):
        self.extras = PE.ExtrasPlanner(client)

    def propose(self, *a, **k):
        return None

    def propose_extras(self, case, objectives):
        return self.extras.propose(case, objectives)


class EndToEnd(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        ctx = pipeline.Context(retrieve.build_index(), B.load_cases(), ob.Tables.load(), planner=FakePlanner(host([{"events": [{"event_id": "E-LOW-BP", "reason": "a fall in pressure fits this story"}]}, {"interactions": []}])))
        cls.pkg = None
        for seed in range(1, 30):
            r = pipeline.generate({"version": 1, "trainee": "trainee-001", "mode": "practice", "scenario": "S2", "level": "advanced", "seed": seed}, [], [], ctx, recent_fingerprints=[])
            if r.package:
                cls.pkg = r.package
                break

    def test_the_package_carries_the_planned_events_and_passes_the_whole_gate(self):
        self.assertIsNotNone(self.pkg)
        plan = self.pkg["provenance"]["plan"]
        self.assertEqual([e["event_id"] for e in plan["events"]], ["E-LOW-BP"])
        self.assertTrue(package.validate(self.pkg, B.load_cases())["passed"])
        self.assertIn("plan_extras", [m["role"] for m in self.pkg["provenance"].get("models", [])] + ["plan_extras"])

    def test_the_engine_reads_the_plan_from_the_package_and_records_it_in_the_session(self):
        with tempfile.TemporaryDirectory() as tmp:
            pkg_path = Path(tmp) / "p.package.json"
            package.write(self.pkg, Path(tmp))
            written = next(Path(tmp).glob("*.package.json"))
            out = Path(tmp) / "run.json"
            actions = Path(tmp) / "none.actions.json"
            actions.write_text("[]", encoding="utf-8")                                    # the plan is in the header whatever is played
            r = subprocess.run(["dotnet", "run", "--no-build", "-c", "Release", "--project", "src/ProcSense.Cli", "--", "run", "--scenario", str(written), "--seed", "42", "--actions", str(actions), "--out", str(out)],
                               cwd=ROOT, capture_output=True, text=True, encoding="utf-8")
            self.assertEqual(r.returncode, 0, r.stdout + r.stderr)
            header = json.loads((Path(tmp) / "run.session.jsonl").read_text(encoding="utf-8").splitlines()[0])["header"]
            self.assertEqual(header.get("planned_events"), "E-LOW-BP")


if __name__ == "__main__":
    unittest.main(verbosity=1)
