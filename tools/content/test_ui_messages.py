"""Tests for the UI message table validator (P3-API-07)."""
from __future__ import annotations

import copy
import json
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import validate_ui_messages as v  # noqa: E402


class UiMessages(unittest.TestCase):
    def setUp(self):
        self.table = json.loads(v.TABLE.read_text(encoding="utf-8"))

    def test_the_shipped_table_passes(self):
        self.assertEqual(v.problems(), [])

    def test_the_server_codes_are_found_in_the_source(self):
        codes = v.server_codes()
        for c in ("pairing_required", "wrong_code", "authorization_pending", "expired_token", "engine_version_unsupported", "content_version_unsupported", "not_signed_in"):
            self.assertIn(c, codes)

    def test_a_server_code_without_a_message_fails(self):
        self.assertIn("ui_messages: no message for code brand_new_error", v.problems(self.table, v.server_codes() | {"brand_new_error"}))

    def test_a_duplicate_an_unknown_code_and_an_empty_text_fail(self):
        t = copy.deepcopy(self.table)
        t["messages"].append(dict(t["messages"][0]))
        t["messages"].append({"code": "made_up", "group": "lan", "title": "x", "message": "x", "action": ""})
        probs = v.problems(t)
        self.assertIn(f"ui_messages: {t['messages'][0]['code']} appears twice", probs)
        self.assertIn("ui_messages: made_up is neither a server code nor a client code", probs)
        self.assertIn("ui_messages.made_up: action is empty", probs)

    def test_a_measurement_fails(self):
        t = copy.deepcopy(self.table)
        t["messages"][0]["message"] = "Give 5 mg now."
        self.assertTrue(any("measurement" in p for p in v.problems(t)))


if __name__ == "__main__":
    unittest.main()
