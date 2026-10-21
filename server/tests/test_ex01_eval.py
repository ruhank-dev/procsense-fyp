"""The layered evaluation tool of the independent set (tools/bench/ex01_eval.py; ADR-0025): the metrics, the separation of a failure of understanding from a rejection by the engine, and the refusals (a set that is not frozen, a configuration that was not frozen first or changed,
a second scored run). No model, no engine process: a scripted provider and a stand-in engine scorer."""
from __future__ import annotations

import json
import sys
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
sys.path.insert(0, str(ROOT / "tools" / "bench"))
import ex01_eval as E  # noqa: E402
import ex01_kit as K  # noqa: E402
from server.evaluate.provider import MockProvider  # noqa: E402
from server.evaluate.reader import Reader  # noqa: E402

CRITICAL = E.CRITICAL
TOPIC_STEP = str(sorted(K.window_of("ASK_TOPIC"))[0])
OUT_STEP = str(next(s for s in range(1, 16) if s not in K.window_of("ASK_TOPIC")))


def item(i, text, kind="positive", family="F01", step=None, acts="ASK_TOPIC", **kw):
    credit = "yes" if kind == "positive" else "no"
    return {"item_id": i, "family": family, "scenario_step": step or TOPIC_STEP, "scenario_context": "fixture", "prior_turns": kw.get("prior_turns", ""), "speaker_role": "trainee", "utterance": text,
            "patient_question_pending": kw.get("q", "no"), "readback_pending": kw.get("r", "no"), "asr_source": "", "gold_acts": acts, "gold_actor": "trainee", "gold_modality": "actual", "gold_credit_eligible": credit, "gold_status": "x"}


def act(obj, assertion="performed", actor="trainee", r=3, c=3, k=3, evidence="how have you"):
    return {"objective": obj, "assertion": assertion, "actor": actor, "recipient": "patient", "form": "question", "performed_now": assertion == "performed" and actor == "trainee", "relevance": r, "correctness": c, "completeness": k, "evidence": evidence, "rationale": "x"}


def reply(*acts):
    return json.dumps({"acts": list(acts), "claims_physical_action": False, "physical_action": "", "critical_topic": False})


def engine(items):
    """a stand-in for the engine's speech-score: the window and the waiting state decide, the grade is 1 for 3.3.3 and 0.6 for completeness 1."""
    out = []
    for it in items:
        acts = []
        for a in it["acts"]:
            ok = a["assertion"] == "performed" and a["actor"] == "trainee" and (a["act"] in CRITICAL or it["steps"][0] in K.window_of(a["act"]))
            g = 0.0 if not ok or a["relevance"] == 0 or a["correctness"] == 0 else (1.0 if a["completeness"] == 3 else 0.6)
            acts.append({"act": a["act"], "accepted": ok and a["act"] not in CRITICAL, "reason": "" if ok else ("NOT_PERFORMED:" + a["assertion"] if a["assertion"] != "performed" else "CONTEXT_MISMATCH"), "behaviour": "b01", "grade": g})
        out.append({"id": it["id"], "acts": acts})
    return out


def test_gold_kinds_and_window_flag():
    assert E.gold_of(item("a", "x"))["kind"] == "positive" and E.gold_of(item("a", "x"))["in_window"] is True
    assert E.gold_of(item("a", "x", step=OUT_STEP))["in_window"] is False
    assert E.gold_of(item("a", "x", "nonperformed", acts="NONE"))["kind"] == "nonperformed"
    assert E.gold_of(item("a", "x", "critical", acts="STATE_ALLERGY"))["kind"] == "critical"


def test_context_holds_the_scene_and_the_authors_flags_but_no_label():
    c = E.context_of(item("a", "x", family="F12", prior_turns="patient: a question | trainee: ok", q="yes"))
    assert c["patient_question_pending"] and not c["readback_pending"] and [t["speaker"] for t in c["last_turns"]] == ["patient", "trainee"]
    assert any(e["act"] == "ASK_TOPIC" for e in c["eligible"]) and c["verified_actions"] == []
    assert "gold_acts" not in c and "family" not in c


