"""The synthetic FYP demonstration profile and the routing of spoken orders (ADR-0028, amended 27 Oct 2026): Gemma 4 31B Cloud first, the offline grammar as the fallback, the reason always known, and the profile never the default or anywhere near real trainees. Fake model host; no database."""
from __future__ import annotations

import json
from pathlib import Path

import httpx
import pytest

from server.config import Settings
from server.llm import client as client_mod, routing
from server.orders.cloud_interpreter import CloudOnlyInterpreter
from server.orders.grammar import ReadBack, vocabulary

ROOT = Path(__file__).resolve().parents[2]
ORDER = {"kind": "order", "intent": "ORDER_TEST", "target": "test:ecg", "confidence": 0.9}
SWITCHES = ("CLOUD_ROUTING_ENABLED", "CLOUD_SYNTHETIC_DEPLOYMENT", "CLOUD_TRAINEE_DATA_APPROVED")


@pytest.fixture(autouse=True)
def clean(monkeypatch):
    for n in SWITCHES:
        monkeypatch.delenv(n, raising=False)
    routing.reload()


class Host:
    """A fake model host that counts the chat calls and names the model it was asked, as the real one does in its reply."""

    def __init__(self, answer=ORDER, status=200, reply_model=None):
        self.answer, self.status, self.reply_model, self.asked = answer, status, reply_model, []
        self.client = client_mod.OllamaClient(transport=httpx.MockTransport(self))

    def __call__(self, request):
        if request.url.path == "/api/tags":
            return httpx.Response(200, json={"models": [{"name": client_mod.LIVE_MODEL}, {"name": "gemma4:31b-cloud"}]})
        model = json.loads(request.content)["model"]
        self.asked.append(model)
        if self.status != 200:
            return httpx.Response(self.status, json={"error": "usage limit"})
        return httpx.Response(200, json={"model": self.reply_model or model, "message": {"content": json.dumps(self.answer)}})


def hear(host, text="could i get a trace of his heart"):
    ci = CloudOnlyInterpreter(host.client)
    h = ReadBack(vocabulary(), None, ci).hear(text)
    return h, ci.last


def demo(monkeypatch, **extra):
    for k, v in {"CLOUD_ROUTING_ENABLED": "1", "CLOUD_SYNTHETIC_DEPLOYMENT": "1", **extra}.items():
        monkeypatch.setenv(k, v)


def test_in_the_demonstration_profile_gemma_cloud_reads_the_sentence_and_the_host_names_it(monkeypatch):
    demo(monkeypatch)
    host = Host(reply_model="gemma4:31b")
    h, last = hear(host)
    assert (h.status, h.source, h.slots["target"]) == ("confirm", "model", "ecg")          # a permission-style "could i" request is read as an order: no word list refused it
    assert host.asked == ["gemma4:31b-cloud"]                                                # one call, to the cloud model, no local second leg
    assert (last["interpreter"], last["model"], last["reported_model"], last["reason"]) == ("gemma_cloud", "gemma4:31b-cloud", "gemma4:31b", "")


@pytest.mark.parametrize("env,consent,reason", [({}, True, "CLOUD_ROUTING_OFF"), ({"CLOUD_ROUTING_ENABLED": "1"}, True, "PRIVACY_GATE"),
                                                  ({"CLOUD_ROUTING_ENABLED": "1", "CLOUD_TRAINEE_DATA_APPROVED": "1"}, False, "NO_CONSENT")])
def test_without_the_cloud_being_allowed_the_model_is_never_called_and_the_grammar_answers(monkeypatch, env, consent, reason):
    for k, v in env.items():
        monkeypatch.setenv(k, v)
    host = Host()
    with routing.consent(consent):
        h, last = hear(host)
    assert host.asked == []                                                                  # not even the local model is asked: its reading would be dropped anyway
    assert (h.source, h.status, last["interpreter"], last["reason"]) == ("grammar", "menu", "offline_grammar", reason)     # the grammar's question guard refuses "could i ..." offline


def test_with_approval_and_the_persons_own_consent_the_cloud_is_used_in_a_real_trainee_deployment(monkeypatch):
    monkeypatch.setenv("CLOUD_ROUTING_ENABLED", "1")
    monkeypatch.setenv("CLOUD_TRAINEE_DATA_APPROVED", "1")
    host = Host()
    with routing.consent(True):
        h, last = hear(host)
    assert (h.source, last["interpreter"], host.asked) == ("model", "gemma_cloud", ["gemma4:31b-cloud"])


