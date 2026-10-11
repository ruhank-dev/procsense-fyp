"""Runs the golden correct session through the CLI, makes the report from its log and checks the facts against content/schema/debrief_facts.schema.json (p0-scoring-debrief s04 and s05, plan tests T-SCO1 and T-SCO5).

    uv run --project tools tools/ci/check_report.py

Also checks the page: one self-contained file (no scripts, no external links), every item sentence shows its rule and source, and every item links to at least one log entry.
"""
from __future__ import annotations

import json
import re
import subprocess
import sys
import tempfile
from pathlib import Path

import jsonschema

ROOT = Path(__file__).resolve().parents[2]
CLI = ["dotnet", "run", "--no-build", "-c", "Release", "--project", "src/ProcSense.Cli", "--"]


def run(args: list[str]) -> str:
    p = subprocess.run(CLI + args, cwd=ROOT, capture_output=True, text=True, encoding="utf-8", errors="replace")
    if p.returncode != 0:
        raise SystemExit("FAIL  " + " ".join(args[:1]) + ": " + (p.stdout + p.stderr).strip()[-400:])
    return p.stdout


def main() -> int:
    schema = json.loads((ROOT / "content" / "schema" / "debrief_facts.schema.json").read_text(encoding="utf-8"))
    with tempfile.TemporaryDirectory() as tmp:
        out = str(Path(tmp) / "run.json")
        run(["run", "--case", "VAR-S1-A", "--seed", "42", "--actions", "tests/golden/skeleton1_correct.actions.json", "--out", out])
        html, js = str(Path(tmp) / "report.html"), str(Path(tmp) / "report.json")
        run(["report", "--log", str(Path(tmp) / "run.session.jsonl"), "--out", html, "--json", js])
        facts = json.loads(Path(js).read_text(encoding="utf-8"))
        page = Path(html).read_text(encoding="utf-8")
    errors = sorted(jsonschema.Draft202012Validator(schema).iter_errors(facts), key=lambda e: list(e.path))
    problems = [f"{'/'.join(map(str, e.path))}: {e.message}" for e in errors]
    if not facts["items"]:
        problems.append("the golden run produced no items")
    for i in facts["items"]:
        if not i["evidence"]:
            problems.append(f"{i['id']} has no log entry behind it")
        if i["rule_id"] not in i["sentence"] or i["source"] not in i["sentence"]:
            problems.append(f"the sentence for {i['id']} does not show its rule and source")
    if re.search(r"<script|https?://|<link|<img", page):
        problems.append("the report page must be one file with no scripts or external links")
    if problems:
        print(f"FAIL  {len(problems)} report problem(s)")
        for p in problems[:30]:
            print("  - " + p)
        return 1
    print(f"report facts valid ({len(facts['items'])} items, {len(facts['timeline'])} timeline entries) and the page is self-contained")
    return 0


if __name__ == "__main__":
    sys.exit(main())
