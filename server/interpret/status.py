"""Status of an utterance (p1-ai-language-s11): what the speaker does with the words, decided by rules (no model).

    status_of("I haven't asked about your sleep yet.")      # "negated"  (the assertion layer's kinds: denied, hypothetical, reported, intended, other_actor, incomplete)
    status_of("Do you sleep well at night?")                # "question"
    status_of("The operation takes an hour.")               # "actual"

The assertion layer of the classifier gate (server/classify/assertion.py) already recognises the non-performed kinds; this module adds the question form, which matters because some acts ARE questions (asking about the history,
asking to repeat) and the others (explaining, reassuring, answering) are not performed by asking. An utterance that ends in a question mark is a question; one with no closing punctuation is a question when it opens with an
interrogative word or an inverted auxiliary (recogniser text often has no punctuation).
"""
from __future__ import annotations

import re

try:
    from classify.assertion import non_assertive
except ImportError:
    from server.classify.assertion import non_assertive

_OPENERS = re.compile(r"^(can|could|would|will|do|does|did|is|are|am|was|were|have|has|had|shall|should|may|what|why|how|when|where|who|whom|which|whose)\b")


def is_question(sentence: str) -> bool:
    text = " ".join(sentence.strip().lower().split())
    if not text:
        return False
    if text.endswith("?"):
        return True
    if text[-1] in ".!":
        return False
    return bool(_OPENERS.match(text))


def status_of(sentence: str) -> str:
    """One of: denied, hypothetical, reported, intended, other_actor, incomplete (the assertion layer), question, actual."""
    kind = non_assertive(sentence)
    if kind:
        return kind
    return "question" if is_question(sentence) else "actual"