def test_evaluator_layer_counts_understanding_before_the_engine():
    items = [item("p1", "how have you been sleeping"), item("p2", "something else entirely"), item("n1", "I did not ask", "nonperformed", acts="NONE"), item("c1", "any allergies", "critical", acts="STATE_ALLERGY")]
    replies = [reply(act("ASK_TOPIC")), reply(act("EXPLAIN_ACTION", evidence="something else")), reply(act("ASK_TOPIC", evidence="I did not ask")), reply(act("STATE_ALLERGY", evidence="any allergies"))]
    m, rows = E.evaluator_layer(items, Reader(MockProvider(replies)))
    assert m["understood_positive_items"]["k"] == 1 and m["understood_positive_items"]["n"] == 2
    assert m["false_performance_of_nonperformed"]["k"] == 1 and m["critical_content_recognised"]["k"] == 1 and m["ordinary_performance_on_critical_items"]["k"] == 0
    assert m["schema_compliance"]["final_valid"]["k"] == 4 and m["schema_compliance"]["first_attempt_valid"]["k"] == 4
    assert m["confusion_by_objective"] == {"ASK_TOPIC -> ASK_TOPIC": 1, "ASK_TOPIC -> EXPLAIN_ACTION": 1}
    assert m["misunderstood_positive_items_by_family"] == {"F01": 1}


def test_a_non_performed_assertion_is_not_counted_as_performance():
    items = [item("n1", "I will ask about sleep later", "nonperformed", acts="NONE")]
    m, rows = E.evaluator_layer(items, Reader(MockProvider([reply(act("ASK_TOPIC", "intended_later", evidence="ask about sleep"))])))
    assert m["false_performance_of_nonperformed"]["k"] == 0 and rows[0]["performed"] == []


def test_schema_failures_are_counted_not_repaired():
    items = [item("p1", "how have you been sleeping")]
    m, rows = E.evaluator_layer(items, Reader(MockProvider(["```json\n{}\n```", "not json"]), max_attempts=2))
    assert m["schema_compliance"]["final_valid"]["k"] == 0 and rows[0]["status"] == "INVALID" and m["understood_positive_items"]["k"] == 0
    assert set(m["schema_compliance"]["unavailable_or_invalid_codes"]) == {"FENCED", "NOT_JSON"}


def test_final_layer_credits_only_what_the_engine_allows_and_separates_understanding_from_rejection():
    items = [item("p1", "how have you been sleeping"), item("p2", "tell me about your sleep"), item("w1", "and your sleep", step=OUT_STEP), item("n1", "I did not ask about sleep", "nonperformed", acts="NONE"), item("c1", "any allergies", "critical", acts="STATE_ALLERGY")]
    replies = [reply(act("ASK_TOPIC")), reply(act("ASK_TOPIC", evidence="tell me")), reply(act("ASK_TOPIC", evidence="and your")), reply(), reply(act("STATE_ALLERGY", evidence="any allergies"))]
    _, rows = E.evaluator_layer(items, Reader(MockProvider(replies)))
    outs = E.final_layer(items, rows, engine)
    s = E.summarise_final(outs)
    assert s["legitimate_recall"]["n"] == 2 and s["legitimate_recall"]["k"] == 2
    assert s["out_of_window_valid_items"]["n"] == 1 and s["out_of_window_valid_items"]["credited"] == 0
    assert s["critical_false_credits"]["k"] == 0 and s["adversarial_false_credits"]["k"] == 0 and s["critical_acts_credited_from_speech"]["k"] == 0
    assert s["credited_precision"]["n"] == 2 and s["credited_precision"]["k"] == 2
    assert E.attribution(rows, outs)["counts"]["understood_and_credited"] == 2


