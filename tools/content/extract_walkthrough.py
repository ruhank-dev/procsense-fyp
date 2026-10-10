"""Parses the walkthrough markdown into content/_extract/walkthrough_extract.json and checks the counts (plan test T-WT2).

    uv run --project tools tools/content/extract_walkthrough.py            # write the extract and check the counts
    uv run --project tools tools/content/extract_walkthrough.py --check    # check the counts only (what CI runs)

The extract is a DRAFTING AID: it saves retyping, nothing more. The walkthrough tables were flattened in the source
(Conversion Report section 5.1), so every number copied from it must still be re-read against the page and given a
source-register row. The generated file is git-ignored; run this to recreate it.
Exit code 0 = all counts as expected, 1 = something is missing or extra.
"""
from __future__ import annotations

import hashlib
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
WALKTHROUGH = ROOT / "docs" / "context" / "ProcSense_Medical_Walkthrough_v5_1.md"
OUT = ROOT / "content" / "_extract" / "walkthrough_extract.json"

# Counts the walkthrough actually contains. One differs from the Change Report and is logged for the owner in docs/research/issues.md:
#   ISS-0003: the report says "15 steps x 21 questions" (315); the book answers 270, because each step skips the questions that do not apply.
# (The report's 47 what-ifs is right: an earlier version of this extractor counted 48 because it also read a Part 9 heading from the table of contents region.)
# If a new walkthrough version changes a number, this check fails on purpose: update it deliberately and say why.
EXPECTED = {"steps": 15, "questions_per_step": 21, "answers_total": 270, "what_ifs": 47, "vr_cards": 24, "stacked_full": 8, "stacked_compact": 20, "flow_stages_per_scenario": 20}
# The parts the book has, in order. Part A is the container for Parts 1-18; Part D appears twice (the full stacked cases, then the master table);
# the book ends with Part 28 (the list of sources).
EXPECTED_PART_IDS = ["A", *(str(n) for n in range(1, 15)), "14B", "15", "16", "17", "18", "B", "C", "D", "D", "E", "F", "G", "H", "I", "28"]

QUESTIONS = [
    "What are we doing?",
    "Why are we doing it?",
    "What does the trainee actually do?",
    "What does the trainee see?",
    "What information do they receive?",
    "What does that information mean?",
    "What is the normal/reference value?",
    "What counts as abnormal?",
    "What counts as seriously concerning?",
    "What could cause the abnormal result?",
    "What should the trainee check next?",
    "What happens if the trainee finds nothing wrong?",
    "What happens if the trainee finds something abnormal?",
    "What happens if the abnormality is mild?",
    "What happens if it is severe?",
    "When do we call the senior doctor?",
    "When do we call the anaesthetist?",
    "When might surgery be delayed?",
    "When should emergency surgery continue despite the abnormality?",
    "What happens if the trainee misses it?",
    "What happens after the correct action?",
]
EXTRA_LABELS = ["Medical words used here", "Where this comes from", "Clinician validation"]

# The ten fields of a flow stage, in order. The long ones wrap onto two lines in the source.
FLOW_FIELDS = [
    ("medical_detail", "Medical detail"),
    ("trainee_action", "Trainee action"),
    ("sees", "What the trainee sees /\ninformation"),
    ("normal_pathway", "Normal pathway"),
    ("abnormal_branches", "Abnormal branches"),
    ("escalation", "Escalation / NPC"),
    ("equipment_response", "Equipment and monitor\nresponse"),
    ("if_missed", "If missed"),
    ("source", "Source"),
    ("validation", "Clinician validation"),
]

BODY_STARTS_AT = 450   # the book proper starts after the table of contents (Part A begins at line 460)
FOOTER = re.compile(r"^(\*ProcSense Medical Walkthrough v5\.1|\*Sources cited at point of use|---\s*$)")


def lines_of(text: str) -> list[str]:
    return text.splitlines()


def clean(block: list[str]) -> str:
    """Joins lines, dropping the page footers and blank edges."""
    kept = [ln.rstrip() for ln in block if not FOOTER.match(ln.strip())]
    return "\n".join(kept).strip()


def find(lines: list[str], pattern: str, start: int = BODY_STARTS_AT) -> int:
    """First line matching the pattern at or after `start`. The table of contents at the top of the book repeats the part titles, so the default skips it."""
    rx = re.compile(pattern)
    for i in range(start, len(lines)):
        if rx.match(lines[i]):
            return i
    raise ValueError(f"heading not found: {pattern}")


def sections(lines: list[str], lo: int, hi: int, heading: str) -> list[tuple[int, str, list[str]]]:
    """Every heading in lines[lo:hi] matching the pattern, as (line number, heading text, body lines)."""
    rx = re.compile(heading)
    marks = [i for i in range(lo, hi) if rx.match(lines[i])]
    out = []
    for n, i in enumerate(marks):
        end = marks[n + 1] if n + 1 < len(marks) else hi
        out.append((i + 1, lines[i], lines[i + 1:end]))
    return out


