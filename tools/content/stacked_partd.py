"""The stacked cases against the walkthrough's Part D (p1-case-library-extended-s02: "ladder ranks match Part D, a T-DE1-style test per case").

    uv run --project tools tools/content/stacked_partd.py            # write docs/research/stacked_partd.md
    uv run --project tools tools/content/stacked_partd.py --check    # fail if a case no longer follows Part D, or the report is out of date (CI)

For each of the eight stacked cases (the two P0 ones and the six P1 ones) it reads the case's sections straight from the walkthrough and checks:
- section 8 (the priority table): every row's words are in the table, the rows map to the case's problems in the table's order (a row may map to two problems, `split`, or to a problem
  that also covers the next row, `merged`), and every problem of the case is mapped;
- section 9 (the decision process): the case has one decision per decision of the book, in the same order, and every number of a decision's "What do I notice?" is in the case's prompt;
- section 12 (the results): the results the table gives, in the book's words, equal the case's values (the six P1 cases).
The engine's own ordering of the same problems is tested in tests/ProcSense.Core.Tests/ProblemRegistryTests.cs (T-DE1 and every stacked case).
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(HERE))
import book  # noqa: E402

CASES = ROOT / "content" / "case_files"
REPORT = ROOT / "docs" / "research" / "stacked_partd.md"
NUM = re.compile(r"(?<![A-Za-z0-9.])\d+(?:\.\d+)?(?![A-Za-z0-9])")   # a number, not the digits of a word such as SpO2 or TG18

# section 8: (book row, words of the row in the table, the case's problems it maps to, how)
RANKS = {
    "STK-S1-A": [(1, "Hypoglycaemia 3.6 mmol/L", ["stka.hypo"], "same"), (2, "SGLT2 taken today; ketones 0.9", ["stka.sglt2"], "same"), (3, "HbA1c 76 mmol/mol", ["stka.hba1c"], "same"),
                 (4, "BP 172/100", ["stka.bp"], "same")],
    "STK-S1-B": [(1, "Apixaban active", ["stkb.dose"], "same"), (2, "Unexplained iron-deficiency anaemia", ["stkb.anaemia"], "same"), (3, "AF (known, controlled)", ["stkb.af"], "same")],
    "STK-S1-C": [(1, "K 6.6 with ECG changes", ["stkc.k"], "same"), (2, "AKI with dehydration", ["stkc.aki"], "same"), (3, "Bradycardia / low BP", ["stkc.brady"], "same"), (4, "Elective surgery", ["stkc.op"], "same")],
    "STK-S1-D": [(1, "High-risk sepsis (NEWS2 7, lactate 2.6)", ["s1d.sepsis"], "same"), (2, "Obstructed infected bile duct", ["s1d.duct"], "same"), (3, "Early kidney strain", ["s1d.aki"], "same"),
                 (4, "Cholecystectomy", ["s1d.chole"], "same")],
    "STK-S2-A": [(1, "Hypovolaemia", ["stka2.fluids"], "same"), (2, "AKI + hypokalaemia", ["stka2.aki", "stka2.k"], "split"), (3, "Appendicitis needing surgery", ["stka2.appx"], "same")],
    "STK-S2-B": [(1, "Sepsis with hypoperfusion", ["s2b.sepsis"], "same"), (2, "Allergy-safe antibiotic", ["s2b.allergy"], "same"), (3, "Urgent surgery", ["s2b.source"], "same")],
    "STK-S2-C": [(1, "Vomiting with reduced consciousness", ["stkc2.airway"], "same"), (2, "RR 9, SpO2 90%", ["stkc2.breath"], "same"), (3, "Opioid sedation", ["stkc2.opioid"], "same"),
                 (4, "Full stomach for anaesthesia", ["stkc2.aspiration"], "same")],
    "STK-S2-D": [(1, "Maternal stability (ABC)", ["stkd.maternal"], "same"), (2, "Timely diagnosis and surgery", ["stkd.preg"], "same"), (3, "Obstetric/fetal assessment", ["stkd.obstetric"], "same"),
                 (4, "Anaesthetic adjustments", ["stkd.anaesthetic"], "same")],
}

# section 12: (words of the result in the table, the case's test, its expected value)
RESULTS = {
    "STK-S1-A": [("Glucose (repeat) 5.8", "Capillary glucose (repeat after treatment)", 5.8), ("Ketones 0.9", "Capillary ketones", 0.9), ("HbA1c 76", "HbA1c", 76),
                 ("Potassium 4.6", "Potassium", 4.6), ("Creatinine 88", "Creatinine", 88), ("Sinus 92, LVH voltage", "ECG", "LVH")],
    "STK-S1-B": [("ECG AF 84", "ECG", "Atrial fibrillation, rate 84"), ("Hb 104", "Hb", 104), ("MCV 74", "MCV", 74), ("Ferritin 9", "Ferritin", 9), ("Creatinine 118", "Creatinine", 118)],
    "STK-S1-C": [("Potassium 6.6", "Potassium", 6.6), ("Peaked T, HR 52", "ECG", "peaked T"), ("Creatinine 178", "Creatinine", 178), ("Repeat K (1 h) 5.7", "Potassium (repeat after 1 hour)", 5.7)],
    "STK-S2-A": [("15.1 / 86%", "WBC", 15.1), ("15.1 / 86%", "Neutrophils", 86), ("CRP (>24 h) 96", "CRP", 96), ("Creatinine 158 (baseline 82)", "Creatinine", 158),
                 ("Potassium 3.0 → 3.5", "Potassium", 3.0), ("Potassium 3.0 → 3.5", "Potassium (repeat after replacement)", 3.5), ("Lactate 1.8", "Lactate", 1.8),
                 ("ssible 9 mm appendix", "Ultrasound appendix", "9 mm")],
    "STK-S2-C": [("Glucose 5.6", "Capillary glucose", 5.6), ("Chest Clear", "Chest X-ray", "Clear")],
    "STK-S2-D": [("WBC 15.8", "WBC", 15.8), ("CRP 64", "CRP", 64), ("MRI Inflamed appendix", "MRI appendix", "Inflamed appendix")],
}
# what the case files do not carry, on purpose, with the reason (listed in the report)
NOT_CARRIED = {"STK-S2-C": ["Venous CO2 7.4 kPa (section 12): the engine has no blood-gas test yet (p1-case-library-extended-s03, DT-186)"]}


def sections(case_id: str) -> dict[int, str]:
    """The case's numbered sections of Part D, each as normalised text (page footers dropped)."""
    lines = book.WALKTHROUGH.read_text(encoding="utf-8").splitlines()
    starts = [i for i, l in enumerate(lines) if l.startswith("## STACKED CASE ")]
    for n, i in enumerate(starts):
        if "STK-" + lines[i].split("STACKED CASE ")[1].split(" ")[0] != case_id:
            continue
        end = starts[n + 1] if n + 1 < len(starts) else len(lines)
        out, cur = {0: []}, 0
        for x in lines[i:end]:
            if x.startswith("*") or x.strip() == "---":
                continue
            if x.startswith("# "):            # the Part D master table after the last case is not part of it
                break
            m = re.match(r"^(\d{1,2})\\\. ", x)
            if m and int(m.group(1)) == cur + 1:
                cur = int(m.group(1))
                out[cur] = []
            out[cur].append(x)
        return {k: book.normalise(" ".join(v)) for k, v in out.items()}
    raise KeyError(case_id)


