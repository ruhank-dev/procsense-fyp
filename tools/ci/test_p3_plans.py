"""The P3 plan checker (tools/plans/p3_plans.py): the real plans pass and the index is current; a missing section, a wrong code, an unknown dependency and a cycle are each caught."""
import importlib.util
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location("p3_plans", ROOT / "tools" / "plans" / "p3_plans.py")
p3 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(p3)


def plan(name: str, code: str, rows: list[str], drop: int | None = None) -> str:
    parts = [f"# {name} — Test plan ({code})", ""]
    for i, s in enumerate(p3.SECTIONS):
        if i == drop:
            continue
        parts += [f"## {i + 1}. {s}", "", "text", ""]
        if s == "Implementation tasks":
            parts += ["| ID | Task | Owner | Depends on | Done when | Status |", "|---|---|---|---|---|---|"] + rows + [""]
    return "\n".join(parts)


class P3PlanTests(unittest.TestCase):
    def run_on(self, files: dict[str, str]) -> int:
        with tempfile.TemporaryDirectory() as t:
            d = Path(t)
            for n, text in files.items():
                (d / n).write_text(text, encoding="utf-8")
            old = (p3.P3, p3.INDEX)
            p3.P3, p3.INDEX = d, d / "p3-task-index.md"
            try:
                return p3.main([])
            finally:
                p3.P3, p3.INDEX = old

    def test_real_plans_pass_and_index_is_current(self):
        self.assertEqual(p3.main(["--check"]), 0)

    def test_good_plan_passes(self):
        rows = ["| P3-AAA-01 | First | Ruhan | — | x | Planned |", "| P3-AAA-02 | Second | Moiz | P3-AAA-01, p0-vr-platform-s07 | y | Planned |"]
        self.assertEqual(self.run_on({"p3-01-a.md": plan("p3-01-a", "AAA", rows)}), 0)

    def test_missing_section_is_caught(self):
        rows = ["| P3-AAA-01 | First | Ruhan | — | x | Planned |"]
        self.assertEqual(self.run_on({"p3-01-a.md": plan("p3-01-a", "AAA", rows, drop=12)}), 1)

    def test_wrong_code_and_owner_are_caught(self):
        rows = ["| P3-BBB-01 | First | Nobody | — | x | Planned |"]
        self.assertEqual(self.run_on({"p3-01-a.md": plan("p3-01-a", "AAA", rows)}), 1)

    def test_unknown_dependency_is_caught(self):
        rows = ["| P3-AAA-01 | First | Ruhan | P3-ZZZ-09 | x | Planned |"]
        self.assertEqual(self.run_on({"p3-01-a.md": plan("p3-01-a", "AAA", rows)}), 1)
        rows = ["| P3-AAA-01 | First | Ruhan | p0-nothing-s99 | x | Planned |"]
        self.assertEqual(self.run_on({"p3-01-a.md": plan("p3-01-a", "AAA", rows)}), 1)

    def test_cycle_is_caught(self):
        rows = ["| P3-AAA-01 | First | Ruhan | P3-AAA-02 | x | Planned |", "| P3-AAA-02 | Second | Ruhan | P3-AAA-01 | y | Planned |"]
        self.assertEqual(self.run_on({"p3-01-a.md": plan("p3-01-a", "AAA", rows)}), 1)


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    unittest.main(verbosity=1)
