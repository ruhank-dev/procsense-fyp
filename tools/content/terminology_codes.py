"""Standard terminology codes beside our own ids (p2-content-extensions-s03): `uv run --project tools tools/content/terminology_codes.py [--check | --verify]`.

content/codes/loinc.csv   one LOINC code for every analyte of content/lab_bands.csv (the laboratory results of the cases), with the unit our content uses
content/codes/icd11.csv   one ICD-11 (MMS) code for every condition word of content/ai/lexicon.json (the diagnoses and comorbidities the AI is allowed to name), or an explicit "no single code" with the reason

The codes are extra columns of meaning, never ids: nothing in the engine reads these files, and a code never replaces one of our ids. --check (CI, offline) tests the format (the LOINC check digit, the ICD-11
code shape), that every analyte and every condition word is covered, that a row without a code says why, and that each row carries the date it was checked. --verify (by hand, needs the internet) looks each
code up on the public HL7 terminology server (tx.fhir.org, LOINC and ICD-11 MMS) and compares the display text. The codes were chosen by the builder and checked against that server on 18 October 2026; a named
clinician or terminologist has not reviewed the choices (several ICD-11 codes can fit one word; the most general fitting code was used and the note says when a more specific one exists).

Licences: LOINC is copyright Regenstrief Institute and free to use under the LOINC licence (the code and its name may be used, with the notice in content/codes/README.md). ICD-11 is copyright World Health
Organization, CC BY-ND 3.0 IGO: codes are quoted and not altered. Standard library only.
"""
from __future__ import annotations

import csv
import json
import re
import sys
import urllib.parse
import urllib.request
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
ROOT = Path(__file__).resolve().parents[2]
CODES = ROOT / "content" / "codes"
LOINC_SHAPE = re.compile(r"^\d{1,7}-\d$")
ICD_SHAPE = re.compile(r"^[0-9A-Z]{2,4}(\.[0-9A-Z]{1,2})?$")
DATE = re.compile(r"^\d{4}-\d{2}-\d{2}$")
SYSTEMS = {"loinc": "http://loinc.org", "icd11": "http://id.who.int/icd/release/11/mms"}


def loinc_check_digit_ok(code: str) -> bool:
    """LOINC's check digit is the mod-10 (Luhn) digit of the number before the hyphen."""
    body, check = code.split("-")
    total = 0
    for i, ch in enumerate(reversed(body)):
        d = int(ch)
        if i % 2 == 0:
            d *= 2
            d = d - 9 if d > 9 else d
        total += d
    return (10 - total % 10) % 10 == int(check)


def read(name: str) -> list[dict]:
    with (CODES / name).open(encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f))


def analytes() -> set[str]:
    with (ROOT / "content" / "lab_bands.csv").open(encoding="utf-8", newline="") as f:
        return {r["analyte"] for r in csv.DictReader(f)}


def condition_words() -> set[str]:
    return set(json.loads((ROOT / "content" / "ai" / "lexicon.json").read_text(encoding="utf-8"))["categories"]["condition"])


def problems(loinc: list[dict], icd: list[dict], need_analytes: set[str], need_words: set[str]) -> list[str]:
    out = []
    seen = {r["analyte"] for r in loinc}
    out += [f"loinc.csv: no row for analyte {a}" for a in sorted(need_analytes - seen)]
    out += [f"loinc.csv: row for {a} is not an analyte of content/lab_bands.csv" for a in sorted(seen - need_analytes)]
    if len(seen) != len(loinc):
        out.append("loinc.csv: an analyte appears twice")
    for r in loinc:
        c = r["loinc"]
        if not LOINC_SHAPE.match(c) or not loinc_check_digit_ok(c):
            out.append(f"loinc.csv: {r['analyte']} has an invalid LOINC code {c!r}")
        if not DATE.match(r["checked"]) or not r["loinc_version"] or not r["display"]:
            out.append(f"loinc.csv: {r['analyte']} lacks its display, check date or LOINC version")
    terms = {r["term"] for r in icd}
    out += [f"icd11.csv: no row for condition word {w}" for w in sorted(need_words - terms)]
    out += [f"icd11.csv: row for {w} is not a condition word of content/ai/lexicon.json" for w in sorted(terms - need_words)]
    if len(terms) != len(icd):
        out.append("icd11.csv: a term appears twice")
    for r in icd:
        if not DATE.match(r["checked"]) or not r["icd11_release"]:
            out.append(f"icd11.csv: {r['term']} lacks its check date or release")
        if r["icd11"]:
            if not ICD_SHAPE.match(r["icd11"]) or not r["display"]:
                out.append(f"icd11.csv: {r['term']} has an invalid ICD-11 code {r['icd11']!r} or no display")
        elif not r["note"].startswith("no single code:"):
            out.append(f"icd11.csv: {r['term']} has no code and does not say why (note must start with 'no single code:')")
    return out


def lookup(kind: str, code: str) -> str | None:
    url = "https://tx.fhir.org/r4/CodeSystem/$lookup?system=%s&code=%s&_format=json" % (SYSTEMS[kind], urllib.parse.quote(code))
    try:
        with urllib.request.urlopen(url, timeout=30) as resp:
            data = json.load(resp)
    except Exception:
        return None
    return next((p["valueString"] for p in data["parameter"] if p["name"] == "display"), None)


def main(argv: list[str]) -> int:
    loinc, icd = read("loinc.csv"), read("icd11.csv")
    found = problems(loinc, icd, analytes(), condition_words())
    if "--verify" in argv:
        for kind, rows, key in (("loinc", loinc, "loinc"), ("icd11", [r for r in icd if r["icd11"]], "icd11")):
            for r in rows:
                shown = lookup(kind, r[key])
                if shown is None:
                    found.append(f"{kind}: {r[key]} could not be looked up on tx.fhir.org")
                elif shown != r["display"]:
                    found.append(f"{kind}: {r[key]} is {shown!r} on the server, {r['display']!r} in the file")
    if found:
        print("\n".join(found))
        return 1
    print(f"terminology codes ok: {len(loinc)} LOINC analytes, {sum(1 for r in icd if r['icd11'])} ICD-11 codes, {sum(1 for r in icd if not r['icd11'])} words with no single code")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
