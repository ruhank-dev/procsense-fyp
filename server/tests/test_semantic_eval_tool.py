"""The budget guard of the semantic evaluation tool (tools/bench/semantic_eval.py): a live run is capped, stops at the first quota or billing signal, reuses one reading per row and never sends more once stopped. No network."""
from __future__ import annotations

import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools" / "bench"))
from server.interpret.llm.provider import MockProvider, ProviderResult  # noqa: E402
import semantic_eval as SE  # noqa: E402


def test_one_call_per_row_and_a_hard_cap():
    mp = MockProvider(["{}"])
    b = SE.BudgetedProvider(mp, 2)
    for key in ("a", "a", "b", "c", "d"):
        r = b.complete("s", "u", key=key)
    assert len(mp.calls) == 2 and b.calls == 2 and b.stopped == "MAX_CALLS" and r.error == "NOT_CONFIGURED"        # a, b were asked; the repeat of a came from memory; c and d were refused


def test_a_quota_or_billing_signal_stops_everything_at_once():
    for status in (401, 402, 403, 429):
        mp = MockProvider([ProviderResult(error=f"HTTP_{status}", status=status), "{}"])
        b = SE.BudgetedProvider(mp, 50)
        assert b.complete("s", "u", key="a").error == f"HTTP_{status}"
        assert b.stopped == f"BLOCKER_HTTP_{status}"
        assert b.complete("s", "u", key="b").error == "NOT_CONFIGURED" and len(mp.calls) == 1


def test_failures_are_not_remembered_so_a_transient_error_can_be_read_again():
    mp = MockProvider([ProviderResult(error="TIMEOUT"), "{}"])
    b = SE.BudgetedProvider(mp, 5)
    assert b.complete("s", "u", key="a").error == "TIMEOUT"
    assert b.complete("s", "u", key="a").error == "" and len(mp.calls) == 2


def test_a_retry_is_a_new_call_not_the_remembered_first_reply():
    mp = MockProvider([ProviderResult(text="```x```", done_reason="stop"), ProviderResult(text="{}", done_reason="stop")])
    b = SE.BudgetedProvider(mp, 5)
    assert b.complete("s", "Utterance: x", key="a").text == "```x```"
    assert b.complete("s", "Utterance: x", key="a").text == "```x```" and len(mp.calls) == 1            # another condition of the same row reuses the first reading
    assert b.complete("s", "Utterance: x\nYour previous answer was rejected", key="a").text == "{}" and len(mp.calls) == 2


def test_a_token_cap_stops_a_live_run():
    mp = MockProvider([ProviderResult(text="{}", done_reason="stop", prompt_tokens=800, output_tokens=300)])
    b = SE.BudgetedProvider(mp, 50, max_tokens=2000)
    for k in ("a", "b", "c", "d"):
        b.complete("s", "u", key=k)
    assert len(mp.calls) == 2 and b.stopped == "TOKEN_CAP"
