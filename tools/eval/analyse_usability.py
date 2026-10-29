"""Analysis of the P0 usability study (p0-evaluation-performance-s05, EXP-EVAL-1) and the usability pass (p0-vr-interaction-ui-s11, EXP-UI-1 and EXP-VR-2):
`uv run --project tools tools/eval/analyse_usability.py study|pass --csv data.csv [--out results.md]`.

Nothing here collects data. It reads the table the sessions produce (one row per participant and task) and scores the standard questionnaires: the System Usability Scale
(Brooke 1996: ten items 1 to 5, odd items score x - 1, even items 5 - x, the sum times 2.5 gives 0 to 100) and the raw NASA-TLX (Hart and Staveland 1988: six subscales 0 to
100, the unweighted mean). It reports medians, interquartile ranges and ranges only, with no inferential claim, because the samples are small (the plan says so).
Participant ids starting `SIM` mark a simulation: the page says so in its first line and the script refuses to write it into the results folder.

study.csv  participant,group,case,task,success,seconds,errors,end_state,sus1..sus10,tlx_mental,tlx_physical,tlx_temporal,tlx_performance,tlx_effort,tlx_frustration
           (the questionnaire columns are filled on the participant's first row and left empty on the others; group is medical_student or junior_doctor)
pass.csv   participant,round,task,input,success,seconds,confusions,note     (round 1 or 2 for the re-test; input controllers or hands)
"""
from __future__ import annotations

import argparse
import csv
import statistics
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RESULTS = {"study": ROOT / "docs" / "plans" / "p0" / "p0-evaluation-performance" / "results",
           "pass": ROOT / "docs" / "plans" / "p0" / "p0-vr-interaction-ui" / "results"}
SUS_ITEMS = [f"sus{i}" for i in range(1, 11)]
TLX = ["tlx_mental", "tlx_physical", "tlx_temporal", "tlx_performance", "tlx_effort", "tlx_frustration"]
STEP1 = "step1_identity"
# Bangor, Kortum and Miller (2009): about 68 is the average across studies; a description, not a pass mark
SUS_AVERAGE = 68.0


def read(path: Path) -> list[dict]:
    with path.open(encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f))


def sus(row: dict) -> float:
    """One participant's SUS score (0 to 100). Raises on a missing or out-of-range answer: a half-filled form is not scored."""
    total = 0
    for i, key in enumerate(SUS_ITEMS, start=1):
        x = int(row[key])
        if not 1 <= x <= 5:
            raise ValueError(f"{row.get('participant')}: {key} = {x} is not 1 to 5")
        total += (x - 1) if i % 2 else (5 - x)
    return total * 2.5


def raw_tlx(row: dict) -> float:
    vals = [float(row[k]) for k in TLX]
    if any(not 0 <= v <= 100 for v in vals):
        raise ValueError(f"{row.get('participant')}: a NASA-TLX subscale is outside 0 to 100")
    return sum(vals) / len(vals)


def describe(xs: list[float]) -> str:
    """median [IQR] (min to max), n"""
    if not xs:
        return "no data"
    xs = sorted(xs)
    med = statistics.median(xs)
    if len(xs) >= 4:
        q = statistics.quantiles(xs, n=4, method="inclusive")
        iqr = f"[{q[0]:.1f} to {q[2]:.1f}]"
    else:
        iqr = "[IQR needs 4 or more]"
    return f"{med:.1f} {iqr} ({xs[0]:.1f} to {xs[-1]:.1f}), n = {len(xs)}"


def simulated(rows: list[dict]) -> bool:
    return any(r["participant"].startswith("SIM") for r in rows)


def first_rows(rows: list[dict]) -> dict[str, dict]:
    """Each participant's questionnaire row (the first with the SUS filled)."""
    out: dict[str, dict] = {}
    for r in rows:
        if r["participant"] not in out and (r.get("sus1") or "").strip():
            out[r["participant"]] = r
    return out


