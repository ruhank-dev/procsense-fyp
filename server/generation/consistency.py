"""Deterministic checks of the patient's own words against the structured case: the side of the body and pain that is denied (p1-live-scenario-generation-s15, feature B18, stage LG-11).

    from generation.consistency import check
    findings = check(case)          # [] or [Finding(line, rule, detail)]

The text critic (critic.py) is a small language model. Measured on the seeded set it found only 13 of 29 flipped sides and 14 of 36 denied pains, so the two defects that matter most for a trainee who
reads the patient's words are no longer left to it. These rules are plain code over the same lines the critic reads (critic.lines_of) and the structured facts the case already carries (the examination
findings and the problem labels). They need no model, are always run by the package gate, and only ever add a defect; nothing here is advice. They are written to be cautious: a side word only counts
when it is about the body (not "I do not feel right"), a negated side ("not on the left") is ignored, a side next to a limb or the chest is ignored (a pain may be referred), and a denial of pain only
counts when it is not about somewhere else ("no chest pain" is a normal answer). All authored cases and all their variants pass (tests/generation and tools/ci/test_generation_consistency.py), so a flag
on an authored case would be a bug in the rule, not in the case.

Rules:
  SIDE_VS_EXAM   a line puts the pain on one side, the examination and problems of the case put it on the other
  SIDE_VS_OPERATION  with no side in the structured facts, a line puts the pain on the left of a gallbladder or appendix operation (both organs are on the right)
  PAIN_DENIED    a line denies pain without naming another place, in a case whose examination found a painful abdomen or whose complaint, reason or summary gives pain
"""
from __future__ import annotations

import re
from dataclasses import dataclass

try:
    from generation import critic
except ImportError:
    from server.generation import critic


@dataclass(frozen=True)
class Finding:
    line: str
    rule: str
    detail: str


SIDE = re.compile(r"\b(left|right)\b", re.I)
IDIOM_BEFORE = re.compile(r"(?:feel|feels|felt|feeling|all|alright|that'?s|it'?s|is|isn'?t|not|be|get|got|go|going|done|do|did|sounds?|looks?|seems?|just|about|exactly|quite|very|so)\s+$", re.I)
IDIOM_AFTER = re.compile(r"^\s*(?:away|now|after|before|then|at|there|here|thing|choice|to|of way)\b", re.I)
LEFT_VERB = re.compile(r"(?:\bI|\bhe|\bshe|\bthey|\bwe|\bhave|\bhas|\bhad|\bwas|\bwere|\bjust|\bbeen|\bto)\s+$", re.I)
NEGATED = re.compile(r"(?:\bnot|\bno|\bnever|\bwithout|n't|\bnor|\bneither)(?:\s+\w+){0,3}\s+$", re.I)
ELSEWHERE = re.compile(r"^\W*(?:\w+\W+){0,2}?(?:shoulder|arm|leg|hand|foot|feet|ear|eye|knee|hip|jaw|chest|breast|neck|thigh|calf|ankle|wrist|elbow|testicle|ovary|ovarian)\b", re.I)
PAINFUL = re.compile(r"\b(?:tender|tenderness|guarded|guarding|rigid|rigidity|peritonism|rebound)\b", re.I)
PAIN_WORD = re.compile(r"\bpain|\bache|\bhurt|\bcramp|\bsore\b", re.I)
BODY_AFTER = re.compile(r"^\W*(?:in|on|at|around|under|near)\s+(?:my|the|his|her)\s+(?:tummy|belly|stomach|abdomen|side|middle)\b|^\W*(?:at all|anywhere|now|today|at the moment|right now|whatsoever)\b|^\W*$|^\W*[,.;]|^\W*(?:and|but|just|only|I)\b", re.I)
DENIAL = re.compile(r"(?:\bno|\bnot any|\bwithout any|\bnever had any|\bdon'?t have any|\bdo not have any|\bhaven'?t (?:got|had) any|\bnothing like)\s+((?:\w+\s+){0,1})pain\b|\bnot in (?:any )?pain\b|\bpain[- ]free\b|\bno aches?\b|\bnothing hurts\b|\bit doesn'?t hurt\b|\bit does not hurt\b", re.I)
ANATOMY = {"S1": "right", "S2": "right"}                      # S1 cholecystectomy and S2 appendicectomy: both organs are on the right in every authored case
PLACE_BEFORE = {"chest", "back", "leg", "legs", "calf", "arm", "arms", "jaw", "joint", "joints", "muscle", "bone", "ear", "throat", "head", "neck", "urinary", "burning", "shoulder", "knee", "foot", "feet", "hand", "eye", "tooth", "period", "pelvic", "testicular", "leg's"}


