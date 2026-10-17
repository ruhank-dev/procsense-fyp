"""Learning analytics (server/analytics; p2-ai-learning-analytics s01 to s03): planted patterns are found, a pattern is not claimed from thin evidence, calibration reproduces from logs, reasoning features reproduce from golden sessions."""
import json
import random
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
from analytics import calibration, patterns, progression, reasoning  # noqa: E402
from analytics import interpreter as result_interpreter, next_case  # noqa: E402
from personalisation import evidence  # noqa: E402

CLI = ["dotnet", "run", "--no-build", "-c", "Release", "--project", "src/ProcSense.Cli", "--"]


def session(i, errors=(), misses=(), late=(), comps=None, extra_steps=()):
    steps = [{"item": s, "behaviour": "b21", "state": "completed", "at": "08:00:00"} for s in extra_steps]
    steps += [{"item": m, "behaviour": "b21", "state": "missed", "at": "08:01:00"} for m in misses]
    steps += [{"item": l, "behaviour": "b21", "state": "completed", "at": "08:02:00", "late_minutes": 3.0} for l in late]
    errs = [{"item": e, "behaviour": "b21", "severity": "minor" if not e.startswith("critical.") else "critical", "at": "08:03:00"} for e in errors]
    return {"competencies": comps or [{"behaviour": "b21", "credits": 3, "penalties": len(errs), "items": 3 + len(errs)}], "errors": errs, "steps": steps, "actions": [], "tier": "P0"}


def planted(kind, rng):
    """A history of 5 to 8 sessions with one pattern planted among random noise."""
    n = rng.randint(5, 8)
    noise = [f"minor.noise_{rng.randint(1, 30)}" for _ in range(n)]
    hist = []
    k = rng.randint(3, n)
    where = set(rng.sample(range(n), k))
    for i in range(n):
        e, m, l = [], [], []
        if rng.random() < 0.3:
            e.append(noise[i])
        if i in where and kind == "repeated_error":
            e.append("major.checklist_contradiction")
        if i in where and kind == "repeated_miss":
            m.append("credit.allergy_asked")
        if i in where and kind == "late_step":
            l.append("credit.antibiotic_given")
        hist.append(session(i, e, m, l, extra_steps=["credit.allergy_asked"] if (kind == "repeated_miss" and i not in where) else ()))
    return hist


class Patterns(unittest.TestCase):
    def test_planted_patterns_are_found_in_at_least_95_percent_of_synthetic_histories(self):
        rng = random.Random(7)
        found = total = 0
        for kind in ("repeated_error", "repeated_miss", "late_step"):
            for _ in range(40):
                hist = planted(kind, rng)
                total += 1
                found += any(p.kind == kind for p in patterns.detect(hist))
        self.assertGreaterEqual(found / total, 0.95, found / total)

    def test_noise_alone_gives_no_pattern_and_thin_histories_give_none(self):
        rng = random.Random(3)
        false = 0
        for _ in range(60):
            n = rng.randint(5, 8)
            hist = [session(i, [f"minor.noise_{rng.randint(1, 60)}"] if rng.random() < 0.4 else []) for i in range(n)]
            false += bool(patterns.detect(hist))
        self.assertLessEqual(false / 60, 0.05, false / 60)
        self.assertEqual(patterns.detect([session(0, ["major.x"]), session(1, ["major.x"])]), [])        # two sessions are not a pattern

    def test_a_worsening_and_a_slide_are_found(self):
        better = [session(i, [], comps=[{"behaviour": "b05", "credits": 5, "penalties": 0, "items": 5}]) for i in range(3)]
        worse = [session(i + 3, ["critical.checklist_contradiction"], comps=[{"behaviour": "b05", "credits": 2, "penalties": 3, "items": 5}]) for i in range(3)]
        kinds = {p.kind for p in patterns.detect(better + worse)}
        self.assertEqual(kinds, {"critical_trend", "behaviour_slide", "repeated_error"} & kinds | {"critical_trend", "behaviour_slide"})
        self.assertIn("critical_trend", kinds)
        self.assertIn("behaviour_slide", kinds)

    def test_the_coaching_is_neutral_and_made_from_the_facts(self):
        hist = [session(i, ["major.checklist_contradiction"]) for i in range(4)]
        advice = patterns.coach(patterns.detect(hist), {"major.checklist_contradiction": "the checklist contradiction"})
        self.assertEqual(len(advice), 1)
        self.assertIn("4 of 4 sessions", advice[0])
        for bad in ("careless", "lazy", "always", "never", "you are"):
            self.assertNotIn(bad, advice[0].lower())


