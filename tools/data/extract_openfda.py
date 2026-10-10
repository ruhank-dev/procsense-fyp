"""Extracts the contraindication and boxed-warning wording of one representative medicine per medicine group from the openFDA drug label API (CC0) into tools/data/derived/openfda_label_safety.json.

    uv run --project tools tools/data/extract_openfda.py

Only safety wording is taken (contraindications, boxed warning). Any sentence that contains a dose, rate or strength (a number followed by mg, mcg, g, mL, units, mEq, %) is dropped, so no dose is
ever imported (the walkthrough's PATIENT-SPECIFIC DOSE rule). The label's set id and effective date are kept so the wording can be traced. Needs the network; CI checks the committed result.
The API terms: https://open.fda.gov/terms/ (CC0 1.0 for the data). The US label of the generic name is a stand-in for the UK product of the same medicine group; the clinician confirms the local wording.
"""
from __future__ import annotations

import json
import re
import sys
import urllib.parse
import urllib.request
from pathlib import Path

OUT = Path(__file__).resolve().parent / "derived" / "openfda_label_safety.json"
# medicine group id (content/medicines.csv) -> the generic name searched in openFDA
GENERIC = {
    "paracetamol": "acetaminophen", "nsaids": "ibuprofen", "opioids": "morphine sulfate", "antiemetics": "ondansetron", "abx_s1_low_risk": "cefazolin", "abx_s2_complicated": "piperacillin and tazobactam",
    "warfarin": "warfarin sodium", "doacs": "apixaban", "dabigatran": "dabigatran etexilate", "aspirin": "aspirin", "p2y12": "clopidogrel", "metformin": "metformin hydrochloride", "sulfonylureas": "glipizide",
    "sglt2": "dapagliflozin", "glp1": "semaglutide", "dpp4": "sitagliptin", "insulin_long": "insulin glargine", "ace_arb": "lisinopril", "beta_blockers": "metoprolol tartrate", "diuretics": "furosemide",
    "statins_ccb": "atorvastatin calcium", "steroids": "prednisone", "adrenaline": "epinephrine", "naloxone": "naloxone hydrochloride",
}
DOSE = re.compile(r"\b\d+(?:\.\d+)?\s?(?:mg|mcg|µg|g|ml|mL|units?|IU|mEq|mmol|%)\b|\bmg/|\bunits/", re.I)


def get(url: str) -> dict:
    req = urllib.request.Request(url, headers={"User-Agent": "procsense-fyp-data-extract"})
    with urllib.request.urlopen(req, timeout=90) as r:
        return json.loads(r.read())


def clean(text: str, limit: int = 420) -> str:
    text = re.sub(r"^(?:\d+(?:\.\d+)?\s*)?(?:CONTRAINDICATIONS|BOXED WARNING|WARNING:?)\s*:?\s*", "", text.strip(), flags=re.I)
    text = re.sub(r"\(\s*\d+(?:\.\d+)?(?:\s*,\s*\d+(?:\.\d+)?)*\s*\)", "", text)     # section cross-references like (4) or (5.1)
    text = re.sub(r"\s+", " ", text)
    kept, size = [], 0
    for s in re.split(r"(?<=[.;])\s+", text):
        if DOSE.search(s) or len(s) < 12:
            continue
        if size + len(s) > limit:
            break
        kept.append(s)
        size += len(s)
    return " ".join(kept)


def main() -> int:
    result = {"source": "openFDA drug label API", "url": "https://open.fda.gov/apis/drug/label/", "licence": "CC0 1.0", "note": "contraindications and boxed warning wording only; sentences with doses are dropped", "medicines": {}}
    for med, generic in GENERIC.items():
        q = urllib.parse.urlencode({"search": f'openfda.generic_name:"{generic}" AND _exists_:contraindications', "limit": 25})
        rows = get("https://api.fda.gov/drug/label.json?" + q)["results"]
        rows = [r for r in rows if [g.lower() for g in r["openfda"].get("generic_name", [])] == [generic.lower()] or generic.lower() in [g.lower() for g in r["openfda"].get("generic_name", [])]]
        rows.sort(key=lambda r: (len(r["openfda"].get("generic_name", [])), -int(r["effective_time"])))   # single-ingredient labels first, newest first
        if not rows:
            print("no label for", med, generic, file=sys.stderr)
            continue
        r = rows[0]
        contra = clean(r.get("contraindications", [""])[0])
        boxed = clean(r.get("boxed_warning", [""])[0]) if r.get("boxed_warning") else ""
        if not contra and not boxed:
            print("nothing usable for", med, file=sys.stderr)
            continue
        result["medicines"][med] = {"generic_name": generic, "label_set_id": r["set_id"], "effective_time": r["effective_time"], "contraindications": contra, "boxed_warning": boxed}
        print(med, "ok", len(contra), len(boxed))
    OUT.write_text(json.dumps(result, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
    return 0


if __name__ == "__main__":
    sys.exit(main())
