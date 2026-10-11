"""Audits the clinical evidence register: ids, blocks, provenance and statuses, and writes docs/research/register_id_allocation.md.

    uv run --project tools tools/content/register_audit.py            # write the allocation document
    uv run --project tools tools/content/register_audit.py --check    # fail on any problem or if the document is out of date

What it checks (12 Oct 2026, after hand-written rows were lost twice by being put inside a generated block):
  1. every id is unique, the file is sorted by id and the header is the expected one;
  2. no hand-written row sits inside the id block of a generated table (the table builder would delete it);
  3. every hand-written row lies in a range that is documented below with its purpose and owner;
  4. status is VERIFIED, UNVERIFIED or AWAITING_CLINICIAN; evidence type is A, B, C or D;
  5. a verified A row names an https source and either 'opened' or 'Checked' in its notes, and every claim id (V-nn) in the notes exists in clinical_verification.md;
  6. an UNVERIFIED row says it has not been opened; an AWAITING_CLINICIAN row says so and is listed in docs/research/clinician_signoff.md;
  7. every row has a source name and an approval or attestation text.
"""
from __future__ import annotations

import csv
import re
import sys
from collections import Counter
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE / "tables"))
sys.path.insert(0, str(HERE))
import build_tables  # noqa: E402

ROOT = HERE.parents[1]
REGISTER = ROOT / "docs" / "research" / "source_register.csv"
CLAIMS = ROOT / "docs" / "research" / "clinical_verification.md"
SIGNOFF = ROOT / "docs" / "research" / "clinician_signoff.md"
OUT = ROOT / "docs" / "research" / "register_id_allocation.md"
HEADER = ["id", "item", "value", "unit", "walkthrough_ref", "source_name", "source_location", "source_url", "evidence_type", "status", "date_checked", "checked_by", "clinician_approval", "notes"]

# the ranges outside every generated block that hold hand-written rows, and why
HAND_RANGES = [
    (1, 99, "Early hand-written rows: the first skeleton steps, then the clinical-logic and workflow design rules (SR-0085 to SR-0099: pregnancy gate, sepsis, clashes, decision, deterioration, escalation); type D design rules with the owner's approval text"),
    (2830, 2849, "Workflow design rules for the ward checklist, theatre readiness, handover, safety checklist, end states, LitePhysio and behaviour (type D)"),
    (2850, 2869, "Consent and the 12 Oct 2026 evidence review: verified figures (type A), institutional figures (type C) and the HbA1c, blood pressure and NG253 rules (type A)"),
    (2870, 2879, "AI safety: the grounding validator's word lists and the intents each AI-voiced character may use (type D and B)"),
    (4100, 4109, "Treatment library and medicine workflow rows written by Abdul Moiz's builder (tools/content/build_treatments.py)"),
]
STATUSES = {"VERIFIED", "UNVERIFIED", "AWAITING_CLINICIAN"}


def blocks() -> list[tuple[int, int, str]]:
    tables, _, _ = build_tables.load()
    return sorted((t.block, t.block + t.size - 1, t.name) for t in tables)


