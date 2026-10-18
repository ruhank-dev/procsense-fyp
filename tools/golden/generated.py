"""Golden runs of generated cases (p1-live-scenario-generation-s11, stage LG-16 to LG-18).

    uv run --project tools tools/golden/generated.py make     # (re)make the snapshot in tests/golden_generated: packages, scripts, then `procsense-sim golden --update` writes the expected hashes
    uv run --project tools tools/golden/generated.py check    # the snapshot still passes the generation gate (schema, Stage B, media, cross-modal) and the sealed hashes hold
    uv run --project tools tools/golden/generated.py report   # write docs/research/review-packs/2026-10-18-composed.md: one row per composed case with its Stage B result, golden result and decision

The set is fixed: fourteen generated cases (eight S1 and six S2, each of a different approved case) played on the correct path of the case they keep the story of, with the checklist allergy answer following the
composed patient. The engine plays them like any case (`golden --dir tests/golden_generated` runs in CI), so replay hashes stay stable and the scoring rules are the ones the library cases use. The
snapshot is deliberate, like the other goldens: remake it when the engine, the composer or the content changes it and the diff is reviewed.
"""
import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools"))
OUT = ROOT / "tests" / "golden_generated"
PICK = {"S1": ("VAR-S1-G", "VAR-S1-H", "VAR-S1-D", "VAR-S1-A", "VAR-S1-C", "VAR-S1-E", "VAR-S1-J", "VAR-S1-PB"),
        "S2": ("VAR-S2-G", "VAR-S2-B", "VAR-S2-F", "VAR-S2-D", "VAR-S2-PC", "VAR-S2-PD")}      # spines with a correct-path script and a dangerous-path script: 8 S1 and 6 S2 (p1-scenario-composer-difficulty-s05), each of a different approved case


def make() -> None:
    from composer import blocks as B
    from generation import objectives as ob, package, pipeline, retrieve
    cases = B.load_cases()
    ctx = pipeline.Context(retrieve.build_index(), cases, ob.Tables.load())
    OUT.mkdir(parents=True, exist_ok=True)
    for old in OUT.glob("*"):
        old.unlink()
    for scenario, spines in PICK.items():
        for k, spine in enumerate(spines):
            seed = 100 * k                                                    # a different first seed for each spine, so the generated case ids are all different
            while True:                                                       # the first seed whose composed case keeps this spine's story
                r = pipeline.generate({"version": 1, "trainee": "trainee-001", "mode": "practice", "scenario": scenario, "level": "advanced", "seed": seed}, [], [], ctx,
                                      recent_fingerprints=[], force_spine=spine)
                if r.package and r.package["provenance"]["spine"] == spine:
                    break
                seed += 1
                if seed > 400:
                    raise SystemExit(f"no seed gives a case from {spine}")
            p = r.package
            name = f"generated_{spine}"
            package.write(p, OUT)
            (OUT / f"{p['id']}.package.json").rename(OUT / f"{name}.package.json")
            actions = json.loads((ROOT / "tests" / "golden" / f"{spine}_correct.actions.json").read_text(encoding="utf-8"))
            spine_case = next(c for c in cases if c["id"] == spine)
            for a in actions:
                if a.get("verb") == "checklist_item" and a.get("params", {}).get("item") == "sign_in.05":
                    a["params"]["response"] = "yes" if p["case"]["allergies"] else "no"
            (OUT / f"{name}.actions.json").write_text(json.dumps(actions, indent=2) + "\n", encoding="utf-8", newline="\n")
            (OUT / f"{name}.expected.json").write_text(json.dumps({"case": p["id"], "seed": "42", "mode": "practice"}) + "\n", encoding="utf-8", newline="\n")
            print(f"{name}: {p['id']} from seed {seed}, allergy {'yes' if p['case']['allergies'] else 'no'} (spine {'yes' if spine_case['allergies'] else 'no'})")
    cli = ["dotnet", "run", "--no-build", "-c", "Release", "--project", "src/ProcSense.Cli", "--", "golden", "--dir", str(OUT), "--update"]
    done = subprocess.run(cli, cwd=ROOT, capture_output=True, text=True, encoding="utf-8")
    print(done.stdout.strip()[-300:])
    if done.returncode != 0:
        raise SystemExit(done.stderr)


