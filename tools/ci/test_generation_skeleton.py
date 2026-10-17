"""Component selection and the scenario skeleton (server/generation/skeleton.py, p1-live-scenario-generation-s03, stages LG-04 baseline and LG-05).

The property test runs SEEDS seeds (200 in CI to keep the build short; PROCSENSE_SEEDS=10000 is the full run of the plan, about 15 minutes).
"""
import json
import os
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
from generation import contracts, objectives as ob, retrieve, skeleton  # noqa: E402

sys.path.insert(0, str(ROOT / "tools"))
sys.path.insert(0, str(ROOT / "tools" / "content"))
from composer import blocks as B, solve, stage_b  # noqa: E402

SEEDS = int(os.environ.get("PROCSENSE_SEEDS", "200"))
TABLES = ob.Tables.load()
CASES = B.load_cases()
INDEX = retrieve.build_index()
LIBRARY = solve.load_library()
LABELS = {b["id"]: b["label"] for b in json.loads((ROOT / "content" / "assessment" / "mapping.json").read_text(encoding="utf-8"))["behaviours"]}
DISCOVERY = json.loads((ROOT / "content" / "problems" / "discovery.json").read_text(encoding="utf-8"))["discovery"]


def objectives_for(seed: int, scenario: str = "S1", level: str = "beginner", history=None, fingerprints=None) -> dict:
    req = contracts.accept_request({"version": 1, "trainee": "trainee-001", "mode": "practice", "scenario": scenario, "level": level, "seed": seed}, lambda n: 0)
    o = ob.derive(req, history or [], TABLES, fingerprints)
    return o


class Skeleton(unittest.TestCase):
    def test_a_new_trainee_gets_a_valid_composed_case_for_each_family(self):
        ctx = stage_b.load_ctx(False, CASES)
        for scenario in ("S1", "S2"):
            sk = skeleton.build_skeleton(objectives_for(11, scenario), INDEX, CASES, TABLES, LABELS, LIBRARY)
            card = sk["case"]
            self.assertRegex(card["id"], r"^GEN-S[12]-[0-9]{4}-[0-9]{3}$")
            self.assertEqual((card["scenario"], card["created_by"], card["status"]), (scenario, "composer", "DRAFT"))
            ctx["others"] = CASES
            self.assertTrue(stage_b.validate(card, ctx).passed)
            self.assertEqual(contracts.errors("scenario_package", {"version": 1, "id": card["id"], "status": "GENERATED_PRACTICE_ONLY", "request_id": sk["request_id"], "case": card,
                                                                    "provenance": {"generator": "skeleton", "seed": 11, "spine": card["composition"]["spine"], "blocks": card["composition"]["blocks"], "content_hash": INDEX.content_hash, "packs": {}},
                                                                    "fingerprint": sk["fingerprint"]}), [])

    def test_the_same_objectives_give_the_same_skeleton(self):
        a = skeleton.build_skeleton(objectives_for(21), INDEX, CASES, TABLES, LABELS, LIBRARY)
        b = skeleton.build_skeleton(objectives_for(21), INDEX, CASES, TABLES, LABELS, LIBRARY)
        self.assertEqual(json.dumps(a, sort_keys=True), json.dumps(b, sort_keys=True))

    def test_the_skeleton_carries_the_spines_discovery_rules(self):
        sk = skeleton.build_skeleton(objectives_for(31), INDEX, CASES, TABLES, LABELS, LIBRARY)
        spine = sk["selection"]["spine"]
        self.assertEqual(sk["discovery"], [r for r in DISCOVERY if r["case"] == spine])

    def test_recently_played_spines_and_met_stories_are_left_out(self):
        first = skeleton.build_skeleton(objectives_for(41), INDEX, CASES, TABLES, LABELS, LIBRARY)
        spine, fp = first["selection"]["spine"], first["fingerprint"]
        history = [ob.SessionRecord(spine, "P0", {"b01": (3, 1)})]
        second = skeleton.build_skeleton(objectives_for(41, history=history, fingerprints=[fp]), INDEX, CASES, TABLES, LABELS, LIBRARY)
        self.assertNotEqual(second["selection"]["spine"], spine)
        # the same spine allowed again but its story fingerprint avoided: other blocks are tried, so the story differs
        o = objectives_for(41, fingerprints=[fp])
        third = skeleton.build_skeleton(o, INDEX, CASES, TABLES, LABELS, LIBRARY)
        self.assertNotEqual(third["fingerprint"], fp)

    def test_the_advanced_level_may_use_harder_tiers_than_the_beginner(self):
        tiers_b = {next(c for c in CASES if c["id"] == skeleton.build_skeleton(objectives_for(s, level="beginner"), INDEX, CASES, TABLES, LABELS, LIBRARY)["selection"]["spine"])["tier"] for s in range(30)}
        tiers_a = {next(c for c in CASES if c["id"] == skeleton.build_skeleton(objectives_for(s, level="advanced"), INDEX, CASES, TABLES, LABELS, LIBRARY)["selection"]["spine"])["tier"] for s in range(30)}
        self.assertLessEqual(tiers_b, {"P0", "P1"})
        self.assertTrue(tiers_a >= tiers_b)

    def test_no_spine_gives_a_clear_error(self):
        o = objectives_for(1)
        o["exclusions"]["spines"] = [c["id"] for c in CASES if c["scenario"] == "S1"]
        with self.assertRaises(solve.Unsatisfiable):
            skeleton.build_skeleton(o, INDEX, CASES, TABLES, LABELS, LIBRARY)

    def test_property_zero_constraint_violations_and_every_problem_discoverable_over_many_seeds(self):
        by_id = {b["id"]: b for b in LIBRARY}
        listed = {(r["case"], r["problem"]) for r in DISCOVERY}
        bad = []
        for seed in range(SEEDS):
            scenario = "S1" if seed % 2 == 0 else "S2"
            level = "advanced" if seed % 3 == 0 else "beginner"
            sk = skeleton.build_skeleton(objectives_for(seed, scenario, level), INDEX, CASES, TABLES, LABELS, LIBRARY)
            card = sk["case"]
            spine = next(c for c in CASES if c["id"] == card["composition"]["spine"])
            tags = set(B.spine_tags(spine))
            for bid in card["composition"]["blocks"]:
                tags |= set(by_id[bid]["tags"])
            for bid in card["composition"]["blocks"]:
                b = by_id[bid]
                if not set(b["requires"]) <= tags or set(b["excludes"]) & (tags - set(b["tags"])) or set(b["excludes"]) & B.spine_tags(spine):
                    bad.append((seed, bid, "constraint"))
            if spine["tier"] == "P0":
                for p in card["problems"]:
                    if p["type"] != "information_only" and (spine["id"], p["id"]) not in listed:
                        bad.append((seed, p["id"], "not discoverable"))
            if card["identity"]["sex"] != spine["identity"]["sex"] or B.age_class(card["identity"]["age"]) != B.age_class(spine["identity"]["age"]):
                bad.append((seed, "identity", "changed the sex or age class"))
        self.assertEqual(bad[:5], [])


if __name__ == "__main__":
    unittest.main()
