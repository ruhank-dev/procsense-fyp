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


SCENARIOS = {
    "default": "mastery uniform between 0.30 and 0.95 (the original experiment)",
    "few_deficits": "most behaviours strong (about 0.85), three behaviours weak (0.25 to 0.50)",
    "broad_weakness": "every behaviour between 0.35 and 0.65",
    "strong": "every behaviour between 0.60 and 0.98",
}


def _mastery(scenario: str, seed: int, behaviours: list[str]) -> dict[str, float] | None:
    """The hidden mastery of a simulated learner for a scenario family (None: the learner draws its own, the original experiment)."""
    if scenario == "default":
        return None
    rng = random.Random(seed * 31 + 7)
    if scenario == "few_deficits":
        weak = set(rng.sample(behaviours, 3))
        return {b: (rng.uniform(0.25, 0.5) if b in weak else min(0.98, max(0.7, rng.gauss(0.85, 0.06)))) for b in behaviours}
    if scenario == "broad_weakness":
        return {b: rng.uniform(0.35, 0.65) for b in behaviours}
    return {b: rng.uniform(0.6, 0.98) for b in behaviours}


def _run_one(policy_name: str, seed: int, sessions: int, tables, scenario: str = "default") -> dict:
    behaviours = sorted(b for d in tables.dimensions for b in d.weak_behaviours)
    learner = S.Learner(seed, behaviours, mastery=_mastery(scenario, seed, behaviours), learning=BASE_LEARNING)
    start = dict(learner.mastery)
    weakest = sorted(behaviours, key=lambda b: (start[b], b))[:5]
    model, elo = LM.LearnerModel(), LM.EloBaseline()
    rules, bandit, gain = P.RulePolicy(tables), P.BanditPolicy(tables, seed=seed), P.GainPolicy(tables)
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
        elif policy_name == "gain":
            choice = gain.choose(model, recent)
        elif policy_name == "random":
            d = rng.choice(tables.dimensions)
            choice = P.Choice(d.id, list(d.weak_behaviours[:3]), "random", 1.0, "random")
        elif policy_name == "oracle":                             # knows the TRUE mastery: the ceiling no policy can pass
            truth = {d.id: sum((1.0 - learner.mastery[b]) ** 2 for b in d.weak_behaviours) for d in tables.dimensions}
            best = sorted((-v, k) for k, v in truth.items())[0][1]
            choice = P.Choice(best, [], "oracle", 1.0, "oracle")
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
    gain_all = statistics.mean(learner.mastery[b] - start[b] for b in behaviours)
    weakest3 = sorted(behaviours, key=lambda b: (start[b], b))[:3]
    return {"gain": statistics.mean(learner.mastery[b] - start[b] for b in weakest), "gain_weakest3": statistics.mean(learner.mastery[b] - start[b] for b in weakest3), "gain_all": gain_all,
            "log": log, "dimensions_used": len(set(recent))}


def bootstrap(diffs: list[float], rounds: int = 1000, seed: int = 5) -> tuple[float, float, float]:
    rng = random.Random(seed)
    means = sorted(statistics.mean(rng.choices(diffs, k=len(diffs))) for _ in range(rounds))
    return statistics.mean(diffs), means[int(0.025 * rounds)], means[int(0.975 * rounds)]


def run(learners: int = 200, sessions: int = 12, seed: int = 1, scenario: str = "default") -> dict:
    tables = _tables()
    runs = {name: [_run_one(name, seed * 100000 + i, sessions, tables, scenario) for i in range(learners)] for name in ("elo", "rules", "bandit")}
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


def study(learners: int = 200, sessions: int = 12, seeds: tuple[int, ...] = (1, 2, 3)) -> dict:
    """The fair comparison of 18 October 2026 (third session): every policy, including a random floor and an ORACLE ceiling that knows the true mastery, on four fixed learner families and several seeds, three measures
    (the original five weakest behaviours, the three weakest, and all behaviours). Nothing is chosen after seeing a result: the families, seeds and measures are listed in this function. A policy is reported better than
    Elo only where the paired 95 percent interval of the difference is above zero, per family and pooled; the pooled figure weights the families equally."""
    tables = _tables()
    names = ("elo", "rules", "gain", "bandit", "random", "oracle")
    out = {"learners_per_family_and_seed": learners, "sessions": sessions, "seeds": list(seeds), "scenarios": SCENARIOS, "measures": ["gain (five weakest at the start)", "gain_weakest3", "gain_all"], "families": {}, "pooled": {}}
    pooled: dict[str, dict[str, list[float]]] = {}
    for fam in SCENARIOS:
        runs = {n: [] for n in names}
        for sd in seeds:
            for n in names:
                runs[n] += [_run_one(n, sd * 100000 + i, sessions, tables, fam) for i in range(learners)]
        fam_out = {"means": {n: {m: round(statistics.mean(r[m] for r in rs), 4) for m in ("gain", "gain_weakest3", "gain_all")} for n, rs in runs.items()}, "paired_vs_elo": {}}
        for n in names:
            if n == "elo":
                continue
            fam_out["paired_vs_elo"][n] = {}
            for m in ("gain", "gain_weakest3", "gain_all"):
                diffs = [x[m] - y[m] for x, y in zip(runs[n], runs["elo"])]
                mean, lo, hi = bootstrap(diffs)
                fam_out["paired_vs_elo"][n][m] = {"mean": round(mean, 4), "ci95": [round(lo, 4), round(hi, 4)], "better": lo > 0}
                pooled.setdefault(n, {}).setdefault(m, []).append(mean)
        fam_out["oracle_headroom_over_elo"] = round(fam_out["means"]["oracle"]["gain"] - fam_out["means"]["elo"]["gain"], 4)
        out["families"][fam] = fam_out
    out["pooled"] = {n: {m: round(statistics.mean(v), 4) for m, v in ms.items()} for n, ms in pooled.items()}
    wins = {n: all(out["families"][f]["paired_vs_elo"][n]["gain"]["better"] for f in SCENARIOS) for n in names if n != "elo"}
    out["better_than_elo_in_every_family"] = wins
    return out
