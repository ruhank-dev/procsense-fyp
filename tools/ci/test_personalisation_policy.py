"""Targeting policy, explanation and the simulated experiment (server/personalisation/policy.py, explain.py, experiment.py; p1-personalisation-s03 and s05)."""
import json
import sys
import unittest
from pathlib import Path

import jsonschema

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
from generation import contracts, objectives as ob  # noqa: E402
from personalisation import experiment, explain, learner_model as LM, policy as P  # noqa: E402

TABLES = ob.Tables.load()


def ev(*rows, tier="P0"):
    return {"competencies": [{"behaviour": b, "credits": c, "penalties": p, "items": c + p} for b, c, p in rows], "errors": [], "tier": tier}


def trained(rows, sessions=4):
    m = LM.LearnerModel()
    for _ in range(sessions):
        m.update(ev(*rows))
    return m


class Rules(unittest.TestCase):
    def test_a_demonstrated_weakness_is_targeted_through_its_dimension(self):
        weak_b = TABLES.dimensions[2].weak_behaviours[0]
        m = trained([(weak_b, 0, 5)] + [(b, 5, 0) for d in TABLES.dimensions if d is not TABLES.dimensions[2] for b in d.weak_behaviours[:1]])
        c = P.RulePolicy(TABLES).choose(m, [])
        self.assertEqual(c.dimension, TABLES.dimensions[2].id)
        self.assertIn("demonstrated weakness", c.why)
        self.assertEqual(c.policy, "rules")

    def test_one_bad_session_is_a_hypothesis_and_says_so(self):
        b = TABLES.dimensions[1].weak_behaviours[0]
        m = LM.LearnerModel()
        m.update(ev((b, 0, 5)))
        c = P.RulePolicy(TABLES).choose(m, [])
        self.assertEqual(c.dimension, TABLES.dimensions[1].id)
        self.assertIn("hypothesis", c.why)

    def test_the_same_dimension_is_not_chosen_again_and_again(self):
        weak_b = TABLES.dimensions[0].weak_behaviours[0]
        m = trained([(weak_b, 0, 5), (TABLES.dimensions[3].weak_behaviours[0], 1, 4)])
        rules = P.RulePolicy(TABLES)
        d0 = TABLES.dimensions[0].id
        self.assertNotEqual(rules.choose(m, [d0, d0, d0]).dimension, d0)                  # three in a row: skipped

    def test_with_no_evidence_it_explores_and_is_deterministic(self):
        a = P.RulePolicy(TABLES).choose(LM.LearnerModel(), [])
        b = P.RulePolicy(TABLES).choose(LM.LearnerModel(), [])
        self.assertEqual((a.dimension, a.why), (b.dimension, b.why))

    def test_rules_target_a_demonstrated_weakness_in_at_least_90_percent_of_sessions_when_one_exists(self):
        import random
        rng = random.Random(3)
        hit = n = 0
        for _ in range(100):
            dim = rng.choice(TABLES.dimensions)
            rows = [(dim.weak_behaviours[0], 0, 5)] + [(d.weak_behaviours[0], 5, 0) for d in TABLES.dimensions if d is not dim]
            m = trained(rows, sessions=4)
            c = P.RulePolicy(TABLES).choose(m, [rng.choice(TABLES.dimensions).id])
            n += 1
            hit += c.dimension == dim.id
        self.assertGreaterEqual(hit / n, 0.9, hit / n)


class Bandit(unittest.TestCase):
    def test_propensities_are_a_distribution_with_a_floor_and_a_cap(self):
        b = P.BanditPolicy(TABLES, seed=1)
        for _ in range(40):
            b.reward(TABLES.dimensions[0].id, True)                 # one arm looks far better
        p = b.propensities([])
        self.assertAlmostEqual(sum(p.values()), 1.0, places=6)
        self.assertGreaterEqual(min(p.values()), P.EXPLORE_FLOOR / len(p) - 1e-9)
        capped = b.propensities([TABLES.dimensions[0].id] * 10)
        self.assertEqual(capped[TABLES.dimensions[0].id], 0.0)       # chosen in every one of the last 10: the cap takes it out
        self.assertAlmostEqual(sum(capped.values()), 1.0, places=6)

    def test_the_choice_carries_its_propensity(self):
        b = P.BanditPolicy(TABLES, seed=2)
        c = b.choose(LM.LearnerModel(), [])
        self.assertTrue(0 < c.propensity <= 1)
        self.assertEqual(c.policy, "bandit")