def parse_steps(lines: list[str]) -> list[dict]:
    lo = find(lines, r"^# Part 11 — The steps, one by one")
    hi = find(lines, r"^# Part 12 — ", lo)
    steps = []
    for line_no, heading, body in sections(lines, lo, hi, r"^## STEP \d+"):
        number = int(re.match(r"^## STEP (\d+)", heading).group(1))
        title = re.sub(r"^## STEP \d+( \(optional\))? — ", "", heading).strip()
        labels = QUESTIONS + EXTRA_LABELS
        marks: list[tuple[int, str]] = []
        cursor = 0
        for label in labels:   # the labels appear in this order inside each step
            for i in range(cursor, len(body)):
                text = body[i].strip()
                # "Where this comes from: WHO SSC 2009." and "Clinician validation: ..." carry their text on the same line
                if text == label or (label in EXTRA_LABELS and text.startswith(label + ":")):
                    marks.append((i, label))
                    cursor = i + 1
                    break
        found = {label for _, label in marks}
        answers = {}
        for n, (i, label) in enumerate(marks):
            end = marks[n + 1][0] if n + 1 < len(marks) else len(body)
            inline = body[i].strip()[len(label) + 1:].strip() if body[i].strip().startswith(label + ":") else ""
            answers[label] = clean(([inline] if inline else []) + body[i + 1:end])
        steps.append({
            "number": number, "title": title, "line": line_no,
            "questions": {q: answers[q] for q in QUESTIONS if q in answers},
            "missing_questions": [q for q in QUESTIONS if q not in found],
            "extras": {x: answers[x] for x in EXTRA_LABELS if x in answers},
        })
    return steps


def parse_what_ifs(lines: list[str]) -> list[dict]:
    lo = find(lines, r"^# Part 14 — WHAT IF")
    hi = find(lines, r"^# Part 14B — ", lo)
    out = []
    for n, h, b in sections(lines, lo, hi, r"^### What if"):
        cut = next((i for i, ln in enumerate(b) if re.match(r"^#{1,2} ", ln)), len(b))   # a what-if ends where the next section heading starts
        out.append({"title": h[4:].strip(), "line": n, "body": clean(b[:cut])})
    return out


def parse_vr_cards(lines: list[str]) -> list[dict]:
    lo = find(lines, r"^# PART C — ")
    hi = find(lines, r"^# PART D — ", lo)
    return [{"id": re.match(r"^### (C\.\d+)", h).group(1), "title": re.sub(r"^### C\.\d+ ", "", h).strip(), "line": n, "body": clean(b)}
            for n, h, b in sections(lines, lo, hi, r"^### C\.\d+ ")]


def parse_stacked(lines: list[str]) -> tuple[list[dict], list[dict]]:
    lo = find(lines, r"^# PART D — Expanded Stacked Cases")
    hi = find(lines, r"^# PART D — Stacked-case master table", lo)
    full = [{"id": re.match(r"^## STACKED CASE (S[12]-[A-D])", h).group(1), "title": h[3:].strip(), "line": n, "body": clean(b)}
            for n, h, b in sections(lines, lo, hi, r"^## STACKED CASE ")]
    lo2 = find(lines, r"^## 14B\.4 Worked stacked cases")
    hi2 = find(lines, r"^## 14B\.5 ", lo2)
    compact = [{"id": re.match(r"^### (SC-\d+)", h).group(1), "scenario": re.match(r"^### SC-\d+ \((S[12])\)", h).group(1), "title": h[4:].strip(), "line": n, "body": clean(b)}
               for n, h, b in sections(lines, lo2, hi2, r"^### SC-\d+ ")]
    return full, compact


def parse_flow(lines: list[str], part_pattern: str, next_pattern: str) -> list[dict]:
    """One scenario map: stages 'N\\. title' each followed by the ten labelled fields."""
    lo = find(lines, part_pattern)
    hi = find(lines, next_pattern, lo + 1)
    region = [ln for ln in lines[lo:hi] if not FOOTER.match(ln.strip())]
    starts = [i for i, ln in enumerate(region) if re.match(r"^\d+\\\. \S", ln)]
    stages = []
    for n, i in enumerate(starts):
        end = starts[n + 1] if n + 1 < len(starts) else len(region)
        block = region[i:end]
        number = int(re.match(r"^(\d+)\\\.", block[0]).group(1))
        title = re.sub(r"^\d+\\\. ", "", block[0]).strip()
        text = "\n".join(x.rstrip() for x in block[1:])
        fields: dict[str, str] = {}
        positions = []
        cursor = 0
        for key, label in FLOW_FIELDS:
            at = text.find(label, cursor)
            if at < 0:
                continue
            positions.append((at, key, label))
            cursor = at + len(label)
        for k, (at, key, label) in enumerate(positions):
            stop = positions[k + 1][0] if k + 1 < len(positions) else len(text)
            fields[key] = re.sub(r"\s*\n\s*", " ", text[at + len(label):stop]).strip()
        stages.append({"number": number, "title": title, "fields": fields, "missing_fields": [k for k, _ in FLOW_FIELDS if k not in fields]})
    return stages