def _mentions(text: str) -> list[tuple[str, int]]:
    """The positive, bodily mentions of a side in a text: [(left|right, position)]."""
    out = []
    for m in SIDE.finditer(text):
        side = m.group(1).lower()
        before, after = text[:m.start()], text[m.end():]
        if side == "right" and (IDIOM_BEFORE.search(before) or IDIOM_AFTER.match(after)):
            continue
        if side == "left" and (LEFT_VERB.search(before) or re.match(r"^\s*(?:over|me|him|her|it|them|us|school|hospital|work|home|my|out|behind)\b", after, re.I)):
            continue
        if NEGATED.search(before[-40:]):
            continue
        if ELSEWHERE.match(after):
            continue
        out.append((side, m.start()))
    return out


def _sides(text: str) -> set[str]:
    return {s for s, _ in _mentions(text)}


def structured_sides(case: dict) -> set[str]:
    """The sides the case itself gives for the abdomen: from the abnormal examination findings and the problem labels."""
    sides: set[str] = set()
    for e in case["examination"]:
        if e["abnormal"]:
            sides |= _sides(e["finding"])
    for p in case["problems"]:
        sides |= _sides(p["label"])
    return sides


def painful_abdomen(case: dict) -> bool:
    return any(e.get("painful") or (e["abnormal"] and PAINFUL.search(e["finding"]) and not re.search(r"\bno (?:tenderness|guarding|rigidity|peritonism)\b", e["finding"], re.I)) for e in case["examination"] if e["abnormal"])


def side_findings(case: dict, lines: dict[str, str]) -> list[Finding]:
    ref = structured_sides(case)
    source = "the examination and problems of the case"
    if len(ref) != 1:
        ref = {ANATOMY[case["scenario"]]} if case["scenario"] in ANATOMY else set()
        source = "the operation (the gallbladder and the appendix are on the right)"
    if len(ref) != 1:
        return []
    (want,) = ref
    out = []
    for line, text in lines.items():
        wrong = _sides(text) - {want}
        if wrong:
            out.append(Finding(line, "SIDE_VS_EXAM" if source.startswith("the exam") else "SIDE_VS_OPERATION", f"says {sorted(wrong)[0]}, but {source} say {want}"))
    return out


def denies_pain(text: str) -> bool:
    for m in DENIAL.finditer(text):
        qualifier = (m.group(1) or "").strip().lower() if m.lastindex else ""
        if qualifier in PLACE_BEFORE:
            continue
        if qualifier and qualifier not in {"abdominal", "tummy", "belly", "stomach", "other", "any", "real", "much", "severe", "bad"}:
            continue
        if m.group(0).lower().endswith("pain") and not BODY_AFTER.match(text[m.end():]):
            continue                                        # "no pain when I pass urine", "no pain in my calf": about somewhere else or something else
        return True
    return False


def has_pain(case: dict, lines: dict[str, str]) -> str:
    """Why the case has pain: the examination found it, or a line that does not deny it says so ("" if neither)."""
    if painful_abdomen(case):
        return "the examination found a painful abdomen"
    for line in ("h.complaint", "reason", "summary"):
        text = lines.get(line, "")
        if PAIN_WORD.search(text) and not denies_pain(text):
            return f"the {line.removeprefix('h.')} line gives pain"
    return ""


def pain_findings(case: dict, lines: dict[str, str]) -> list[Finding]:
    why = has_pain(case, lines)
    return [Finding(line, "PAIN_DENIED", f"denies pain, but {why}") for line, text in lines.items() if why and denies_pain(text)]


def check(case: dict) -> list[Finding]:
    lines = critic.lines_of(case)
    return side_findings(case, lines) + pain_findings(case, lines)