def test_a_rejection_by_the_engine_of_a_correct_judgement_is_not_counted_as_misunderstanding():
    items = [item("p1", "how have you been sleeping", family="F01")]
    _, rows = E.evaluator_layer(items, Reader(MockProvider([reply(act("ASK_TOPIC", r=1, c=1, k=0))])))     # understood, but so weak that it falls under the credit floor
    outs = E.final_layer(items, rows, lambda its: [{"id": its[0]["id"], "acts": [{"act": "ASK_TOPIC", "accepted": False, "reason": "BELOW_CREDIT_FLOOR", "behaviour": "b01", "grade": 0.0}]}])
    s = E.summarise_final(outs)
    assert s["legitimate_recall"]["k"] == 0
    a = E.attribution(rows, outs)
    assert a["counts"] == {"understood_but_rejected_by_the_engine": 1} and a["understood_but_rejected_by_engine_rule"] == {"BELOW_CREDIT_FLOOR": 1}
    assert s["misses_by_engine_rule"] == {"BELOW_CREDIT_FLOOR": 1}


def test_unsafe_credits_are_counted_with_denominators():
    items = [item("c1", "any allergies", "critical", acts="STATE_ALLERGY"), item("n1", "I did not ask about sleep", "nonperformed", acts="NONE")]
    _, rows = E.evaluator_layer(items, Reader(MockProvider([reply(act("ASK_TOPIC", evidence="any allergies")), reply(act("ASK_TOPIC", evidence="ask about sleep"))])))      # a hostile model: performs an ordinary act on both
    s = E.summarise_final(E.final_layer(items, rows, engine))
    assert s["critical_false_credits"]["k"] == 1 and s["critical_false_credits"]["n"] == 1
    assert s["adversarial_false_credits"]["k"] == 1 and s["adversarial_false_credits"]["n"] == 1


def test_wilson_interval_is_reported():
    r = E.rate(34, 34)
    assert r["wilson95"][0] == pytest.approx(0.898, abs=0.002) and r["wilson95"][1] == 1.0
    assert E.rate(0, 0)["wilson95"] is None


# ---- refusals -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
def frozen_dir(tmp_path, independent=True):
    d = tmp_path / "set"
    d.mkdir()
    cols = ["item_id", "family", "scenario_step", "scenario_context", "prior_turns", "speaker_role", "utterance", "patient_question_pending", "readback_pending", "asr_source", "gold_acts", "gold_actor", "gold_modality", "gold_credit_eligible", "gold_status"]
    K.write(d / "items.csv", cols, [item("X1", "fixture sentence one")])
    for f in K.FILES:
        if f != "items.csv":
            (d / f).write_text("{}\n" if f.endswith(".json") else "x\n", encoding="utf-8")
    man = {"independent": independent, "version": "t", "files": {f: K.sha(d / f) for f in K.FILES}}
    (d / "manifest.json").write_text(json.dumps(man), encoding="utf-8")
    return d


def test_an_unfrozen_or_changed_set_is_refused(tmp_path):
    with pytest.raises(SystemExit):
        E.load_set(tmp_path / "nothing")
    d = frozen_dir(tmp_path)
    assert len(E.load_set(d)) == 1
    (d / "items.csv").write_text((d / "items.csv").read_text(encoding="utf-8") + "\n", encoding="utf-8")
    with pytest.raises(SystemExit):
        E.load_set(d)
    assert len(E.load_set(d, allow_unfrozen=True)) >= 1


def test_run_is_refused_without_a_frozen_configuration_or_after_drift_or_for_a_second_look(tmp_path):
    d = frozen_dir(tmp_path)
    with pytest.raises(SystemExit) as e:
        E.main(["run", "--dir", str(d), "--live", "m", "--max-calls", "1", "--max-tokens", "1"])
    assert "freeze the configuration" in str(e.value)
    E.main(["config-freeze", "--dir", str(d), "--model", "m"])
    cfg = json.loads((d / "eval_config.json").read_text(encoding="utf-8"))
    assert cfg["evaluator_prompt_sha256"] and cfg["code_sha256"]["server/evaluate/prompt.py"] and cfg["code_sha256"]["src/ProcSense.Core/Scoring/CommunicationScorer.cs"] and cfg["manifest_sha256"]
    bad = dict(cfg, evaluator_prompt_sha256="0" * 64)
    (d / "eval_config.json").write_text(json.dumps(bad), encoding="utf-8")
    with pytest.raises(SystemExit) as e:
        E.main(["run", "--dir", str(d), "--live", "m", "--max-calls", "1", "--max-tokens", "1"])
    assert "changed after it was frozen" in str(e.value)
    (d / "eval_config.json").write_text(json.dumps(cfg), encoding="utf-8")
    with pytest.raises(SystemExit) as e:
        E.main(["run", "--dir", str(d), "--live", "another-model", "--max-calls", "1", "--max-tokens", "1"])
    assert "changed after it was frozen" in str(e.value)
    (d / "RUN_LOCK.json").write_text("{}", encoding="utf-8")
    with pytest.raises(SystemExit) as e:
        E.main(["run", "--dir", str(d), "--live", "m", "--max-calls", "1", "--max-tokens", "1"])
    assert "already scored once" in str(e.value)


