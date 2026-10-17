"""The personalisation experiment on simulated learners (p1-personalisation-s03 and s05): do targeted policies improve the weakest competencies faster than the engine's Elo selection?

    result = run(learners=200, sessions=12, seed=1)       # {"policies": {name: {...}}, "paired": {...}}

Each simulated learner has a hidden mastery per behaviour (server/personalisation/simulate.py). For each policy, the same learners (same hidden mastery, same random streams for the learner's own luck) play
`sessions` sessions. A session exercises the dimension the policy chose (all the behaviours that dimension trains, practised hard: PRACTISE_BOOST) plus two other behaviours (practised a little: BASE_LEARNING).
The measure is the improvement of the learner's TRUE mastery over the five behaviours that were weakest at the start, which is what a good policy should raise. Policies:
  elo     the engine's selection: the weakest behaviour by the Elo rating picks its dimension (src/ProcSense.Core/Difficulty/Selector).
  rules   the learner model's demonstrated weaknesses first, with spacing (policy.RulePolicy).
  bandit  Thompson sampling with an exploration floor and a share cap (policy.BanditPolicy).
The differences are paired by learner and reported with a bootstrap 95 percent interval. This is a simulation: it shows whether a policy can find and train a weakness when the world is as assumed, not that a real
trainee improves. A pilot with real trainees is p1-evaluation-s03. The off-policy estimate (inverse propensity weighting of the bandit's logged choices) is computed from the bandit run's own logs.
"""
from __future__ import annotations

import random
import statistics

from . import learner_model as LM, policy as P, simulate as S

PRACTISE_BOOST = 0.12
BASE_LEARNING = 0.02
EXTRA = 2
TIERS = ("P0", "P1", "P2")


def _tables():
    import sys
    from pathlib import Path
    root = Path(__file__).resolve().parents[2]
    if str(root / "server") not in sys.path:
        sys.path.insert(0, str(root / "server"))
    from generation import objectives as ob
    return ob.Tables.load()


def _run_one(policy_name: str, seed: int, sessions: int, tables) -> dict:
    behaviours = sorted(b for d in tables.dimensions for b in d.weak_behaviours)
    learner = S.Learner(seed, behaviours, learning=BASE_LEARNING)
    start = dict(learner.mastery)
    weakest = sorted(behaviours, key=lambda b: (start[b], b))[:5]
    model, elo = LM.LearnerModel(), LM.EloBaseline()
    rules, bandit = P.RulePolicy(tables), P.BanditPolicy(tables, seed=seed)
    recent: list[str] = []
    log = []
    rng = random.Random(seed * 17 + 3)                     # the choice of the extra behaviours and tiers: the same for every policy
    for k in range(sessions):
        tier = rng.choice(TIERS)
        extras = rng.sample(behaviours, EXTRA)
        before = {b: model.mastery(b).mean for b in behaviours}
        if policy_name == "elo":
            rated = sorted(elo.rating, key=lambda b: (elo.rating[b], b))
            b = rated[0] if rated and k >= tables.min_sessions else rng.choice(behaviours)
            d = tables.for_behaviour(b)
            choice = P.Choice(d.id, [b], "elo weakest", 1.0, "elo")
        elif policy_name == "rules":
            choice = rules.choose(model, recent)
        else:
            choice = bandit.choose(model, recent)
        dim = next(x for x in tables.dimensions if x.id == choice.dimension)
        targeted = list(dim.weak_behaviours)
        in_case = targeted + [b for b in extras if b not in targeted]
        boost = {b: PRACTISE_BOOST for b in targeted}
        ev = learner.play("X", tier, in_case, k, practise_boost=boost)
        model.update(ev)
        elo.update(ev)
        after = {b: model.mastery(b).mean for b in behaviours}
        target_b = [b for b in targeted if before[b] > 0] or targeted
        improved = (sum(after[b] for b in target_b) / len(target_b)) > (sum(before[b] for b in target_b) / len(target_b))
        if policy_name == "bandit":
            bandit.reward(choice.dimension, improved)
        recent.append(choice.dimension)
        log.append((choice.dimension, choice.propensity, improved))
    gain = statistics.mean(learner.mastery[b] - start[b] for b in weakest)
    return {"gain": gain, "log": log, "dimensions_used": len(set(recent))}


def bootstrap(diffs: list[float], rounds: int = 1000, seed: int = 5) -> tuple[float, float, float]:
    rng = random.Random(seed)
    means = sorted(statistics.mean(rng.choices(diffs, k=len(diffs))) for _ in range(rounds))
    return statistics.mean(diffs), means[int(0.025 * rounds)], means[int(0.975 * rounds)]


def run(learners: int = 200, sessions: int = 12, seed: int = 1) -> dict:
    tables = _tables()
    runs = {name: [_run_one(name, seed * 100000 + i, sessions, tables) for i in range(learners)] for name in ("elo", "rules", "bandit")}
    out = {"learners": learners, "sessions": sessions, "policies": {}, "paired": {}}
    for name, rs in runs.items():
        out["policies"][name] = {"gain_mean": round(statistics.mean(r["gain"] for r in rs), 4), "dimensions_used_mean": round(statistics.mean(r["dimensions_used"] for r in rs), 2)}
    for a, b in (("rules", "elo"), ("bandit", "elo"), ("bandit", "rules")):
        mean, lo, hi = bootstrap([x["gain"] - y["gain"] for x, y in zip(runs[a], runs[b])])
        out["paired"][f"{a}_minus_{b}"] = {"mean": round(mean, 4), "ci95": [round(lo, 4), round(hi, 4)], "better": lo > 0}
    # off-policy estimate of the bandit's reward rate from its own logs: inverse propensity weighting against the uniform policy
    ips = []
    for r in runs["bandit"]:
        for _dim, p, improved in r["log"]:
            ips.append((1.0 / (p * len(tables.dimensions))) * (1.0 if improved else 0.0))
    out["off_policy"] = {"bandit_ips_reward_vs_uniform": round(statistics.mean(ips), 4), "bandit_logged_reward": round(statistics.mean(1.0 if i else 0.0 for r in runs["bandit"] for _, _, i in r["log"]), 4),
                         "note": "inverse-propensity estimate of the reward a uniformly random policy would have had on the bandit's logged sessions; a bandit worth adopting logs a higher reward than this"}
    return out
