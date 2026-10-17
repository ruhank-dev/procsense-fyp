"""The block library and the solver (p1-scenario-composer-difficulty s01 and s02, plan tests T-CO1 to T-CO5)."""
import copy
import json
import random
import sys
import unittest
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
sys.path.insert(0, str(HERE.parents[1] / "tools" / "content"))
from composer import blocks as B  # noqa: E402
from composer import solve, stage_b  # noqa: E402

CASES = B.load_cases()
LIBRARY = solve.load_library()


def tags_of(card: dict) -> set[str]:
    by_id = {b["id"]: b for b in LIBRARY}
    spine = next(c for c in CASES if c["id"] == card["composition"]["spine"])
    tags = set(B.spine_tags(spine))
    for bid in card["composition"]["blocks"]:
        tags |= set(by_id[bid]["tags"])
    return tags


class Library(unittest.TestCase):
    def test_the_committed_library_is_current_tagged_and_cut_from_named_cases(self):
        self.assertEqual(B.problems(), [])

    def test_all_21_block_types_are_named_and_five_carry_variation(self):
        self.assertEqual(len(B.TYPES), 21)
        self.assertEqual(sorted(t for t, v in B.TYPES.items() if v[1] == "variable"), ["ALG", "DEM", "NAME", "PSH", "SOC"])

    def test_a_block_takes_the_lowest_status_of_the_cases_it_was_cut_from(self):
        for b in LIBRARY:
            statuses = [next(c["status"] for c in CASES if c["id"] == cid) for cid in b["derived_from"]]
            self.assertEqual(b["status"], B.worst(statuses), b["id"])

    def test_the_templates_name_every_type_once_and_the_difficulty_limits_widen(self):
        for s in ("S1", "S2"):
            t = solve.load_template(s)
            self.assertEqual(sorted(x["type"] for x in t["slots"]), sorted(B.TYPES))
            self.assertLess(t["difficulty_limits"]["1"]["max_problems"], t["difficulty_limits"]["3"]["max_problems"])


