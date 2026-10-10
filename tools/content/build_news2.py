"""Builds content/news2/news2.json: the NEWS2 scoring cells and response bands the engine uses (p0-clinical-logic-s03).

    uv run --project tools tools/content/build_news2.py            # write
    uv run --project tools tools/content/build_news2.py --check    # fail if the file is out of date

Every number comes from the verified tables content/news2_scoring.csv (RCP NEWS2, ISS-0007) and content/news2_response.csv; nothing is typed here. The script checks that the cells cover every
value the monitor can show exactly once (no gap, no overlap), because a gap would silently score 0. The 'precision' of each parameter is the number of decimals its table limits are written
in (whole numbers for breathing, SpO2, blood pressure and pulse; one decimal for temperature): a reading is rounded to that before it is scored, as it would be read off a monitor.
"""
from __future__ import annotations

import csv
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONTENT = ROOT / "content"
OUT = CONTENT / "news2" / "news2.json"

PARAMETERS = ["respiratory_rate", "spo2", "systolic_bp", "heart_rate", "temperature_c"]
GRID = {"respiratory_rate": (0, 80, 1), "spo2": (0, 100, 1), "systolic_bp": (0, 300, 1), "heart_rate": (0, 300, 1), "temperature_c": (25.0, 45.0, 0.1)}


def rows(name: str) -> list[dict]:
    with open(CONTENT / f"{name}.csv", encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f))


def decimals(text: str) -> int:
    return len(text.split(".")[1]) if "." in text else 0


def cell(r: dict) -> dict:
    out: dict = {}
    if r["v_low"].strip():
        out["min"] = float(r["v_low"])
    if r["v_high"].strip():
        out["max"] = float(r["v_high"])
    out["points"] = int(r["o_points"])
    out["ref"] = r["ref"]
    return out


def covers(c: dict, v: float) -> bool:
    return ("min" not in c or v >= c["min"]) and ("max" not in c or v <= c["max"])


def grid(param: str):
    lo, hi, step = GRID[param]
    n = round((hi - lo) / step)
    for i in range(n + 1):
        yield round(lo + i * step, 4)


def condition(r: dict) -> str:
    s = r["v_state"].strip()
    return "on_air" if s == "on air" else "on_oxygen" if s == "on oxygen" else "any"


def build() -> str:
    cells = rows("news2_scoring")
    scale1 = {p: [cell(r) for r in cells if r["scale"] == "1" and r["parameter"] == p] for p in PARAMETERS}
    scale2 = [{**cell(r), "condition": condition(r)} for r in cells if r["scale"] == "2" and r["parameter"] == "spo2"]
    for p, cs in scale1.items():
        for v in grid(p):
            hits = [c for c in cs if covers(c, v)]
            if len(hits) != 1:
                raise SystemExit(f"news2_scoring.csv: scale 1 {p} = {v} is covered by {len(hits)} cells (it must be exactly 1)")
    for state in ("on_air", "on_oxygen"):
        for v in grid("spo2"):
            hits = [c for c in scale2 if c["condition"] in ("any", state) and covers(c, v)]
            if len(hits) != 1:
                raise SystemExit(f"news2_scoring.csv: scale 2 SpO2 = {v} {state} is covered by {len(hits)} cells (it must be exactly 1)")
    oxygen = {r["id"]: r for r in cells if r["parameter"] == "supplemental_oxygen" and r["scale"] == "1"}
    acvpu = {r["id"]: r for r in cells if r["parameter"] == "acvpu" and r["scale"] == "1"}
    precision = {p: max((decimals(r["v_low"]) for r in cells if r["parameter"] == p and r["v_low"].strip()), default=0) for p in PARAMETERS}
    precision["temperature_c"] = max(precision["temperature_c"], max(decimals(r["v_high"]) for r in cells if r["parameter"] == "temperature_c" and r["v_high"].strip()))
    response = []
    for r in rows("news2_response"):
        b = {"id": r["id"], "risk": r["risk"], "frequency": r["frequency"], "responder": r["responder"], "ref": r["ref"]}
        if r["id"] != "single3":
            b["min"] = int(r["v_min"])
            if r["v_max"].strip():
                b["max"] = int(r["v_max"])
        response.append(b)
    doc = {
        "parameters": scale1,
        "spo2_scale2": scale2,
        "supplemental_oxygen": {"air": int(oxygen["1.oxygen.air"]["o_points"]), "oxygen": int(oxygen["1.oxygen.oxygen"]["o_points"]), "refs": [oxygen["1.oxygen.air"]["ref"], oxygen["1.oxygen.oxygen"]["ref"]]},
        "acvpu": {"alert": int(acvpu["1.acvpu.alert"]["o_points"]), "other": int(acvpu["1.acvpu.cvpu"]["o_points"]), "refs": [acvpu["1.acvpu.alert"]["ref"], acvpu["1.acvpu.cvpu"]["ref"]]},
        "precision": precision,
        "response": response,
    }
    return json.dumps(doc, indent=1, ensure_ascii=False) + "\n"


def main(argv: list[str]) -> int:
    text = build()
    if "--check" in argv:
        if not OUT.exists() or OUT.read_text(encoding="utf-8") != text:
            print("FAIL  content/news2/news2.json is out of date; run tools/content/build_news2.py")
            return 1
        print("content/news2/news2.json is up to date")
        return 0
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
