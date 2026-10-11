"""Builds content/problems/problems.json: the six problem types, the 7-step priority ladder and the four Part D priorities (p0-clinical-logic-s14).

    uv run --project tools tools/content/build_problems.py            # write
    uv run --project tools tools/content/build_problems.py --check    # fail if the file is out of date

Sources (verified in earlier steps): content/problem_types.csv, priority_ladder.csv and priority_scale_part_d.csv. What this script adds is the project's reading of how they fit together (SR-0088):
each problem type sits at one rung of the ladder (rung 5, 'do treatments conflict', is found by the clash checker, so no type sits there) and has a default Part D priority.
"""
from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONTENT = ROOT / "content"
OUT = CONTENT / "problems" / "problems.json"

# type -> (ladder rung, default Part D priority)
PLACE = {
    "life_threat": (1, 1),
    "safety_blocker": (2, 2),
    "time_critical": (3, 2),
    "pathway_changing": (4, 2),
    "optimisable": (6, 3),
    "information_only": (7, 4),
}


def rows(name: str) -> list[dict]:
    with open(CONTENT / f"{name}.csv", encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f))


def build() -> str:
    types = []
    for r in rows("problem_types"):
        if r["type"] not in PLACE:
            raise SystemExit(f"problem_types.csv: no ladder place chosen for '{r['type']}'")
        rung, priority = PLACE[r["type"]]
        types.append({"id": r["type"], "ladder_rank": rung, "priority": priority, "effect": r["v_effect"], "examples": r["v_examples"], "ref": r["ref"]})
    extra = sorted(set(PLACE) - {t["id"] for t in types})
    if extra:
        raise SystemExit(f"places given for types the table does not have: {extra}")
    ladder = [{"rank": int(r["rank"]), "question": r["v_question"], "action": r["v_action"], "ref": r["ref"]} for r in rows("priority_ladder")]
    priorities = [{"priority": int(r["priority"]), "meaning": r["v_meaning"], "ref": r["ref"]} for r in rows("priority_scale_part_d")]
    if [x["rank"] for x in ladder] != list(range(1, 8)):
        raise SystemExit("the ladder must have rungs 1 to 7")
    if [x["priority"] for x in priorities] != [1, 2, 3, 4]:
        raise SystemExit("the priority scale must have P1 to P4")
    doc = {"types": types, "ladder": ladder, "priorities": priorities, "refs": ["SR-0088", "SR-0089"]}
    return json.dumps(doc, indent=1, ensure_ascii=False) + "\n"


def main(argv: list[str]) -> int:
    text = build()
    if "--check" in argv:
        if not OUT.exists() or OUT.read_text(encoding="utf-8") != text:
            print("FAIL  content/problems/problems.json is out of date; run tools/content/build_problems.py")
            return 1
        print("content/problems/problems.json is up to date")
        return 0
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