def study(rows: list[dict]) -> tuple[str, dict]:
    people = sorted({r["participant"] for r in rows})
    forms = first_rows(rows)
    sus_scores = {p: sus(r) for p, r in forms.items()}
    tlx_scores = {p: raw_tlx(r) for p, r in forms.items() if all((r.get(k) or "").strip() for k in TLX)}
    tasks = sorted({r["task"] for r in rows})
    lines = []
    if simulated(rows):
        lines.append("> **SIMULATED DATA.** These rows exercise the analysis; they are not a result about people.\n")
    lines += ["# Usability study (EXP-EVAL-1)", "",
              f"Participants: {len(people)} ({', '.join(f'{g} {n}' for g, n in sorted(count(rows, 'group').items()))}). "
              "Small sample: medians, interquartile ranges and ranges only; no claim of a difference between groups or of significance.", "",
              "## Questionnaires", "",
              f"* System Usability Scale: {describe(list(sus_scores.values()))}. For reference only, about {SUS_AVERAGE:.0f} is the average score across published studies (Bangor et al. 2009).",
              f"* Raw NASA-TLX (0 low to 100 high workload): {describe(list(tlx_scores.values()))}.",
              *[f"  * {k[4:]}: {describe([float(forms[p][k]) for p in tlx_scores])}" for k in TLX],
              "", "## Tasks", "", "| Task | Success | Time (s) | Errors |", "|---|---|---|---|"]
    for t in tasks:
        rs = [r for r in rows if r["task"] == t]
        ok = sum(int(r["success"]) for r in rs)
        lines.append(f"| {t} | {ok} of {len(rs)} | {describe([float(r['seconds']) for r in rs if r['seconds']])} | {describe([float(r['errors']) for r in rs if r.get('errors')])} |")
    ends = count([r for r in rows if r.get("end_state")], "end_state")
    if ends:
        lines += ["", "## End states reached", "", *[f"* {k}: {v}" for k, v in sorted(ends.items())]]
    lines += ["", "## Limitations", "",
              "* Volunteers recruited through the supervisor; they may be keener on VR than the average trainee.",
              "* One session each: nothing here measures learning or retention.",
              "* The questionnaire is answered straight after a session with the researcher present; scores may be kind."]
    return "\n".join(lines) + "\n", {"sus": sus_scores, "tlx": tlx_scores}


def usability_pass(rows: list[dict]) -> tuple[str, dict]:
    lines = []
    if simulated(rows):
        lines.append("> **SIMULATED DATA.** These rows exercise the analysis; they are not a result about people.\n")
    r1 = [r for r in rows if r["round"] == "1"]
    step1 = [r for r in r1 if r["task"] == STEP1 and r["input"] == "controllers"]
    unaided = sum(int(r["success"]) for r in step1)
    verdict = "met" if step1 and unaided >= 4 and len(step1) >= 5 else ("not met" if len(step1) >= 5 else "not yet run with 5 people")
    lines += ["# Usability pass (EXP-UI-1, EXP-VR-2)", "",
              f"EXP-UI-1 (at least 4 of 5 new users complete Step 1 unaided): **{verdict}**, {unaided} of {len(step1)}.", "",
              "## Round 1 by task and input", "", "| Task | Input | Success | Time (s) | Confusion points |", "|---|---|---|---|---|"]
    for (task, inp) in sorted({(r["task"], r["input"]) for r in r1}):
        rs = [r for r in r1 if r["task"] == task and r["input"] == inp]
        lines.append(f"| {task} | {inp} | {sum(int(r['success']) for r in rs)} of {len(rs)} | {describe([float(r['seconds']) for r in rs])} | {sum(int(r['confusions'] or 0) for r in rs)} |")
    # EXP-VR-2: the same small-object tasks with controllers and with hands
    lines += ["", "## Controllers against hand tracking (EXP-VR-2)", ""]
    for task in sorted({r["task"] for r in r1 if r["input"] == "hands"}):
        c = [r for r in r1 if r["task"] == task and r["input"] == "controllers"]
        h = [r for r in r1 if r["task"] == task and r["input"] == "hands"]
        lines.append(f"* {task}: controllers {sum(int(r['success']) for r in c)} of {len(c)}, {describe([float(r['seconds']) for r in c])} s; "
                     f"hands {sum(int(r['success']) for r in h)} of {len(h)}, {describe([float(r['seconds']) for r in h])} s.")
    r2 = [r for r in rows if r["round"] == "2"]
    if r2:
        lines += ["", "## Re-test after the fixes (round 2)", ""]
        for task in sorted({r["task"] for r in r2}):
            rs = [r for r in r2 if r["task"] == task]
            lines.append(f"* {task}: {sum(int(r['success']) for r in rs)} of {len(rs)}, {describe([float(r['seconds']) for r in rs])} s.")
    notes = [r for r in rows if (r.get("note") or "").strip()]
    if notes:
        lines += ["", "## Observer notes", "", *[f"* {r['participant']} ({r['task']}, {r['input']}): {r['note'].strip()}" for r in notes]]
    return "\n".join(lines) + "\n", {"step1_unaided": unaided, "step1_n": len(step1), "verdict": verdict}


def count(rows: list[dict], key: str) -> dict[str, int]:
    out: dict[str, int] = {}
    for r in rows:
        out[r[key]] = out.get(r[key], 0) + 1
    return out


def main(argv: list[str]) -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    ap = argparse.ArgumentParser()
    ap.add_argument("kind", choices=["study", "pass"])
    ap.add_argument("--csv", required=True, type=Path)
    ap.add_argument("--out", type=Path)
    a = ap.parse_args(argv)
    rows = read(a.csv)
    page, _ = (study if a.kind == "study" else usability_pass)(rows)
    if a.out:
        if simulated(rows) and RESULTS[a.kind].resolve() in a.out.resolve().parents:
            print("refused: simulated rows (participant SIM...) cannot be written into the results folder")
            return 2
        a.out.parent.mkdir(parents=True, exist_ok=True)
        a.out.write_text(page, encoding="utf-8", newline="\n")
        print(f"wrote {a.out}")
    else:
        print(page)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
