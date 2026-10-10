"""Builds content/flow/flow_maps.json: the two scenario master maps (S1 and S2, 20 stages each) as data the engine runs (p0-simulation-workflow-s06).

    uv run --project tools tools/content/build_flow_maps.py            # write
    uv run --project tools tools/content/build_flow_maps.py --check    # fail if the file is out of date

Titles and source-register ids come from content/flow_stages.csv (the walkthrough's Parts F and G, verified). What this file adds is how the stages run:
  * track      "patient" (the main line), "theatre" (theatre preparation, in parallel) or "optimisation" (S2: treat and optimise while results are pending)
  * after      stages that must be finished before this one starts (this is how a parallel track joins the main line again)
  * steps      which of the 15 walkthrough steps (the canonical ids) the stage works through
  * applies_if a stage that does not apply to the case is skipped, and the line moves on
  * done_if    a rule over the same facts the goals read; stages whose step is not built yet finish on a flag ("stage.<id>.done") that the step's own subsystem will set
  * interrupt  deterioration (18) and emergency (19) stop the tracks named in "suspends" as soon as their flag appears, and hand control back when they are resolved
The stage order, tracks and joins are the project's design (type D); the clinical content of each stage is in flow_stages.csv.
"""
from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "content" / "flow" / "flow_maps.json"


def flag(name: str) -> dict:
    return {"in": [name, {"var": "flags"}]}


def topic(name: str) -> dict:
    return {"in": [name, {"var": "topics"}]}


def has(key: str, name: str) -> dict:
    return {"in": [name, {"var": key}]}


def done_flag(sid: str) -> dict:
    return flag(done_name(sid))


def done_name(sid: str) -> str:
    return "stage." + sid.lower().replace(".", "_") + ".done"


# (steps, done_if or None for "its step's subsystem sets the done flag")
COMMON = {
    1: ([1], {"==": [{"var": "wristband_read"}, True]}),
    2: ([1], {"and": [{"==": [{"var": "identity_confirmed"}, True]}, {"==": [{"var": "allergies_known"}, True]}]}),
    3: ([3], {"and": [has("sensors", "spo2"), has("measured", "systolic_bp")]}),
    4: ([2, 4], {"and": [topic("complaint"), topic("medicines")]}),
    5: ([6], None), 6: ([7], None), 7: ([8], None), 8: ([8], None), 9: ([9], None), 10: ([5, 10], None),
    11: ([12, 13], None), 12: ([11], None), 13: ([14], None), 14: ([14], None), 15: ([15], None), 16: ([15], None), 17: ([15], None),
    18: ([8], None), 19: ([8], None), 20: ([15], None),
}

# stage number -> (track, stages it waits for). The main line is 1..7, 9..11, 13..17, 20; 7 is skipped when no result was abnormal.
S1 = {1: ("patient", []), 2: ("patient", [1]), 3: ("patient", [2]), 4: ("patient", [3]), 5: ("patient", [4]), 6: ("patient", [5]), 7: ("patient", [6]),
      8: ("patient", [7]), 9: ("patient", [8]), 10: ("patient", [9]), 11: ("patient", [10]),
      12: ("theatre", []),                      # runs in parallel from the start, before the patient arrives
      13: ("patient", [11, 12]),                # Time Out needs Sign In and a ready theatre: the two tracks join here
      14: ("patient", [13]), 15: ("patient", [14]), 16: ("patient", [15]), 17: ("patient", [16]), 18: ("interrupt", []), 19: ("interrupt", []), 20: ("patient", [17])}
S2 = {1: ("patient", []), 2: ("patient", [1]), 3: ("patient", [2]), 4: ("patient", [3]), 5: ("patient", [4]), 6: ("patient", [5]), 7: ("patient", [6]),
      8: ("optimisation", [5]),                 # treat and optimise while the results are pending
      9: ("patient", [7]), 10: ("patient", [9]), 11: ("patient", [10]),
      12: ("theatre", [7]),                     # the theatre is prepared once the plan is to operate
      13: ("patient", [11, 12]), 14: ("patient", [13]), 15: ("patient", [14]), 16: ("patient", [15]), 17: ("patient", [16]), 18: ("interrupt", []), 19: ("interrupt", []), 20: ("patient", [17])}


def stages(scenario: str, rows: list[dict]) -> list[dict]:
    layout = S1 if scenario == "S1" else S2
    out = []
    for r in rows:
        n = int(r["stage"])
        sid = r["id"]
        track, after = layout[n]
        steps, done = COMMON[n]
        st = {"id": sid, "stage": n, "title": r["title"], "track": track, "steps": steps, "after": [f"{scenario}.{a:02d}" for a in after],
              "applies_if": True, "start_if": True, "done_if": done if done is not None else done_flag(sid), "interrupt": False, "suspends": [], "refs": [r["ref"]]}
        if n == 7:
            st["applies_if"] = flag("branch.abnormal")        # an abnormal-result branch only exists when a result was abnormal
        if n in (18, 19):
            st["interrupt"] = True
            st["suspends"] = ["patient"]
            st["start_if"] = flag("event.deterioration" if n == 18 else "event.emergency")
        out.append(st)
    return out


def build() -> str:
    with open(ROOT / "content" / "flow_stages.csv", encoding="utf-8", newline="") as f:
        rows = list(csv.DictReader(f))
    maps = [{"scenario": sc, "stages": stages(sc, [r for r in rows if r["scenario"] == sc])} for sc in ("S1", "S2")]
    return json.dumps({"maps": maps}, indent=1, ensure_ascii=False) + "\n"


def main(argv: list[str]) -> int:
    text = build()
    if "--check" in argv:
        if not OUT.exists() or OUT.read_text(encoding="utf-8") != text:
            print("FAIL  content/flow/flow_maps.json is out of date; run tools/content/build_flow_maps.py")
            return 1
        print("content/flow/flow_maps.json is up to date")
        return 0
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT)}: 2 maps, {sum(len(m['stages']) for m in json.loads(text)['maps'])} stages")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
