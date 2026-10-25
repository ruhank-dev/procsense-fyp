"""Per-task model routing (ADR-0026): a cloud model is used only when the switch is on and the data class is allowed; an outage, a usage limit or a bad reply falls back to the local model; the reply is still validated
strictly. No database and no network: a fake model host."""
from __future__ import annotations

import json

import httpx
import pytest

from server.llm import client as client_mod, gateway, routing

LOCAL = client_mod.BATCH_MODEL
CLOUD = "gemma4:31b-cloud"
VARS = {"style": "brief", "age": "60", "sex_word": "man", "topic": "medicines", "original": "I take 5 mg of amlodipine every day."}
GOOD = json.dumps({"text": "Every day I take 5 mg of amlodipine."})


def fake(script):
    """script(model, request_body) -> httpx.Response | Exception; records the models asked."""
    seen: list[dict] = []

    def handler(request: httpx.Request) -> httpx.Response:
        if request.url.path == "/api/tags":
            return httpx.Response(200, json={"models": [{"name": LOCAL, "digest": "d"}]})
        body = json.loads(request.content)
        seen.append(body)
        out = script(body["model"], body)
        if isinstance(out, Exception):
            raise out
        return out
    c = client_mod.OllamaClient(transport=httpx.MockTransport(handler))
    c.seen = seen
    return c


def ok(text=GOOD):
    return httpx.Response(200, json={"message": {"content": text}, "eval_count": 9})


@pytest.fixture
def routes(monkeypatch):
    def set_(entry, enabled=True, trainee_ok=False):
        monkeypatch.setattr(routing, "_table", lambda: {"routes": {"compose_narrative": entry} if entry else {}})
        for name, on in (("CLOUD_ROUTING_ENABLED", enabled), ("CLOUD_TRAINEE_DATA_APPROVED", trainee_ok)):
            monkeypatch.setenv(name, "1") if on else monkeypatch.delenv(name, raising=False)
    return set_


def run(c):
    return gateway.run(c, "compose.narrative", VARS, task="compose_narrative", retries=0)


def test_no_route_uses_the_local_model_as_before(routes):
    routes(None)
    c = fake(lambda m, b: ok())
    r = run(c)
    assert r.ok and [b["model"] for b in c.seen] == [LOCAL] and r.audit["route"] == "no_route" and not r.audit["cloud"]
    assert "Format:" not in c.seen[0]["messages"][1]["content"]                 # a local model enforces the schema itself


def test_a_cloud_route_with_the_switch_off_stays_local(routes):
    routes({"model": CLOUD, "data_class": "synthetic"}, enabled=False)
    c = fake(lambda m, b: ok())
    assert run(c).audit["route"] == "switch_off" and [b["model"] for b in c.seen] == [LOCAL]


def test_a_cloud_route_for_synthetic_data_is_used_and_asked_for_plain_json(routes):
    routes({"model": CLOUD, "data_class": "synthetic"})
    c = fake(lambda m, b: ok())
    r = run(c)
    assert r.ok and [b["model"] for b in c.seen] == [CLOUD] and r.audit["cloud"] and r.audit["fell_back_from"] == ""
    assert "code fence" in c.seen[0]["messages"][1]["content"] and '"text"' in c.seen[0]["messages"][1]["content"]


def test_trainee_data_stays_local_until_the_privacy_decision_is_recorded(routes):
    routes({"model": CLOUD, "data_class": "trainee"})
    c = fake(lambda m, b: ok())
    assert run(c).audit["route"] == "privacy_gate" and [b["model"] for b in c.seen] == [LOCAL]
    routes({"model": CLOUD, "data_class": "trainee"}, trainee_ok=True)                   # the institutional approval alone is not enough: this person has not consented
    c = fake(lambda m, b: ok())
    assert run(c).audit["route"] == "no_consent" and [b["model"] for b in c.seen] == [LOCAL]
    c = fake(lambda m, b: ok())
    with routing.consent(True):                                                         # approval and this person's own consent
        run(c)
    assert [b["model"] for b in c.seen] == [CLOUD]
    c = fake(lambda m, b: ok())
    with routing.consent(False):                                                        # withdrawn
        run(c)
    assert [b["model"] for b in c.seen] == [LOCAL]


