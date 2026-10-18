"""Tick steps whose documented criteria are met: `uv run --project tools tools/plans/tick.py [--apply] [STEP_ID ...]` (owner instruction, 18 October 2026).

A tick is the leading check mark in a step file's name. By the owner's instruction of 18 October 2026 it is a status marker the engineer sets when the step's own verification is satisfied by evidence in the repository; it
is not a request for a manual approval. A step is tickable here only if docs/plans/reconciliation.json says DONE (not ENG_DONE, PARTLY, BLOCKED, DONE_GATE_OFF or RESEARCH_NEGATIVE: those have an open external part),
every evidence path exists, and it is not ticked yet. With no ids, every tickable step is listed (and ticked with --apply). Ticking renames the file (git mv) and rewrites the links to it in the tracked
text files; regenerate the plan index (tools/plans/milestones.py) and the reconciliation page afterwards. It never unticks a step.
"""
from __future__ import annotations

import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "plans"))
import json  # noqa: E402

import reconcile as R  # noqa: E402

TEXT = (".md", ".json", ".py", ".yml", ".yaml", ".txt", ".csv")
SKIP = ("docs/previous_prompt.md",)


def tickable() -> list[str]:
    found, data = R.steps(), json.loads(R.DATA.read_text(encoding="utf-8"))
    if R.problems(found, data):
        raise SystemExit("reconciliation has problems: run tools/plans/reconcile.py")
    return [s for s in sorted(found) if data["steps"][s]["now"] == "DONE" and not found[s]["approved"]]


def link_forms(old: str) -> tuple[str, str]:
    return old, "✅ " + old


def rewrite_links(old_name: str, folder: str) -> int:
    """Point every mention of folder/old_name at the ticked name (%20 inside a markdown link)."""
    files = subprocess.run(["git", "ls-files"], cwd=ROOT, capture_output=True, text=True, encoding="utf-8").stdout.splitlines()
    n = 0
    for f in files:
        if not f.endswith(TEXT) or f in SKIP or "unity/Assets" in f:
            continue
        p = ROOT / f
        try:
            text = p.read_text(encoding="utf-8")
        except (UnicodeDecodeError, OSError):
            continue
        needle = f"steps/{old_name}"
        if needle not in text:
            continue
        new = text.replace(needle + ")", f"steps/✅%20{old_name})").replace(needle, f"steps/✅ {old_name}")
        if new != text:
            p.write_text(new, encoding="utf-8", newline="")
            n += 1
    return n


def tick(step: str, found: dict) -> None:
    rel = found[step]["file"]
    old = ROOT / "docs" / "plans" / rel
    new = old.with_name("✅ " + old.name)
    subprocess.run(["git", "mv", str(old), str(new)], cwd=ROOT, check=True)
    rewrite_links(old.name, "")


def main(argv: list[str]) -> int:
    apply = "--apply" in argv
    ids = [a for a in argv if not a.startswith("--")]
    can = tickable()
    pick = ids or can
    bad = [s for s in pick if s not in can]
    if bad:
        print("not tickable (not DONE, already ticked, or unknown): " + ", ".join(bad))
        return 1
    found = R.steps()
    for s in pick:
        print(("ticking " if apply else "would tick ") + s)
        if apply:
            tick(s, found)
    print(f"{len(pick)} step(s) {'ticked' if apply else 'tickable'}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
