"""The book's facts for the ten P1 variant cases, each tied to the field of the case file that holds it (p1-case-library-extended-s01, "verbatim 100% per case").

    uv run --project tools tools/content/variant_facts.py            # write docs/research/p1_variant_verbatim.md
    uv run --project tools tools/content/variant_facts.py --check    # fail if a fact is not in the case file, or the report is out of date (CI)

Every fact has the book's own words (`phrase`). The phrase must be in the case's story in content/case_seeds.csv, which is itself checked word for word against
its page of the walkthrough (tools/content/tables/d8_cases.py), so a phrase here is the book's text. The fact then says where the case file holds the value and
what it must be. A fact the case file states in plain words rather than the book's number is `reworded`: it is listed in the report with the wording used, so a
reader can see every place the file does not repeat the book exactly.

The case files themselves are built by tools/content/build_case_files.py from tools/content/cases/p1v.py (Ruhan Kamran, 10 Oct 2026).
"""
from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CASES = ROOT / "content" / "case_files"
SEEDS = ROOT / "content" / "case_seeds.csv"
REPORT = ROOT / "docs" / "research" / "p1_variant_verbatim.md"


def _obs(field):
    return lambda c: c["observations"][0][field]


def _result(test):
    return lambda c: next((r["value"] for r in c["results"] if r["test"] == test), None)


def _score(name):
    return lambda c: next((s["value"] for s in c["scores"] if s["name"] == name), None)


def _allergy(substance):
    return lambda c: next((a["reaction"] for a in c["allergies"] if a["substance"] == substance), None)


def _history(topic):
    return lambda c: next((h["answer"] for h in c["history"] if h["topic"] == topic), None)


def _problem_why(problem_id):
    return lambda c: next((p["label"] + " " + p["why"] for p in c["problems"] if p["id"] == problem_id), None)


def _content(rel, key):
    return lambda c: json.loads((ROOT / "content" / rel).read_text(encoding="utf-8"))[key]


def name(c):
    return f"{c['identity']['title']} {c['identity']['full_name'].split()[0]}"


def first_name(c):
    return c["identity"]["full_name"].split()[0]


def age(c):
    return c["identity"]["age"]


def pregnant(c):
    return bool(c.get("pregnant"))


# (phrase in the book, what it is, where the case file holds it, how to compare, the expected value)
#   equals: the value equals the expected one (numbers compared as numbers); contains: the text contains it (case-insensitive); reworded: as contains, and listed
FACTS: dict[str, list[tuple]] = {
    "VAR-S1-I": [("Mrs Amna, 45", "name", name, "equals", "Mrs Amna"), ("Mrs Amna, 45", "age", age, "equals", 45),
                 ("penicillin caused throat swelling and collapse in 2015", "penicillin reaction", _allergy("Penicillin"), "contains", "Throat swelling and collapse in 2015")],
    "VAR-S1-K": [("Mrs Shazia, 52", "name", name, "equals", "Mrs Shazia"), ("Mrs Shazia, 52", "age", age, "equals", 52),
                 ("Hb 98 g/L", "Hb", _result("Hb"), "equals", 98), ("MCV 72", "MCV", _result("MCV"), "equals", 72)],
    "VAR-S1-L": [("Mr Aslam, 70", "name", name, "equals", "Mr Aslam"), ("Mr Aslam, 70", "age", age, "equals", 70),
                 ("HR 128", "heart rate", _obs("heart_rate"), "equals", 128), ("BP 132/84", "systolic BP", _obs("systolic_bp"), "equals", 132),
                 ("BP 132/84", "diastolic BP", _obs("diastolic_bp"), "equals", 84), ("ECG: atrial fibrillation (new)", "ECG", _result("ECG"), "contains", "atrial fibrillation")],
    "VAR-S1-M": [("Mr Rizwan, 44", "name", name, "equals", "Mr Rizwan"), ("Mr Rizwan, 44", "age", age, "equals", 44),
                 ("BMI 41", "BMI", _result("BMI"), "equals", 41), ("STOP-BANG 6", "STOP-BANG", _score("STOP-BANG"), "equals", 6),
                 ("SpO2 95%", "SpO2", _obs("spo2"), "equals", 95), ("ASA 3", "ASA grade", _score("ASA grade"), "equals", 3)],
    "VAR-S2-E": [("Mr Akbar, 68", "name", name, "equals", "Mr Akbar"), ("Mr Akbar, 68", "age", age, "equals", 68),
                 ("T 37.6", "temperature", _obs("temperature_c"), "equals", 37.6), ("HR 96", "heart rate", _obs("heart_rate"), "equals", 96),
                 ("Hb 112", "Hb", _result("Hb"), "equals", 112), ("CRP 64", "CRP", _result("CRP"), "equals", 64),
                 ("weight loss recently", "weight loss", _history("red_flags"), "contains", "weight loss")],
    "VAR-S2-H": [("Mrs Mehwish, 30", "name", name, "equals", "Mrs Mehwish"), ("Mrs Mehwish, 30", "age", age, "equals", 30),
                 ("20 weeks pregnant", "pregnant", pregnant, "equals", True), ("20 weeks pregnant", "weeks of pregnancy", _history("pregnancy"), "contains", "20 weeks"),
                 ("T 37.8", "temperature", _obs("temperature_c"), "equals", 37.8), ("HR 102", "heart rate", _obs("heart_rate"), "equals", 102),
                 ("WBC 14", "WBC", _result("WBC"), "equals", 14)],
    "VAR-S2-I": [("Hamid, 16", "first name", first_name, "equals", "Hamid"), ("Hamid, 16", "age", age, "equals", 16),
                 ("Minor (under 18)", "age of a minor (content rule for every case, not the case file)", _content("consent/consent.json", "minor_age"), "equals", 18)],
    "VAR-S2-J": [("Ms Aqsa, 26", "name", name, "equals", "Ms Aqsa"), ("Ms Aqsa, 26", "age", age, "equals", 26),
                 ("leucocytes 2+, nitrite negative, trace blood", "urine dipstick", _result("Urine dipstick"), "contains", "leucocytes 2+, nitrite negative, trace blood"),
                 ("Pregnancy negative", "pregnancy test", _result("Pregnancy test"), "contains", "negative")],
    "VAR-S2-K": [("Mr Faisal, 21", "name", name, "equals", "Mr Faisal"), ("Mr Faisal, 21", "age", age, "equals", 21),
                 ("I got a rash as a child", "penicillin reaction", _allergy("Penicillin"), "contains", "rash as a child")],
    "VAR-S2-L": [("Mr Naveed, 37", "name", name, "equals", "Mr Naveed"), ("Mr Naveed, 37", "age", age, "equals", 37),
                 ("simple appendicitis", "ultrasound", _result("Ultrasound appendix"), "contains", "simple appendicitis"),
                 ("39.1% recurrence over 5 years (APPAC trial)", "recurrence after antibiotics alone", _problem_why("s2l.choice"), "reworded", "two in five people treated with antibiotics alone had a recurrence over five years (APPAC trial)")],
}


