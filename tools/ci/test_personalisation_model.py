"""The learner model against simulated learners with a known mastery (server/personalisation/learner_model.py, p1-personalisation-s02)."""
import random
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
from personalisation import learner_model as LM, simulate as S  # noqa: E402

BEHAVIOURS = [f"b{n:02d}" for n in range(1, 11)]


def evidence(competencies, errors=(), tier="P0"):
    return {"competencies": [{"behaviour": b, "credits": c, "penalties": p, "items": c + p} for b, c, p in competencies], "errors": list(errors), "tier": tier}


class Model(unittest.TestCase):
    def test_no_evidence_is_unknown_with_a_wide_interval(self):
        m = LM.LearnerModel().mastery("b01")
        self.assertEqual((m.status, m.sessions, m.low, m.high), ("unknown", 0, 0.0, 1.0))

    def test_one_slip_never_makes_a_weakness(self):
        m = LM.LearnerModel()
        for _ in range(4):
            m.update(evidence([("b01", 5, 0)]))
        m.update(evidence([("b01", 0, 5)], [{"behaviour": "b01", "severity": "critical"}]))        # one terrible session after four good ones
        self.assertNotEqual(m.mastery("b01").status, "weakness")
        one = LM.LearnerModel()
        one.update(evidence([("b02", 0, 5)]))
        self.assertEqual(one.mastery("b02").status, "hypothesis")                                  # one bad session is a hypothesis, never acted on as a fact

    def test_repeated_failure_over_three_sessions_is_a_weakness_and_repeated_success_a_strength(self):
        m = LM.LearnerModel()
        for _ in range(4):
            m.update(evidence([("b03", 1, 4), ("b04", 5, 0)]))
        self.assertEqual(m.mastery("b03").status, "weakness")
        self.assertEqual(m.mastery("b04").status, "strength")
        self.assertEqual(m.weaknesses(), ["b03"])

    def test_old_evidence_fades(self):
        m = LM.LearnerModel()
        for _ in range(4):
            m.update(evidence([("b05", 0, 5)]))
        low = m.mastery("b05").mean
        for _ in range(8):
            m.update(evidence([("b05", 5, 0)]))
        self.assertGreater(m.mastery("b05").mean, low + 0.4)

    def test_severe_errors_count_for_more(self):
        a, b = LM.LearnerModel(), LM.LearnerModel()
        a.update(evidence([("b06", 3, 2)], [{"behaviour": "b06", "severity": "minor"}] * 2))
        b.update(evidence([("b06", 3, 2)], [{"behaviour": "b06", "severity": "critical"}] * 2))
        self.assertLess(b.mastery("b06").mean, a.mastery("b06").mean)

    def test_a_harder_case_lowers_the_prediction_like_elo(self):
        m = LM.LearnerModel()
        m.update(evidence([("b07", 4, 1)]))
        self.assertGreater(m.predict("b07", "P0"), m.predict("b07", "P1"))
        self.assertGreater(m.predict("b07", "P1"), m.predict("b07", "P2"))
        e = LM.EloBaseline()
        self.assertGreater(e.predict("b07", "P0"), e.predict("b07", "P2"))

    def test_the_intervals_cover_the_true_mastery_most_of_the_time(self):
        hit = total = 0
        for seed in range(60):
            learner = S.Learner(seed, BEHAVIOURS, learning=0.0)
            m = LM.LearnerModel(forget=1.0)               # a learner who does not learn: the truth stands still, so coverage is meaningful
            for k in range(8):
                m.update({**learner.play("X", "P0", BEHAVIOURS, k), "errors": []})        # no extra weight for severe errors: the weighting is a design choice, not part of the truth being estimated
            for b in BEHAVIOURS:
                x = m.mastery(b)
                hit += x.low <= learner.mastery[b] <= x.high
                total += 1
        self.assertGreater(hit / total, 0.70, hit / total)        # an 80 percent interval from a normal approximation: the bar is 70 percent

    def test_it_predicts_the_next_session_better_than_elo(self):
        """Simulated learners with a known mastery; for each session after the first, the log-loss of each item outcome under each model's prediction (made before the session)."""
        beta_loss = elo_loss = n = 0.0
        rng = random.Random(1)
        for seed in range(80):
            learner = S.Learner(seed, BEHAVIOURS)
            model, elo = LM.LearnerModel(), LM.EloBaseline()
            for k in range(10):
                tier = rng.choice(["P0", "P1", "P2"])
                chosen = rng.sample(BEHAVIOURS, 5)
                ev = learner.play("X", tier, chosen, k)
                if k >= 1:
                    for c in ev["competencies"]:
                        for outcome, count in ((1, c["credits"]), (0, c["penalties"])):
                            for _ in range(count):
                                beta_loss += S.log_loss(model.predict(c["behaviour"], tier), outcome)
                                elo_loss += S.log_loss(elo.predict(c["behaviour"], tier), outcome)
                                n += 1
                model.update(ev)
                elo.update(ev)
        self.assertLess(beta_loss / n, elo_loss / n, (beta_loss / n, elo_loss / n))


if __name__ == "__main__":
    unittest.main()