@pytest.mark.parametrize("failure", [
    lambda: httpx.Response(402, text="not in the Free plan"),
    lambda: httpx.Response(429, text="usage limit"),
    lambda: httpx.Response(404, text="model not found"),
    lambda: httpx.ConnectError("down"),
    lambda: httpx.ReadTimeout("slow"),
])
def test_a_cloud_outage_or_usage_limit_falls_back_to_the_local_model(routes, failure):
    routes({"model": CLOUD, "data_class": "synthetic"})
    c = fake(lambda m, b: failure() if m == CLOUD else ok())
    r = run(c)
    assert r.ok and [b["model"] for b in c.seen] == [CLOUD, LOCAL]
    assert r.audit["model"] == LOCAL and r.audit["fell_back_from"] == CLOUD


def test_a_fenced_cloud_reply_is_rejected_not_repaired_and_the_local_model_answers(routes):
    routes({"model": CLOUD, "data_class": "synthetic"})
    c = fake(lambda m, b: ok("```json\n" + GOOD + "\n```") if m == CLOUD else ok())
    r = run(c)
    assert r.ok and r.audit["model"] == LOCAL and [a["ok"] for a in r.attempts] == [False, True]


def test_a_schema_violation_from_the_cloud_is_never_accepted(routes):
    routes({"model": CLOUD, "data_class": "synthetic"})
    c = fake(lambda m, b: ok(json.dumps({"wrong": 1})))
    r = run(c)
    assert not r.ok and r.fallback and all(a["defects"] or not a["ok"] for a in r.attempts)


def test_both_models_failing_gives_the_caller_its_template_fallback(routes):
    routes({"model": CLOUD, "data_class": "synthetic"})
    r = run(fake(lambda m, b: httpx.Response(500, text="x")))
    assert not r.ok and r.fallback


def test_an_explicit_cloud_model_still_falls_back_and_an_explicit_local_one_does_not(routes):
    routes(None)
    c = fake(lambda m, b: httpx.Response(402, text="plan") if m == CLOUD else ok())
    assert gateway.run(c, "compose.narrative", VARS, model=CLOUD, retries=0).audit["model"] == LOCAL
    c = fake(lambda m, b: httpx.Response(500, text="x"))
    assert not gateway.run(c, "compose.narrative", VARS, model=LOCAL, retries=0).ok and [b["model"] for b in c.seen] == [LOCAL]


def test_thinking_models_get_a_token_budget_and_others_do_not():
    assert routing.tokens_for("gpt-oss:120b-cloud", 160) == 160 + routing.THINKING_BUDGET
    assert routing.tokens_for(CLOUD, 160) == 160 and routing.tokens_for(LOCAL, 160) == 160
    assert routing.is_cloud(CLOUD) and routing.is_cloud("x:cloud") and not routing.is_cloud(LOCAL)


def test_the_committed_routes_are_well_formed_and_never_send_trainee_data_by_default(monkeypatch):
    table = json.loads(routing.ROUTES_FILE.read_text(encoding="utf-8"))
    for task, e in table["routes"].items():
        assert e["data_class"] in ("synthetic", "trainee"), task
        assert e["model"] and e.get("evidence"), task                      # every route names the benchmark file that chose it
    for name in ("CLOUD_ROUTING_ENABLED", "CLOUD_TRAINEE_DATA_APPROVED"):
        monkeypatch.delenv(name, raising=False)
    routing.reload()
    for task, e in table["routes"].items():
        assert not routing.resolve(task, "x", "live").cloud                  # nothing leaves the machine unless the owner switches it on


def test_every_route_matches_the_decision_the_benchmark_rule_gave():
    report = json.loads((routing.ROUTES_FILE.parents[2] / "reports" / "ai" / "cloud-routing-benchmark.json").read_text(encoding="utf-8"))
    table = json.loads(routing.ROUTES_FILE.read_text(encoding="utf-8"))
    for task, e in table["routes"].items():
        ref = e["evidence"].split("#decisions.")
        assert ref[0] == "reports/ai/cloud-routing-benchmark.json", task
        if len(ref) == 2:
            assert report["decisions"][ref[1]]["selected"] == e["model"], task        # the route is what the benchmark chose
        else:
            assert e["model"] == client_mod.LIVE_MODEL, task                          # a task with no benchmark stays on the local model


def test_a_route_to_a_cloud_model_always_names_the_local_fallback():
    for task, e in json.loads(routing.ROUTES_FILE.read_text(encoding="utf-8"))["routes"].items():
        if routing.is_cloud(e["model"]):
            assert e["fallback"] == client_mod.LIVE_MODEL, task


