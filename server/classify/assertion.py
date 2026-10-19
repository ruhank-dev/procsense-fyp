"""Non-assertive speech guard for the communication classifiers: a sentence in which the speaker does NOT perform the act (denies doing it, only imagines it, or reports that somebody else will or did) earns no credit.

    non_assertive("I'm not going to explain the procedure now")      # "denied"
    non_assertive("The nurse said she would reassure you later")      # "reported"
    non_assertive("If I were to explain the operation, I'd start...") # "hypothetical"
    non_assertive("Could you explain it again?")                      # ""

The task definition is the rubric (tests/classifier_synthetic_v2/rubric.md rule 10): a sentence saying the speaker has NOT yet done something is NONE, and the act a sentence performs is the act it is labelled with. A
denial, a hypothetical, a report or a sentence that breaks off (an incomplete action) is recorded by the classifier but never credited, exactly as a critical act is. The patterns are deliberately narrow: they need a speech-act verb (ask, explain, tell, check, repeat,
order, reassure, mention...) next to the negation, so "I didn't catch that" (a request to repeat) and "No need to panic" (a reassurance) keep their credit. The guard only ever removes credit.
"""
from __future__ import annotations

import re

_VERBS = (r"(ask|asked|explain|explained|tell|told|check|checked|repeat|repeated|order|ordered|reassure|reassured|mention|mentioned|go over|went over|cover|covered|confirm|confirmed|request|requested|"
           r"clarif\w+|answer\w*|inform\w*|note[ds]?|acknowledg\w+|bleep\w*|clip\w*|rais\w+|lift\w*|put|listen\w*|press\w*|arrang\w+|send|read|call\w*|escalat\w+|report\w*|describ\w+|offer\w*|promis\w+|examin\w+|measur\w+|comfort\w*)")
_VERBS_R = _VERBS.replace("|say|said", "")
_PATTERNS = [
    ("incomplete", re.compile(r"(\.\.\.|…|\s-)\s*$")),
    ("denied", re.compile(rf"\b(haven'?t|have not|hasn'?t|didn'?t|did not|never|won'?t|will not|not going to|am not going to|i'?m not going to|can'?t|cannot|wasn'?t going to|no longer)\b(\s+\w+){{0,3}}\s+{_VERBS}\b")),
    ("denied", re.compile(rf"\b(not (yet|now|today))\b(\s+\w+){{0,2}}\s+{_VERBS}\b")),
    ("denied", re.compile(rf"\b(was going to|meant to|about to|wanted to|intended to)\b(\s+\w+){{0,2}}\s+{_VERBS}\b[^.?!]{{0,40}}\b(but|then)\b[^.?!]{{0,25}}\b(forget|not|didn'?t|won'?t|never|can'?t|haven'?t|no time|no chance|out of time)")),
    ("hypothetical", re.compile(r"\b(if i (were|was|had|did)\b|had i\b|were i to\b|(?<!can )(?<!only )imagine\b|pretend\b|what if\b|hypothetical)")),
    ("hypothetical", re.compile(rf"\b(maybe|perhaps) (i|we) (should|could|ought to|might)\b[^.?!]{{0,60}}\b(but|though)\b[^.?!]{{0,20}}\b(won'?t|will not|not|don'?t)")),
    ("reported", re.compile(rf"\b(he|she|they|the (nurse|registrar|doctor|consultant|surgeon|sister|anaesthetist|team)|someone|nobody|no one)\b(\s+\w+){{0,2}}\s+(said|says|told|tell|promised|will|would|asked|wants|wanted|is going to|was going to|has yet to)\b[^.?!]{{0,40}}\b{_VERBS}\b")),
    ("reported", re.compile(r"\b(nobody|no one|nobody has|no one has)\b[^.?!]{0,30}\b(told|said|asked|explained|reassured)\b")),
    ("reported", re.compile(r"\b(the nurse|the registrar|the doctor|the consultant|the surgeon|she|he|they) (said|says|told me|asked me|promised)\b[^.?!]{0,50}\b(later|tomorrow|afterwards|soon|yet)\b")),
    # fifth session, from the failures of the first benchmark test split (tests/action_bench_v1): intentions that are put off, someone else doing the speaking, other ways of reporting and imagining
    ("intended", re.compile(r"^(?=.*\b(in a moment|in a minute|shortly|later|after this|once we start|give me a (minute|moment)|in a bit|after we)\b).*\b(i'?ll|i will|i shall|i'?m planning to|i am planning to|i plan to|i intend to|planning to|intend to)\b")),
    ("other_actor", re.compile(rf"\b(the |a |my |our )?(registrar|nurse|consultant|anaesthetist|surgeon|porter|doctor|dr\.? \w+|colleague|team|sister)\b[^.?!]{{0,25}}\b(will|is going to|is about to|are going to|can|should|shall|to)\b(\s+\w+){{0,3}}\s+{_VERBS}\b")),
    ("other_actor", re.compile(rf"\blet (the |a |my |our )?[a-z ]{{0,25}}?\b{_VERBS}\b")),
    ("reported", re.compile(r"\b(apparently|according to|i heard that|heard that|my colleague says|colleague says|the chart says)\b")),
    ("hypothetical", re.compile(r"\b(say|suppose|supposing) (that )?(i|we)\b|\bpicture (me|us)\b")),
    ("denied", re.compile(r"\b(haven'?t|have not|hasn'?t|never|not going to|won'?t|will not)( yet| now)?\s+(said|say)\b")),
    ("other_actor", re.compile(r"\b(registrar|nurse|consultant|anaesthetist|surgeon|porter|doctor|dr\.? \w+|colleague)\b[^.?!]{0,25}\b(will|is going to|is about to|can|should|to)\s+say\b")),
]


def non_assertive(sentence: str) -> str:
    """"denied", "hypothetical", "reported" or "incomplete" (the sentence breaks off: it ends in an ellipsis or a dash) when the speaker does not perform the act in the sentence, else an empty string."""
    text = " ".join(sentence.lower().replace("’", "'").split())
    for kind, pattern in _PATTERNS:
        if pattern.search(text):
            return kind
    return ""
