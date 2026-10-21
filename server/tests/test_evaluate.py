"""The Gemma speech evaluator (p1-ai-language-s22, ADR-0025) without a network or a database: the strict contract, the reader's bounded retries and fail-safe, the service answers, and the prompt's pins (version, approved rubric, original examples, no dataset leakage, no classifier on the path)."""
from __future__ import annotations

import json
import re
from pathlib import Path

import pytest

from server.evaluate import contract, examples, prompt as P, rubric, service
from server.evaluate.contract import Invalid, parse
from server.evaluate.provider import MockProvider, ProviderResult
from server.evaluate.reader import Reader

ROOT = Path(__file__).resolve().parents[2]
UTT = "Do you smoke at all?"


def reply(**over) -> str:
    act = {"objective": "ASK_TOPIC", "assertion": "performed", "actor": "trainee", "recipient": "patient", "form": "question", "performed_now": True, "relevance": 3, "correctness": 3, "completeness": 3, "evidence": "do you smoke", "rationale": "asks a history question"}
    act.update(over)
    return json.dumps({"acts": [act], "claims_physical_action": False, "physical_action": "", "critical_topic": False})


CTX = {"steps": [2], "eligible": [{"act": "ASK_TOPIC", "behaviour": "b01", "critical": False}], "patient_question_pending": False, "readback_pending": False, "last_turns": [], "verified_actions": []}


# ------------------------------------------------------------------ the contract
def test_a_valid_reply_parses():
    e = parse(reply(), "stop", UTT)
    assert e.acts[0].objective == "ASK_TOPIC" and e.acts[0].performed_now and not e.claims_physical_action


@pytest.mark.parametrize("bad,code", [("```json\n" + reply() + "\n```", "FENCED"), ("no json at all", "NOT_JSON"), ("Sure: " + reply(), "SURROUNDING_TEXT"), (reply(objective="MADE_UP"), "SCHEMA"), (reply(relevance=4), "SCHEMA"), (reply(relevance=True), "SCHEMA"),
                                      (reply(assertion="maybe"), "SCHEMA"), (reply(form="shout"), "SCHEMA"), (reply(evidence="something never said"), "SCHEMA"), (reply(assertion="negated"), "SCHEMA"), (reply(performed_now=False), "SCHEMA"),
                                      (reply(actor="other"), "SCHEMA"), (reply(evidence=""), "SCHEMA")])
def test_a_reply_that_breaks_the_contract_is_rejected_never_repaired(bad, code):
    with pytest.raises(Invalid) as e:
        parse(bad, "stop", UTT)
    assert e.value.code == code


def test_a_truncated_reply_extra_keys_and_a_repeated_objective_are_rejected():
    with pytest.raises(Invalid):
        parse(reply(), "length", UTT)
    with pytest.raises(Invalid):
        parse('{"acts":[],"claims_physical_action":false,"physical_action":"","critical_topic":false,"score":9}', "stop", UTT)
    two = json.dumps({"acts": [json.loads(reply())["acts"][0]] * 2, "claims_physical_action": False, "physical_action": "", "critical_topic": False})
    with pytest.raises(Invalid):
        parse(two, "stop", UTT)
    assert parse('{"acts":[],"claims_physical_action":false,"physical_action":"","critical_topic":false}', "stop", UTT).acts == []


def test_a_non_performed_assertion_with_consistent_flags_is_valid_and_carries_no_performance():
    e = parse(reply(assertion="intended_later", performed_now=False), "stop", "I will ask: do you smoke")
    assert not e.acts[0].performed_now and e.acts[0].assertion == "intended_later"


# ------------------------------------------------------------------ the reader
def test_the_reader_retries_a_format_rejection_once_then_succeeds():
    r = Reader(MockProvider(["```" + reply() + "```", reply()])).read(CTX, UTT)
    assert (r.status, r.attempts) == ("VALID", 2) and r.codes == ["FENCED"]


def test_the_reader_gives_up_after_two_rejections_and_never_repairs():
    r = Reader(MockProvider(["nonsense"])).read(CTX, UTT)
    assert (r.status, r.attempts, r.evaluation) == ("INVALID", 2, None)


def test_a_time_out_is_retried_but_a_blocking_status_is_not():
    r = Reader(MockProvider([ProviderResult(error="TIMEOUT", wall_ms=10), reply()])).read(CTX, UTT)
    assert (r.status, r.attempts) == ("VALID", 2)
    r = Reader(MockProvider([ProviderResult(error="HTTP_429", status=429)])).read(CTX, UTT)
    assert (r.status, r.attempts) == ("UNAVAILABLE", 1)
    r = Reader(MockProvider([RuntimeError("boom")])).read(CTX, UTT)
    assert r.status == "UNAVAILABLE" and r.codes[0].startswith("PROVIDER_RAISED")


def test_the_retry_prompt_names_the_problem_and_the_utterance_is_data():
    p = MockProvider(["```" + reply() + "```", reply()])
    Reader(p).read(CTX, UTT)
    assert "code fence" in p.calls[1]["user"] and json.dumps(UTT) in p.calls[0]["user"]


# ------------------------------------------------------------------ the service answers
def test_without_a_reader_the_answer_is_unavailable_with_the_menu_and_no_credit():
    out, outcome = service.evaluate(None, CTX, UTT, "u-1")
    assert (out["decision"], out["acts"], out["reasons"], outcome) == ("UNAVAILABLE", [], ["NOT_CONFIGURED"], None)
    assert any(m["behaviour"] == "b01" and m["actions"] for m in out["menu"])


