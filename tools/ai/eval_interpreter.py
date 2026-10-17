"""Measures the performance interpreter against random picks (p1-personalisation-s04): `uv run --project tools tools/ai/eval_interpreter.py --model qwen3.5:9b-q4_K_M --learners 24`.

Simulated learners have a hidden cause: one or two of the eight difficulty dimensions are weak, which makes every behaviour that dimension trains weak (the others are mostly fine). Each learner plays 5 sessions
of random cases; the model then reads the profile and proposes hypotheses (production code: server/personalisation/interpreter.py). After 3 more sessions the tracker marks each hypothesis confirmed
(every cited competency is still weak) or refuted. Compared with a baseline that links the same number of competencies picked at random from the weak ones. Reported: hypotheses proposed, proposals the checks
refused (no hypothesis), the share confirmed by the model and by chance, and latency. A link that stays inside one dimension is the right kind of link in this simulation, which the model can only guess from the
plain names of the competencies; the result says how often it does. Results go to artifacts/ai-bench/ (git-ignored). Needs a running Ollama.
"""
import argparse
import json
import random
import statistics
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))

if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", default="qwen3.5:9b-q4_K_M")
    ap.add_argument("--learners", type=int, default=24)
    ap.add_argument("--timeout", type=float, default=300.0)
    a = ap.parse_args()
    from generation import objectives as ob
    from llm import client as C
    from personalisation import interpreter as I, learner_model as LM, simulate as S
    client = C.OllamaClient()
    if a.model not in client.models():
        raise SystemExit(f"{a.model} is not installed")
    tables = ob.Tables.load()
    scoring = json.loads((ROOT / "content" / "scoring" / "rubric.json").read_text(encoding="utf-8"))
    labels = {}
    try:
        beh = json.loads((ROOT / "content" / "assessment" / "mapping.json").read_text(encoding="utf-8"))
        labels = {b["id"]: b["label"] for b in beh["behaviours"]}
    except (OSError, KeyError, ValueError):
        pass
    behaviours = sorted(b for d in tables.dimensions for b in d.weak_behaviours)
    interp = I.Interpreter(client, model=a.model, timeout=a.timeout)
    model_conf = model_n = base_conf = base_n = refused = secs = 0
    samples = []
    for i in range(a.learners):
        rng = random.Random(7000 + i)
        weak_dims = rng.sample(tables.dimensions, rng.choice([1, 2]))
        mastery = {b: (rng.uniform(0.15, 0.4) if any(b in d.weak_behaviours for d in weak_dims) else rng.uniform(0.7, 0.95)) for b in behaviours}
        learner = S.Learner(i, behaviours, mastery, learning=0.0)
        m = LM.LearnerModel()
        for k in range(5):
            m.update(learner.play("X", "P0", rng.sample(behaviours, 12), k))
        t = time.monotonic()
        hyps = interp.propose(m, labels)
        secs += time.monotonic() - t
        if not hyps:
            refused += 1
        weak = [b for b in behaviours if m.mastery(b).status in ("weakness", "hypothesis")]
        tracker = I.Tracker()
        tracker.add(hyps)
        base = [I.Hypothesis(rng.sample(weak, min(2, len(weak))), ["e"], "low", "random", sessions_at_proposal=m.sessions) for _ in hyps] if len(weak) >= 2 else []
        base_t = I.Tracker()
        base_t.add(base)
        for k in range(5, 8):
            m.update(learner.play("X", "P0", rng.sample(behaviours, 12), k))
        tracker.update(m)
        base_t.update(m)
        model_n += len(tracker.items)
        model_conf += sum(h.status == "confirmed" for h in tracker.items)
        base_n += len(base_t.items)
        base_conf += sum(h.status == "confirmed" for h in base_t.items)
        if hyps and len(samples) < 6:
            samples.append({"weak_dimensions": [d.id for d in weak_dims], "hypotheses": [{"competencies": h.competencies, "text": h.text, "confidence": h.confidence, "status": h.status} for h in tracker.items]})
    out = {"model": a.model, "learners": a.learners, "hypotheses": model_n, "learners_with_no_accepted_proposal": refused, "confirmed_by_model": round(model_conf / max(model_n, 1), 3),
           "confirmed_by_random_picks": round(base_conf / max(base_n, 1), 3), "seconds_per_learner": round(secs / a.learners, 1), "samples": samples}
    path = ROOT / "artifacts" / "ai-bench" / "interpreter-eval.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(out, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({k: v for k, v in out.items() if k != "samples"}))