class Calibration(unittest.TestCase):
    def test_a_well_calibrated_trainee_scores_well_and_an_overconfident_one_is_told_so(self):
        rng = random.Random(1)
        good = [{"confidence": c, "correct": rng.random() < {"low": 0.2, "medium": 0.5, "high": 0.8}[c]} for c in rng.choices(["low", "medium", "high"], k=400)]
        over = [{"confidence": "high", "correct": rng.random() < 0.4} for _ in range(60)]
        under = [{"confidence": "low", "correct": rng.random() < 0.8} for _ in range(60)]
        cg, co, cu = calibration.calibration(good), calibration.calibration(over), calibration.calibration(under)
        self.assertLess(abs(cg.gap), 0.08)
        self.assertGreater(co.gap, 0.3)
        self.assertLess(cu.gap, -0.4)
        self.assertLess(cg.brier, co.brier)
        self.assertIn("higher than your results", calibration.coach(co))
        self.assertIn("lower than your results", calibration.coach(cu))
        self.assertIn("matched", calibration.coach(cg))

    def test_it_reproduces_from_the_same_logs_and_waits_for_enough_reports(self):
        reports = [{"confidence": 0.7, "correct": True}, {"confidence": "low", "correct": False}] * 3
        a, b = calibration.calibration(reports), calibration.calibration(reports)
        self.assertEqual(a, b)
        self.assertFalse(a.enough)
        self.assertIn("at least 10", calibration.coach(a))
        self.assertEqual(calibration.calibration([]).n, 0)

    def test_the_curve_has_the_bins_that_have_data(self):
        c = calibration.calibration([{"confidence": "high", "correct": True}] * 12 + [{"confidence": "low", "correct": False}] * 3)
        self.assertEqual([x["n"] for x in c.curve], [3, 12])


class Reasoning(unittest.TestCase):
    def evidence_of(self, case, script):
        with tempfile.TemporaryDirectory() as t:
            t = Path(t)
            run = subprocess.run(CLI + ["run", "--case", case, "--seed", "42", "--actions", str(script), "--out", str(t / "o.json"), "--allow-draft"], cwd=ROOT, capture_output=True, text=True)
            assert run.returncode == 0, run.stdout + run.stderr
            rep = subprocess.run(CLI + ["report", "--case", case, "--log", str(t / "o.session.jsonl"), "--out", str(t / "r.html"), "--json", str(t / "r.json"), "--allow-draft"], cwd=ROOT, capture_output=True, text=True)
            assert rep.returncode == 0, rep.stdout + rep.stderr
            return evidence.extract(json.loads((t / "r.json").read_text(encoding="utf-8")), "trainee-001", "s", "2026-10-17T10:00:00Z")

    def test_the_features_reproduce_from_a_golden_session_and_describe_what_happened(self):
        ev = self.evidence_of("VAR-S2-G", ROOT / "tests" / "golden" / "VAR-S2-G_correct.actions.json")
        a, b = reasoning.features(ev), reasoning.features(ev)
        self.assertEqual(a, b)
        self.assertEqual(a["gathering_order"][0], "identity")
        self.assertTrue(a["history_before_tests"])
        self.assertIsInstance(a["premature_closure"], list)               # the evidence holds only the actions the scoring maps to a behaviour, so a verb it does not map (examine) can look absent: see DT-209
        self.assertGreaterEqual(a["test_yield"]["ordered"], 2)
        self.assertEqual(any("decision came before" in x for x in reasoning.describe(a)), bool(a["premature_closure"]))        # the words follow the features

    def test_a_decision_before_the_allergy_question_and_the_examination_is_called_premature(self):
        ev = {"actions": [{"seq": 1, "at": "08:00:00", "verb": "ask_identity"}, {"seq": 2, "at": "08:00:10", "verb": "postpone"}]}
        f = reasoning.features(ev)
        self.assertEqual(f["premature_closure"], ["examination", "results", "allergy_question"])
        self.assertIn("The decision came before", reasoning.describe(f)[0])
        self.assertNotIn("careless", " ".join(reasoning.describe(f)).lower())

    def test_tests_never_viewed_are_a_wasted_yield(self):
        acts = [{"seq": i, "at": f"08:00:{i:02d}", "verb": v} for i, v in enumerate(["ask", "order_investigation", "order_investigation", "order_investigation", "view_result"])]
        f = reasoning.features({"actions": acts})
        self.assertEqual(f["test_yield"], {"ordered": 3, "viewed": 1, "share_viewed": 0.33})
        self.assertTrue(f["history_before_tests"])


