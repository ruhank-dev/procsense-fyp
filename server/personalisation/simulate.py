"""Simulated learners (p1-personalisation-s02 and s05): a learner with a known mastery per behaviour that plays sessions and produces SessionEvidence, so the model and the policies can be tested against the truth.

    learner = Learner(seed, behaviours, mastery)          # mastery: behaviour -> chance an item is done right on a P0 case
    evidence = learner.play(story, tier, behaviours_in_case, session_no)      # one session; practising a behaviour raises its mastery a little (learning)

The truth is known only to the simulation, never to the models. A case of a harder tier lowers the chance on the Elo logit scale, the same scale the models use for difficulty. Items per behaviour per session are 1 to
`ITEMS` (what a real case gives: a handful). Nothing here is clinical data; it is a test bench with a fixed seed.
"""
from __future__ import annotations

import math
import random

ITEMS = 5
OFFSETS = {"P0": 0.0, "P1": 100.0, "P2": 200.0}
SEVERE = 0.25                       # share of failures that are a major error


def _p(base: float, tier: str) -> float:
    x = math.log(max(base, 1e-6) / max(1 - base, 1e-6)) - OFFSETS.get(tier, 0.0) * math.log(10) / 400.0
    return 1 / (1 + math.exp(-x))


class Learner:
    def __init__(self, seed: int, behaviours: list[str], mastery: dict[str, float] | None = None, learning: float = 0.04, trainee: str = "trainee-sim"):
        self.rng = random.Random(seed)
        self.behaviours = list(behaviours)
        self.mastery = dict(mastery) if mastery else {b: self.rng.uniform(0.3, 0.95) for b in behaviours}
        self.learning = learning
        self.trainee = trainee

    def true_p(self, behaviour: str, tier: str = "P0") -> float:
        return _p(self.mastery[behaviour], tier)

    def play(self, story: str, tier: str, behaviours_in_case: list[str], session_no: int, practise_boost: dict[str, float] | None = None) -> dict:
        competencies, errors = [], []
        for b in behaviours_in_case:
            n = self.rng.randint(1, ITEMS)
            p = self.true_p(b, tier)
            good = sum(self.rng.random() < p for _ in range(n))
            competencies.append({"behaviour": b, "credits": good, "penalties": n - good, "items": n})
            for _ in range(n - good):
                errors.append({"item": f"sim.{b}", "behaviour": b, "severity": "major" if self.rng.random() < SEVERE else "minor", "at": "00:00:00"})
            # practising a behaviour teaches it: the gap to full mastery closes a little
            self.mastery[b] += (practise_boost or {}).get(b, self.learning) * (1 - self.mastery[b])
        return {"version": 1, "trainee": self.trainee, "session_id": f"sim-{session_no}", "ended_at": "2026-10-17T00:00:00Z", "case": story, "story": story, "tier": tier, "mode": "practice", "generated": False,
                "end_state": "READY_SAFE", "result": "SUCCESS", "steps": [], "errors": errors, "actions": [], "events": [], "competencies": competencies, "unrecorded": ["hints"]}


def log_loss(p: float, outcome: int) -> float:
    p = min(max(p, 1e-6), 1 - 1e-6)
    return -(math.log(p) if outcome else math.log(1 - p))
