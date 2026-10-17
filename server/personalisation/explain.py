"""Why this scenario: an explanation made only from the facts that chose it (p1-personalisation-s05, feature D68).

    facts = facts_for(choice, model, tables, labels)       # the numbers and names behind the choice
    text = render(facts)                                    # a template sentence or two
    problems = check(text, facts)                           # [] when every number and name in the text is one of the facts

The sentence is built from the policy's choice and the learner model's numbers and nothing else: the dimension, the behaviour that led to it, how sure the model is (the status and the interval), and how many
sessions it rests on. A hypothesis is called a hypothesis and never a weakness. A language model may reword the sentence later; whatever it returns goes through `check` and the template is shown if it fails
(p1-ai-language-s08 uses the same rule for the debrief). Nothing here praises or blames; it says what the next case is for.
"""
from __future__ import annotations

import re

NUMBER = re.compile(r"\d+(?:\.\d+)?")
STATUS_WORDS = {"weakness": "a demonstrated weakness", "hypothesis": "something that may be a weakness (not enough evidence yet)", "developing": "still developing", "strength": "a strength", "unknown": "not yet measured"}


def facts_for(choice, model, tables, labels: dict[str, str] | None = None) -> dict:
    """`labels` maps a behaviour id to its plain name (content/scoring); the id is used if there is none."""
    labels = labels or {}
    dim = next(d for d in tables.dimensions if d.id == choice.dimension)
    focus = choice.behaviours[0] if choice.behaviours else (dim.weak_behaviours[0] if dim.weak_behaviours else "")
    m = model.mastery(focus) if focus else None
    return {"dimension": dim.id, "dimension_label": dim.label, "behaviour": focus, "behaviour_label": labels.get(focus, focus), "status": m.status if m else "unknown",
            "right_percent": round(100 * m.mean) if m and m.sessions else None, "low_percent": round(100 * m.low) if m and m.sessions else None, "high_percent": round(100 * m.high) if m and m.sessions else None,
            "sessions": m.sessions if m else 0, "policy": choice.policy}


def render(f: dict) -> str:
    if f["sessions"] == 0:
        return f"This case exercises {f['dimension_label'].lower()}, which you have not practised yet, so the next sessions can measure it."
    status = STATUS_WORDS[f["status"]]
    return (f"This case exercises {f['dimension_label'].lower()} because {f['behaviour_label'].lower()} looks like {status}: about {f['right_percent']} out of 100 items right "
            f"(likely between {f['low_percent']} and {f['high_percent']}) over {f['sessions']} session{'s' if f['sessions'] != 1 else ''}.")


def check(text: str, f: dict) -> list[str]:
    """Defects of an explanation (a template output or a model's rewording): a number that is not a fact, a name that is not one of the facts, a hypothesis called a weakness."""
    allowed = {str(x) for x in (f["right_percent"], f["low_percent"], f["high_percent"], f["sessions"], 100) if x is not None}
    out = [f"UNGROUNDED_NUMBER: {n}" for n in NUMBER.findall(text) if n not in allowed]
    low = text.lower()
    if f["status"] in ("hypothesis", "developing", "unknown") and re.search(r"\bdemonstrated weakness\b|\bweakness\b", low) and "may be a weakness" not in low:
        out.append("OVERSTATED: the model has not shown a weakness")
    if f["status"] == "strength" and re.search(r"\bweak", low):
        out.append("CONTRADICTION: the behaviour is a strength")
    return out
