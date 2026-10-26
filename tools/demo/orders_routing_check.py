"""End-to-end check of the demonstration routing of spoken orders (ADR-0028, amended 27 Oct 2026): `uv run --project server python tools/demo/orders_routing_check.py` (task demo-orders-check).

With the synthetic demonstration profile (server/demo/synthetic-fyp-demo.env) it sends invented sentences, with invented scenario contexts, to the REAL Gemma 4 31B Cloud through the signed-in Ollama and records, per sentence, which system answered (Gemma Cloud or the offline grammar), the model asked and the
model name the host reported back in its reply (the evidence that the cloud model answered, not only that a flag is on), the reason for any fallback and the latency. It then proves each fallback through the same code path: no personal consent in a non-synthetic deployment (no model call at all), no
institutional approval, cloud routing off, an unreachable host, and a timeout. Nothing real is sent: the words are written here. Writes reports/ai/demo-orders-routing.json; exits 1 if Gemma did not answer, or a fallback did not fall back to the grammar, or an ambiguous request produced an item."""
from __future__ import annotations

import json
import os
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
for line in (ROOT / "server" / "demo" / "synthetic-fyp-demo.env").read_text(encoding="utf-8").splitlines():      # the demonstration profile, exactly as the service loads it
    if line.strip() and not line.startswith("#") and "=" in line:
        k, v = line.split("=", 1)
        os.environ.setdefault(k.strip(), v.strip())

from server.llm import client as C, routing  # noqa: E402
from server.orders.cloud_interpreter import CloudOnlyInterpreter  # noqa: E402
from server.orders.grammar import ReadBack, vocabulary  # noqa: E402

OUT = ROOT / "reports" / "ai" / "demo-orders-routing.json"
CTX = {"ward": "Pre-operative ward, planned cholecystectomy. Type 2 diabetes on metformin. No known drug allergies. Fasted since midnight.",
       "sepsis": "Emergency department. Fever, heart rate 118, blood pressure 92/58, confused. Suspected urine sepsis. Penicillin allergy (rash). Lactate and cultures not taken, no antibiotic given.",
       "hypo": "Diabetic patient on insulin, sweaty and drowsy, glucose 2.8, can still swallow. Nothing given yet."}
SCENARIOS = [("order", "ward", "Order a full blood count and send it to the lab."), ("permission-style order", "sepsis", "Can I get a lactate and cultures on her?"), ("polite order", "ward", "Could you ask the porter to come?"),
             ("hold", "ward", "Hold the metformin this morning."), ("real question", "ward", "What does a CRP actually tell us?"), ("negated", "sepsis", "Don't call the surgeon."), ("hypothetical", "sepsis", "If she gets worse we could give the fluid bolus."),
             ("ambiguous", "ward", "Get a scan of her tummy."), ("incomplete", "sepsis", "Give her something."), ("unsupported", "sepsis", "Intubate her now.")]
MUST_NOT_PROPOSE = {"real question", "negated", "hypothetical", "ambiguous", "incomplete", "unsupported"}


class Counting(C.OllamaClient):
    calls = 0

    def chat(self, *a, **k):
        type(self).calls += 1
        return super().chat(*a, **k)


def ask(host: str, text: str, ctx: str, timeout: float | None = None) -> dict:
    Counting.calls = 0
    ci = CloudOnlyInterpreter(Counting(host), timeout=timeout)
    t0 = time.monotonic()
    h = ReadBack(vocabulary(), None, ci).hear(text, CTX[ctx])
    used = "gemma_cloud" if h.source == "model" else "offline_grammar"
    return {"sentence": text, "status": h.status, "interpretation": h.interpretation or "-", "label": h.slots.get("label", ""), "action_proposed": h.action is not None, "interpreter": used, "model": ci.last.get("model", ""),
            "reported_model": ci.last.get("reported_model", ""), "fallback_reason": "" if used == "gemma_cloud" else ci.last.get("reason", ""), "model_calls": Counting.calls, "ms": round((time.monotonic() - t0) * 1000)}