@pytest.mark.skipif(not __import__("os").environ.get("PROCSENSE_LIVE_CLOUD"), reason="live check against a signed-in Ollama; set PROCSENSE_LIVE_CLOUD=1 (synthetic text only, uses the free plan)")
def test_live_cloud_route_answers_with_synthetic_text(routes):
    routes({"model": CLOUD, "data_class": "synthetic"})
    r = gateway.run(client_mod.OllamaClient(), "compose.narrative", VARS, task="compose_narrative", retries=1)
    assert r.ok and r.audit["cloud"] and r.audit["fell_back_from"] == ""


PATIENT_VARS = {"style": "plain", "age": "50", "sex_word": "man", "intents": "answer_question, cannot_recall", "facts": "- h.x: I am fine.", "turns": "(none)", "question": "How are you?"}
PATIENT_REPLY = json.dumps({"intent": "answer_question", "text": "I am fine.", "fact_ids": ["h.x"]})


def test_the_cloud_model_gets_the_latest_prompt_and_the_local_model_keeps_the_one_written_for_it(monkeypatch):
    monkeypatch.setattr(routing, "_table", lambda: {"routes": {"dialogue_patient": {"model": CLOUD, "data_class": "synthetic", "evidence": "x"}}})
    monkeypatch.setenv("CLOUD_ROUTING_ENABLED", "1")
    c = fake(lambda m, b: httpx.Response(402, text="plan") if m == CLOUD else ok(PATIENT_REPLY))
    r = gateway.run(c, "dialogue.patient", PATIENT_VARS, task="dialogue_patient", retries=0)
    assert r.ok and [b["model"] for b in c.seen] == [CLOUD, LOCAL]
    cloud_system, local_system = c.seen[0]["messages"][0]["content"], c.seen[1]["messages"][0]["content"]
    assert cloud_system.startswith("ROLE") and "WHAT TO DO WITH WHAT THE TRAINEE SAYS" in cloud_system          # version 2, written and measured for the cloud model
    assert not local_system.startswith("ROLE") and len(local_system) < len(cloud_system) / 2                   # version 1: the longer prompt costs the 4B model seconds
    assert r.audit["prompt_version"] == 1 and r.audit["fell_back_from"] == CLOUD


def test_a_prompt_for_every_model_is_used_by_both(monkeypatch):
    monkeypatch.setattr(routing, "_table", lambda: {"routes": {}})
    c = fake(lambda m, b: ok())
    r = gateway.run(c, "compose.narrative", VARS, task="compose_narrative", retries=0)
    assert r.audit["prompt_version"] == 2 and c.seen[0]["messages"][0]["content"].startswith("ROLE")


def test_a_busy_cloud_slot_sends_the_task_to_the_local_model_instead_of_queueing(routes, monkeypatch):
    routes({"model": CLOUD, "data_class": "synthetic"})
    c = fake(lambda m, b: ok())
    monkeypatch.setattr(routing, "BATCH_WAIT_SECONDS", 0.05)
    assert routing.acquire_cloud("live")                                      # another request holds the single cloud slot
    try:
        r = run(c)
    finally:
        routing.release_cloud()
    assert r.ok and [b["model"] for b in c.seen] == [LOCAL] and r.attempts[0]["error"] == "CLOUD_BUSY" and r.audit["fell_back_from"] == CLOUD


def test_the_trainee_class_needs_the_privacy_approval_or_a_synthetic_declaration_and_the_evaluator_follows_the_same_rule(monkeypatch):
    for name in ("CLOUD_ROUTING_ENABLED", "CLOUD_TRAINEE_DATA_APPROVED", "CLOUD_SYNTHETIC_DEPLOYMENT"):
        monkeypatch.delenv(name, raising=False)
    assert not routing.trainee_cloud_allowed()
    monkeypatch.setenv("CLOUD_SYNTHETIC_DEPLOYMENT", "1")
    assert routing.trainee_cloud_allowed() and not routing.trainee_data_approved()
    monkeypatch.setenv("CLOUD_SYNTHETIC_DEPLOYMENT", "0")
    monkeypatch.setenv("CLOUD_TRAINEE_DATA_APPROVED", "1")
    assert not routing.trainee_cloud_allowed()                                          # approved, but this person has not consented
    with routing.consent(True):
        assert routing.trainee_cloud_allowed()
    with routing.consent(False):
        assert not routing.trainee_cloud_allowed()
    monkeypatch.setenv("CLOUD_TRAINEE_DATA_APPROVED", "0")
    with routing.consent(True):
        assert not routing.trainee_cloud_allowed()                                      # consent without the institutional approval does not open the cloud
    assert routing.status()["trainee_routes_use_cloud"] is False