def parts(lines: list[str]) -> list[dict]:
    """Every top-level part of the book, in order: 'Part 7', 'PART C'..., with its line number."""
    out = []
    for i, ln in enumerate(lines):
        m = re.match(r"^# (Part (\d+B?)|PART ([A-I])) — ", ln)
        if m and i > 450:   # skip the table of contents at the top
            out.append({"id": m.group(2) or m.group(3), "title": ln[2:].strip(), "line": i + 1})
    return out


def extract() -> dict:
    text = WALKTHROUGH.read_text(encoding="utf-8")
    lines = lines_of(text)
    stacked_full, stacked_compact = parse_stacked(lines)
    return {
        "source": {"file": "docs/context/ProcSense_Medical_Walkthrough_v5_1.md", "sha256": hashlib.sha256(text.encode("utf-8")).hexdigest(), "lines": len(lines)},
        "parts": parts(lines),
        "steps": parse_steps(lines),
        "what_ifs": parse_what_ifs(lines),
        "vr_cards": parse_vr_cards(lines),
        "stacked_full": stacked_full,
        "stacked_compact": stacked_compact,
        "flow": {
            "S1": parse_flow(lines, r"^# PART F — ", r"^# PART G — "),
            "S2": parse_flow(lines, r"^# PART G — ", r"^# PART H — "),
        },
    }


def problems(data: dict) -> list[str]:
    out = []
    e = EXPECTED
    if len(data["steps"]) != e["steps"]:
        out.append(f"steps: expected {e['steps']}, found {len(data['steps'])}")
    answered = sum(len(s["questions"]) for s in data["steps"])
    if answered != e["answers_total"]:
        out.append(f"step answers: expected {e['answers_total']} of {e['steps'] * e['questions_per_step']}, found {answered} (see ISS-0003)")
    for s in data["steps"]:
        if len(s["questions"]) == 0:
            out.append(f"step {s['number']}: no questions found")
    if [s["number"] for s in data["steps"]] != list(range(1, len(data["steps"]) + 1)):
        out.append("steps are not numbered 1..N in order")
    for key, label in (("what_ifs", "what-ifs"), ("vr_cards", "VR cards"), ("stacked_full", "full stacked cases"), ("stacked_compact", "compact stacked cases")):
        want = e[key]
        if len(data[key]) != want:
            out.append(f"{label}: expected {want}, found {len(data[key])}")
    for scenario in ("S1", "S2"):
        stages = data["flow"][scenario]
        if len(stages) != e["flow_stages_per_scenario"]:
            out.append(f"flow {scenario}: expected {e['flow_stages_per_scenario']} stages, found {len(stages)}")
        if [s["number"] for s in stages] != list(range(1, len(stages) + 1)):
            out.append(f"flow {scenario}: stages are not numbered 1..N in order")
        for s in stages:
            if s["missing_fields"]:
                out.append(f"flow {scenario} stage {s['number']}: missing fields {s['missing_fields']}")
    ids = [p["id"] for p in data["parts"]]
    if ids != EXPECTED_PART_IDS:
        out.append(f"parts: expected {EXPECTED_PART_IDS}, found {ids}")
    return out


def summary(data: dict) -> str:
    q = sum(len(s["questions"]) for s in data["steps"])
    return (f"steps {len(data['steps'])} ({q} of {len(data['steps']) * len(QUESTIONS)} question slots answered), what-ifs {len(data['what_ifs'])}, VR cards {len(data['vr_cards'])}, "
            f"stacked {len(data['stacked_full'])} full + {len(data['stacked_compact'])} compact, "
            f"flow stages S1 {len(data['flow']['S1'])} / S2 {len(data['flow']['S2'])}, parts {len(data['parts'])}")


def main(argv: list[str]) -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    data = extract()
    print(summary(data))
    found = problems(data)
    for p in found:
        print("  - " + p)
    if "--check" not in argv:
        OUT.parent.mkdir(parents=True, exist_ok=True)
        OUT.write_text(json.dumps(data, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
        print(f"wrote {OUT.relative_to(ROOT)}")
    print("counts OK" if not found else f"{len(found)} problem(s)")
    return 1 if found else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
