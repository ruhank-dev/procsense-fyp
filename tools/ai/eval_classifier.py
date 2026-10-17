"""Measures the communication act classifier (p1-ai-language-s07, EXP-AI-3; p2-ai-extensions-s03): `uv run --project tools --with scikit-learn tools/ai/eval_classifier.py --model qwen3.5:4b-q4_K_M`.

Three classifiers on the frozen splits of tests/classifier (synthetic, split by speaker, SD-006):
  keyword   a bag-of-words logistic regression trained on the train split (the small classifier of p2-ai-extensions-s03, the baseline to beat);
  llm       the language model with the closed list of acts (server/classify/classifier.py); its confidences on the dev split set the per-act thresholds; scored on the test split.
Reported: macro-F1 and accuracy on the test split for each, the precision of the credited predictions of the LLM at its thresholds, the share of utterances credited, the critical acts credited (must be 0),
and latency. The thresholds are written to content/assessment/thresholds.json with --write. LIMIT: synthetic, templated wording from one generator; scores here are an optimistic bound on real speech and
measure agreement with the generator's label, not with two human labellers (DT-210). Needs a running Ollama for the llm part.
"""
import argparse
import json
import statistics
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
D = ROOT / "tests" / "classifier"


def load(name):
    return [json.loads(l) for l in (D / f"{name}.jsonl").read_text(encoding="utf-8").splitlines()]


def f1_scores(truth, pred, labels):
    out = {}
    for a in labels:
        tp = sum(t == a and p == a for t, p in zip(truth, pred))
        fp = sum(t != a and p == a for t, p in zip(truth, pred))
        fn = sum(t == a and p != a for t, p in zip(truth, pred))
        pr, rc = tp / (tp + fp) if tp + fp else 0.0, tp / (tp + fn) if tp + fn else 0.0
        out[a] = (2 * pr * rc / (pr + rc) if pr + rc else 0.0, pr, rc, tp + fn)
    return out


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", default="qwen3.5:4b-q4_K_M")
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--skip-llm", action="store_true")
    a = ap.parse_args()
    from classify import classifier as C
    from llm import client as L
    train, dev, test = load("train"), load("dev"), load("test")
    labels = [x["id"] for x in C.taxonomy()["acts"]]
    out = {"splits": json.loads((D / "splits.json").read_text(encoding="utf-8"))["rows"], "model": a.model}
    # --- the small classifier
    from sklearn.feature_extraction.text import TfidfVectorizer
    from sklearn.linear_model import LogisticRegression
    vec = TfidfVectorizer(ngram_range=(1, 2), lowercase=True, sublinear_tf=True)
    X = vec.fit_transform([r["text"] for r in train])
    lr = LogisticRegression(max_iter=2000, C=10).fit(X, [r["act"] for r in train])
    t = time.monotonic()
    pred = list(lr.predict(vec.transform([r["text"] for r in test])))
    lat = (time.monotonic() - t) / len(test) * 1000
    s = f1_scores([r["act"] for r in test], pred, labels)
    out["small"] = {"macro_f1": round(statistics.mean(v[0] for v in s.values()), 3), "accuracy": round(sum(p == r["act"] for p, r in zip(pred, test)) / len(test), 3), "ms_per_utterance": round(lat, 2)}
    if not a.skip_llm:
        client = L.OllamaClient()
        if a.model not in client.models():
            raise SystemExit(f"{a.model} is not installed")
        clf = C.ActClassifier(client, model=a.model, timeout=60.0, thresholds={"per_act": {}})

        def run(rows):
            res, ms = [], []
            for r in rows:
                t0 = time.monotonic()
                res.append(clf.classify(r["text"]))
                ms.append((time.monotonic() - t0) * 1000)
            return res, ms
        dres, _ = run(dev)
        thresholds = C.calibrate([(r["act"], x.act, x.confidence) for r, x in zip(dev, dres)], C.taxonomy())
        clf.thresholds = thresholds
        tres, ms = run(test)
        truth, got = [r["act"] for r in test], [x.act for x in tres]
        s = f1_scores(truth, got, labels)
        credited = [(r, x) for r, x in zip(test, tres) if x.credited]
        out["llm"] = {"macro_f1": round(statistics.mean(v[0] for v in s.values()), 3), "accuracy": round(sum(p == t for p, t in zip(got, truth)) / len(test), 3), "unavailable": sum(x.source == "unavailable" for x in tres),
                      "credited_share": round(len(credited) / len(test), 3), "credited_precision": round(sum(r["act"] == x.act for r, x in credited) / max(len(credited), 1), 3),
                      "critical_credited": sum(x.critical and x.credited for x in tres), "ms_p50": round(statistics.median(ms)), "ms_p95": round(sorted(ms)[int(0.95 * len(ms)) - 1]),
                      "thresholds": {k: v for k, v in thresholds.items()}, "per_act_f1": {k: round(v[0], 2) for k, v in s.items()}}
        if a.write:
            (ROOT / "content" / "assessment" / "thresholds.json").write_text(json.dumps({"note": "Per-act credit thresholds calibrated on the dev split (tools/ai/eval_classifier.py): the lowest confidence at which predictions of the act reach 0.8 precision. None = never credited. Critical acts are never credited from speech whatever their threshold.",
                                                                                         "model": a.model, "digest": client.models().get(a.model, ""), "dev_rows": len(dev), "per_act": thresholds}, indent=1) + "\n", encoding="utf-8", newline="\n")
    path = ROOT / "artifacts" / "ai-bench" / "classifier-eval.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(out, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(out, indent=1))
