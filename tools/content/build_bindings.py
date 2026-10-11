"""Builds content/whatif_bindings.csv: which walkthrough what-ifs each P0 case can show, and how (p0-case-library s03, plan test T-E8).

    uv run --project tools tools/content/build_bindings.py            # write
    uv run --project tools tools/content/build_bindings.py --check    # fail if the file is out of date

A what-if is bound to a case when the case's own content makes it reachable, by rules over the case file:
  * the allergic reaction and the antibiotic allergy check (w29, w28): the case lists a real allergy;
  * a vital-sign event (w01, w03, w04, w07, w08, w09): one of the case's observations crosses the event's threshold;
  * a blood thinner, diabetes, steroid / GLP-1 / SGLT2 medicine (w30, w31, w32): the medicines answer names one;
  * fasting (w33): the last meal does not meet the rule; pregnancy (w26): a positive pregnancy result; sepsis (w40) and deterioration (w39): the case has a sepsis problem or a NEWS2 that reaches 7 without action;
  * a confused patient and consent (w42): the patient is not alert; a minor (w43): under 18; a critical result phone call (w38): a result is flagged critical.
Only what-ifs that the coverage list (docs/research/whatif_coverage.md) says an event or an existing subsystem handles are ever bound; a data-only what-if is never bound, so no binding promises behaviour that does not exist.
The "event" column is filled only for the Event Director's events; the "test" column names the unit-test file that shows an engine-handled what-if.
"""
from __future__ import annotations

import csv
import io
import json
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(ROOT / "tools"))
import build_events as be  # noqa: E402
import build_discovery as bd  # noqa: E402

OUT = ROOT / "content" / "whatif_bindings.csv"
HEADER = ["case", "whatif", "event", "test", "how", "ref"]
VITAL_EVENTS = {"E-LOW-BP": "w01", "E-HIGH-HR": "w03", "E-LOW-HR": "w04", "E-LOW-OXYGEN": "w07", "E-FAST-BREATHING": "w08", "E-SLOW-BREATHING": "w09"}


def _hist(c: dict, topic: str) -> str:
    return next((h["answer"] for h in c["history"] if h["topic"] == topic and h.get("variant") is None), "").lower()


def bindings_for(c: dict) -> list[tuple[str, str, str, str]]:
    """(whatif, event, test file, how) for one case."""
    from composer.checks.c05_to_c11 import event_reachable
    out: list[tuple[str, str, str, str]] = []
    meds = _hist(c, "medicines")
    real_allergy = [a for a in c["allergies"] if "rash as a child" not in a["reaction"].lower()]
    if real_allergy:
        out.append(("w28", "", "MedicineWorkflowTests.cs", "the case lists a " + real_allergy[0]["substance"].lower() + " allergy, so an antibiotic request is challenged or reacts"))
        out.append(("w29", "E-HYPERSENSITIVITY", "EventDirectorTests.cs", "the allergen can be given and the reaction fires"))
    for ev, w in VITAL_EVENTS.items():
        if event_reachable(ev, c):
            out.append((w, ev, "EventDirectorTests.cs", "an observation of the case crosses the threshold"))
    if re.search(r"warfarin|apixaban|rivaroxaban|clopidogrel", meds):
        out.append(("w30", "", "ClashTests.cs", "the medicines answer names a blood thinner"))
    if re.search(r"metformin|gliclazide|insulin|semaglutide|sglt2|dapagliflozin", meds):
        out.append(("w31", "", "ClashTests.cs", "the medicines answer names a diabetes medicine"))
    if re.search(r"semaglutide|sglt2|dapagliflozin|prednisolone|steroid", meds):
        out.append(("w32", "", "ClashTests.cs", "the medicines answer names a steroid, GLP-1 or SGLT2 medicine"))
    if c["scenario"] == "S1" and any(r["test"].lower().startswith("urine pregnancy") and r["flag"] == "positive" for r in c["results"] if r.get("variant") is None):
        out.append(("w26", "", "DecisionEngineTests.cs", "the pregnancy test is positive"))
    if any("fasting" in p["id"] or "stomach" in p["id"] for p in c["problems"]):
        out.append(("w33", "", "FastingTests.cs", "the fasting rule is not met"))
    if any("sepsis" in (p["id"] + p["label"]).lower() for p in c["problems"] if p.get("variant") is None):
        out.append(("w40", "", "SepsisTests.cs", "the case has a sepsis problem"))
    if any(o["news2"] >= 7 and o["condition"] == "if_no_action" for o in c["observations"]):
        out.append(("w39", "", "DeteriorationTests.cs", "without action the NEWS2 reaches 7 or more"))
    if any(o["acvpu"] != "A" for o in c["observations"]) or any(r["test"].startswith("Capillary glucose") and r["flag"] == "low" for r in c["results"]):
        out.append(("w42", "", "ConsentTests.cs", "the patient is confused or has a low sugar, so cannot give valid consent"))
    if c["identity"]["age"] < 18:
        out.append(("w43", "", "ConsentTests.cs", "the patient is under 18"))
    if any(r["flag"].startswith("critical") for r in c["results"] if r.get("variant") is None):
        out.append(("w38", "", "CriticalCallTests.cs", "a result is flagged critical, so the lab phones it through"))
    return out


def build() -> str:
    coverage = {w: v for w, v in be.COVER.items()}
    buf = io.StringIO()
    w = csv.writer(buf, lineterminator="\n")
    w.writerow(HEADER)
    for case in bd.P0:
        c = json.loads((ROOT / "content" / "case_files" / f"{case}.json").read_text(encoding="utf-8"))
        for whatif, event, test, how in bindings_for(c):
            kind = coverage[whatif][0]
            if kind == "data":
                raise SystemExit(f"{case}: {whatif} is data only and must not be bound")
            if kind == "event" and coverage[whatif][1] != event:
                raise SystemExit(f"{case}: {whatif} is the event {coverage[whatif][1]}, not {event}")
            if kind == "engine" and not (ROOT / "tests" / "ProcSense.Core.Tests" / test).exists():
                raise SystemExit(f"{case}: the test file {test} does not exist")
            w.writerow([case, whatif, event, test, how, "SR-2869"])
    return buf.getvalue()


def main(argv: list[str]) -> int:
    text = build()
    if "--check" in argv:
        if not OUT.exists() or OUT.read_text(encoding="utf-8").replace("\r\n", "\n") != text:
            print("FAIL  content/whatif_bindings.csv is out of date; run tools/content/build_bindings.py")
            return 1
        print(f"content/whatif_bindings.csv is up to date ({text.count(chr(10)) - 1} bindings for 17 P0 cases)")
        return 0
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT)} ({text.count(chr(10)) - 1} bindings)")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