def problems() -> list[str]:
    out: list[str] = []
    with open(REGISTER, encoding="utf-8", newline="") as f:
        reader = csv.reader(f)
        header = next(reader)
        rows = [dict(zip(header, r)) for r in reader]
    if header != HEADER:
        out.append(f"register header changed: {header}")
    ids = [r["id"] for r in rows]
    dupes = sorted({i for i in ids if ids.count(i) > 1})
    out += [f"{i}: duplicate id" for i in dupes]
    if ids != sorted(ids):
        out.append("the register is not sorted by id")
    claims = set(re.findall(r"\| (V-\d+) \|", CLAIMS.read_text(encoding="utf-8")))
    signoff = SIGNOFF.read_text(encoding="utf-8") if SIGNOFF.exists() else ""
    bl = blocks()
    for r in rows:
        i = r["id"]
        n = int(i[3:])
        in_block = any(lo <= n <= hi for lo, hi, _ in bl)
        if not in_block:
            if not any(lo <= n <= hi for lo, hi, _ in HAND_RANGES):
                out.append(f"{i}: a hand-written row outside every documented range (add the range to HAND_RANGES in tools/content/register_audit.py)")
        elif r["checked_by"] == "Project team" and "Rule definition" in r["notes"] and r["evidence_type"] == "D" and n not in range(0, 100):
            pass
        if r["status"] not in STATUSES:
            out.append(f"{i}: unknown status '{r['status']}'")
        if r["evidence_type"] not in ("A", "B", "C", "D"):
            out.append(f"{i}: unknown evidence type '{r['evidence_type']}'")
        if not r["source_name"].strip() or not r["clinician_approval"].strip():
            out.append(f"{i}: missing source name or approval text")
        for v in re.findall(r"V-\d+", r["notes"]):
            if v not in claims:
                out.append(f"{i}: cites {v}, which clinical_verification.md does not have")
        if not in_block and r["evidence_type"] == "A" and r["status"] == "VERIFIED":
            if not r["source_url"].startswith("https://"):
                out.append(f"{i}: a verified A row needs an https source")
            if "opened" not in r["notes"] and "Checked" not in r["notes"]:
                out.append(f"{i}: a verified A row must say what was opened or checked")
            if not re.search(r"V-\d+", r["notes"]):
                out.append(f"{i}: a verified A row must cite its claim id (V-nn)")
        if r["status"] == "UNVERIFIED" and "not been opened" not in r["notes"] and "NOT confirmed" not in r["notes"]:
            out.append(f"{i}: an UNVERIFIED row must say why")
        if r["status"] == "AWAITING_CLINICIAN":
            if "clinician" not in r["notes"].lower():
                out.append(f"{i}: an AWAITING_CLINICIAN row must say what the clinician is asked")
            if i not in signoff:
                out.append(f"{i}: an AWAITING_CLINICIAN row must be listed in docs/research/clinician_signoff.md")
    return out


def render() -> str:
    with open(REGISTER, encoding="utf-8", newline="") as f:
        rows = list(csv.DictReader(f))
    bl = blocks()
    lines = ["# Source register: id allocation and provenance audit", "",
             "Generated by `tools/content/register_audit.py` (do not edit by hand; `--check` fails if it is stale). It records where each kind of register row lives so a hand-written row is never put inside a generated block again (the table builder rewrites those blocks and would delete it).", "",
             "## Totals", "",
             f"- {len(rows)} rows. Evidence types: " + ", ".join(f"{k} {v}" for k, v in sorted(Counter(r['evidence_type'] for r in rows).items())) + ".",
             "- Statuses: " + ", ".join(f"{k} {v}" for k, v in sorted(Counter(r['status'] for r in rows).items())) + ".", "",
             "## Generated blocks (written by `tools/content/build_tables.py`; never edit these ids by hand)", "", "| Ids | Table | Rows used |", "|---|---|---|"]
    for lo, hi, name in bl:
        used = sum(1 for r in rows if lo <= int(r["id"][3:]) <= hi)
        lines.append(f"| SR-{lo:04d} to SR-{hi:04d} | {name} | {used} |")
    lines += ["", "## Hand-written ranges (the only places a hand-written row may go)", "", "| Ids | Rows used | Purpose |", "|---|---|---|"]
    for lo, hi, why in HAND_RANGES:
        used = sum(1 for r in rows if lo <= int(r["id"][3:]) <= hi and not any(a <= int(r["id"][3:]) <= b for a, b, _ in bl))
        lines.append(f"| SR-{lo:04d} to SR-{hi:04d} | {used} | {why} |")
    lines += ["", "## How to add a row", "",
              "1. Put it in a hand-written range above (the next free id), then keep the file sorted: `uv run --project tools tools/content/build_tables.py` re-sorts it.",
              "2. A source-backed number is type A: it needs an https source, a claim `V-nn` in `docs/research/clinical_verification.md` and notes that say what was opened or checked (primary or secondary).",
              "3. A design value is type D: say it is provisional. Institutional figures are type C.",
              "4. If a clinician has to confirm it, give it the status `AWAITING_CLINICIAN` and list the precise question in `docs/research/clinician_signoff.md`; never mark clinical approval that has not been given.", ""]
    return "\n".join(lines)


def main(argv: list[str]) -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    found = problems()
    text = render()
    if "--check" in argv:
        if not OUT.exists() or OUT.read_text(encoding="utf-8").replace("\r\n", "\n") != text:
            found.append("docs/research/register_id_allocation.md is out of date: run tools/content/register_audit.py")
        if found:
            print(f"FAIL  {len(found)} register problem(s)")
            for p in found[:60]:
                print("  - " + p)
            return 1
        print("register ids, blocks and provenance are consistent")
        return 0
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT)}" + (f" with {len(found)} problem(s) still open" if found else ""))
    for p in found[:60]:
        print("  - " + p)
    return 1 if found else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
