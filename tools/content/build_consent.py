"""Builds content/consent/consent.json: what the consent conversation must cover for each scenario, the patient's questions with their approved answers, and the rules (p0-simulation-workflow-s11).

    uv run --project tools tools/content/build_consent.py            # write
    uv run --project tools tools/content/build_consent.py --check    # fail if the file is out of date

Source: walkthrough Part 10 'What to tell each patient' (S1 keyhole gallbladder removal, S2 appendix removal) and 'Common patient questions', and Step 9. The risk figures are the book's, each checked (SR-2850 to SR-2853); the
unit's own audit figures are preferred when a unit has them (institutional decision). The conversation rules, the understanding limit and the scores are the project's design (SR-2854, SR-2855).
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "content" / "consent" / "consent.json"

TOPICS = ["diagnosis", "operation", "benefits", "risks", "alternatives", "no_treatment", "matters"]


def el(topic, text, ref):
    return {"topic": topic, "text": text, "ref": ref}


S1 = [
    el("diagnosis", "You have gallstones which are causing attacks of pain.", "SR-2854"),
    el("operation", "We remove your gallbladder through 3-4 small cuts using a camera; we fill your tummy with gas to see.", "SR-2854"),
    el("benefits", "No more attacks; prevents complications (inflammation, jaundice, pancreatitis).", "SR-2854"),
    el("risks", "Wound infection (roughly 1-5%); bleeding; bile leak (about 0.5%, pooled 0.46%); major injury to the main bile duct (about 0.3%, pooled 0.28%, may need major repair); stone left in duct; conversion to open "
                "(about 2-5% planned operations, up to about 20% in emergency or inflamed cases); shoulder-tip pain from the gas; hernia at cut; ongoing symptoms; clots; bowel injury (rare); death (very rare, under about 0.1%).", "SR-2850 SR-2853"),
    el("alternatives", "Watch and wait, low-fat diet (attacks often return; complications possible).", "SR-2854"),
    el("no_treatment", "More attacks, possible inflammation, jaundice, pancreatitis.", "SR-2854"),
    el("matters", "Ask what matters most to the patient and answer questions honestly.", "SR-2854"),
]
S2 = [
    el("diagnosis", "Your appendix is inflamed: appendicitis.", "SR-2854"),
    el("operation", "We remove your appendix through 3 small cuts, or sometimes one larger cut.", "SR-2854"),
    el("benefits", "Removes the infection; prevents bursting, abscess and sepsis.", "SR-2854"),
    el("risks", "Wound infection (more in open or burst cases); pus collection inside (about 2-5% simple, more if burst); bleeding; injury to bowel or bladder (rare); conversion to open; appendix turns out normal; "
                "bowel slow to restart; clots.", "SR-2853"),
    el("alternatives", "For simple appendicitis without a stone inside: antibiotics only. 29% needed appendectomy by 90 days (25% without and 41% with an appendicolith) in the CODA trial (NEJM 2020); "
                       "39.1% had recurrence within 5 years in the APPAC trial (JAMA 2018).", "SR-2851 SR-2852"),
    el("no_treatment", "Bursting, abscess, peritonitis, sepsis, possibly death.", "SR-2854"),
    el("matters", "Ask what matters most to the patient and answer questions honestly.", "SR-2854"),
]
FALSE = "Don't worry, nothing can go wrong."
DISMISS = "I will tell you later, we are busy."


def question(id, scenario, text, approved):
    return {"id": id, "scenario": scenario, "text": text, "answers": [
        {"id": "approved", "text": approved, "kind": "approved"}, {"id": "false_reassurance", "text": FALSE, "kind": "false_reassurance"}, {"id": "dismissive", "text": DISMISS, "kind": "dismissive"}], "ref": "SR-2854"}


QUESTIONS = [
    question("s1.home", "S1", "Can I go home today?", "Often yes."),
    question("s1.scars", "S1", "Scars?", "3-4 small."),
    question("s1.diet", "S1", "Diet after?", "Normal; some find fatty food upsets them."),
    question("s1.work", "S1", "Work?", "1-2 weeks."),
    question("s2.children", "S2", "Will I be able to have children?", "Removing the appendix does not reduce fertility; a burst appendix with pelvic infection may slightly increase tube problems (the surgeon approves the wording)."),
    question("s2.tablets", "S2", "Can't I just take tablets?", "For simple appendicitis without a stone, antibiotics only is possible: 29% needed an appendectomy by 90 days (CODA); the surgeon discusses it."),
]
# a question comes up after these topics have been explained (the first unasked one of the scenario)
ASK_AFTER = ["benefits", "alternatives"]


def build() -> str:
    doc = {
        "topics": TOPICS,
        "procedures": {"S1": {"label": "keyhole gallbladder removal", "elements": S1}, "S2": {"label": "appendix removal", "elements": S2}},
        "questions": QUESTIONS,
        "ask_after": ASK_AFTER,
        "understanding_needed": 6,
        "minor_age": 18,
        "interpreters": {"trained": True, "family": False, "child": False},
        "refuses_at_cooperation": 2,
        "items": {
            "element": "credit.consent_element", "complete": "credit.consent_valid", "risks_skipped": "major.consent_risks_skipped", "alternatives_skipped": "major.consent_alternatives_skipped",
            "no_capacity_signed": "critical.consent_signed_without_capacity", "capacity_not_checked": "major.consent_capacity_not_checked", "capacity_checked": "credit.capacity_checked",
            "low_understanding": "major.consent_signed_low_understanding", "teach_back_ok": "credit.teach_back", "false_reassurance": "minor.false_reassurance", "answer_approved": "credit.question_answered",
            "answer_dismissive": "minor.question_dismissed", "untrained_interpreter": "major.consent_untrained_interpreter", "pressure": "major.consent_pressure", "minor_no_senior": "major.consent_minor_without_senior",
            "emergency_route": "credit.consent_emergency_route", "question_unanswered": "minor.question_unanswered",
        },
        "signed_flag": "consent.signed",
        "refs": ["SR-2850", "SR-2851", "SR-2852", "SR-2853", "SR-2854", "SR-2855"],
    }
    ids = [q["id"] for q in QUESTIONS]
    if len(set(ids)) != len(ids):
        raise SystemExit("duplicate question ids")
    for sc in ("S1", "S2"):
        if [e["topic"] for e in doc["procedures"][sc]["elements"]] != TOPICS:
            raise SystemExit(f"{sc} must cover every topic in order")
    return json.dumps(doc, indent=1, ensure_ascii=False) + "\n"


def main(argv: list[str]) -> int:
    text = build()
    if "--check" in argv:
        if not OUT.exists() or OUT.read_text(encoding="utf-8") != text:
            print("FAIL  content/consent/consent.json is out of date; run tools/content/build_consent.py")
            return 1
        print("content/consent/consent.json is up to date")
        return 0
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
