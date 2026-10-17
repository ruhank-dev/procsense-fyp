"""Confidence calibration (p2-ai-learning-analytics-s02, feature D41): how well a trainee's stated confidence matches what happened.

    report = calibration(reports)       # reports: [{"confidence": "low|medium|high" or 0..1, "correct": bool}] from the logs of the sessions
    advice = coach(report)

A confidence report is the trainee's answer to "how sure are you?" at a decision point, written into the session log next to the decision and its outcome. Low, medium and high are read as 0.2, 0.5 and 0.8.
Measures: the Brier score (mean squared gap between confidence and outcome, 0 is perfect, 0.25 is what saying 0.5 every time earns), the calibration curve (confidence bins against the share correct) and
the signed gap (mean confidence minus the share correct: positive is over-confidence). Coaching is a template from those numbers and needs at least MIN_REPORTS reports; the engine does not record
confidence reports yet (`unrecorded` in SessionEvidence, hook 8 of docs/architecture/unity_ai_hooks.md), so today this runs on logs a test builds.
"""
from __future__ import annotations

from dataclasses import dataclass

LEVELS = {"low": 0.2, "medium": 0.5, "high": 0.8}
MIN_REPORTS = 10
BINS = ((0.0, 0.35), (0.35, 0.65), (0.65, 1.01))
GAP = 0.15


@dataclass
class Calibration:
    n: int
    brier: float
    gap: float
    curve: list[dict]
    enough: bool


def value(c) -> float:
    return LEVELS[c] if isinstance(c, str) else float(c)


def calibration(reports: list[dict]) -> Calibration:
    n = len(reports)
    if n == 0:
        return Calibration(0, 0.0, 0.0, [], False)
    conf = [value(r["confidence"]) for r in reports]
    hit = [1.0 if r["correct"] else 0.0 for r in reports]
    brier = sum((c - h) ** 2 for c, h in zip(conf, hit)) / n
    gap = sum(conf) / n - sum(hit) / n
    curve = []
    for lo, hi in BINS:
        idx = [i for i, c in enumerate(conf) if lo <= c < hi]
        if idx:
            curve.append({"from": lo, "to": min(hi, 1.0), "n": len(idx), "mean_confidence": round(sum(conf[i] for i in idx) / len(idx), 3), "share_correct": round(sum(hit[i] for i in idx) / len(idx), 3)})
    return Calibration(n, round(brier, 4), round(gap, 4), curve, n >= MIN_REPORTS)


def coach(c: Calibration) -> str:
    if not c.enough:
        return f"{c.n} confidence answers so far; at least {MIN_REPORTS} are needed before a pattern is shown."
    pct = lambda x: round(100 * x)          # noqa: E731
    if c.gap >= GAP:
        return f"Your confidence ran higher than your results: on average {pct(c.gap)} points above the share you got right (Brier score {c.brier})."
    if c.gap <= -GAP:
        return f"Your confidence ran lower than your results: on average {pct(-c.gap)} points below the share you got right (Brier score {c.brier})."
    return f"Your confidence matched your results well (Brier score {c.brier})."