def book_decisions(text9: str) -> list[str]:
    """The 'What do I notice?' of each decision of section 9."""
    return [m.group(1).strip() for m in re.finditer(r"Decision \d+ What do I notice\? (.*?) What does it mean\?", text9)]


def _value(case: dict, test: str):
    return next((r["value"] for r in case["results"] if r["test"] == test), None)


def _same(value, expected) -> bool:
    if value is None:
        return False
    if isinstance(expected, (int, float)):
        try:
            return float(str(value).split()[0]) == float(expected)
        except ValueError:
            return False
    return str(expected).lower() in str(value).lower()


def check(case_id: str) -> tuple[dict, list[str]]:
    case = json.loads((CASES / f"{case_id}.json").read_text(encoding="utf-8"))
    sec = sections(case_id)
    rank = {p["id"]: p["rank"] for p in case["problems"]}
    out, last = [], 0
    for row, words, ids, how in RANKS[case_id]:
        if words.lower() not in sec[8].lower():
            out.append(f"{case_id}: section 8 row {row} '{words}' is not in the book's priority table")
        for pid in ids:
            if pid not in rank:
                out.append(f"{case_id}: section 8 row {row} maps to {pid}, which the case does not have")
            elif rank[pid] <= last:
                out.append(f"{case_id}: {pid} (rank {rank[pid]}) is out of the book's order (section 8 row {row})")
            else:
                last = rank[pid]
    mapped = [pid for _, _, ids, _ in RANKS[case_id] for pid in ids]
    out += [f"{case_id}: problem {pid} is not in the book's priority table" for pid in rank if pid not in mapped]
    notices = book_decisions(sec[9])
    if len(notices) != len(case["decisions"]):
        out.append(f"{case_id}: the book has {len(notices)} decisions in section 9, the case has {len(case['decisions'])}")
    for i, (notice, d) in enumerate(zip(notices, case["decisions"]), 1):
        missing = [n for n in NUM.findall(notice) if not re.search(r"(?<![\d.])" + re.escape(n) + r"(?![\d])", d["prompt"])]
        if missing:
            out.append(f"{case_id}: decision {i} ({d['id']}): the book notices '{notice}' but the prompt lacks {missing}: {d['prompt']!r}")
    for words, test, expected in RESULTS.get(case_id, []):
        if words.lower() not in sec[12].lower():
            out.append(f"{case_id}: section 12 '{words}' is not in the book's results table")
        if not _same(_value(case, test), expected):
            out.append(f"{case_id}: {test}: the book's results table says '{words}' but the case has {_value(case, test)!r}")
    summary = dict(case=case_id, tier=case["tier"], rows=len(RANKS[case_id]), problems=len(rank), how=[how for *_, how in RANKS[case_id] if how != "same"],
                   book_decisions=len(notices), decisions=len(case["decisions"]), results=len(RESULTS.get(case_id, [])), not_carried=NOT_CARRIED.get(case_id, []))
    return summary, out


