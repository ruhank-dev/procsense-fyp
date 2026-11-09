"""Which checks does this change need?  (change-aware testing; guide: docs/qa/change_aware_testing.md)

    uv run --project tools python tools/ci/select.py [--base REF] [--head REF] [--staged] [--all] [--json] [--no-cache]

Default: what differs from HEAD in the working tree (modified, staged, deleted, untracked). --base REF compares REF...HEAD (plus the
working tree unless --head is given: what GitHub sees on a pull request). --staged is the index only (a pre-commit view). --all selects
everything. --json prints ONE object (read by .github/workflows/ci.yml):

  {"full": bool, "reason": str, "changed": [...],
   "jobs":   {"engine", "server", "repo_checks", "dependencies", "unity_compile", "unity_tests", "android_build": bool},
   "checks": {<name>: {"run": bool, "why": str, "cached": bool, "inputs": [...], "kind": "local"|"unity", "job": str, "heavy": bool}}}

Exit code 0 whenever a valid object was printed. Any error while selecting selects EVERYTHING (full = true) instead of skipping checks.
"""
from __future__ import annotations

import argparse
import json
import sys

import changeaware as ca


def build(args) -> dict:
    rules = ca.load_rules()
    try:
        if args.all:
            files, verdict = [], ca.select(rules, [], force_all=True)
        else:
            files = ca.changed_files(args.base, args.head, args.staged)
            verdict = ca.select(rules, files)
        if not args.no_cache:
            ca.mark_cached(verdict, rules, ca.file_hashes())
        return verdict
    except Exception as exc:   # never skip a check because the selector broke
        return ca.select(rules, [], force_all=True, all_reason=f"selection error, so everything runs: {exc}")


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--base"); ap.add_argument("--head")
    ap.add_argument("--staged", action="store_true"); ap.add_argument("--all", action="store_true")
    ap.add_argument("--json", action="store_true"); ap.add_argument("--no-cache", action="store_true")
    args = ap.parse_args(argv)
    v = build(args)
    if args.json:
        print(json.dumps(v, indent=2))
        return 0
    print(f"{len(v['changed'])} changed file(s); full run: {v['full']} ({v['reason']})")
    for n, c in v["checks"].items():
        state = "RUN   " if c["run"] and not c["cached"] else "REUSE " if c["run"] else "skip  "
        print(f"  {state}{n:<32}{' [heavy]' if c['heavy'] else '':9}{c['why']}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
