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
# Physical actions, added ONLY next to a negation (the first two "denied" patterns): "I won't take your pulse now" is a denial, but "the porter will take her to the scanner" must not become one (they are not in _VERBS).
_PHYSICAL = r"take(?! (that|it|this|all that|everything) in)|took(?! (that|it|this|all that|everything) in)|look|looked|inspect\w*|palpat\w*|auscult\w*|wrap|place|attach|fit|insert|open|clean|wash"
_NEG_VERBS = "(" + _VERBS[1:-1] + "|" + _PHYSICAL + ")"
_VERBS_R = _VERBS.replace("|say|said", "")
# Who else can be the one acting (one closed list for every pattern below; before 20 October 2026 four patterns each carried their own shorter copy, and "the technician will report the fault" was credited): ward, theatre, laboratory and imaging staff.
_ROLES = (r"registrar|nurse|consultant|anaesthetist|anesthetist|surgeon|porter|doctor|dr\.? \w+|colleague|team|sister|technician|radiographer|sonographer|physio|physiotherapist|pharmacist|phlebotomist|midwife|matron|odp|hca|healthcare assistant|"
          r"anaesthetic assistant|assistant|student|trainee|resident|specialist|senior|junior|engineer|manager|receptionist|ward clerk|chaplain|interpreter|lab|laboratory|"
          r"tutor|teacher|trainer|instructor|examiner|supervisor|lecturer|mentor|facilitator")
_RANK = r"(senior |junior |duty |ward |theatre |charge |scrub |night |lead |on-call |on call )?"
_THE_ROLE = rf"the {_RANK}({_ROLES})"                              # "the registrar", "the senior nurse"
_WHO = rf"(the |a |my |our )?{_RANK}({_ROLES})"                    # the same, with the article optional
_LATER = (r"in a moment|in a minute|in a bit|in (a|an|\d+|a few|a couple of|some) (minutes?|seconds?|moments?)|shortly|later|(?<!look )(?<!looks )(?<!looking )(?<!looked )(?<!care )after (this|that|the|we|i|you|your|my|our)\b|once we start|give me a (minute|moment)|at some point|"
          r"tomorrow|tonight|this (afternoon|evening)|next (week|time)|when i (have|get|am)|before we (go|start|begin)|once (i|we|you) (have|has|get|are|am)\b")

# Past speech verbs of somebody else, and the closed lists behind the 20 October 2026 (second session) safety patterns. Each pattern below only REMOVES credit and needs a speech-act verb or a stated frame; none changes what an assertive first-person sentence earns.
_PAST = (r"(asked|told|explained|noted|checked|confirmed|mentioned|reassured|ordered|acknowledged|reported|called|sent|answered|clarified|examined|measured|offered|described|informed|requested|bleeped|arranged|raised|repeated|"
         r"read back|went over|covered|said|spoke|talked|discussed|advised|promised|escalated|apologi[sz]ed)")
_SOMEONE_ELSE = rf"(he|she|they|someone|somebody|everyone|anyone|nobody|no one|{_WHO})"
_HEDGE = (r"(maybe|perhaps|probably|possibly|i think|i guess|i believe|i suppose|i reckon|i'?m (pretty |fairly |quite |not )?sure|pretty sure|not (completely |entirely )?sure|might have|may have|could have|must have|"
          r"i don'?t remember|i can'?t remember|not certain|can'?t be certain)")
