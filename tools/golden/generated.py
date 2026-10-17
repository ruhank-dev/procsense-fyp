"""Golden runs of generated cases (p1-live-scenario-generation-s11, stage LG-16 to LG-18).

    uv run --project tools tools/golden/generated.py make     # (re)make the snapshot in tests/golden_generated: packages, scripts, then `procsense-sim golden --update` writes the expected hashes
    uv run --project tools tools/golden/generated.py check    # the snapshot still passes the generation gate (schema, Stage B, media, cross-modal) and the sealed hashes hold

The set is fixed: six generated cases (three of each family, each of a different approved case) played on the correct path of the case they keep the story of, with the checklist allergy answer following the
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
PICK = {"S1": ("VAR-S1-G", "VAR-S1-H", "VAR-S1-D"), "S2": ("VAR-S2-G", "VAR-S2-B", "VAR-S2-F")}      # spines with a correct-path script, one set per family


def make() -> None:
    from composer import blocks as B
    from generation import objectives as ob, package, pipeline, retrieve
    cases = B.load_cases()
    ctx = pipeline.Context(retrieve.build_index(), cases, ob.Tables.load())
    OUT.mkdir(parents=True, exist_ok=True)
    for old in OUT.glob("*"):
        old.unlink()
    for scenario, spines in PICK.items():
        for spine in spines:
            seed = 0
            while True:                                                       # the first seed whose composed case keeps this spine's story
                r = pipeline.generate({"version": 1, "trainee": "trainee-001", "mode": "practice", "scenario": scenario, "level": "advanced", "seed": seed}, [], [], ctx,
                                      recent_fingerprints=[])
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
    else:
        raise SystemExit(check())
