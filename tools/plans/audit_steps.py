"""A mechanical audit of every P0, P1 and P2 step against the repository (`uv run --project tools tools/plans/audit_steps.py [--write|--check]` -> docs/plans/step-audit.md).

For each P0, P1 and P2 step the page states, from files only: the tick on the step file, the reconciliation status, whether the step claims implementation complete, how many evidence paths it names and whether one of them is a TEST or a RESULT (a path under tests/, tools/ci/, a *Tests.cs / test_*.py file or reports/) that is run by CI (the dotnet test project, the Python server tests, tools/ci/run_all.py, the Unity EditMode tests), the external items that cover it, and the
file names its "Files changed" section lists that do not exist (planning-time names, informational). A step that claims implementation complete (DONE, or ENG_DONE with implementation: complete) and names no test or result that CI runs is a FINDING; so is a tick on a step whose status does not allow it, and an external item missing for a step that needs one. The page is a check of the reconciliation, not a replacement for reading the evidence: it cannot say that a test is any good.
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "plans"))
import external_testing as XT  # noqa: E402
import reconcile as R  # noqa: E402

OUT = ROOT / "docs" / "plans" / "step-audit.md"
TESTISH = re.compile(r"(^tests/|^tools/ci/|Tests?\.cs$|(^|/)test_[^/]*\.py$|^reports/|^server/tests/|^unity/Assets/Tests/|\.expected\.json$)")


def ci_run(path: str, run_all: str) -> bool:
    """Whether CI runs or reads this path: a Python test named in run_all.py, a dotnet test (all of tests/ProcSense.Core.Tests), the server tests (all of server/tests), the Unity tests (run locally in batch mode, not by GitHub), or a result file that a registered check reads."""
    if path.startswith("tests/ProcSense.Core.Tests/") or path.startswith("server/tests/"):
        return True
    name = Path(path).name
    if name in run_all:
        return True
    if path.startswith("reports/") or path.startswith("tests/"):
        return path.split("/")[0] in ("reports", "tests") and Path(path).name in run_all or path.startswith("tests/golden") or path.startswith("tests/") and any(path.startswith(p) for p in ("tests/classifier", "tests/orders", "tests/action_bench", "tests/fixtures"))
    return False


_TEST_FILES: list[tuple[str, str]] | None = None


def referenced_by_a_test(path: str) -> list[str]:
    """Test files (CI's Python tests, the dotnet and server tests) whose text names this evidence file or its folder by file name: a cheap sign that something tests it. Names shorter than 5 characters are ignored."""
    global _TEST_FILES
    if _TEST_FILES is None:
        files = []
        for pat in ("tools/**/test_*.py", "tools/ci/*.py", "server/tests/*.py", "tests/ProcSense.Core.Tests/*.cs", "unity/Assets/Tests/**/*.cs"):
            for f in ROOT.glob(pat):
                files.append((f.relative_to(ROOT).as_posix(), f.read_text(encoding="utf-8", errors="replace")))
        _TEST_FILES = files
    stem = Path(path.rstrip("/")).stem if "." in Path(path).name else Path(path.rstrip("/")).name
    if len(stem) < 5:
        return []
    return [name for name, text in _TEST_FILES if stem in text][:3]


def render() -> tuple[str, list[str]]:
    found, data = R.steps(), json.loads(R.DATA.read_text(encoding="utf-8"))
    entries = R.entries_of(found, data)
    ext = XT.all_items(json.loads(XT.DATA.read_text(encoding="utf-8")))
    covered: dict[str, list[str]] = {}
    for i in ext:
        for s in i["steps"]:
            covered.setdefault(s, []).append(i["id"])
    run_all = (ROOT / "tools" / "ci" / "run_all.py").read_text(encoding="utf-8") + (ROOT / "tools" / "ci" / "repo_checks.py").read_text(encoding="utf-8")
    findings: list[str] = []
    rows = []
    for sid in sorted(found):
        e, f = entries[sid], found[sid]
        text = (ROOT / "docs" / "plans" / f["file"]).read_text(encoding="utf-8")
        m = re.search(r"## Files changed\n(.*?)(?=\n## |\Z)", text, re.S)
        listed = re.findall(r"`([^`\s]+/[^`\s]*)`", m.group(1)) if m else []
        stale = [p for p in listed if not (ROOT / p.rstrip("/")).exists() and not any(c in p for c in "*<{")]
        ev = e.get("evidence", [])
        tests = [p for p in ev if TESTISH.search(p)]
        run = [p for p in tests if ci_run(p, run_all)]
        complete = R.implementation_complete(e)
        flags = []
        found_tests = sorted({t for p in ev for t in referenced_by_a_test(p)}) if complete and not run else []
        note = e.get("ci_note", "")
        if complete and not run and not found_tests and not note:
            flags.append("claims implementation complete but names no test or result that CI runs, no test file names its evidence, and gives no `ci_note`")
        if f["approved"] and e["now"] not in ("APPROVED", "NOT_RUHAN", "DONE", "ENG_DONE"):
            flags.append(f"ticked but status {e['now']}")
        if e["now"] in ("ENG_DONE", "PARTLY", "BLOCKED", "DONE_GATE_OFF", "RESEARCH_NEGATIVE") and sid not in covered:
            flags.append("needs an outside actor but is in no external item")
        if e["now"] == "NOT_RUHAN" and not f["approved"] and sid not in covered:
            flags.append("not Ruhan's, not ticked, in no external item")
        if flags:
            findings += [f"{sid}: {x}" for x in flags]
        rows.append((sid, "yes" if f["approved"] else "no", e["now"], "yes" if complete else "no", len(ev), f"{len(run)}/{len(tests)}" + (f" (+ named in {', '.join(found_tests[:2])})" if found_tests else "") + (f" (note: {note})" if note else ""), ", ".join(covered.get(sid, [])) or "-", len(stale), "; ".join(flags)))
    n = len(rows)
    L = [f"# Mechanical audit of the {n} steps", "",
         "_Generated by `tools/plans/audit_steps.py`; do not edit by hand. It checks the reconciliation against files only (see the tool's docstring); it does not read the evidence or judge a test. 'Tests run/named' counts evidence paths that are tests or results and are run or read by CI. 'Stale names' counts file names in the step's own \"Files changed\" section that do not exist: planning-time names, shown for information._", "",
         f"* Steps: **{n}**. Findings: **{len(findings)}**.", "",
         "| Step | Ticked | Status | Implementation complete | Evidence paths | Tests run/named | External items | Stale names | Finding |", "|---|---|---|---|---|---|---|---|---|"]
    L += [f"| {s} | {t} | {st} | {c} | {ne} | {tr} | {x} | {sn} | {fl} |" for s, t, st, c, ne, tr, x, sn, fl in rows]
    return "\n".join(L) + "\n", findings


def main(argv: list[str]) -> int:
    text, findings = render()
    if "--check" in argv:
        ok = OUT.exists() and OUT.read_text(encoding="utf-8") == text
        print("step audit page " + ("current" if ok else "OUT OF DATE: run tools/plans/audit_steps.py --write") + f"; {len(findings)} finding(s)")
        for f in findings:
            print("  " + f)
        return 0 if ok and not findings else 1
    if "--write" in argv:
        OUT.write_text(text, encoding="utf-8", newline="\n")
        print("wrote", OUT.relative_to(ROOT).as_posix())
    print(f"{len(findings)} finding(s)")
    for f in findings:
        print("  " + f)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