def test_a_model_that_cannot_give_a_valid_judgement_is_unavailable_never_a_guess():
    out, _ = service.evaluate(Reader(MockProvider(["nonsense"])), CTX, UTT, "u-1")
    assert out["decision"] == "UNAVAILABLE" and out["acts"] == [] and out["reasons"][0] == "EVAL_INVALID"


def test_a_valid_judgement_is_returned_as_judged_with_no_score_field():
    out, _ = service.evaluate(Reader(MockProvider([reply()])), CTX, UTT, "u-7")
    assert out["decision"] == "EVALUATED" and out["utterance_id"] == "u-7" and out["acts"][0]["objective"] == "ASK_TOPIC"
    flat = json.dumps(out)
    assert "score" not in flat and "credit" not in flat and "confidence" not in flat


# ------------------------------------------------------------------ the prompt
def test_the_prompt_forces_the_semantic_order_and_names_every_approved_objective():
    s = P.full_system_prompt()
    order = [s.index(f"{n} {w}") for n, w in ((1, "WHO"), (2, "WHEN"), (3, "ASSERTION"), (4, "OBJECTIVE"), (5, "ONE OR SEVERAL"), (6, "GRADES"), (7, "PHYSICAL CLAIMS"), (8, "CRITICAL TOPICS"))]
    assert order == sorted(order)
    assert all(f"{o}:" in s for o in rubric.objective_ids())
    assert "NEVER know whether such an action really happened" in s


def test_the_rubric_rules_in_the_prompt_are_the_approved_annotation_rubric_verbatim():
    rules = [l for l in (ROOT / "tests" / "classifier_synthetic_v2" / "rubric.md").read_text(encoding="utf-8").splitlines() if re.match(r"^[1-9]\. ", l)]
    assert rubric.wording()["rules"] == rules and all(r in P.full_system_prompt() for r in rules)


def test_the_prompt_version_and_hash_are_pinned_so_a_change_must_be_re_evaluated():
    assert P.VERSION == "gemma-eval-5"
    assert P.prompt_hash().startswith(PINNED_PREFIX), "the prompt changed: re-run tools/synth_dev/evaluator_run.py, record the result in docs/research/gemma_evaluator_results.md, then update PINNED_PREFIX and the version"


def test_no_worked_example_is_a_development_or_held_out_set_row():
    from server.evaluate.contract import norm
    spoken = {norm(json.loads(l)["utterance"]) for d in ("synthetic_dev_v1", "synthetic_heldout_v1", "synthetic_heldout_v2") for f in [ROOT / "tests" / d / "dataset.jsonl"] if f.exists() for l in f.read_text(encoding="utf-8").splitlines() if l.strip()}
    for _, items in examples.GROUPS:
        for u, _e in items:
            n = norm(u.strip('"'))
            assert n not in spoken
            words = set(n.split())
            assert not any(len(words & set(s.split())) / max(1, len(words | set(s.split()))) >= 0.7 for s in spoken), u


def test_the_prompt_carries_the_second_objective_test_and_the_critical_precedence():
    s = P.full_system_prompt()
    for phrase in ("SECOND-OBJECTIVE TEST", "never ASK_TOPIC", "SEPARATE clause", "performs no objective", "Never drop a genuine ordinary objective"):
        assert phrase in s, phrase


def test_the_evaluator_does_not_import_the_old_classifier_cap_or_gate_modules():
    for path in (ROOT / "server" / "evaluate").glob("*.py"):
        src = path.read_text(encoding="utf-8")
        assert not re.search(r"^\s*(from|import)\s+(server\.)?(classify|interpret)\b", src, re.M), path.name
        assert not re.search(r"critical_mass|critical_cue|EnsembleActClassifier|status_of", src), path.name


# ------------------------------------------------------------------ DT-247: longer natural speech
LONG = ("You will have a nerve block in the neck that numbs the shoulder, then the surgeon passes a small camera through two tiny cuts to repair the damaged tendon, and your arm goes into a sling afterwards, "
        "and we will check how you feel at every step along the way so that nothing comes as a surprise to you.")


def test_a_long_natural_utterance_may_be_quoted_whole_as_evidence():
    assert 160 < len(LONG) <= contract.MAX_UTTERANCE
    e = parse(reply(objective="EXPLAIN_PROCEDURE", evidence=LONG), "stop", LONG)
    assert e.acts[0].evidence == LONG and e.acts[0].performed_now


def test_evidence_is_still_grounded_and_still_bounded():
    with pytest.raises(Invalid) as e:
        parse(reply(evidence=LONG + " and something the speaker never said"), "stop", LONG)
    assert e.value.code == "SCHEMA"
    huge = "word " * 120                                      # 600 characters: beyond the longest utterance the route accepts
    with pytest.raises(Invalid):
        parse(reply(evidence=huge.strip()), "stop", huge)
    with pytest.raises(Invalid):
        parse(reply(rationale="x" * 221), "stop", UTT)           # the other text field keeps its own limit


def test_short_replies_are_judged_exactly_as_before():
    e = parse(reply(), "stop", UTT)
    assert (e.acts[0].objective, e.acts[0].evidence, e.acts[0].relevance) == ("ASK_TOPIC", "do you smoke", 3)


def test_the_evidence_cap_is_the_route_cap_and_the_engine_span_cap():
    schema = (ROOT / "server" / "schemas" / "evaluate.py").read_text(encoding="utf-8")
    assert f"utterance: str = Field(min_length=1, max_length={contract.MAX_UTTERANCE})" in schema
    engine = (ROOT / "src" / "ProcSense.Core" / "Speech" / "SpeechEvidenceSubsystem.cs").read_text(encoding="utf-8")
    assert f"public const int MaxSpan = {contract.MAX_EVIDENCE};" in engine


PINNED_PREFIX = "5cf37bc78515"
