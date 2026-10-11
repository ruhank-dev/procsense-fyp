"""Run every check CI runs, on your machine, in the same order. Standard library only.

Usage (from repo root):  task ci     or     uv run --project tools tools/ci/run_all.py [--quick]
--quick skips the slow steps (build/test) when only docs or content changed.
Keep this list in step with .github/workflows/ci.yml and docs/CI.md (INSTRUCTIONS.md section 6b).
"""
import os
import shutil
import subprocess
import sys
import tempfile
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.stdout.reconfigure(encoding="utf-8")
CLI = ["dotnet", "run", "--no-build", "-c", "Release", "--project", "src/ProcSense.Cli", "--"]


def run(cmd: list[str], fail_if_in_output: str | None = None) -> tuple[bool, str]:
    p = subprocess.run(cmd, cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")
    out = (p.stdout or "") + (p.stderr or "")
    ok = p.returncode == 0 and not (fail_if_in_output and fail_if_in_output in out)
    return ok, out


def replay_round_trip() -> tuple[bool, str]:
    with tempfile.TemporaryDirectory() as tmp:
        out = str(Path(tmp) / "run.json")
        ok, text = run(CLI + ["run", "--case", "VAR-S1-A", "--seed", "42", "--actions", "tests/golden/skeleton1_correct.actions.json", "--out", out])
        if not ok:
            return False, text
        return run(CLI + ["replay", "--log", str(Path(tmp) / "run.session.jsonl")])


def preflight(quick: bool) -> list[str]:
    """Tools the checks need. A missing one is an error with an install command, never a silent skip."""
    need = {"uv": "winget install --id astral-sh.uv -e", "git": "winget install --id Git.Git -e"}
    if not quick:
        need["dotnet"] = "winget install --id Microsoft.DotNet.SDK.9 -e"
    return [f"{tool} is not installed or not on PATH -> {cmd} (then restart the terminal)" for tool, cmd in need.items() if not shutil.which(tool)]


def main(argv: list[str]) -> int:
    quick = "--quick" in argv
    missing = preflight(quick)
    if missing:
        print("FAIL  required tools missing; nothing was checked")
        for m in missing:
            print("        " + m)
        return 1
    steps: list[tuple[str, callable]] = []
    if not quick:
        steps += [
            ("build (warnings are errors)", lambda: run(["dotnet", "build", "ProcSense.sln", "-c", "Release", "-warnaserror"])),
            ("unit tests", lambda: run(["dotnet", "test", "ProcSense.sln", "--no-build", "-c", "Release"])),
            ("golden runs", lambda: run(CLI + ["golden", "--dir", "tests/golden"])),
            ("replay round trip", replay_round_trip),
        ]
        if os.name != "nt":  # Windows checkouts use CRLF; CI (Linux, LF) is the authority for formatting
            steps.append(("code format", lambda: run(["dotnet", "format", "ProcSense.sln", "--verify-no-changes"])))
        else:  # on Windows only the indentation/spacing rules are checked (line endings are CRLF here), so a format failure shows up before the push, not in CI
            steps.append(("code format (whitespace rules only)", lambda: (lambda r: (r[0] or "error WHITESPACE" not in r[1], r[1]))(
                run(["dotnet", "format", "whitespace", "ProcSense.sln", "--verify-no-changes"]))))
        steps.append(("no vulnerable packages", lambda: run(["dotnet", "list", "ProcSense.sln", "package", "--vulnerable", "--include-transitive"],
                                                          fail_if_in_output="has the following vulnerable packages")))
    steps += [
        ("content + source register", lambda: run(["uv", "run", "--project", "tools", "tools/content/validate.py"])),
        ("walkthrough extract counts", lambda: run(["uv", "run", "--project", "tools", "tools/content/extract_walkthrough.py", "--check"])),
        ("traceability up to date", lambda: run(["uv", "run", "--project", "tools", "tools/content/trace.py", "--check"])),
        ("rule evaluator twin tests (same vectors as the C# evaluator)", lambda: run(["uv", "run", "--project", "tools", "tools/content/test_jsonlogic.py"])),
        ("content tools tests", lambda: run(["uv", "run", "--project", "tools", "tools/content/test_content_tools.py"])),
        ("content tables verified against the book and up to date", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_tables.py", "--check"])),
        ("content table meaning tests", lambda: run(["uv", "run", "--project", "tools", "tools/content/test_tables.py"])),
        ("case content gaps list up to date", lambda: run(["uv", "run", "--project", "tools", "tools/content/case_gaps.py", "--check"])),
        ("images + reports valid (ultrasound, ECG, chest X-ray, CT, MRI, MRCP)", lambda: run(["uv", "run", "--project", "tools", "tools/content/validate_images.py"])),
        ("case files built, valid and up to date (53 cases: 17 P0, 16 P1, 20 P2)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_case_files.py", "--check"])),
        ("flow maps up to date (S1 and S2, 20 stages each)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_flow_maps.py", "--check"])),
        ("score tables up to date (AIR, AAS, Alvarado, TG18, ASGE, STOP-Bang, AKI)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_scores.py", "--check"])),
        ("imaging reports and categories up to date (51 findings, approved reports, 35 cases)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_imaging.py", "--check"])),
        ("consent content up to date (elements, questions, rules)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_consent.py", "--check"])),
        ("behaviour model up to date (attributes, transitions, modes, disclosure)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_behaviour.py", "--check"])),
        ("behaviour magnitude plots up to date (for the review pack)", lambda: run(["uv", "run", "--project", "tools", "tools/behaviour/plot.py", "--check"])),
        ("physiology model up to date (variables, couplings, noise)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_physio.py", "--check"])),
        ("end states up to date (5 states, delay items, surface-later facts)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_end_states.py", "--check"])),
        ("safety checklist up to date (24 items, expected-answer rules)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_checklist.py", "--check"])),
        ("handover items up to date (14 items, planned and emergency sets)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_handover.py", "--check"])),
        ("theatre readiness stations up to date (12 stations, faults)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_or.py", "--check"])),
        ("ward checklist up to date (13 items, verify rules)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_ward.py", "--check"])),
        ("deterioration clock and Usman timeline up to date", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_deterioration.py", "--check"])),
        ("decision outcomes and checks up to date (5 outcomes, 6 ordered checks, emergency signs)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_decision.py", "--check"])),
        ("clash table and medicine plans up to date (18 clashes, factors, plans)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_clashes.py", "--check"])),
        ("sepsis tiers and timers up to date (NG253 criteria, timers, infection cases)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_sepsis.py", "--check"])),
        ("problem types and ladder up to date (6 types, 7 rungs, P1 to P4)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_problems.py", "--check"])),
        ("critical result limits up to date (16 limits, conditions, actions)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_critical.py", "--check"])),
        ("normal results up to date (planned patient liver panel from the verified bands)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_normal_results.py", "--check"])),
        ("investigation indications up to date (17 tests, planned and emergency)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_indications.py", "--check"])),
        ("NEWS2 chart up to date (scale 1 and 2, response bands)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_news2.py", "--check"])),
        ("fasting table up to date (clear 2 h, milk 6 h, light meal 6 h, heavy meal 8 h)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_fasting.py", "--check"])),
        ("treatment library up to date (17 treatments, checks, doses only with a reviewer)", lambda: run(["uv", "run", "--project", "tools", "tools/content/build_treatments.py", "--check"])),
        ("case file tests", lambda: run(["uv", "run", "--project", "tools", "tools/content/test_case_files.py"])),
        ("EXP-DATA-2: generated lab values stay in their band (10,000 seeds)", lambda: run(["uv", "run", "--project", "tools", "tools/content/exp_data2.py"])),
        ("equipment sheets up to date (53 items)", lambda: run(["uv", "run", "--project", "tools", "tools/content/equipment_sheets.py", "--check"])),
        ("equipment sheet tests", lambda: run(["uv", "run", "--project", "tools", "tools/content/test_equipment.py"])),
        ("imaging library complete and up to date", lambda: run(["uv", "run", "--project", "tools", "tools/content/imaging_library.py", "--check"])),
        ("image review pack up to date", lambda: run(["uv", "run", "--project", "tools", "tools/content/image_review_pack.py", "--check"])),
        ("dataset register and cards up to date", lambda: run(["uv", "run", "--project", "tools", "tools/data/dataset_cards.py", "--check"])),
        ("image and dataset tests", lambda: run(["uv", "run", "--project", "tools", "tools/content/test_images.py"])),
        ("content manifest up to date", lambda: run(["uv", "run", "--project", "tools", "tools/content/content_manifest.py", "--check"])),
        ("medical review pack up to date", lambda: run(["uv", "run", "--project", "tools", "tools/content/review_pack.py", "--check"])),
        ("plan index up to date", lambda: run(["uv", "run", "--project", "tools", "tools/plans/milestones.py", "--check"])),
        ("project rules", lambda: run(["uv", "run", "--project", "tools", "tools/ci/repo_checks.py"])),
        ("pre-push hook behaves (isolated test repo)", lambda: run(["uv", "run", "--project", "tools", "tools/ci/test_pre_push_hook.py"])),
    ]
    results = []
    for name, step in steps:
        start = time.time()
        ok, out = step()
        results.append((name, ok))
        print(f"{'PASS' if ok else 'FAIL'}  {name}  ({time.time() - start:.0f}s)")
        if not ok:
            print("\n".join("        " + line for line in out.strip().splitlines()[-25:]))
    failed = [n for n, ok in results if not ok]
    print(f"\n{len(results) - len(failed)}/{len(results)} checks passed" + (f"; failed: {', '.join(failed)}" if failed else ""))
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
