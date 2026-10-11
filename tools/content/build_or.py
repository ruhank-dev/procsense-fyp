"""Builds content/or/or_requirements.json: the theatre stations of Step 11, who checks each, the cases whose theatre has a fault, and the replacement delay (p0-simulation-workflow-s14).

    uv run --project tools tools/content/build_or.py            # write
    uv run --project tools tools/content/build_or.py --check    # fail if the file is out of date

Source: the walkthrough's Step 11 'What does the trainee actually do?' list (1 to 10) and its 'seriously concerning' line (anaesthetic machine failure, no working suction, no oximeter), registered as SR-2832.
Which station a case's theatre fault is on (SC-18: the insufflator, in the keyhole tower), the replacement delay and the score items are the project's design (SR-2833). The clinician confirms who checks what.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONTENT = ROOT / "content"
OUT = CONTENT / "or" / "or_requirements.json"


def station(id, order, label, items, owner="trainee", critical=False, role="anaesthetist", required=True):
    return {"id": id, "order": order, "label": label, "items": items, "owner": owner, "critical": critical, "call": role, "required": required, "ref": "SR-2832"}


STATIONS = [
    station("anaesthetic_machine", 1, "Anaesthetic machine check done and logged", ["machine check", "check log signed"], critical=True),
    station("monitors", 2, "Monitors ready: oxygen (oximeter), blood pressure, ECG, carbon dioxide, temperature", ["oximeter", "blood pressure", "ECG", "carbon dioxide", "temperature"], critical=True),
    station("suction", 3, "Suction working", ["suction"], critical=True),
    station("oxygen_airway", 4, "Oxygen and airway equipment; difficult-airway trolley known", ["oxygen", "airway equipment", "difficult-airway trolley"]),
    station("table", 5, "Operating table and padding", ["table", "padding"], owner="npc_verified", role="senior"),
    station("lap_tower", 6, "Keyhole tower: camera, light and gas pump (insufflator)", ["camera", "light", "gas pump"], role="senior"),
    station("co2_cylinder", 7, "CO2 gas cylinder with a spare; instruments and an open set in case of conversion", ["CO2 cylinder", "spare cylinder", "instruments", "open set"], role="senior"),
    station("diathermy", 8, "Diathermy with its sticky return pad", ["diathermy", "return pad"], role="senior"),
    station("warming", 9, "Warming device", ["warming device"], role="anaesthetist"),
    station("emergency_trolley", 10, "Emergency trolley and defibrillator checked", ["emergency trolley", "defibrillator"], owner="npc_verified", role="anaesthetist"),
    station("specimens_counts", 11, "Specimen pots and labels; count sheets", ["specimen pots", "labels", "count sheets"], owner="npc_verified", role="senior"),
    station("team_brief", 12, "Team brief if used locally", ["team brief"], owner="npc_verified", role="senior", required=False),
]
# the theatre of these cases has a fault (the case's own story); the others are found by events
FAULTS = {"SC-18": {"station": "lap_tower", "what": "insufflator fault"}}
REPLACEMENT_DELAY_MINUTES = 15
SUBSTITUTE_THEATRE_IN = ["S2"]


def build() -> str:
    have = {p.stem for p in (CONTENT / "case_files").glob("*.json")}
    missing = sorted(set(FAULTS) - have)
    if missing:
        raise SystemExit(f"fault cases that are not case files: {missing}")
    ids = {s["id"] for s in STATIONS}
    bad = sorted(f["station"] for f in FAULTS.values() if f["station"] not in ids)
    if bad:
        raise SystemExit(f"faults on stations that do not exist: {bad}")
    doc = {
        "stations": STATIONS,
        "faults": FAULTS,
        "replacement_delay_minutes": REPLACEMENT_DELAY_MINUTES,
        "substitute_theatre_in": SUBSTITUTE_THEATRE_IN,
        "items": {"ready": "credit.or_ready", "declared_not_ready": "major.or_declared_ready_not_ready", "fault_ignored": "major.or_fault_ignored", "fault_reported": "credit.or_fault_reported",
                  "critical_unchecked": "major.or_critical_station_unchecked", "substitute": "info.substitute_theatre_used"},
        "ready_flag": "or.ready",
        "fault_event": "E-OR-FAULT",
        "refs": ["SR-2832", "SR-2833"],
    }
    return json.dumps(doc, indent=1, ensure_ascii=False) + "\n"


def main(argv: list[str]) -> int:
    text = build()
    if "--check" in argv:
        if not OUT.exists() or OUT.read_text(encoding="utf-8") != text:
            print("FAIL  content/or/or_requirements.json is out of date; run tools/content/build_or.py")
            return 1
        print("content/or/or_requirements.json is up to date")
        return 0
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