REPORT = ROOT / "docs" / "research" / "review-packs" / "2026-10-18-composed.md"


def report_text() -> str:
    from composer import blocks as B
    from generation import package
    cases = B.load_cases()
    rows = []
    for path in sorted(OUT.glob("*.package.json")):
        p = json.loads(path.read_text(encoding="utf-8"))
        gate = package.validate(p, cases)
        e = json.loads((OUT / path.name.replace(".package.json", ".expected.json")).read_text(encoding="utf-8"))
        problems = "; ".join(x["label"] for x in sorted(p["case"]["problems"], key=lambda x: x["rank"])[:3])
        rows.append(f"| {p['case']['id']} | {p['case']['scenario']} | {p['provenance']['spine']} | {len(p['provenance']['blocks'])} | {'pass' if gate['passed'] else 'FAIL'} | {e['facts'].get('end_state')} / {e['score']['result']} | {problems[:110]} |")
    return "\n".join(["# Composed cases for the practice pool (p1-scenario-composer-difficulty-s05, EXP-COMP-2)", "",
                      "_Generated by `tools/golden/generated.py report` from `tests/golden_generated`; do not edit by hand._", "",
                      "Fourteen composed cases (8 S1, 6 S2), each made by the composer from a different approved spine and a seeded choice of background blocks, and each played on the correct path of its spine by the engine. "
                      "By ADR-0008 a medical review pack is optional research input and not a gate: a case that passes the generation gate (schema, Stage B, media, cross-modal rules) and the automated checks is labelled `GENERATED_PRACTICE_ONLY` and may be used for practice, never for assessment until separately approved. "
                      "No reviewer has seen these cases, so the decision column says so; a clinician who reads them can record a decision here and approve a case by the usual route.", "",
                      "| Case | Family | Spine (story kept) | Blocks | Generation gate (Stage B and the rest) | Golden correct path (end state / result) | First problems of the case |", "|---|---|---|---|---|---|---|"] + rows + ["",
                      "Reviewer decision for every row: none recorded (optional under ADR-0008). Status: GENERATED_PRACTICE_ONLY. Assessment use: not allowed.", ""])


def check() -> int:
    from composer import blocks as B
    from generation import package
    cases = B.load_cases()
    bad = 0
    for path in sorted(OUT.glob("*.package.json")):
        p = json.loads(path.read_text(encoding="utf-8"))
        report = package.validate(p, cases)
        if not report["passed"]:
            bad += 1
            print(f"FAIL {path.name}: {report['defects'][:2]}")
    names = {p.name.replace(".package.json", "") for p in OUT.glob("*.package.json")}
    for n in sorted(names):
        for ext in (".actions.json", ".expected.json"):
            if not (OUT / (n + ext)).exists():
                bad += 1
                print(f"FAIL {n}{ext} is missing")
    if len(names) != sum(len(v) for v in PICK.values()):
        bad += 1
        print(f"FAIL the set holds {len(names)} golden runs, expected {sum(len(v) for v in PICK.values())}")
    print("generated golden snapshot: " + ("OK" if not bad else f"{bad} problem(s)"))
    return 1 if bad else 0


if __name__ == "__main__":
    mode = sys.argv[1] if len(sys.argv) > 1 else "check"
    if mode == "make":
        make()
        REPORT.parent.mkdir(parents=True, exist_ok=True)
        REPORT.write_text(report_text(), encoding="utf-8", newline="\n")
    elif mode == "report":
        REPORT.parent.mkdir(parents=True, exist_ok=True)
        REPORT.write_text(report_text(), encoding="utf-8", newline="\n")
        print("wrote", REPORT.relative_to(ROOT).as_posix())
    else:
        raise SystemExit(check())
