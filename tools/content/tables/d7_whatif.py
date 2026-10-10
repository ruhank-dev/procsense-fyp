"""D7 (p0-medical-research-data s13): the what-if library (Part 14).

Part 14 has 47 'What if...' sections, as the change report says. Each is read into its eleven fields. Trigger type and the
P1-requirement flag are design readings (derived), not book text. Case bindings (which case triggers which what-if) come from the case content and are
not built here: see p0-medical-research-data s10 to s12 and p0-case-library s03.
"""
import re
import sys
from pathlib import Path

from framework import FRAGMENT_SEPARATOR, Table

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import book  # noqa: E402
import extract_walkthrough as ex  # noqa: E402

SEP = FRAGMENT_SEPARATOR
LABELS = [("trigger", "Trigger / what the trainee sees"), ("meaning", "What it might mean"), ("check_next", "What to check next"), ("correct", "Correct action"),
          ("incorrect", "Incorrect action"), ("missed", "Missed abnormality → consequence"), ("escalation", "Escalation (who to call)"),
          ("expected_response", "Expected patient / monitor response"), ("next", "What happens next (S1 / S2 consequence)"), ("source", "Source"),
          ("validation", "Clinician validation")]
KEYS = [k for k, _ in LABELS]

# Which section of Part 14 each what-if sits in, and a design reading of how it is triggered.
SECTION_TRIGGER = {
    "14.1": "value_threshold", "14.2": "value_threshold", "14.3": "value_threshold", "14.6": "system", "14.7": "value_threshold", "14.9": "system",
}
TRIGGER_BY_TITLE = {  # exceptions to the section default
    "What if the patient is pregnant?": "value_threshold", "What if pregnancy status is unknown or the patient declines a test?": "trainee_action",
    "What if the patient is allergic to an antibiotic?": "trainee_action", "What if an allergic reaction happens after a medicine?": "value_threshold",
    "What if the patient takes blood thinners?": "omission", "What if the patient has diabetes?": "omission",
    "What if the patient takes steroids, GLP-1 injections or SGLT2 tablets?": "omission", "What if the patient hasn't fasted?": "omission",
    "What if fasting status is unknown?": "omission", "What if imaging is inconclusive?": "value_threshold", "What if a test result doesn't make sense?": "value_threshold",
    "What if a laboratory result is delayed?": "omission", "What if a critical result is phoned through?": "system",
    "What if the patient refuses surgery?": "trainee_action", "What if the patient cannot understand the consent discussion?": "trainee_action",
    "What if the patient is under 18?": "trainee_action", "What if the patient deteriorates during transfer?": "value_threshold",
    "What if there's an identity mismatch at theatre?": "system", "What if documents are missing?": "system", "What if equipment fails?": "system",
}

wi = Table("whatif_library", 2200, ["section", "title", "v_trigger", "v_meaning", "v_check_next", "v_correct", "v_incorrect", "v_missed", "v_escalation",
                                    "v_expected_response", "v_next", "v_source", "v_validation", "d_trigger_type", "d_requires"],
           "What-if", "Part 14 'WHAT IF something goes wrong?'", evidence="B", size=60)

lines = book.WALKTHROUGH.read_text(encoding="utf-8").splitlines()
section_of = {}
current = ""
for i, ln in enumerate(lines):
    m = re.match(r"^## (14\.\d)", ln)
    if m:
        current = m.group(1)
    if ln.startswith("### What if") and i > ex.BODY_STARTS_AT:
        section_of[i + 1] = current

for n, w in enumerate(ex.extract()["what_ifs"], start=1):
    text = book.normalise(w["body"])
    pos, cursor = [], 0
    for key, label in LABELS:
        at = text.find(label, cursor)
        if at >= 0:
            pos.append((at, key, label))
            cursor = at + len(label)
    found = {k for _, k, _ in pos}
    vals = {key: text[at + len(label):(pos[j + 1][0] if j + 1 < len(pos) else len(text))].strip() for j, (at, key, label) in enumerate(pos)}
    missing = [k for k in KEYS if k not in found]
    trigger_type = TRIGGER_BY_TITLE.get(w["title"]) or SECTION_TRIGGER.get(section_of.get(w["line"], ""), "")
    if not trigger_type:
        raise SystemExit(f"whatif {w['title']}: no trigger type for section {section_of.get(w['line'])}")
    frags = [w["title"]] + [f"{label} {vals[key]}" for _, key, label in pos]
    wi.add(f"w{n:02d}", SEP.join(frags), "span", section=section_of.get(w["line"], ""), title=w["title"],
           **{f"v_{k}": vals.get(k, "") for k in KEYS}, d_trigger_type=trigger_type, d_requires="",
           note=("Missing fields in the book: " + ", ".join(missing) + ". " if missing else "") +
                "Trigger type is a design reading (value_threshold / trainee_action / omission / system); d_requires is empty: no P1-only branch has been identified yet.")

TABLES = [wi]
OFFICIAL_FILES = {}