class Solver(unittest.TestCase):
    def test_T_CO1_a_thousand_compositions_hold_no_excluded_combination(self):
        bad = []
        total = 0
        for scenario in ("S1", "S2"):
            for difficulty in (1, 2, 3):
                for card in solve.compose(scenario, difficulty, 170, 11 + difficulty, cases=CASES, library=LIBRARY):
                    total += 1
                    tags = tags_of(card)
                    spine_tags = B.spine_tags(next(c for c in CASES if c["id"] == card["composition"]["spine"]))
                    for bid in card["composition"]["blocks"]:
                        b = next(x for x in LIBRARY if x["id"] == bid)
                        if not set(b["requires"]) <= tags or set(b["excludes"]) & (tags - set(b["tags"])) or set(b["excludes"]) & spine_tags:
                            bad.append((card["id"], bid))
        self.assertEqual(total, 1020)
        self.assertEqual(bad, [])

    def test_T_CO2_an_unsatisfiable_spine_gives_a_clear_error(self):
        spine = next(c for c in CASES if c["id"] == "VAR-S1-A")
        library = [b for b in LIBRARY if b["type"] != "DEM"]                      # nobody to be
        with self.assertRaises(solve.Unsatisfiable) as e:
            solve.Solver(library, solve.load_template("S1"), spine, random.Random(1)).solve()
        self.assertIn("VAR-S1-A", str(e.exception))
        with self.assertRaises(solve.Unsatisfiable):
            solve.compose("S1", 1, 1, 1, cases=[c for c in CASES if c["scenario"] != "S1"], library=LIBRARY)       # no spine at all

    def test_T_CO3_the_same_seed_gives_the_same_card_and_another_seed_another(self):
        a = solve.compose("S1", 2, 5, 3, cases=CASES, library=LIBRARY)
        b = solve.compose("S1", 2, 5, 3, cases=CASES, library=LIBRARY)
        c = solve.compose("S1", 2, 5, 4, cases=CASES, library=LIBRARY)
        self.assertEqual(json.dumps(a, sort_keys=True), json.dumps(b, sort_keys=True))
        self.assertNotEqual(json.dumps(a, sort_keys=True), json.dumps(c, sort_keys=True))

    def test_T_CO4_every_card_is_valid_and_the_definition_of_done_holds(self):
        ctx = stage_b.load_ctx(False, CASES)
        for scenario, need in (("S1", 8), ("S2", 6)):
            cards = solve.compose(scenario, 2, 20, 21, cases=CASES, library=LIBRARY)
            passed = 0
            for card in cards:
                ctx["others"] = CASES + [c for c in cards if c is not card]
                if stage_b.validate(card, ctx).passed:
                    passed += 1
            self.assertGreaterEqual(passed, need, f"{scenario}: {passed} of {len(cards)} composed cards passed Stage B")
            self.assertEqual(passed, len(cards), f"{scenario}: every composed card should pass Stage B")

    def test_T_CO5_empty_optional_slots_are_allowed_within_min_and_max(self):
        template = copy.deepcopy(solve.load_template("S1"))
        for s in template["slots"]:
            if s["type"] in ("SOC", "PSH", "ALG"):
                s["min"], s["max"] = 0, 0
        spine = next(c for c in CASES if c["id"] == "VAR-S1-A")
        for seed in range(10):
            ch = solve.Solver(LIBRARY, template, spine, random.Random(seed)).solve()
            self.assertEqual(sorted(b["type"] for b in ch.blocks), ["DEM", "NAME", "NAME"])
        template = solve.load_template("S1")
        seen = set()
        for seed in range(80):
            seen.add(len([b for b in solve.Solver(LIBRARY, template, spine, random.Random(seed)).solve().blocks if b["type"] == "SOC"]))
        self.assertEqual(seen, {0, 1})

    def test_the_search_stops_after_its_step_limit_with_a_clear_message(self):
        spine = next(c for c in CASES if c["id"] == "VAR-S1-A")
        with self.assertRaises(solve.Unsatisfiable) as e:
            solve.Solver(LIBRARY, solve.load_template("S1"), spine, random.Random(1), max_steps=1).solve()
        self.assertIn("gave up", str(e.exception))

    def test_a_composed_card_keeps_the_story_and_changes_only_the_patient(self):
        for card in solve.compose("S1", 3, 40, 5, cases=CASES, library=LIBRARY):
            spine = next(c for c in CASES if c["id"] == card["composition"]["spine"])
            for k in ("scenario", "expected_end_state", "risk", "results", "observations", "examination", "decisions", "scores"):
                self.assertEqual(self._strip(card[k], card, spine), self._strip(spine[k], card, spine), (card["id"], k))
            self.assertEqual(card["identity"]["sex"], spine["identity"]["sex"])
            self.assertEqual(B.age_class(card["identity"]["age"]), B.age_class(spine["identity"]["age"]))
            self.assertEqual(card["created_by"], "composer")
            self.assertEqual(card["status"], "DRAFT")
            self.assertNotIn("approval", card)
            if spine["allergies"]:
                self.assertEqual(card["allergies"], spine["allergies"])

    @staticmethod
    def _strip(x, card, spine):
        """Text with the name, title, occupation and age taken out, so a changed name is not a changed story."""
        s = json.dumps(x, sort_keys=True)
        for case in (card, spine):
            i = case["identity"]
            for w in [i["full_name"], *i["full_name"].split(), i["title"], i["occupation"], str(i["age"])]:
                s = s.replace(w, "#")
        return s

    def test_names_and_record_numbers_never_repeat_an_authored_case_or_each_other(self):
        cards = solve.compose("S2", 3, 120, 9, cases=CASES, library=LIBRARY)
        names = [c["identity"]["full_name"] for c in cards] + [c["identity"]["full_name"] for c in CASES]
        mrns = [c["identity"]["mrn"] for c in cards] + [c["identity"]["mrn"] for c in CASES]
        self.assertEqual(len(names), len(set(names)))
        self.assertEqual(len(mrns), len(set(mrns)))

    def test_targets_weight_the_choice_of_spine(self):
        base = [c["composition"]["spine"] for c in solve.compose("S1", 3, 150, 2, cases=CASES, library=LIBRARY)]
        targeted = [c["composition"]["spine"] for c in solve.compose("S1", 3, 150, 2, targets=["jaundice"], cases=CASES, library=LIBRARY)]
        self.assertGreater(targeted.count("VAR-S1-PB"), base.count("VAR-S1-PB"))


if __name__ == "__main__":
    unittest.main()