def report(rows: list[dict]) -> str:
    L = ["# The stacked cases against Part D (p1-case-library-extended-s02)", "",
         "Generated by `tools/content/stacked_partd.py`; do not edit. Each case's sections are read from the walkthrough's Part D and compared with its case file",
         "(`content/case_files/<id>.json`): the priority table (section 8) against the problems and their ranks, the decision process (section 9) against the decisions, and the results table",
         "(section 12) against the results. The engine's ordering of the same problems is tested in `ProblemRegistryTests` (T-DE1).", "",
         "| Case | Tier | Priority rows (section 8) | Problems | Decisions (book / case) | Results checked | Notes |", "|---|---|---|---|---|---|---|"]
    for r in rows:
        notes = "; ".join([f"a row {h} into two problems" if h == "split" else f"a row {h}" for h in r["how"]] + r["not_carried"]) or "-"
        L.append(f"| {r['case']} | {r['tier']} | {r['rows']} | {r['problems']} | {r['book_decisions']} / {r['decisions']} | {r['results']} | {notes} |")
    L += ["", "**STK-S2-D:** Part D is not consistent with itself: the priority table (section 8) puts maternal stability first, the debrief (section 20) names timely diagnosis as the",
          "highest-priority problem. The problems follow section 8 and the debrief keeps section 20's words; the clinician is asked to settle it (DT-185).", ""]
    return "\n".join(L)


def main(argv: list[str]) -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    rows, problems = [], []
    for case_id in RANKS:
        summary, found = check(case_id)
        rows.append(summary)
        problems += found
    text = report(rows)
    if "--check" in argv:
        if not REPORT.exists() or REPORT.read_text(encoding="utf-8").replace("\r\n", "\n") != text:
            problems.append("docs/research/stacked_partd.md is out of date: run tools/content/stacked_partd.py")
    elif not problems:
        REPORT.write_text(text, encoding="utf-8", newline="\n")
    for p in problems:
        print("  - " + p)
    print(f"stacked cases against Part D: {len(rows)} cases" + (f"; {len(problems)} problems" if problems else ", every priority row, decision and result as the book"))
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