class Objectives(unittest.TestCase):
    def test_the_objectives_document_is_valid_for_the_pipeline(self):
        weak_b = TABLES.dimensions[2].weak_behaviours[0]
        m = trained([(weak_b, 0, 5)])
        c = P.RulePolicy(TABLES).choose(m, [])
        req = contracts.accept_request({"version": 1, "trainee": "trainee-001", "mode": "practice", "scenario": "S1", "level": "advanced", "seed": 4}, lambda n: 0)
        doc = P.to_objectives(req, c, m, TABLES, ["VAR-S1-A", "VAR-S1-B"], ["f1"])
        contracts.validate("objectives", doc)
        self.assertEqual((doc["basis"], doc["dimensions"]), ("learner_model", [c.dimension]))
        self.assertEqual(doc["exclusions"]["spines"], ["VAR-S1-A", "VAR-S1-B"])


class Explanation(unittest.TestCase):
    def facts(self, sessions=4, rows=None):
        weak_b = TABLES.dimensions[2].weak_behaviours[0]
        m = trained(rows or [(weak_b, 0, 5)], sessions)
        c = P.RulePolicy(TABLES).choose(m, [])
        return explain.facts_for(c, m, TABLES, {weak_b: "Clarifies unclear information"})

    def test_the_template_is_grounded_in_the_facts(self):
        f = self.facts()
        text = explain.render(f)
        self.assertEqual(explain.check(text, f), [])
        self.assertIn(str(f["right_percent"]), text)
        self.assertIn("demonstrated weakness", text)

    def test_a_hypothesis_is_never_called_a_weakness(self):
        f = self.facts(sessions=1)
        text = explain.render(f)
        self.assertEqual(f["status"], "hypothesis")
        self.assertIn("may be a weakness", text)
        self.assertEqual(explain.check(text, f), [])
        self.assertTrue(explain.check("You have a demonstrated weakness here.", f))

    def test_a_reworded_explanation_with_a_made_up_number_is_caught(self):
        f = self.facts()
        self.assertTrue(any(d.startswith("UNGROUNDED_NUMBER") for d in explain.check("Your score was 77 out of 100.", f)))

    def test_with_no_evidence_it_says_the_dimension_is_new(self):
        c = P.RulePolicy(TABLES).choose(LM.LearnerModel(), [])
        text = explain.render(explain.facts_for(c, LM.LearnerModel(), TABLES))
        self.assertIn("not practised yet", text)


