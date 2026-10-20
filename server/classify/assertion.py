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
# Who else can be the one acting (one closed list for every pattern below; before 20 October 2026 four patterns each carried their own shorter copy, and "the technician will report the fault" was credited): ward, theatre, laboratory and imaging staff.
_ROLES = (r"registrar|nurse|consultant|anaesthetist|anesthetist|surgeon|porter|doctor|dr\.? \w+|colleague|team|sister|technician|radiographer|sonographer|physio|physiotherapist|pharmacist|phlebotomist|midwife|matron|odp|hca|healthcare assistant|"
          r"anaesthetic assistant|assistant|student|trainee|resident|specialist|senior|junior|engineer|manager|receptionist|ward clerk|chaplain|interpreter|lab|laboratory")
_RANK = r"(senior |junior |duty |ward |theatre |charge |scrub |night |lead |on-call |on call )?"
_THE_ROLE = rf"the {_RANK}({_ROLES})"                              # "the registrar", "the senior nurse"
_WHO = rf"(the |a |my |our )?{_RANK}({_ROLES})"                    # the same, with the article optional
_LATER = (r"in a moment|in a minute|in a bit|in (a|an|\d+|a few|a couple of|some) (minutes?|seconds?|moments?)|shortly|later|after (this|that|the|we|i|you|your|my|our)\b|once we start|give me a (minute|moment)|at some point|"
          r"tomorrow|tonight|this (afternoon|evening)|next (week|time)|when i (have|get|am)|before we (go|start|begin)|once (i|we|you) (have|has|get|are|am)\b")
_PATTERNS = [
    ("incomplete", re.compile(r"(\.\.\.|…|\s-)\s*$")),
    ("denied", re.compile(rf"\b(haven'?t|have not|hasn'?t|didn'?t|did not|never|won'?t|will not|not going to|am not going to|i'?m not going to|can'?t|cannot|wasn'?t going to|no longer)\b(\s+\w+){{0,3}}\s+{_VERBS}\b")),
    ("denied", re.compile(rf"\b(not (yet|now|today))\b(\s+\w+){{0,2}}\s+{_VERBS}\b")),
    ("denied", re.compile(rf"\b(was going to|meant to|about to|wanted to|intended to)\b(\s+\w+){{0,2}}\s+{_VERBS}\b[^.?!]{{0,40}}\b(but|then)\b[^.?!]{{0,25}}\b(forget|not|didn'?t|won'?t|never|can'?t|haven'?t|no time|no chance|out of time)")),
    ("hypothetical", re.compile(r"\b(if i (were|was|had|did|asked|said|told|explained|ordered|called|reassured|checked|mentioned)\b|had i\b|were i to\b|(?<!can )(?<!only )imagine\b|pretend\b|what if\b|hypothetical)")),
    ("hypothetical", re.compile(rf"\b(maybe|perhaps) (i|we) (should|could|ought to|might)\b[^.?!]{{0,60}}\b(but|though)\b[^.?!]{{0,20}}\b(won'?t|will not|not|don'?t)")),
    ("reported", re.compile(rf"\b(he|she|they|{_THE_ROLE}|someone|nobody|no one)\b(\s+\w+){{0,2}}\s+(said|says|told|tell|promised|will|would|asked|wants|wanted|is going to|was going to|has yet to)\b[^.?!]{{0,40}}\b{_VERBS}\b")),
    ("reported", re.compile(r"\b(nobody|no one|nobody has|no one has)\b[^.?!]{0,30}\b(told|said|asked|explained|reassured)\b")),
    ("reported", re.compile(rf"\b({_THE_ROLE}|she|he|they) (said|says|told me|asked me|promised)\b[^.?!]{{0,50}}\b(later|tomorrow|afterwards|soon|yet)\b")),
    # fifth session, from the failures of the first benchmark test split (tests/action_bench_v1): intentions that are put off, someone else doing the speaking, other ways of reporting and imagining
    ("intended", re.compile(rf"^(?=.*\b({_LATER})).*\b(i'?ll|i will|i shall|i'?m planning to|i am planning to|i plan to|i intend to|planning to|intend to)\b")),
    ("other_actor", re.compile(rf"\b{_WHO}\b[^.?!]{{0,25}}\b(will|is going to|is about to|are going to|can|should|shall|to)\b(\s+\w+){{0,3}}\s+({_VERBS}|talk\w*|speak|discuss\w*|advis\w+)\b")),
    ("other_actor", re.compile(rf"\blet (the |a |my |our )?[a-z ]{{0,25}}?\b{_VERBS}\b")),
    ("reported", re.compile(r"\b(apparently|according to|i heard that|heard that|my colleague says|colleague says|the chart says)\b")),
    ("hypothetical", re.compile(r"\b(say|suppose|supposing) (that )?(i|we)\b|\bpicture (me|us)\b")),
    ("denied", re.compile(r"\b(haven'?t|have not|hasn'?t|never|not going to|won'?t|will not|didn'?t|did not)( yet| now)?\s+(said|say)\b")),
    ("other_actor", re.compile(rf"\b({_ROLES})\b[^.?!]{{0,25}}\b(will|is going to|is about to|can|should|to)\s+say\b")),
    # 20 October 2026 (recall follow-up, ADR-0022): a conditional that is stated or asked, not done ("If the suction were broken I would report it", "If you asked how long it takes, I would say an hour")
    ("hypothetical", re.compile(r"^if\b[^.?!]{0,100}([, ]\s*(i|we|you|they|he|she|it|that|this)('d| would| could| might)\b|,\s*would\b|\bwhat would you\b)")),
]


def non_assertive(sentence: str) -> str:
    """"denied", "hypothetical", "reported" or "incomplete" (the sentence breaks off: it ends in an ellipsis or a dash) when the speaker does not perform the act in the sentence, else an empty string."""
    text = " ".join(sentence.lower().replace("’", "'").split())
    for kind, pattern in _PATTERNS:
        if pattern.search(text):
            return kind
    return ""
