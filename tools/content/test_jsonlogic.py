"""Tests for the Python twin of the rule evaluator (p0-clinical-logic-s02, T-JL1): the same vectors as the C# tests, and the content check that uses it.

    uv run --project tools tools/content/test_jsonlogic.py
"""
from __future__ import annotations

import copy
import json
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

import jsonlogic  # noqa: E402
from checks import engine_rule_keys, workflow_rule_problems  # noqa: E402

ROOT = Path(__file__).resolve().parents[2]
VECTORS = json.loads((ROOT / "content" / "rules" / "vectors.json").read_text(encoding="utf-8"))
KEYS = engine_rule_keys(ROOT / "src" / "ProcSense.Core" / "Workflow" / "CaseGraphSubsystem.cs")


class Vectors(unittest.TestCase):
    def test_every_vector_gives_the_expected_answer(self):
        self.assertGreaterEqual(len(VECTORS["vectors"]), 150)
        for v in VECTORS["vectors"]:
            with self.subTest(v["name"]):
                jsonlogic.compile_rule(v["rule"])
                got = jsonlogic.evaluate(v["rule"], v["data"])
                self.assertEqual(got, v["expect"], v["name"])
                self.assertIs(type(got) is bool, type(v["expect"]) is bool, v["name"])   # true is not 1

    def test_every_error_vector_fails_in_the_stated_way(self):
        self.assertGreaterEqual(len(VECTORS["errors"]), 10)
        for v in VECTORS["errors"]:
            with self.subTest(v["name"]):
                with self.assertRaises(jsonlogic.RuleError) as ctx:
                    jsonlogic.compile_rule(v["rule"])
                    jsonlogic.evaluate(v["rule"], v.get("data", {}))
                self.assertEqual(ctx.exception.kind, v["error"], v["name"])


class WorkflowRules(unittest.TestCase):
    DOC = json.loads((ROOT / "content" / "workflow" / "workflow.json").read_text(encoding="utf-8"))

    def test_the_engine_keys_are_found_in_the_source(self):
        self.assertTrue({"flags", "topics", "scenario", "identity_confirmed", "airway_total", "fasting"} <= KEYS, sorted(KEYS))

    def test_the_real_workflow_has_no_problems(self):
        self.assertEqual([], workflow_rule_problems(self.DOC, KEYS))

    def test_a_variable_the_engine_does_not_provide_is_caught(self):
        doc = copy.deepcopy(self.DOC)
        doc["goals"][0]["complete_if"] = {"in": ["x", {"var": "flagz"}]}
        found = workflow_rule_problems(doc, KEYS)
        self.assertEqual(1, len(found))
        self.assertIn("flagz", found[0])

    def test_an_unknown_operator_and_a_wrong_argument_count_are_caught(self):
        doc = copy.deepcopy(self.DOC)
        doc["goals"][0]["complete_if"] = {"contains": ["a", "b"]}
        doc["goals"][1]["complete_if"] = {"==": [1]}
        found = workflow_rule_problems(doc, KEYS)
        self.assertEqual(2, len(found))
        self.assertIn("unknown operator", found[0])
        self.assertIn("argument", found[1])

    def test_a_computed_variable_name_is_caught(self):
        doc = copy.deepcopy(self.DOC)
        doc["blockers"][0]["blocked_if"] = {"var": {"var": "case"}}
        self.assertIn("cannot know in advance", workflow_rule_problems(doc, KEYS)[0])


class Semantics(unittest.TestCase):
    def test_strict_types_and_null_rules(self):
        e = jsonlogic.evaluate
        self.assertIs(e({"==": [1, "1"]}), False)
        self.assertIs(e({"==": [True, 1]}), False)
        self.assertIs(e({"<": [{"var": "x"}, 3]}, {}), False)
        self.assertIs(e({"some": [[], {"var": ""}]}), False)
        with self.assertRaises(jsonlogic.RuleError):
            e({"<": ["a", 3]})


if __name__ == "__main__":
    unittest.main(verbosity=1)