class Interpreter(unittest.TestCase):
    """The interpreter against a fake model host: what it accepts, what it refuses, and how a hypothesis is confirmed or refuted without touching the learner model."""

    @staticmethod
    def host(answer):
        import httpx
        sys.path.insert(0, str(ROOT / "server"))
        from llm import client as C

        def handler(request):
            if request.url.path == "/api/tags":
                return httpx.Response(200, json={"models": [{"name": "qwen3.5:4b-q4_K_M", "digest": "d9"}]})
            return httpx.Response(200, json={"message": {"content": json.dumps(answer)}})
        return C.OllamaClient(transport=httpx.MockTransport(handler))

    def model(self):
        return trained([("b03", 1, 4), ("b05", 0, 5), ("b09", 5, 0)], sessions=4)

    def test_a_grounded_hypothesis_is_kept_as_a_hypothesis(self):
        from personalisation import interpreter as I
        ok = {"hypotheses": [{"competencies": ["b03", "b05"], "evidence": ["e-b03", "e-b05"], "confidence": "medium", "text": "Both may depend on asking the patient enough questions."}]}
        hyps = I.Interpreter(self.host(ok), model="qwen3.5:4b-q4_K_M").propose(self.model(), {"b03": "Clarifies unclear information"})
        self.assertEqual(len(hyps), 1)
        self.assertEqual((hyps[0].status, hyps[0].competencies), ("open", ["b03", "b05"]))

    def test_a_cited_id_that_is_not_in_the_profile_is_refused_by_the_schema_and_the_checks(self):
        from personalisation import interpreter as I
        m = self.model()
        bad_comp = {"hypotheses": [{"competencies": ["b03", "b99"], "evidence": ["e-b03"], "confidence": "low", "text": "A made up link."}]}
        self.assertEqual(I.Interpreter(self.host(bad_comp)).propose(m), [])
        _, comps, ev = I.profile_lines(m, {})
        self.assertTrue(I.defects(bad_comp, m, comps, ev, "b03 right 20"))
        ungrounded = {"hypotheses": [{"competencies": ["b03", "b05"], "evidence": ["e-b03"], "confidence": "low", "text": "Their score is 91 out of 100 here."}]}
        self.assertTrue(any(d.startswith("UNGROUNDED_NUMBER") for d in I.defects(ungrounded, m, comps, ev, "b03 right 20")))

    def test_thin_evidence_must_be_low_confidence(self):
        from personalisation import interpreter as I
        m = LM.LearnerModel()
        m.update(ev(("b03", 0, 5), ("b05", 0, 5)))
        _, comps, evs = I.profile_lines(m, {})
        high = {"hypotheses": [{"competencies": ["b03", "b05"], "evidence": ["e-b03"], "confidence": "high", "text": "These are linked."}]}
        self.assertTrue(any(d.startswith("OVERCONFIDENT") for d in I.defects(high, m, comps, evs, "")))

    def test_a_model_that_is_down_proposes_nothing_and_the_tracker_never_changes_mastery(self):
        from personalisation import interpreter as I
        m = self.model()
        self.assertEqual(I.Interpreter(None).propose(m), [])
        before = m.snapshot()
        t = I.Tracker()
        t.add([I.Hypothesis(["b03", "b05"], ["e-b03"], "low", "linked", sessions_at_proposal=m.sessions), I.Hypothesis(["b03", "b09"], ["e-b03"], "low", "linked", sessions_at_proposal=m.sessions)])
        t.update(m)
        self.assertEqual([h.status for h in t.items], ["open", "open"])                      # too soon to judge
        for _ in range(3):
            m.update(ev(("b03", 1, 4), ("b05", 0, 5), ("b09", 5, 0)))
        t.update(m)
        self.assertEqual([h.status for h in t.items], ["confirmed", "refuted"])              # b09 is a strength, b03 and b05 stay weak
        after = m.snapshot()
        self.assertEqual(set(before), set(after))