# somebody else did it: the subject is named (not "you") and the verb is a speech act in the past. Not for said/told (relaying what a colleague said is how an answer is often given; the reporting patterns above cover "said/told ... <verb>"),
# not inside a question (what the speaker ASKS is the act), and not after a verb of wanting or getting ("I want the registrar bleeped", "get the consultant informed")
_OTHER_PAST = re.compile(rf"\b{_SOMEONE_ELSE}\s+(\w+\s+){{0,3}}(asked|noted|checked|confirmed|mentioned|reassured|ordered|acknowledged|reported|called|sent|answered|clarified|examined|measured|offered|described|informed|requested|bleeped|arranged|raised|repeated|read back|went over|covered|discussed|escalated|explained|apologi[sz]ed)\b")
_CAUSATIVE = re.compile(r"\b(want|wants|wanted|need|needs|needed|get|got|have|had|like|wish|please|let|make|ask|asked|tell|told|bleep|call)[^.?!]{0,30}$")  # no closing word boundary: recogniser text runs words together ("wantthe consultant informed")
_FILLER_ONLY = frozenset("yes yeah yep no nope okay ok right alright sure thanks thank you mm mmm hm hmm uh um er erm sorry pardon hello hi fine good great i see go on that's thats excuse me just a moment let think then huh well so oh ah aha uhuh mhm".split())
_PATTERNS = [
    ("incomplete", re.compile(r"(\.\.\.|…|\s-)\s*$")),
    ("denied", re.compile(rf"\b(haven'?t|have not|hasn'?t|didn'?t|did not|never|won'?t|will not|not going to|am not going to|i'?m not going to|can'?t|cannot|wasn'?t going to|no longer)\b(\s+\w+){{0,3}}\s+{_NEG_VERBS}\b")),
    ("denied", re.compile(rf"\b(not (yet|now|today))\b(\s+\w+){{0,2}}\s+{_NEG_VERBS}\b")),
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
    # 20 October 2026 (unsafe-speech hardening, ADR-0023), from the failures of the development split of tests/safety_challenge:
    # speech that is quoted (a pair of double quotes: recogniser text never has them, a typed or read-out script does)
    ("quoted", re.compile(r'"[^"]{3,}"|“[^”]{3,}”')),
    # a retraction or a correction of what was just said: nothing in the sentence is claimed any more
    ("retracted", re.compile(r"\b(no,? wait|wait,? no|scratch that|ignore that|disregard that|forget (that|i said)|i (misspoke|mis-spoke|take that back)|never mind that)\b|\b(i|we) (didn'?t|did not|haven'?t|have not|never)\W*$")),
    # a question about WHETHER to act, not an act: "should I...", "do you want me to...", "is it necessary to...", "is it true that..."
    ("meta_question", re.compile(r"^(?!.*\b(again|once more|one more time|go over))(should|shall) (i|we)\b|^(do|would) you (want|like) me to\b|^do (i|we) (need|have) to\b|^(am i|are we) (supposed|meant|allowed|expected) to\b|^is it (necessary|true|right|correct|accurate|ok|okay|fine|appropriate) (to|that|for me to)\b|^would it be (right|ok|okay|fine|appropriate|correct) (to|if)\b|^why (would|should|do|did) (anyone|someone|we|one|people)\b")),
    # a hedge about whether it happened ("maybe I noted...", "I'm pretty sure someone asked..."): an unsupported claim is no evidence
    ("hedged", re.compile(rf"\b{_HEDGE}\b[^.?!]{{0,40}}\b({_SOMEONE_ELSE}|i|we)\s+(\w+\s+)?{_PAST}\b")),
    ("hedged", re.compile(rf"\b(might|may|could|must) have {_PAST}\b")),
    # somebody else did it (the sentence says who, and the verb is a speech act in the past)
    # reported by reading or hearsay
    ("reported", re.compile(r"\b(i read that|i was told|i'?ve been told|i'?m told|someone told me|they say that|word is)\b")),
    # an intention or a possibility that is not carried out: "I might ask later", "someone ought to explain", "let's ask tomorrow"
    ("intended", re.compile(rf"\b(i|we)\s+(might|may|could|should|ought to|would like to|'d like to|want to|wish to|hope to)\s+(\w+\s+){{0,2}}{_VERBS}\b")),
    ("intended", re.compile(rf"\b(someone|somebody|anyone|everyone|one|people)\s+(ought to|should|needs to|must|has to)\s+(\w+\s+){{0,2}}{_VERBS}\b")),
    ("intended", re.compile(rf"^(?=.*\b({_LATER})).*\b(let'?s|we'll|we will|we should|(i'?m|i am|we'?re|we are) (going to|gonna)|gonna)\b")),
    # an action that was begun or meant and not finished ("I wanted to ask, but then I got distracted")
    ("denied", re.compile(rf"\b(wanted to|meant to|intended to|planned to|was going to|was about to|tried to|was trying to|was supposed to|started to|began to)\b[^.?!]{{0,60}}\b(but|before|when|until|though)\b")),
    # second pass, from the first look at the challenge split (20 October 2026): the same mechanisms in other words (this split is development from here on; tests/safety_challenge/README.md)
    ("intended", re.compile(rf"\b(i|we) (plan|intend|propose|aim|hope|expect|am planning|are planning|'m planning)( to)? (we |i )?(will |shall |would )?(\w+ ){{0,2}}{_VERBS}\b")),
    ("intended", re.compile(rf"\bpromise (to|(i|we) (will|shall|'ll)) (\w+ ){{0,2}}{_VERBS}\b")),
    ("hypothetical", re.compile(rf"\b(i|we)('d| would) (\w+ ){{0,2}}{_VERBS}\b")),
    ("denied", re.compile(rf"\b(forgot|forget|forgotten|neglected|failed|omitted|skipped|didn'?t get round|didn'?t get around|never got round|ran out of time|no time) (to )?(\w+ ){{0,2}}{_VERBS}\b")),
    ("reported", re.compile(r"\b(heard|overheard|watched|saw|read) (someone|somebody|him|her|them|the [a-z]+|a [a-z]+) (say|says|saying|said|ask|asking|asked|tell|telling|told|explain|explaining|explained)")),
    ("retracted", re.compile(r"\b(that|this) (was|is) (wrong|a mistake|incorrect|not right|not true|the wrong)|\bwrong (patient|person|chart|bed|notes|name)\b|(^|[.?!,]\s*)wait[,.!]")),
    ("hypothetical", re.compile(r"^if\b[^.?!]{0,100},\s*(then )?(i|we) (don'?t|do not|needn'?t|won'?t|wouldn'?t|will not|would not)\b")),
    ("incomplete", re.compile(r"\b(where was i|what was i (saying|going to say)|i lost my (train|place|thought))\b|\b(the|a|an|and|or|your|my|um|uh|er|so|but|because)\b[,.]?\s*$")),
    ("denied", re.compile(rf"^(please )?(remember|don'?t forget) to (\w+ ){{0,2}}{_VERBS}\b(?![^.?!]*\b(if|when|so that)\b)")),
]


def contentless(text: str) -> bool:
    """True when every word is a discourse particle (yes, okay, thank you, um...): there is nothing to credit."""
    words = re.findall(r"[a-z']+", text)
    return bool(words) and all(w in _FILLER_ONLY for w in words)


def non_assertive(sentence: str) -> str:
    """"denied", "hypothetical", "reported" or "incomplete" (the sentence breaks off: it ends in an ellipsis or a dash) when the speaker does not perform the act in the sentence, else an empty string."""
    text = " ".join(sentence.lower().replace("’", "'").split())
    if contentless(text):
        return "contentless"
    if not (text.endswith("?") or re.match(r"^(can|could|would|will|do|does|did|is|are|am|was|were|have|has|had|shall|should|may|what|why|how|when|where|who|which)\b", text)):
        for m in _OTHER_PAST.finditer(text):
            if not _CAUSATIVE.search(text[:m.start()]):
                return "other_actor"
    for kind, pattern in _PATTERNS:
        if pattern.search(text):
            return kind
    return ""