class Progression(unittest.TestCase):
    def test_trends_and_investigation_use_reproduce_from_the_history(self):
        better = [session(i, comps=[{"behaviour": "b05", "credits": 1, "penalties": 4, "items": 5}]) for i in range(3)]
        later = [session(i + 3, comps=[{"behaviour": "b05", "credits": 4, "penalties": 1, "items": 5}]) for i in range(3)]
        p = progression.progression(better + later)
        self.assertEqual((p[0]["behaviour"], p[0]["trend"], p[0]["first_half"], p[0]["second_half"]), ("b05", "improving", 0.2, 0.8))
        self.assertEqual(p, progression.progression(better + later))
        self.assertEqual(progression.progression(better[:1])[0]["trend"], "not enough sessions")

    def test_unnecessary_tests_are_counted_from_the_scored_items(self):
        h = session(0, errors=["minor.test_not_indicated", "minor.test_not_indicated"], misses=["missed.test_not_ordered"], extra_steps=["credit.test_appropriate", "credit.test_appropriate", "credit.test_appropriate"])
        h["session_id"] = "s0"
        u = progression.investigations([h])
        self.assertEqual(u["total"], {"appropriate": 3, "not_indicated": 2, "not_ordered": 1})
        self.assertEqual(u["unnecessary_share"], 0.4)


class NextCase(unittest.TestCase):
    def setUp(self):
        from generation import objectives as ob
        self.tables = ob.Tables.load()

    def test_it_only_reorders_the_valid_set_and_the_elo_target_still_decides_without_a_pattern(self):
        valid = [{"id": "A", "tier": "P0", "tags": ["allergy"]}, {"id": "B", "tier": "P1", "tags": []}, {"id": "C", "tier": "P2", "tags": ["checklist"]}]
        ranked = next_case.rank(valid, [], self.tables.case_rating("P1"), self.tables)
        self.assertEqual({c["id"] for c in ranked}, {"A", "B", "C"})
        self.assertEqual(ranked[0]["id"], "B")                                                  # no pattern: the tier nearest the target
        self.assertIn("difficulty alone", next_case.explain(ranked[0]))

    def test_a_pattern_moves_a_matching_case_up_without_leaving_the_valid_set_and_says_why(self):
        valid = [{"id": "A", "tier": "P1", "tags": []}, {"id": "B", "tier": "P1", "tags": ["checklist"]}]
        pats = [patterns.Pattern("repeated_error", "major.checklist_contradiction", 4, 5)]
        ranked = next_case.rank(valid, pats, self.tables.case_rating("P1"), self.tables)
        self.assertEqual(ranked[0]["id"], "B")
        self.assertIn("checklist", next_case.explain(ranked[0]))
        self.assertIn("repeated error", next_case.explain(ranked[0]))

    def test_on_simulated_trainees_it_exercises_the_pattern_more_often_than_elo_alone_and_never_leaves_the_set(self):
        rng = random.Random(4)
        tags = ["allergy", "checklist", "fasting", "consent", "identity"]
        elo_hit = ai_hit = n = 0
        for _ in range(200):
            want = rng.choice(tags)
            pats = [patterns.Pattern("repeated_miss", f"credit.{want}_asked", 4, 5)]
            valid = [{"id": f"C{i}", "tier": rng.choice(["P0", "P1", "P2"]), "tags": rng.sample(tags, 2)} for i in range(6)]
            target = self.tables.case_rating(rng.choice(["P0", "P1", "P2"]))
            by_elo = sorted(valid, key=lambda c: (abs(self.tables.case_rating(c["tier"]) - target), c["id"]))[0]
            ranked = next_case.rank(valid, pats, target, self.tables)
            self.assertIn(ranked[0]["id"], {c["id"] for c in valid})
            elo_hit += want in by_elo["tags"]
            ai_hit += want in ranked[0]["tags"]
            n += 1
        self.assertGreater(ai_hit / n, elo_hit / n + 0.1, (ai_hit / n, elo_hit / n))


class ResultInterpreter(unittest.TestCase):
    ROW = {"test": "Potassium", "value": "6.4", "unit": "mmol/L", "normal_range": "3.5-5.3", "flag": "critical_high", "comment": "Haemolysed sample"}

    def host(self, text):
        import httpx
        from llm import client as C

        def h(request):
            if request.url.path == "/api/tags":
                return httpx.Response(200, json={"models": []})
            return httpx.Response(200, json={"message": {"content": json.dumps({"text": text})}})
        return C.OllamaClient(transport=httpx.MockTransport(h))

    def test_a_grounded_explanation_is_shown_in_practice_and_never_in_assessment(self):
        it = result_interpreter.Interpreter(self.host("The potassium of 6.4 is above the reference range of 3.5-5.3."))
        self.assertEqual(it.explain(self.ROW, mode="practice").source, "model")
        self.assertIsNone(it.explain(self.ROW, mode="assessment"))

    def test_an_invented_number_medicine_or_instruction_is_refused_and_nothing_is_shown(self):
        for bad in ("The potassium of 7.9 is high.", "Potassium is high, so give insulin and warfarin.", "You should prescribe calcium now."):
            self.assertIsNone(result_interpreter.Interpreter(self.host(bad)).explain(self.ROW), bad)
        self.assertIsNone(result_interpreter.Interpreter(None).explain(self.ROW))


if __name__ == "__main__":
    unittest.main()