class Experiment(unittest.TestCase):
    def test_the_experiment_runs_is_paired_reproducible_and_reports_whether_a_policy_won(self):
        a = experiment.run(learners=12, sessions=8, seed=2)
        b = experiment.run(learners=12, sessions=8, seed=2)
        self.assertEqual(a, b)
        self.assertEqual(set(a["policies"]), {"elo", "rules", "bandit"})
        for v in a["paired"].values():
            self.assertEqual(set(v), {"mean", "ci95", "better"})
            self.assertEqual(v["better"], v["ci95"][0] > 0)
        self.assertTrue(all(p["gain_mean"] > 0 for p in a["policies"].values()))         # practice teaches in the simulation, whatever the policy

    def test_the_recorded_result_decides_which_policy_runs(self):
        rec = json.loads((ROOT / "docs" / "research" / "personalisation_simulation.json").read_text(encoding="utf-8"))
        won = [k for k, v in rec["paired"].items() if v["better"]]
        adopted = json.loads((ROOT / "content" / "generation" / "policy.json").read_text(encoding="utf-8"))
        self.assertEqual(adopted["bandit"]["adopted"], "bandit_minus_rules" in won)
        self.assertEqual(adopted["rules"]["adopted"], "rules_minus_elo" in won)
        self.assertIn(adopted["default"], ("gain", "elo"))
        self.assertEqual(adopted["fallback"], "elo")

    def test_gain_is_the_default_only_because_the_study_says_it_beat_elo_in_every_family(self):
        study = json.loads((ROOT / "docs" / "research" / "personalisation_study.json").read_text(encoding="utf-8"))
        adopted = json.loads((ROOT / "content" / "generation" / "policy.json").read_text(encoding="utf-8"))
        beat_everywhere = all(study["families"][f]["paired_vs_elo"]["gain"]["gain"]["better"] for f in study["scenarios"])
        self.assertEqual(study["better_than_elo_in_every_family"]["gain"], beat_everywhere)
        self.assertEqual(adopted["gain"]["adopted"], beat_everywhere)
        self.assertEqual(adopted["default"] == "gain", beat_everywhere)

    def test_the_study_is_fair_it_has_a_random_floor_and_an_oracle_ceiling_and_nothing_beats_the_oracle(self):
        study = json.loads((ROOT / "docs" / "research" / "personalisation_study.json").read_text(encoding="utf-8"))
        self.assertGreaterEqual(len(study["scenarios"]), 4)
        for fam in study["scenarios"]:
            means = study["families"][fam]["means"]
            self.assertGreater(means["oracle"]["gain"], means["random"]["gain"], fam)
            self.assertTrue(all(means[n]["gain"] <= means["oracle"]["gain"] + 1e-9 for n in means), fam)

    def test_the_gain_policy_is_deterministic_and_the_study_is_reproducible(self):
        a = experiment.study(learners=6, sessions=6, seeds=(1,))
        b = experiment.study(learners=6, sessions=6, seeds=(1,))
        self.assertEqual(a, b)


class GainTargeting(unittest.TestCase):
    def test_the_dimension_with_the_most_room_is_chosen_and_one_very_weak_behaviour_is_not_diluted(self):
        small = next(d for d in TABLES.dimensions if len(d.weak_behaviours) == 1)
        big = next(d for d in TABLES.dimensions if len(d.weak_behaviours) >= 4)
        rows = [(b, 5, 0) for d in TABLES.dimensions if d is not big for b in d.weak_behaviours] + [(b, 3, 2) for b in big.weak_behaviours]
        m = trained(rows)
        c = P.GainPolicy(TABLES).choose(m, [])
        self.assertEqual(c.dimension, big.id)
        self.assertEqual(c.policy, "gain")
        m2 = trained([(b, 5, 0) for d in TABLES.dimensions if d is not small for b in d.weak_behaviours] + [(small.weak_behaviours[0], 0, 5)])
        self.assertEqual(P.GainPolicy(TABLES).choose(m2, []).dimension, small.id)

    def test_derive_uses_the_learner_model_under_gain_and_elo_otherwise_and_falls_back_on_thin_history(self):
        import random
        rng = random.Random(3)
        bs = sorted(b for d in TABLES.dimensions for b in d.weak_behaviours)
        hist = [ob.SessionRecord("X", "P0", {b: (rng.randint(1, 5), rng.randint(0, 3)) for b in rng.sample(bs, 8)}) for _ in range(5)]
        req = {"request_id": "0123456789abcdef", "scenario": "S1", "level": "advanced", "seed": 5}
        gain, elo = ob.derive(req, hist, TABLES, None, policy="gain"), ob.derive(req, hist, TABLES, None, policy="elo")
        self.assertEqual((gain["basis"], elo["basis"]), ("learner_model", "ratings"))
        self.assertEqual(ob.derive(req, hist, TABLES, None, policy="gain"), gain)                          # deterministic
        thin = ob.derive(req, hist[:1], TABLES, None, policy="gain")
        self.assertEqual(thin["basis"], "default_objectives")                                                 # below min_sessions the standard objectives, as before
        focus = ob.derive({**req, "focus": [bs[0]]}, hist, TABLES, None, policy="gain")
        self.assertEqual(focus["basis"], "focus_requested")                                                   # a requested focus is never overridden

    def test_an_unreadable_policy_file_means_elo(self):
        self.assertEqual(ob.targeting_policy(ROOT / "no" / "such" / "file.json"), "elo")


if __name__ == "__main__":
    unittest.main()
