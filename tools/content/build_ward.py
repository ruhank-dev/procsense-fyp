"""Builds content/ward/ward_checklist.json: the ward / ED checklist of Step 10, with the rule that verifies each tick against the state (p0-simulation-workflow-s12).

    uv run --project tools tools/content/build_ward.py            # write
    uv run --project tools tools/content/build_ward.py --check    # fail if the file is out of date

Source: the walkthrough's Step 10 'What does the trainee actually do?' list (items 1 to 13), which SR-2830 registers. The temperature limit (36.0) is read from content/decision_rules.csv (theatre_temperature,
SR-0911). Each item names the rule that checks it against the state (JsonLogic over the keys the engine gives every rule); the weight of a false tick is a design value (SR-2831).
"""
from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONTENT = ROOT / "content"
OUT = CONTENT / "ward" / "ward_checklist.json"


def has(flag: str) -> dict:
    return {"in": [flag, {"var": "flags"}]}


def item(id, order, label, verify, required=True, required_if=None, critical=False, action=None, ref="SR-2830"):
    return {"id": id, "order": order, "label": label, "verify_if": verify, "required": required, "required_if": required_if, "critical": critical, "action": action, "ref": ref}


def build() -> str:
    with open(CONTENT / "decision_rules.csv", encoding="utf-8", newline="") as f:
        temp_row = next(r for r in csv.DictReader(f) if r["id"] == "theatre_temperature")
    limit = float(temp_row["v_number"])
    items = [
        item("fasting", 1, "Fasting times re-confirmed (a breach must be passed to the anaesthetist)", {"and": [{"!=": [{"var": "fasting"}, "unknown"]}, {"or": [{"!=": [{"var": "fasting"}, "breach"]}, has("anaesthetist.told.fasting")]}]}),
        item("gown", 2, "Hospital gown on", has("ward.gown.worn"), action=["worn"]),
        item("jewellery", 3, "Jewellery removed or taped, make-up and nail polish off", {"or": [has("ward.jewellery.removed"), has("ward.jewellery.taped")]}, action=["removed", "taped", "left_on"]),
        item("aids", 4, "Dentures, hearing aids, contact lenses and implants noted", has("ward.aids.noted"), action=["noted"]),
        item("hair", 5, "Hair removal only if needed, with clippers (never a razor)", {"!": [has("ward.hair.razor")]}, required=False, action=["clippers", "razor", "not_needed"]),
        item("site_marking", 6, "Site marking per policy (usually not applicable for midline keyhole surgery)", {"or": [has("ward.site_marking.marked"), has("ward.site_marking.not_applicable")]}, action=["marked", "not_applicable"]),
        item("stockings", 7, "Stockings if prescribed", has("ward.stockings.applied"), required=False, action=["applied"]),
        item("pregnancy", 8, "Pregnancy status recorded", {"!": [has("pregnancy.status_required")]}, critical=True),
        item("consent", 9, "Consent present and correct", has("consent.signed"), critical=True),
        item("bands", 10, "Identity band on", {"and": [{"var": "identity_confirmed"}, {"var": "wristband_read"}]}, critical=True),
        item("cbg", 11, "Diabetic: finger-prick sugar recorded", has("ward.cbg.recorded"), required=False, required_if=has("condition.diabetes"), action=["recorded"]),
        item("temperature", 12, f"Temperature at least {limit:.1f} C (warm the patient if not)", {">=": [{"var": "temperature_c"}, limit]}, ref="SR-0911"),
        item("documents", 13, "All documents together: notes, consent, results, drug chart, scans", has("ward.documents.together"), action=["together"]),
    ]
    doc = {
        "items": items,
        "items_scored": {
            "tick_unverified": "minor.ward_tick_unverified", "tick_false_critical": "critical.ward_tick_false", "razor": "minor.razor_used", "late_fasting_not_passed": "major.late_fasting_not_passed",
            "signed_with_gaps": "major.ward_signed_with_gaps", "signed_complete": "credit.ward_checklist_complete", "anaesthetist_told": "credit.late_fasting_passed",
        },
        "discrepancy_event": "E-WARD-DISCREPANCY",
        "signed_flag": "ward.checklist_signed",
        "refs": ["SR-2830", "SR-2831"],
    }
    return json.dumps(doc, indent=1, ensure_ascii=False) + "\n"


def main(argv: list[str]) -> int:
    text = build()
    if "--check" in argv:
        if not OUT.exists() or OUT.read_text(encoding="utf-8") != text:
            print("FAIL  content/ward/ward_checklist.json is out of date; run tools/content/build_ward.py")
            return 1
        print("content/ward/ward_checklist.json is up to date")
        return 0
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
