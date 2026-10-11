"""Builds content/checklist/safety_checklist.json: the WHO Surgical Safety Checklist items with the rule that works out the right answer from the state (p0-simulation-workflow-s16).

    uv run --project tools tools/content/build_checklist.py            # write
    uv run --project tools tools/content/build_checklist.py --check    # fail if the file is out of date

Source: content/who_checklist.csv (the 2009 WHO items, the exact wording, the five ProcSense extensions flagged apart; verified in earlier steps). What this script adds (SR-2836) is the rule for each item's
expected answer ('yes', 'no' or 'na', computed from the state when the answer is given), whether the answer comes from the trainee or from a theatre colleague (an NPC), and the phase each item belongs to.
The severity of a contradiction or a skipped item is a design value (SR-2837).
"""
from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONTENT = ROOT / "content"
OUT = CONTENT / "checklist" / "safety_checklist.json"


def has(flag: str) -> dict:
    return {"in": [flag, {"var": "flags"}]}


def yn(cond: object) -> dict:
    return {"if": [cond, "yes", "no"]}


def station_ready(station: str) -> dict:
    return {"==": [{"var": "or_stations." + station}, "ready"]}


ANTIBIOTIC_60 = {"and": [{"!=": [{"var": "antibiotic_given_minutes_ago"}, None]}, {"<=": [{"var": "antibiotic_given_minutes_ago"}, 60]}]}

# item id -> (who answers: trainee or npc, expected-answer rule)
RULES = {
    "sign_in.01": ("trainee", yn({"and": [{"var": "identity_confirmed"}, has("consent.signed")]})),
    "sign_in.02": ("trainee", {"if": [has("ward.site_marking.marked"), "yes", "na"]}),
    "sign_in.03": ("npc", yn(station_ready("anaesthetic_machine"))),
    "sign_in.04": ("trainee", yn({"in": ["spo2", {"var": "sensors"}]})),
    "sign_in.05": ("trainee", yn({"var": "has_allergy"})),
    "sign_in.06": ("trainee", yn({"or": [{"==": [{"var": "scenario"}, "S2"]}, {"==": [{"var": "fasting"}, "breach"]}]})),
    "sign_in.07": ("trainee", yn(has("risk.blood_loss"))),
    "time_out.01": ("npc", "yes"),
    "time_out.02": ("trainee", yn({"var": "identity_confirmed"})),
    "time_out.03": ("trainee", {"if": [ANTIBIOTIC_60, "yes", {"if": [{"==": [{"var": "scenario"}, "S2"]}, "no", "na"]}]}),
    "time_out.04": ("npc", "yes"),
    "time_out.05": ("npc", "yes"),
    "time_out.06": ("npc", yn(station_ready("lap_tower"))),
    "time_out.07": ("trainee", {"if": [{"var": "imaging_viewed"}, "yes", "na"]}),
    "sign_out.01": ("npc", "yes"),
    "sign_out.02": ("npc", "yes"),
    "sign_out.03": ("npc", "yes"),
    "sign_out.04": ("npc", "yes"),
    "sign_out.05": ("npc", "yes"),
    "extension.01": ("trainee", yn(has("vte.done"))),
    "extension.02": ("trainee", yn({"or": [{"!": [has("condition.diabetes")]}, has("ward.cbg.recorded")]})),
    "extension.03": ("trainee", yn({">=": [{"var": "temperature_c"}, 36.0]})),
    "extension.04": ("trainee", yn({"!": [has("pregnancy.status_required")]})),
    "extension.05": ("trainee", yn(station_ready("team_brief"))),
}
# a theatre colleague's 'no' that also stops the phase although the table gives no instruction: unconfirmed sterility (an indicator or the tower fails) holds the list
NO_STOPS = {"time_out.06"}
PHASES = ["sign_in", "time_out", "sign_out", "extension"]


def build() -> str:
    with open(CONTENT / "who_checklist.csv", encoding="utf-8", newline="") as f:
        rows = list(csv.DictReader(f))
    ids = [r["id"] for r in rows]
    if sorted(ids) != sorted(RULES):
        raise SystemExit(f"who_checklist.csv and RULES disagree: {sorted(set(ids) ^ set(RULES))}")
    items = []
    for r in rows:
        who, rule = RULES[r["id"]]
        items.append({
            "id": r["id"], "phase": r["phase"], "order": int(r["order"]), "prompt": r["v_item"], "extension": r["extension"] == "yes", "critical": r["d_critical"] == "yes",
            "if_no": r["v_if_no"], "no_stops": r["v_if_no"].startswith("STOP") or r["id"] in NO_STOPS, "answered_by": who, "expected_if": rule, "ref": r["ref"],
        })
    if {i["phase"] for i in items} != set(PHASES):
        raise SystemExit("unexpected phases in who_checklist.csv")
    doc = {
        "phases": PHASES,
        "items": items,
        "npc_delay_seconds": 4,
        "skips_before_stop": 2,
        "scored": {"contradiction": "critical.checklist_contradiction", "skipped": "major.checklist_item_skipped", "skipped_repeat": "critical.checklist_skipped_repeatedly", "sign_in_complete": "credit.sign_in_complete",
                   "time_out_complete": "credit.time_out_complete", "stopped_honestly": "info.checklist_stopped_honestly"},
        "ready_flag": "checklist.ready_for_incision",
        "rsi_flag": "plan.rsi",
        "refs": ["SR-2836", "SR-2837"],
    }
    return json.dumps(doc, indent=1, ensure_ascii=False) + "\n"


def main(argv: list[str]) -> int:
    text = build()
    if "--check" in argv:
        if not OUT.exists() or OUT.read_text(encoding="utf-8") != text:
            print("FAIL  content/checklist/safety_checklist.json is out of date; run tools/content/build_checklist.py")
            return 1
        print("content/checklist/safety_checklist.json is up to date")
        return 0
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
