"""Targeting policy: from the learner model to the objectives of the next generated case (p1-personalisation-s03, features D68 and A7).

    rules = RulePolicy(tables)
    arm = rules.choose(model, recent_dimensions)        # which dimension to exercise next, and why
    bandit = BanditPolicy(tables, seed)                 # Thompson sampling over the same arms, with a floor of exploration

Two policies pick a *dimension* (one of the eight difficulty dimensions, content/difficulty/dimensions.json; each trains a set of behaviours):
  - RulePolicy: the dimension whose behaviours hold the lowest demonstrated weakness (status `weakness` in the learner model), then the lowest hypothesis, with spacing: the dimension just practised is not chosen
    twice in a row unless it is the only weakness, and a dimension practised in each of the last SPACING sessions is skipped. With no weakness or hypothesis yet it names the dimension with the least evidence
    (so unknown behaviours get measured).
  - BanditPolicy: Thompson sampling. Each dimension is an arm with a Beta posterior over "a session on this dimension raised the trainee's weakest mastery in it". The arm with the highest sample is chosen, except
    that with probability EXPLORE_FLOOR an arm is drawn at random (so no arm is starved), and no arm may be chosen more than MAX_SHARE of the last window. The probability of the chosen arm is logged (a propensity)
    for off-policy evaluation. The bandit is adopted only after it beats the rules in simulation and in off-policy estimates (tools/personalisation/simulate.py); until then the rules run.
Both are for practice only. Assessment follows fixed cases. `to_objectives` turns the chosen dimension into the objectives document of the generation pipeline (server/schemas/objectives.json).
"""
from __future__ import annotations

import random
from collections import Counter
from dataclasses import dataclass

EXPLORE_FLOOR = 0.1
MAX_SHARE = 0.6
WINDOW = 10
SPACING = 3


@dataclass
class Choice:
    dimension: str
    behaviours: list[str]
    why: str
    propensity: float = 1.0           # the probability with which the policy chose this arm (1.0 for the deterministic rules)
    policy: str = "rules"


def dimension_of(tables, behaviour: str):
    return tables.for_behaviour(behaviour)


WEIGHT = {"weakness": 2.0, "hypothesis": 1.5, "developing": 1.0, "unknown": 1.0, "strength": 0.5}


class RulePolicy:
    """Explainable and deterministic. Each dimension is scored by the shortfall from full mastery of the behaviours it trains, each weighted by what the model is sure of: a demonstrated weakness counts double,
    a hypothesis one and a half, a strength half; a behaviour with no evidence counts at the prior (a shortfall of one half), so unknown ground is explored before it is ignored. The highest score wins;
    spacing: the dimension just practised is skipped unless it is clearly the biggest need, and one practised in every one of the last SPACING sessions is skipped."""

    def __init__(self, tables):
        self.tables = tables

    def scores(self, model) -> dict[str, float]:
        out = {}
        for d in self.tables.dimensions:
            total = 0.0
            for b in d.weak_behaviours:
                m = model.mastery(b)
                total += (1.0 - m.mean) * WEIGHT[m.status]
            out[d.id] = total / len(d.weak_behaviours)
        return out

    def choose(self, model, recent: list[str]) -> Choice:
        """`recent` lists the dimensions of the last sessions, oldest first."""
        scores = self.scores(model)
        last = recent[-1] if recent else None
        banned = {d for d in set(recent[-SPACING:]) if len(recent) >= SPACING and recent[-SPACING:].count(d) == SPACING}
        ranked = sorted((-v, k) for k, v in scores.items() if k not in banned)
        best = ranked[0][1]
        if best == last and len(ranked) > 1 and (-ranked[0][0]) < 1.25 * (-ranked[1][0]):          # the same again only if it is clearly the biggest need
            best = ranked[1][1]
        d = next(x for x in self.tables.dimensions if x.id == best)
        by_need = sorted(d.weak_behaviours, key=lambda b: (model.mastery(b).mean, b))
        weak = [b for b in by_need if model.mastery(b).status in ("weakness", "hypothesis")]
        why = (f"{'demonstrated weakness' if model.mastery(weak[0]).status == 'weakness' else 'hypothesis'}: {weak[0]} at {model.mastery(weak[0]).mean:.2f}" if weak else f"the biggest shortfall ({scores[best]:.2f})")
        return Choice(best, (weak or by_need)[:3], why, 1.0, "rules")


