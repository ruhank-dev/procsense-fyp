"""Measures the patient dialogue on the real cases (p1-ai-language-s04, EXP-AI-2): `uv run --project tools tools/ai/eval_dialogue.py --model qwen3.5:4b-q4_K_M [--cases 12]`.

For each approved P0 case and each non-volunteered history topic the nurse asks about the topic twice: once with the fact earned (in the context, as the Core would after the question) and once with it withheld
(not earned). The production code answers (server/dialogue). Reported: for EARNED questions, the share answered by the model (accepted by the schema and the grounding validator), the share that fell back to
the authored line, and the share where the reply used the fact (fact id cited); for WITHHELD questions, the share answered cannot_recall, the share where the hidden fact's own words appear in the reply
(leaks: the validator's lexical check and a stricter check of the fact's content words), and latency. The leak count the plan wants is zero. The lexical checks cannot see a paraphrase, so every WITHHELD reply is written to
the output for a person to read (DT-190 / DT-203). Context stand-in: the Core's context builder is the source of truth (ContextBuilder.cs); this builds the same shape from the case file.
"""
import argparse
import json
import re
import statistics
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools"))


def context(case, asked_topic, earn):
    facts = []
    for h in case["history"]:
        if h.get("variant") is not None:
            continue
        facts.append({"id": "h." + h["topic"], "value": h["answer"], "disclosed": False, "disclosable_now": bool(h["volunteered"]) or (earn and h["topic"] == asked_topic)})
    return {"role": "patient", "case": case["id"], "allowed_intents": json.loads((ROOT / "content" / "ai" / "intents.json").read_text(encoding="utf-8"))["roles"]["patient"], "facts": facts, "last_turns": [], "results": []}


INTENTS = json.loads((ROOT / "content" / "ai" / "intents.json").read_text(encoding="utf-8"))["roles"]


def words(text):
    return {w for w in re.findall(r"[a-z]{5,}", text.lower())}


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", default="qwen3.5:4b-q4_K_M")
    ap.add_argument("--cases", type=int, default=12)
    ap.add_argument("--timeout", type=float, default=60.0)
    ap.add_argument("--team", action="store_true", help="answer through Dialogue.team (the patient and the staff of the three voices) and score the patient part")
    ap.add_argument("--out", default="")
    a = ap.parse_args()
    from composer import blocks as B
    from dialogue import dialogue as D
    from llm import client as L
    client = L.OllamaClient()
    if a.model not in client.models():
        raise SystemExit(f"{a.model} is not installed")
    cases = [c for c in B.load_cases() if c["tier"] == "P0" and c["status"].startswith("APPROVED")][: a.cases]
    d = D.Dialogue(client, model=a.model, timeout=a.timeout)
    earned = {"n": 0, "model": 0, "used_fact": 0, "fallback": 0}
    withheld = {"n": 0, "cannot_recall": 0, "fallback": 0, "validator_leaks": 0, "word_leaks": 0}
    lat, samples = [], []
    for c in cases:
        authored = {"h." + h["topic"]: h["answer"] for h in c["history"] if h.get("variant") is None}
        for h in c["history"]:
            if h.get("variant") is not None or h["volunteered"] or len(h["answer"]) < 12:
                continue
            q = f"Can you tell me about your {h['topic'].replace('_', ' ')}?"
            fid = "h." + h["topic"]
            for earn in (True, False):
                d.cache.clear()
                t = time.monotonic()
                if a.team:
                    d.cache.clear()
                    staff = {role: {"role": role, "case": c["id"], "allowed_intents": INTENTS[role], "facts": [], "last_turns": [], "results": []} for role in ("nurse", "anaesthesia")}
                    r = d.team(context(c, h["topic"], earn), staff, q, age=c["identity"]["age"], sex=c["identity"]["sex"], authored=authored, asked_fact=fid)["patient"]
                else:
                    r = d.reply(context(c, h["topic"], earn), q, age=c["identity"]["age"], sex=c["identity"]["sex"], authored=authored, asked_fact=fid)
                lat.append((time.monotonic() - t) * 1000)
                if earn:
                    earned["n"] += 1
                    earned["model"] += r.source == "model"
                    earned["fallback"] += r.source == "fallback"
                    earned["used_fact"] += fid in r.fact_ids
                else:
                    withheld["n"] += 1
                    withheld["cannot_recall"] += r.intent == "cannot_recall"
                    withheld["fallback"] += r.source == "fallback"
                    withheld["validator_leaks"] += "DISCLOSURE_LEAK" in r.reasons
                    shared = words(h["answer"]) & words(r.text) - words(q)
                    withheld["word_leaks"] += bool(shared)
                    if len(samples) < 40:
                        samples.append({"case": c["id"], "topic": h["topic"], "hidden_fact": h["answer"], "reply": r.text, "intent": r.intent, "source": r.source, "shared_words": sorted(shared)})
    out = {"model": a.model, "digest": client.models().get(a.model, ""), "cases": len(cases), "earned": earned, "withheld": withheld, "ms_p50": round(statistics.median(lat)), "ms_p95": round(sorted(lat)[int(0.95 * len(lat)) - 1]), "samples": samples}
    path = Path(a.out) if a.out else ROOT / "artifacts" / "ai-bench" / "dialogue-eval.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(out, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps({k: v for k, v in out.items() if k != "samples"}, indent=1))
