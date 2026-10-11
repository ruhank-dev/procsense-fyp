"""Stage B validator: the ordered checks every case must pass before a trainee can play it (p0-case-library s04, guide B-M2).

    uv run --project tools tools/composer/stage_b.py --all                 # every case file in content/case_files
    uv run --project tools tools/composer/stage_b.py --case VAR-S1-A       # one case
    uv run --project tools tools/composer/stage_b.py --all --headless      # also the idle run (check 9) and the golden run (check 10) through the CLI

Checks, in order: 1 schema, 2 required fields, 3 constraints, 4 contradictions (12 types, content/contradictions.json), 5 combinations and status, 6 difficulty, 7 event eligibility, 8 every result is of an orderable test,
9 five-minute idle run, 10 golden correct run, 11 status of an AI draft. Every defect is collected (nothing is lost to stopping early) and each case gets an HTML report in build/stage_b/.
Hand-made cases, cases composed by the solver and cases proposed by AI all go through this one gate. It never approves anything: approval is a person's.
"""
from __future__ import annotations

import html
import json
import sys
from dataclasses import dataclass, field
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(HERE.parent))
sys.path.insert(0, str(ROOT / "tools" / "content"))

from composer.checks import Defect  # noqa: E402
from composer.checks import base  # noqa: E402
from composer.checks.c01_to_c03 import c01_schema, c02_required, c03_constraints  # noqa: E402
from composer.checks.c04_contradictions import c04_contradictions  # noqa: E402
from composer.checks.c05_to_c11 import c05_status, c06_difficulty, c07_events, c08_results, c09_idle, c10_golden, c11_ai_status  # noqa: E402

CHECKS = [
    (1, "schema", c01_schema), (2, "required fields", c02_required), (3, "constraints", c03_constraints), (4, "contradictions", c04_contradictions), (5, "combinations and status", c05_status),
    (6, "difficulty", c06_difficulty), (7, "event eligibility", c07_events), (8, "results and orderable tests", c08_results), (9, "idle run", c09_idle), (10, "golden run", c10_golden), (11, "AI status", c11_ai_status),
]


@dataclass
class Report:
    case: str
    defects: list[Defect] = field(default_factory=list)

    @property
    def passed(self) -> bool:
        return not self.defects

    def codes(self) -> set[str]:
        return {d.code for d in self.defects}

    def as_dict(self) -> dict:
        return {"case": self.case, "pass": self.passed, "defects": [d.as_dict() for d in self.defects]}


def load_ctx(headless: bool = False, cases: list[dict] | None = None) -> dict:
    import case_file_checks
    ctx = case_file_checks.load_context()
    ctx["investigations"] = json.loads((ROOT / "content" / "investigations" / "investigations.json").read_text(encoding="utf-8"))["tests"]
    ctx["headless"] = headless
    ctx["others"] = cases or load_cases()
    return ctx


def load_cases() -> list[dict]:
    return [json.loads(p.read_text(encoding="utf-8")) for p in sorted((ROOT / "content" / "case_files").glob("*.json"))]


def validate(card: dict, ctx: dict) -> Report:
    report = Report(card.get("id", "?"))
    for _, _, fn in CHECKS:
        report.defects += fn(card, ctx)
    return report


def html_report(r: Report) -> str:
    rows = "".join(f"<tr><td>{d.check}</td><td>{html.escape(d.code)}</td><td>{html.escape(d.detail)}</td></tr>" for d in r.defects)
    body = "<p><b>PASS</b>: no defects.</p>" if r.passed else f"<p><b>FAIL</b>: {len(r.defects)} defect(s).</p><table border=\"1\" cellpadding=\"4\"><tr><th>Check</th><th>Code</th><th>Detail</th></tr>{rows}</table>"
    names = "".join(f"<li>{n}. {html.escape(t)}</li>" for n, t, _ in CHECKS)
    return f"<!doctype html><html><head><meta charset=\"utf-8\"><title>Stage B: {html.escape(r.case)}</title></head><body><h1>Stage B report: {html.escape(r.case)}</h1>{body}<h2>Checks run</h2><ol>{names}</ol></body></html>\n"


def main(argv: list[str]) -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    cases = load_cases()
    ctx = load_ctx("--headless" in argv, cases)
    wanted = [a for i, a in enumerate(argv) if i > 0 and argv[i - 1] == "--case"]
    todo = cases if "--all" in argv or not wanted else [c for c in cases if c["id"] in wanted]
    out = ROOT / "build" / "stage_b"
    out.mkdir(parents=True, exist_ok=True)
    failed = 0
    for c in todo:
        r = validate(c, ctx)
        (out / f"{c['id']}.html").write_text(html_report(r), encoding="utf-8", newline="\n")
        if not r.passed:
            failed += 1
            print(f"FAIL  {c['id']}")
            for d in r.defects[:12]:
                print(f"  - [{d.check}] {d.code}: {d.detail}")
    print(f"{len(todo) - failed}/{len(todo)} cases pass Stage B (reports in build/stage_b/)")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
