"""The language-model classifier, 4B against 9B, on the same sentences: `uv run --project tools tools/bench/llm_classifier_compare.py --model qwen3.5:4b-q4_K_M [--per-act 5]`.

Same prompt, same closed list of acts, temperature as set by the production client, same rows for both models: a stratified sample of the v3 test split (`--per-act` rows per act, fixed seed; the split was scored once
before for the heads, so this is post hoc) and the 48 sentences of adversarial hold-out 3. Reported: accuracy and macro-F1 over the acts, how often a critical-content sentence was named as an ORDINARY act before
the guard and after it (the unsafe case), the share of answers the model could not give, and latency per call. Results go to reports/ai/llm-classifier-<size>.json. SYNTHETIC and AI-judged, not independent.
`sample_rows()` is shared with tools/bench/heads_same_rows.py so the shipped heads are scored on exactly these sentences.
"""
from __future__ import annotations

import argparse
import json
import random
import statistics
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
for sub in ("server", "tools/ai"):
    sys.path.insert(0, str(ROOT / sub))
from classify import classifier as C  # noqa: E402
from classify.guard import critical_cue  # noqa: E402
from eval_classifier import f1_scores  # noqa: E402
from llm import client as L  # noqa: E402


def sample_rows(per_act: int = 5) -> tuple[list[dict], list[dict]]:
    """The two row sets both models and the heads are scored on: a stratified sample of the v3 hold-out (fixed seed) and the 48 sentences of adversarial set 4."""
    labels = [t["id"] for t in C.taxonomy()["acts"]]
    rows = [json.loads(l) for l in (ROOT / "tests/classifier_synthetic_v3/test.jsonl").read_text(encoding="utf-8").splitlines() if l.strip()]
    rng = random.Random(20261018)
    sample = []
    for act in labels:
        pool = [r for r in rows if r["act"] == act]
        rng.shuffle(pool)
        sample += pool[:per_act]
    adv = [{"id": r["id"], "text": r["text"], "act": r["act"]} for r in json.loads((ROOT / "tests/classifier_synthetic_v2/adversarial_critical_holdout3.json").read_text(encoding="utf-8"))["rows"]]
    return sample, adv


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", required=True)
    ap.add_argument("--ollama", default="http://127.0.0.1:11434")
    ap.add_argument("--per-act", type=int, default=5)
    ap.add_argument("--timeout", type=float, default=120.0)
    a = ap.parse_args(argv)
    tax = C.taxonomy()["acts"]
    labels, critical = [t["id"] for t in tax], {t["id"] for t in tax if t["critical"]}
    sample, adv = sample_rows(a.per_act)
    client = L.OllamaClient(a.ollama)
    if a.model not in client.models():
        raise SystemExit(f"{a.model} is not installed")
    clf = C.ActClassifier(client, model=a.model, timeout=a.timeout, thresholds={"per_act": {}})
    clf.classify("warm-up sentence")                                  # load the model before timing
    out = {"model": a.model, "synthetic": True, "independent": False, "sample_rows": len(sample), "adversarial_rows": len(adv), "seed": 20261018}
    for name, data in (("v3_test_sample", sample), ("adversarial_holdout3", adv)):
        ms, truth, pred, unavailable, raw_ordinary, guarded = [], [], [], 0, 0, 0
        for r in data:
            t0 = time.perf_counter()
            res = clf.classify(r["text"])
            ms.append((time.perf_counter() - t0) * 1000)
            truth.append(r["act"])
            if res.source == "unavailable":
                unavailable += 1
                pred.append("NONE")
                continue
            pred.append(res.act)
            if r["act"] in critical and res.act not in critical:
                raw_ordinary += 1
                guarded += not critical_cue(r["text"]) and res.act != "NONE"
        f1 = f1_scores(truth, pred, labels)
        ms.sort()
        out[name] = {"rows": len(data), "accuracy": round(sum(t == p for t, p in zip(truth, pred)) / len(data), 3), "macro_f1": round(statistics.mean(v[0] for v in f1.values() if v[3]), 3),
                     "unavailable": unavailable, "critical_rows_named_ordinary_before_guard": raw_ordinary, "critical_rows_named_ordinary_and_not_stopped_by_cues": guarded,
                     "latency_ms_p50": round(statistics.median(ms)), "latency_ms_p95": round(ms[int(len(ms) * 0.95) - 1])}
        print(name, json.dumps(out[name]))
    size = a.model.split(":")[1].split("-")[0]
    p = ROOT / "reports" / "ai" / f"llm-classifier-{size}.json"
    p.write_text(json.dumps(out, indent=1) + "\n", encoding="utf-8", newline="\n")
    print("wrote", p.relative_to(ROOT).as_posix())
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
