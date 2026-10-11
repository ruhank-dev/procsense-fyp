"""Tests of the Stage B validator and the mutation generator (plan tests T-VB1 to T-VB5; run by tools/ci/run_all.py)."""
from __future__ import annotations

import copy
import json
import sys
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(HERE.parent))
sys.path.insert(0, str(ROOT / "tools" / "content"))

from composer import mutate, stage_b  # noqa: E402
from composer.checks.c04_contradictions import CONTRADICTIONS  # noqa: E402

CASES = stage_b.load_cases()
CTX = stage_b.load_ctx(False, CASES)
BY_ID = {c["id"]: c for c in CASES}


class StageB(unittest.TestCase):
    def test_T_VB2_every_valid_case_passes_all_checks(self):
        bad = {c["id"]: [d.code for d in stage_b.validate(c, CTX).defects] for c in CASES if not stage_b.validate(c, CTX).passed}
        self.assertEqual(bad, {})
        self.assertEqual(len(CASES), 53)

    def test_there_are_exactly_12_contradiction_types_and_the_file_describes_each(self):
        codes = [c for c, _ in CONTRADICTIONS]
        self.assertEqual(len(codes), 12)
        described = json.loads((ROOT / "content" / "contradictions.json").read_text(encoding="utf-8"))["contradictions"]
        self.assertEqual([d["code"] for d in described], codes)
        self.assertEqual(sorted(mutate.MUTATIONS), sorted(codes))
        for d in described:
            self.assertTrue(d["meaning"] and d["source"], d["code"])

    def test_T_VB1_all_12_types_are_rejected_60_of_60(self):
        rows = mutate.run(list(mutate.MUTATIONS), 5, CTX, CASES)
        missed = [r for r in rows if r[2] != "rejected"]
        self.assertEqual(missed, [])
        self.assertEqual(len(rows), 60)

    def test_a_mutation_that_changes_nothing_is_reported_as_the_mutators_error_not_a_validator_miss(self):
        rows = mutate.run(["PREGNANT_MALE"], 500, CTX, CASES)             # asks for more cases than apply
        self.assertTrue(any("DID NOT LAND" in r[2] for r in rows))

    def test_T_VB3_an_ai_draft_is_never_approved_by_the_validator(self):
        c = copy.deepcopy(BY_ID["VAR-S1-A"])
        c["created_by"] = "ai_draft"
        self.assertIn("AI_CARD_AUTO_APPROVED", stage_b.validate(c, CTX).codes())     # APPROVED_FOR_TRAINING from a draft
        c["status"] = "CLINICAL_REVIEW"
        c.pop("approval", None)
        self.assertNotIn("AI_CARD_AUTO_APPROVED", stage_b.validate(c, CTX).codes())

    def test_T_VB5_an_event_bound_to_a_case_that_can_never_fire_it_is_ineligible(self):
        from composer.checks import c05_to_c11 as c
        original = c.bound_events
        try:
            c.bound_events = lambda case_id: [("w29", "E-HYPERSENSITIVITY")] if case_id == "VAR-S1-A" else original(case_id)     # the example patient has no allergy
            self.assertIn("EVENT_INELIGIBLE", stage_b.validate(BY_ID["VAR-S1-A"], CTX).codes())
            c.bound_events = lambda case_id: [("w29", "E-HYPERSENSITIVITY")] if case_id == "STK-S2-B" else original(case_id)
            self.assertNotIn("EVENT_INELIGIBLE", stage_b.validate(BY_ID["STK-S2-B"], CTX).codes())      # this one lists a penicillin allergy
        finally:
            c.bound_events = original

    def test_every_binding_in_the_bindings_file_can_fire(self):
        import csv
        from composer.checks.c05_to_c11 import event_reachable
        with open(ROOT / "content" / "whatif_bindings.csv", encoding="utf-8", newline="") as f:
            rows = list(csv.DictReader(f))
        self.assertGreater(len(rows), 20)
        for r in rows:
            if r["event"]:
                self.assertTrue(event_reachable(r["event"], BY_ID[r["case"]]), f"{r['case']} {r['event']}")

    def test_the_report_names_every_defect_and_the_checks_run(self):
        c = copy.deepcopy(BY_ID["VAR-S1-A"])
        c["identity"]["age"] += 9
        page = stage_b.html_report(stage_b.validate(c, CTX))
        self.assertIn("IDENTITY_MISMATCH", page)
        self.assertIn("8. results and orderable tests", page)
        self.assertIn("FAIL", page)

    def test_schema_errors_stop_the_later_checks_from_running_on_a_broken_card(self):
        c = copy.deepcopy(BY_ID["VAR-S1-A"])
        del c["identity"]
        r = stage_b.validate(c, CTX)
        self.assertEqual({d.check for d in r.defects}, {1})


if __name__ == "__main__":
    unittest.main()