def main() -> int:
    host = "http://127.0.0.1:11434"
    out = {"synthetic": True, "profile": {k: os.environ[k] for k in ("CLOUD_ROUTING_ENABLED", "CLOUD_SYNTHETIC_DEPLOYMENT", "CLOUD_TRAINEE_DATA_APPROVED")}, "status_before": routing.status()["spoken_orders"], "scenarios": [], "fallbacks": {}}
    ok = True
    for kind, ctx, text in SCENARIOS:
        r = {"kind": kind, **ask(host, text, ctx)}
        out["scenarios"].append(r)
        print(f"{r['interpreter']:15} {r['reported_model'] or '-':14} {r['ms']:5} ms  {r['status']:8} {r['interpretation']:12} {kind}: {text}")
        if kind in MUST_NOT_PROPOSE and r["action_proposed"]:
            ok = False
            print("FAIL: an action was proposed for", kind)
    gem = [r for r in out["scenarios"] if r["interpreter"] == "gemma_cloud"]
    out["gemma_answered"] = len(gem)
    out["gemma_reported_models"] = sorted({r["reported_model"] for r in gem})
    if not gem or not all("gemma" in r["reported_model"].lower() for r in gem):
        ok = False
        print("FAIL: the host did not report a Gemma model for the cloud answers")
    env0 = {k: os.environ.get(k) for k in ("CLOUD_ROUTING_ENABLED", "CLOUD_SYNTHETIC_DEPLOYMENT", "CLOUD_TRAINEE_DATA_APPROVED")}
    s = SCENARIOS[1]
    fb = out["fallbacks"]
    os.environ.update(CLOUD_SYNTHETIC_DEPLOYMENT="0", CLOUD_TRAINEE_DATA_APPROVED="1")                       # a real-trainee deployment: the approval is recorded, the person decides
    with routing.consent(False):
        fb["no_personal_consent"] = ask(host, s[2], s[1])
    with routing.consent(True):
        fb["personal_consent_given"] = ask(host, s[2], s[1])
    os.environ.update(CLOUD_TRAINEE_DATA_APPROVED="0")
    with routing.consent(True):
        fb["no_institutional_approval"] = ask(host, s[2], s[1])
    os.environ.update(CLOUD_SYNTHETIC_DEPLOYMENT="1", CLOUD_ROUTING_ENABLED="0")
    fb["cloud_routing_off"] = ask(host, s[2], s[1])
    os.environ.update(CLOUD_ROUTING_ENABLED="1")
    fb["host_unreachable"] = ask("http://127.0.0.1:9", s[2], s[1])
    fb["timeout"] = ask(host, s[2], s[1], timeout=0.001)
    for k, v in env0.items():
        os.environ[k] = v
    want = {"no_personal_consent": ("NO_CONSENT", 0), "no_institutional_approval": ("PRIVACY_GATE", 0), "cloud_routing_off": ("CLOUD_ROUTING_OFF", 0), "host_unreachable": ("CLOUD_UNREACHABLE", None), "timeout": ("CLOUD_TIMEOUT", None)}
    for k, (reason, calls) in want.items():
        r = fb[k]
        good = r["interpreter"] == "offline_grammar" and r["fallback_reason"] == reason and (calls is None or r["model_calls"] == calls)
        print(f"{'ok  ' if good else 'FAIL'} fallback {k}: {r['interpreter']} reason={r['fallback_reason']} model_calls={r['model_calls']} {r['ms']} ms")
        ok = ok and good
    g = fb["personal_consent_given"]
    good = g["interpreter"] == "gemma_cloud" and "gemma" in g["reported_model"].lower()
    print(f"{'ok  ' if good else 'FAIL'} consent given -> {g['interpreter']} ({g['reported_model']})")
    ok = ok and good
    out["passed"] = ok
    OUT.write_text(json.dumps(out, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    print("wrote", OUT.relative_to(ROOT), "PASS" if ok else "FAIL")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