def _number(v):
    try:
        return float(str(v).split()[0])
    except ValueError:
        return None


def _matches(value, how: str, expected) -> bool:
    if value is None:
        return False
    if how == "equals":
        if isinstance(expected, bool):
            return value is expected
        if isinstance(expected, (int, float)):
            return _number(value) == float(expected)
        return str(value) == expected
    return str(expected).lower() in str(value).lower()


def run() -> tuple[list[dict], list[str]]:
    with SEEDS.open(encoding="utf-8", newline="") as f:
        seeds = {r["case_id"]: r["v_text"] for r in csv.DictReader(f)}
    rows, problems = [], []
    for case_id, facts in FACTS.items():
        case = json.loads((CASES / f"{case_id}.json").read_text(encoding="utf-8"))
        for phrase, what, where, how, expected in facts:
            in_book = phrase.lower() in seeds[case_id].lower()
            try:
                value = where(case)
            except (KeyError, IndexError):
                value = None
            ok = _matches(value, how, expected)
            if not in_book:
                problems.append(f"{case_id}: '{phrase}' is not in the book's story of this case (content/case_seeds.csv)")
            if not ok:
                problems.append(f"{case_id}: {what}: the book says '{phrase}' but the case file has {value!r}")
            rows.append(dict(case=case_id, phrase=phrase, what=what, value=value, how=how, ok=ok and in_book))
    return rows, problems


def report(rows: list[dict]) -> str:
    L = ["# P1 variant cases against the book (p1-case-library-extended-s01)", "",
         "Generated by `tools/content/variant_facts.py`; do not edit. For each of the ten P1 variant cases: every fact the book's story gives, in the book's own words, and the",
         "value the case file (`content/case_files/<id>.json`) holds for it. The words are checked to be in the case's story in `content/case_seeds.csv`, which is checked",
         "word for word against its page of the walkthrough (Parts 12 and 13). The story's reasoning (\"How the trainee thinks\") is carried by the case's problems, decisions",
         "and debrief, which `tools/composer/stage_b.py` and `tools/content/case_file_checks.py` check.", "",
         "| Case | Facts | In the case file | Reworded |", "|---|---|---|---|"]
    for case_id in FACTS:
        mine = [r for r in rows if r["case"] == case_id]
        L.append(f"| {case_id} | {len(mine)} | {sum(r['ok'] for r in mine)} of {len(mine)} ({100 * sum(r['ok'] for r in mine) // len(mine)}%) | {sum(r['how'] == 'reworded' for r in mine)} |")
    total = len(rows)
    L += [f"| **All** | **{total}** | **{sum(r['ok'] for r in rows)} of {total}** | **{sum(r['how'] == 'reworded' for r in rows)}** |", "",
          "## Every fact", "", "| Case | The book says | Field | The case file holds | Match |", "|---|---|---|---|---|"]
    for r in rows:
        value = str(r["value"]).replace("|", "/")
        match = ("yes" if r["ok"] else "**NO**") + (" (reworded)" if r["how"] == "reworded" else "")
        L.append(f"| {r['case']} | {r['phrase']} | {r['what']} | {value} | {match} |")
    L += ["", "**Reworded:** VAR-S2-L gives the APPAC result as \"about two in five people treated with antibiotics alone had a recurrence over five years\" where the",
          "book says \"39.1% recurrence over 5 years\"; the consent content (`content/consent/consent.json`) and VAR-S2-PC give the book's figure. The age of a minor (VAR-S2-I) is",
          "a rule for every case, so it lives in `content/consent/consent.json` (`minor_age`), not in the case file.", ""]
    return "\n".join(L)


def main(argv: list[str]) -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    rows, problems = run()
    text = report(rows)
    if "--check" in argv:
        if not REPORT.exists() or REPORT.read_text(encoding="utf-8").replace("\r\n", "\n") != text:
            problems.append("docs/research/p1_variant_verbatim.md is out of date: run tools/content/variant_facts.py")
    elif not problems:
        REPORT.write_text(text, encoding="utf-8", newline="\n")
    for p in problems:
        print("  - " + p)
    print(f"P1 variants against the book: {sum(r['ok'] for r in rows)} of {len(rows)} facts in the case files ({sum(r['how'] == 'reworded' for r in rows)} reworded)"
          + (f"; {len(problems)} problems" if problems else ""))
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