class BanditPolicy:
    def __init__(self, tables, seed: int = 0, explore: float = EXPLORE_FLOOR, max_share: float = MAX_SHARE):
        self.tables = tables
        self.rng = random.Random(seed)
        self.explore, self.max_share = explore, max_share
        self.alpha = {d.id: 1.0 for d in tables.dimensions}
        self.beta = {d.id: 1.0 for d in tables.dimensions}

    def propensities(self, recent: list[str], samples: int = 400) -> dict[str, float]:
        """Probability of each arm under this policy now (Monte Carlo over the posterior, with the exploration floor and the share cap), for logging."""
        arms = [d.id for d in self.tables.dimensions]
        wins = Counter()
        rng = random.Random(len(recent) * 7919 + 13)
        for _ in range(samples):
            wins[max(arms, key=lambda a: rng.betavariate(self.alpha[a], self.beta[a]))] += 1
        base = {a: (1 - self.explore) * wins[a] / samples + self.explore / len(arms) for a in arms}
        window = recent[-WINDOW:]
        capped = {a for a in arms if window and window.count(a) / len(window) >= self.max_share}
        if capped and len(capped) < len(arms):
            free = sum(p for a, p in base.items() if a not in capped)
            return {a: (0.0 if a in capped else p / free) for a, p in base.items()}
        return base

    def choose(self, model, recent: list[str]) -> Choice:
        probs = self.propensities(recent)
        arms = sorted(probs)
        pick = self.rng.choices(arms, weights=[probs[a] for a in arms])[0]
        d = next(x for x in self.tables.dimensions if x.id == pick)
        weak = sorted((model.mastery(b).mean, b) for b in d.weak_behaviours if model.mastery(b).sessions > 0)
        return Choice(pick, [b for _, b in weak[:3]] or list(d.weak_behaviours[:3]), f"thompson sample (p={probs[pick]:.2f})", probs[pick], "bandit")

    def reward(self, dimension: str, improved: bool) -> None:
        if improved:
            self.alpha[dimension] += 1.0
        else:
            self.beta[dimension] += 1.0


def to_objectives(request: dict, choice: Choice, model, tables, recent_spines: list[str], recent_fingerprints: list[str]) -> dict:
    """The objectives document (server/schemas/objectives.json) for the pipeline, built from the chosen dimension. The difficulty vector is the learner model's mean mastery mapped onto the rating scale of the tiers."""
    vector = {}
    for d in tables.dimensions:
        ms = [model.mastery(b).mean for b in d.weak_behaviours if model.mastery(b).sessions > 0]
        mean = sum(ms) / len(ms) if ms else 0.5
        vector[d.id] = round(tables.start + (mean - 0.5) * 400.0, 3)         # 0.5 mastery sits at the start rating; every 0.25 moves it by a tier-sized step of 100
    comps = [{"behaviour": b, "rating": round(tables.start + (model.mastery(b).mean - 0.5) * 400.0, 3), "times_rated": model.mastery(b).sessions} for b in choice.behaviours][:3]
    doc = {"version": 1, "request_id": request["request_id"], "scenario": request["scenario"], "level": request["level"], "seed": request["seed"], "basis": "learner_model", "sessions_rated": model.sessions,
           "competencies": comps, "dimensions": [choice.dimension], "difficulty_vector": vector,
           "exclusions": {"spines": list(dict.fromkeys(recent_spines[-3:])), "fingerprints": list(recent_fingerprints)[-20:]},
           "explanation": f"The next case exercises {choice.dimension.replace('_', ' ')} because of {choice.why}."}
    return doc
