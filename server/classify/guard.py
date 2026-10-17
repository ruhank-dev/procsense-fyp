"""Critical-content guard for the communication classifiers (shared by the embedding classifier and the language-model classifier).

    cue = critical_cue("Please note she is allergic to penicillin")     # "allergy", or "" when the sentence touches no critical content

A speech classification may only ever add evidence for a NON-critical act, and never for a critical one (the engine refuses those; server/classify/classifier.py never credits them). The gap this closes: a classifier can
label a sentence that is really about an allergy, an identity check, consent, the fasting state, a worsening sign or a structured handover as some ordinary act (the benchmark found the language model doing this) and so earn
credit for it. If the sentence carries such a cue and the predicted act is not critical, the answer is recorded but not credited. This only ever removes credit, so it cannot make anything easier to pass. The cues live in
content/ai/critical_cues.json so a reviewer can read and change them without code.
"""
from __future__ import annotations

import json
import re
from functools import lru_cache
from pathlib import Path

CUES = Path(__file__).resolve().parents[2] / "content" / "ai" / "critical_cues.json"


LEXICON = Path(__file__).resolve().parents[2] / "content" / "ai" / "lexicon.json"


@lru_cache
def _compiled() -> list[tuple[str, re.Pattern]]:
    cues = json.loads(CUES.read_text(encoding="utf-8"))["cues"]
    out = [(kind, re.compile(p)) for kind, patterns in cues.items() for p in patterns]
    allergens = json.loads(LEXICON.read_text(encoding="utf-8"))["categories"]["allergen"]       # a sentence that names an allergen may be about an allergy
    out += [("allergy", re.compile(r"\b" + re.escape(a.lower()) + r"\b")) for a in allergens]
    return out


def critical_cue(sentence: str) -> str:
    """The kind of critical content the sentence touches ("allergy", "identity", "consent", "fasting", "worsening", "handover") or an empty string."""
    text = " ".join(sentence.lower().split())
    for kind, pattern in _compiled():
        if pattern.search(text):
            return kind
    return ""