@pytest.mark.parametrize("status,reason", [(429, "CLOUD_LIMIT"), (500, "CLOUD_LIMIT")])
def test_a_cloud_failure_falls_back_to_the_grammar_with_no_local_second_leg(monkeypatch, status, reason):
    demo(monkeypatch)
    host = Host(status=status)
    h, last = hear(host, "order a crp")
    assert host.asked == ["gemma4:31b-cloud"]                                                # the local model is not asked after the cloud failed
    assert (h.source, h.status, h.slots["target"], last["interpreter"], last["reason"]) == ("grammar", "confirm", "crp", "offline_grammar", reason)       # the plain order still works offline


def test_an_unreachable_host_and_a_timeout_fall_back_to_the_grammar(monkeypatch):
    demo(monkeypatch)
    down = CloudOnlyInterpreter(client_mod.OllamaClient("http://127.0.0.1:9"))
    h = ReadBack(vocabulary(), None, down).hear("order a crp")
    assert (h.source, down.last["reason"]) == ("grammar", "CLOUD_UNREACHABLE")
    slow = CloudOnlyInterpreter(Host().client, timeout=0.0)
    assert slow.interpret("order a crp", vocabulary()) in (None,) or slow.last["interpreter"] == "gemma_cloud"     # a fake host answers at once; the real timeout is checked by tools/demo/orders_routing_check.py


def test_an_invalid_cloud_answer_is_a_fallback_not_an_action(monkeypatch):
    demo(monkeypatch)
    host = Host(answer={"kind": "order", "intent": "ORDER_TEST", "target": "test:invented", "confidence": 0.99})
    h, last = hear(host, "order the thing")
    assert (h.source, h.action, last["interpreter"]) == ("grammar", None, "offline_grammar") and last["reason"] == "CLOUD_INVALID"


def test_an_ambiguous_cloud_answer_asks_and_never_proposes_an_action(monkeypatch):
    demo(monkeypatch)
    host = Host(answer={"kind": "ambiguous", "intent": "NONE", "target": "NONE", "alternatives": ["test:ct", "test:mri"], "confidence": 0.8, "clarification": "CT or MRI?"})
    h, last = hear(host, "get a scan")
    assert (h.status, h.action, h.source, last["interpreter"]) == ("clarify", None, "model", "gemma_cloud")


def test_the_status_says_which_system_reads_a_spoken_order(monkeypatch):
    s = routing.status()["spoken_orders"]
    assert (s["preferred"], s["active"], s["grammar_fallback_reason"], s["profile"]) == ("gemma4:31b-cloud", "offline_grammar", "CLOUD_ROUTING_OFF", "standard")
    demo(monkeypatch)
    s = routing.status()["spoken_orders"]
    assert (s["active"], s["profile"]) == ("gemma_cloud", "synthetic_demonstration")
    monkeypatch.setenv("CLOUD_SYNTHETIC_DEPLOYMENT", "0")
    monkeypatch.setenv("CLOUD_TRAINEE_DATA_APPROVED", "1")
    assert routing.status()["spoken_orders"]["active"] == "gemma_cloud_with_personal_consent"


def test_the_shipped_defaults_are_off_and_the_demonstration_profile_is_the_only_place_that_turns_them_on():
    s = Settings(_env_file=None)
    assert (s.cloud_routing_enabled, s.cloud_synthetic_deployment, s.cloud_trainee_data_approved) == (False, False, False)
    example = (ROOT / "server" / ".env.example").read_text(encoding="utf-8")
    for name in SWITCHES:
        assert f"\n{name}=false" in example
    profile = dict(line.split("=", 1) for line in (ROOT / "server" / "demo" / "synthetic-fyp-demo.env").read_text(encoding="utf-8").splitlines() if line.strip() and not line.startswith("#"))
    assert (profile["CLOUD_ROUTING_ENABLED"], profile["CLOUD_SYNTHETIC_DEPLOYMENT"], profile["CLOUD_TRAINEE_DATA_APPROVED"], profile["APP_ENV"]) == ("1", "1", "0", "dev")
    for rel in ("server/Dockerfile", ".github/workflows/ci.yml", ".github/workflows/release.yml", ".github/workflows/publish-image.yml", "server/config.py"):
        text = (ROOT / rel).read_text(encoding="utf-8")
        assert "CLOUD_SYNTHETIC_DEPLOYMENT=1" not in text and "CLOUD_SYNTHETIC_DEPLOYMENT: 1" not in text and "cloud_synthetic_deployment: bool = True" not in text, rel


def test_a_service_started_with_the_synthetic_declaration_beside_production_or_real_trainee_approval_refuses_to_start():
    assert Settings(_env_file=None, cloud_synthetic_deployment=True).cloud_synthetic_deployment is True
    with pytest.raises(ValueError, match="APP_ENV=prod"):
        Settings(_env_file=None, cloud_synthetic_deployment=True, app_env="prod")
    with pytest.raises(ValueError, match="CLOUD_TRAINEE_DATA_APPROVED"):
        Settings(_env_file=None, cloud_synthetic_deployment=True, cloud_trainee_data_approved=True)
