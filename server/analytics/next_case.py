"""Ranking the valid next cases by the trainee's patterns, with the Elo rating still deciding (p2-ai-learning-analytics-s06).

    ranked = rank(valid, patterns_found, elo_target, tables)       # the same cases, reordered; never a case that was not in `valid`
    why = explain(ranked[0], ...)                                  # the reason in the facts that ordered it

`valid` is the set the engine's Selector already allows (approved, in the right tier band, not just played): this module can only reorder it. A case scores by how well its tier matches the Elo target (the
decider, weight 1.0) minus 0.3 for every recent pattern of the trainee that the case does not touch, plus 0.3 for every pattern it exercises: a pattern `repeated_error` on an item of a checklist favours a
case that has that checklist phase, a `repeated_miss` of an allergy question favours a case with an allergy. The weights are design values; the explanation names the pattern and the case tag that matched.
If no pattern matches any case the Elo order stands. A language model is not needed: the plan's LLM option is left out because the simple scoring is explainable and its effect is measured below.
"""
from __future__ import annotations

PATTERN_WEIGHT = 0.3
TAG_OF_ITEM = (("checklist", "checklist"), ("allergy", "allergy"), ("fasting", "fasting"), ("consent", "consent"), ("identity", "identity"), ("test", "investigations"), ("antibiotic", "antibiotic"), ("handover", "handover"))


def tags_of(item: str) -> set[str]:
    return {tag for word, tag in TAG_OF_ITEM if word in item}


def rank(valid: list[dict], patterns: list, elo_target: float, tables) -> list[dict]:
    """`valid`: [{"id", "tier", "tags": [...]}]; `patterns`: server/analytics/patterns.Pattern objects. Returns new dicts with `score` and `matched`, best first (ties by id)."""
    wanted: dict[str, str] = {}
    for p in patterns:
        for tag in tags_of(p.key):
            wanted.setdefault(tag, p.kind + " " + p.key)
    out = []
    for c in valid:
        gap = abs(tables.case_rating(c["tier"]) - elo_target) / 100.0
        hit = [t for t in c.get("tags", []) if t in wanted]
        out.append({**c, "score": round(-gap + PATTERN_WEIGHT * len(hit), 3), "matched": [(t, wanted[t]) for t in hit]})
    return sorted(out, key=lambda c: (-c["score"], c["id"]))


def explain(first: dict) -> str:
    if first["matched"]:
        tag, pattern = first["matched"][0]
        return f"This case is chosen because it includes {tag}, where the pattern was: {pattern.replace('_', ' ')}."
    return "This case is chosen for its difficulty alone; no recent pattern points at a different one."
