"""Validate every file in content/ against its schema and the source register.

Usage (from repo root):  task content-validate
                    or:  uv run --project tools tools/content/validate.py
Exit code 0 = all valid, 1 = problems found.
"""
import sys
from pathlib import Path

from checks import case_problems, load_json, load_register, ref_problems, schema_errors

ROOT = Path(__file__).resolve().parents[2]
CONTENT = ROOT / "content"
REGISTER = ROOT / "docs" / "research" / "source_register.csv"


def main() -> int:
    register = load_register(REGISTER)
    case_schema = load_json(CONTENT / "schema" / "case.schema.json")
    failed = False

    for path in sorted((CONTENT / "cases").glob("*.json")):
        doc = load_json(path)
        problems = schema_errors(doc, case_schema) + case_problems(doc, register)
        failed |= _report(path.stem, problems, f"{len(doc.get('refs', {}))} refs")

    for name in ("devices", "behaviour", "history_topics"):
        path = CONTENT / f"{name}.json"
        if path.exists():
            failed |= _report(name, ref_problems(load_json(path), register, name), "refs ok")

    print(f"register: {len(register)} rows")
    return 1 if failed else 0


def _report(name: str, problems: list[str], ok_detail: str) -> bool:
    if problems:
        print(f"{name}: INVALID ({len(problems)} problems)")
        for p in problems:
            print(f"  - {p}")
        return True
    print(f"{name}: valid, {ok_detail}, 0 missing refs")
    return False


if __name__ == "__main__":
    sys.exit(main())
