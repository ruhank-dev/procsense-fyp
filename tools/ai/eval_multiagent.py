"""Three characters answering one trainee turn on one model (p2-ai-advanced-simulation-s03, feature E49): `uv run --project tools tools/ai/eval_multiagent.py --model qwen3.5:4b-q4_K_M [--turns 12]`.

The step asks for p95 under 4 s with three roles. The 6 GB GPU holds one 4B model and answers one request at a time (the single slot of ADR-0011), so three characters can only answer one after the other. For
each turn the trainee says something that a patient, a nurse and an anaesthetist could each answer; the production dialogue code (server/dialogue) answers as the patient (a real P0 case, the fact earned),
then as the nurse, then as the anaesthetist, and the time is measured to the FIRST reply and to ALL THREE. Reported: p50 and p95 of both, the share of turns where all three came within 4 s, and the share of replies
that fell back to a fixed line. Staff contexts carry no case facts (the staff intents are closed lists, content/ai/intents.json), so these replies are short; the patient reply is the real one. The result says
whether the Core's ReactionQueue (priority, debounce, one spontaneous remark per 8 s) is needed to hide the delay. Results go to artifacts/ai-bench/ (git-ignored). Needs a running Ollama.
"""
import argparse
import json
import statistics
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools"))
TURNS = ["Can you tell me about your medicines?", "I am going to check your wristband now.", "Has anyone asked about your last meal?", "We are nearly ready to go through to theatre.",
         "Is there anything you are worried about?", "Can someone confirm the antibiotic has been given?", "The blood pressure is a little low.", "Are you allergic to anything?",
         "We will need to move you to the trolley.", "Does anyone know when the scan result will be back?", "I would like to go over the plan again.", "Can you all hear me?"]
STAFF = ("nurse", "anaesthesia")
BUDGET_S = 4.0


def pct(xs, q):
    xs = sorted(xs)
    return xs[max(0, int(q * len(xs)) - 1)]


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", default="qwen3.5:4b-q4_K_M")
    ap.add_argument("--turns", type=int, default=12)
    a = ap.parse_args()
    from composer import blocks as B
    from dialogue import dialogue as D
    from llm import client as L
    sys.path.insert(0, str(ROOT / "tools" / "ai"))
    import eval_dialogue as E
    client = L.OllamaClient()
    if a.model not in client.models():
        raise SystemExit(f"{a.model} is not installed")
    intents = json.loads((ROOT / "content" / "ai" / "intents.json").read_text(encoding="utf-8"))["roles"]
    cases = [c for c in B.load_cases() if c["tier"] == "P0" and c["status"].startswith("APPROVED")]
    d = D.Dialogue(client, model=a.model, timeout=60.0)
    first, total, fallbacks, replies = [], [], 0, 0
    for n in range(a.turns):
        c = cases[n % len(cases)]
        topic = next(h["topic"] for h in c["history"] if h.get("variant") is None and not h["volunteered"] and len(h["answer"]) >= 12)
        authored = {"h." + h["topic"]: h["answer"] for h in c["history"] if h.get("variant") is None}
        say = TURNS[n % len(TURNS)]
        d.cache.clear()
        t0 = time.monotonic()
        r = d.reply(E.context(c, topic, True), say, age=c["identity"]["age"], sex=c["identity"]["sex"], authored=authored, asked_fact="h." + topic)
        t_first = time.monotonic() - t0
        replies += 1
        fallbacks += r.source == "fallback"
        for role in STAFF:
            ctx = {"role": role, "case": c["id"], "allowed_intents": intents[role], "facts": [], "last_turns": [{"who": "trainee", "text": say}], "results": []}
            r = d.reply(ctx, say, role=role)
            replies += 1
            fallbacks += r.source == "fallback"
        first.append(t_first)
        total.append(time.monotonic() - t0)
    out = {"model": a.model, "turns": a.turns, "characters": ["patient", *STAFF], "first_reply_s": {"p50": round(statistics.median(first), 2), "p95": round(pct(first, 0.95), 2)},
           "all_three_s": {"p50": round(statistics.median(total), 2), "p95": round(pct(total, 0.95), 2)}, "all_three_within_budget": round(sum(t <= BUDGET_S for t in total) / len(total), 3), "budget_s": BUDGET_S,
           "fallback_share": round(fallbacks / replies, 3), "meets_target": pct(total, 0.95) <= BUDGET_S,
           "reading": "one model answers one request at a time, so three characters answer in turn; compare all_three_s with the budget before deciding how much the ReactionQueue must hide",
           "limit": "staff replies carry no case facts; twelve fixed trainee turns; measured with whatever else was running"}
    path = ROOT / "artifacts" / "ai-bench" / "multiagent-eval.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(out, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(out, indent=1))
