"""Recurring performance patterns across sessions (p2-ai-learning-analytics-s01, features D39 and D40).

    found = detect(history)           # history: SessionEvidence records (server/personalisation/evidence.py), oldest first
    advice = coach(found)             # one neutral sentence per pattern, made from the pattern's own facts

Deterministic detectors; each needs a minimum of evidence before it speaks (MIN_SESSIONS sessions that could have shown it, and the thing happening in at least MIN_SHARE of them) so a pattern is never
claimed from one or two sessions:
  repeated_error     the same scored error item (for example a checklist contradiction) in at least 3 sessions and a share of 0.5 of the sessions where it could occur
  repeated_miss      the same missed step in at least 3 sessions
  late_step          the same step completed late (lateness over zero minutes) in at least 3 sessions
  critical_trend     critical errors in the last 3 sessions after none in the 3 before (a worsening)
  behaviour_slide    a behaviour's share of items right fell by at least 0.25 from the first half of the history to the second (at least 6 sessions)
Wording is a template from the pattern's facts (no model, no judgement of the person); a model may reword it later only through the debrief path with the same facts and the same checks.
"""
from __future__ import annotations

from collections import Counter
from dataclasses import dataclass

MIN_SESSIONS = 3
MIN_SHARE = 0.5


@dataclass
class Pattern:
    kind: str
    key: str
    sessions: int            # sessions it showed in
    of: int                  # sessions that could have shown it
    detail: str = ""


def _items(history: list[dict]):
    return [(h, {e["item"] for e in h["errors"]}, {s["item"] for s in h["steps"] if s["state"] == "missed"}, {s["item"] for s in h["steps"] if s.get("late_minutes", 0) > 0}) for h in history]


def detect(history: list[dict]) -> list[Pattern]:
    out: list[Pattern] = []
    n = len(history)
    if n < MIN_SESSIONS:
        return out
    rows = _items(history)
    errors, misses, lates = Counter(), Counter(), Counter()
    seen = Counter()
    for h, e, m, l in rows:
        errors.update(e)
        misses.update(m)
        lates.update(l)
        seen.update({s["item"] for s in h["steps"]} | e)
    for item, k in sorted(errors.items()):
        if k >= MIN_SESSIONS and k / max(seen[item], k) >= MIN_SHARE:
            out.append(Pattern("repeated_error", item, k, max(seen[item], k)))
    for item, k in sorted(misses.items()):
        if k >= MIN_SESSIONS and k / max(seen[item], k) >= MIN_SHARE:
            out.append(Pattern("repeated_miss", item, k, max(seen[item], k)))
    for item, k in sorted(lates.items()):
        if k >= MIN_SESSIONS:
            out.append(Pattern("late_step", item, k, max(seen[item], k)))
    crit = [any(e["severity"] == "critical" for e in h["errors"]) for h in history]
    if n >= 6 and any(crit[-3:]) and not any(crit[-6:-3]):
        out.append(Pattern("critical_trend", "critical", sum(crit[-3:]), 3, "none in the 3 sessions before"))
    if n >= 6:
        half = n // 2
        for b in sorted({c["behaviour"] for h in history for c in h["competencies"]}):
            def share(part):
                good = sum(c["credits"] for h in part for c in h["competencies"] if c["behaviour"] == b)
                tot = sum(c["credits"] + c["penalties"] for h in part for c in h["competencies"] if c["behaviour"] == b)
                return (good / tot, tot) if tot else (None, 0)
            (a, na), (z, nz) = share(history[:half]), share(history[half:])
            if a is not None and z is not None and na >= 5 and nz >= 5 and a - z >= 0.25:
                out.append(Pattern("behaviour_slide", b, 0, n, f"{round(100 * a)} to {round(100 * z)} out of 100 right"))
    return out


def coach(patterns: list[Pattern], labels: dict[str, str] | None = None) -> list[str]:
    """One neutral sentence each, from the pattern's facts."""
    labels = labels or {}
    nm = lambda k: labels.get(k, k.replace(".", " ").replace("_", " "))          # noqa: E731
    out = []
    for p in patterns:
        if p.kind == "repeated_error":
            out.append(f"In {p.sessions} of {p.of} sessions this was scored as an error: {nm(p.key)}. It is worth practising on its own.")
        elif p.kind == "repeated_miss":
            out.append(f"In {p.sessions} of {p.of} sessions this step was not done: {nm(p.key)}.")
        elif p.kind == "late_step":
            out.append(f"This step was done late in {p.sessions} of {p.of} sessions: {nm(p.key)}.")
        elif p.kind == "critical_trend":
            out.append(f"Critical errors appeared in {p.sessions} of the last 3 sessions, after none in the 3 before.")
        elif p.kind == "behaviour_slide":
            out.append(f"Items right in {nm(p.key)} went from the first half of your sessions to the second: {p.detail}.")
    return out