def test_config_freeze_refuses_a_set_that_is_not_frozen(tmp_path):
    with pytest.raises(SystemExit):
        E.main(["config-freeze", "--dir", str(tmp_path / "nothing"), "--model", "m"])


def test_the_repository_holds_no_set_scored_or_frozen_by_the_tests():
    lock = ROOT / "tests" / "ex01_semantic" / "RUN_LOCK.json"
    assert not lock.exists()           # the first run against the independent set has not been made; this test must be updated by the owner's decision, not by a tool


# ---- reporting of the engineering targets: point estimate AND interval, never silently redefined --------------------------------------------------------------------------------------------------
def test_the_recall_target_is_tested_on_the_point_estimate_and_the_interval_is_reported_separately():
    a = E.assess("recall", E.rate(126, 140), 0.90)
    assert a["point_estimate_meets_target"] is True and a["interval_lower_bound_at_or_above_target"] is False
    assert a["wilson95"][0] == pytest.approx(0.839, abs=0.002) and a["target"] == ">= 90%"
    assert E.assess("recall", E.rate(125, 140), 0.90)["point_estimate_meets_target"] is False
    c = E.assess("recall", E.rate(190, 200), 0.90)
    assert c["point_estimate_meets_target"] and c["interval_lower_bound_at_or_above_target"]
    assert E.assess("precision", E.rate(95, 100), 0.95)["point_estimate_meets_target"] is True
    assert E.assess("schema", E.rate(199, 200), 0.995)["point_estimate_meets_target"] is True and E.assess("schema", E.rate(198, 200), 0.995)["point_estimate_meets_target"] is False


def test_an_observed_zero_is_reported_with_its_upper_bound_not_as_proof():
    r = E.rate(0, 300)
    assert r["exact95_upper_bound_for_zero_events"] == pytest.approx(0.0122, abs=0.0002) and r["wilson95"][1] < 0.0135
    a = E.assess("critical", r, zero=True)
    assert a["point_estimate_meets_target"] is True and a["upper_bound_95"] == r["exact95_upper_bound_for_zero_events"] and "not proof" in a["note"]
    one = E.assess("critical", E.rate(1, 300), zero=True)
    assert one["point_estimate_meets_target"] is False and one["upper_bound_95"] > 0.0122


def test_target_assessment_covers_every_engineering_target_and_critical_credit_from_speech():
    items = [item("p1", "how have you been sleeping"), item("c1", "any allergies", "critical", acts="STATE_ALLERGY"), item("n1", "I did not ask about sleep", "nonperformed", acts="NONE")]
    m, rows = E.evaluator_layer(items, Reader(MockProvider([reply(act("ASK_TOPIC")), reply(), reply()])))
    s = E.summarise_final(E.final_layer(items, rows, engine))
    t = E.target_assessment(s, m)
    assert set(t) == {"legitimate_recall", "credited_precision", "critical_false_credits", "adversarial_false_credits", "critical_acts_credited_from_speech", "schema_compliance_final", "schema_compliance_first_attempt", "all_point_estimate_targets_met"}
    assert t["all_point_estimate_targets_met"] is True and t["critical_acts_credited_from_speech"]["observed"] == "0/3"
