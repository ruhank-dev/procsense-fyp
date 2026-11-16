"""The examination staff-dialogue policy (owner decision D3), without a database: who counts as spoken to, which roles exist, and that every line is examination-safe."""
from __future__ import annotations

import json

from server.dialogue import exam_policy

INTENTS = json.loads((exam_policy.POLICY_FILE.parent / "intents.json").read_text(encoding="utf-8"))["roles"]


def test_a_role_is_addressed_only_by_its_own_words_as_whole_words():
    assert exam_policy.addressed("nurse", "Nurse, what are the observations?")
    assert exam_policy.addressed("anaesthesia", "Could the anaesthetist come and see her?")
    assert not exam_policy.addressed("nurse", "Where is the pain?")
    assert not exam_policy.addressed("nurse", "nursery rhymes")
    assert not exam_policy.addressed("anaesthesia", "Nurse, please help.")


def test_only_policy_roles_speak_and_every_intent_has_a_safe_line():
    assert exam_policy.roles(["nurse", "anaesthesia", "surgeon"]) == ["nurse", "anaesthesia"]
    for role in ("nurse", "anaesthesia"):
        lines = exam_policy.lines(role)
        assert set(lines) == set(INTENTS[role])
        for line in lines.values():
            low = line.lower()
            assert not any(w in low for w in ("consent", "fasting", "allerg", "sign in", "concern", "hint", "score"))
