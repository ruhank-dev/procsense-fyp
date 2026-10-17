"""Benchmark of three communication-act classifiers: `uv run --project tools --with scikit-learn tools/bench/bench_classifier.py [--data smoke|independent] [--skip-qwen] [--skip-embed] [--limit N]`.

Candidates (nothing else): 1. TF-IDF + logistic regression, 2. sentence embeddings (Ollama all-minilm:22m) + logistic regression, 3. the existing Qwen 3.5 4B classifier (server/classify/classifier.py, unchanged prompt,
temperature 0 and a fixed seed set HERE, in this process only). Reused: f1_scores (tools/ai/eval_classifier.py), calibrate and ActClassifier (server/classify/classifier.py), the duplicate checks of tools/bench/dataset.py.

--data smoke       the old synthetic set (tests/classifier, SD-006): templates shared by all splits, one generator, self-labelled. It only proves the pipeline runs. The result file says valid_for_claims false.
--data independent the frozen, independently authored dev and test set (tests/classifier_independent, made by tools/bench/dataset.py freeze). It refuses to run unless the manifest exists and the files match its hashes,
                   and the test split is scored once (a lock file records it; --allow-test-rerun overrides, deliberately). Training is always the old synthetic set; thresholds are tuned on dev only.

Safety: nothing here writes content/assessment/thresholds.json, the mapping, the gate or any scoring file; results go to artifacts/ai-bench/ (git-ignored). A speech act is "credited" only if it is not NONE, not a critical
act (taken from mapping.json, which must agree with the taxonomy) and its confidence reaches the threshold tuned on dev; the count of critical acts credited from speech is reported and must be 0.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import random
import statistics
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
for sub in ("server", "tools/ai", "tools/bench"):
    sys.path.insert(0, str(ROOT / sub))
import dataset as DS                                  # noqa: E402  (near-duplicate checks)
from eval_classifier import f1_scores                 # noqa: E402

OLD = ROOT / "tests" / "classifier"
INDEP = ROOT / "tests" / "classifier_independent"
OUT = ROOT / "artifacts" / "ai-bench"
COSINE_REVIEW = 0.9                                   # embedding cosine at or above this between a dev/test sentence and a training sentence is listed for review (paraphrase leakage)
MIN_PRECISION = 0.8
RESAMPLES = 1000


def rows_of(path: Path) -> list[dict]:
    return [json.loads(l) for l in path.read_text(encoding="utf-8").splitlines() if l.strip()]


def load_data(mode: str, allow_rerun: bool) -> dict:
    train = [{**r, "group": r["speaker"], "bucket": "plain"} for n in ("train", "dev", "test") for r in rows_of(OLD / f"{n}.jsonl")] if mode == "independent" else \
            [{**r, "group": r["speaker"], "bucket": "plain"} for r in rows_of(OLD / "train.jsonl")]
    if mode == "smoke":
        return {"train": train, "dev": [{**r, "group": r["speaker"], "bucket": "plain"} for r in rows_of(OLD / "dev.jsonl")],
                "test": [{**r, "group": r["speaker"], "bucket": "plain"} for r in rows_of(OLD / "test.jsonl")], "valid_for_claims": False, "manifest": None,
                "asr": asr_copies(OUT / "asr-smoke-copies.jsonl", None),
                "provenance": "SMOKE: old synthetic template data from one generator (SD-006), labelled by that generator, templates shared by all splits. Proves the pipeline runs. NOT an independent benchmark and not evidence of how any classifier does on real trainees."}
    manifest_path = INDEP / "manifest.json"
    if not manifest_path.exists():
        raise SystemExit("BLOCKED: tests/classifier_independent/manifest.json does not exist. The independent dev and test set has not been authored; see docs/research/classifier_benchmark/README.md.")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    for name, digest in manifest["files"].items():
        if hashlib.sha256((INDEP / name).read_bytes()).hexdigest() != digest:
            raise SystemExit(f"REFUSED: {name} does not match the frozen hash in the manifest")
    lock = OUT / "classifier-bench-test-used.json"
    if lock.exists() and not allow_rerun and json.loads(lock.read_text(encoding="utf-8")).get("manifest_sha") == hashlib.sha256(manifest_path.read_bytes()).hexdigest():
        raise SystemExit("REFUSED: the independent test split has already been scored for this manifest (it is scored once). Use --allow-test-rerun only deliberately.")
    dev, test = rows_of(INDEP / "dev.jsonl"), rows_of(INDEP / "test.jsonl")
    if not all(r.get("independent") for r in dev + test):
        raise SystemExit("REFUSED: rows without independent provenance")
    return {"train": train, "dev": [{**r, "group": r["author"]} for r in dev], "test": [{**r, "group": r["author"]} for r in test], "valid_for_claims": True, "manifest": manifest,
            "asr": asr_copies(INDEP / "asr_test.jsonl", test),
            "provenance": "independent authors, second-labelled critical acts, adjudicated disagreements (see manifest)"}


def asr_copies(path: Path, source_rows: list[dict] | None) -> list[dict]:
    """Recogniser copies made by tools/bench/asr_roundtrip.py. For the independent set each copy must point at a frozen test sentence whose text still hashes to the recorded value."""
    if not path.exists():
        return []
    rows = rows_of(path)
    if source_rows is not None:
        by_id = {r["id"]: r for r in source_rows}
        for r in rows:
            src = by_id.get(r["derived_from"])
            if src is None or hashlib.sha256(src["text"].encode("utf-8")).hexdigest() != r["source_sha256"]:
                raise SystemExit(f"REFUSED: ASR copy {r['id']} does not match a frozen test sentence")
    return rows


def critical_acts(tax: list[dict]) -> set[str]:
    mapping = set(json.loads((ROOT / "content" / "assessment" / "mapping.json").read_text(encoding="utf-8"))["critical_acts"])
    flagged = {a["id"] for a in tax if a["critical"]}
    if mapping != flagged:
        raise SystemExit(f"STOP: taxonomy critical acts and mapping.json critical_acts disagree: {sorted(mapping ^ flagged)}")
    return mapping


def macro_f1(truth: list[str], pred: list[str], labels: list[str]) -> float:
    present = [l for l in labels if l in set(truth)]
    s = f1_scores(truth, pred, present)
    return sum(v[0] for v in s.values()) / len(present)


def credited_stats(truth: list[str], pred: list[str], credited: list[bool]) -> dict:
    n_c = sum(credited)
    wrong = sum(c and t != p for t, p, c in zip(truth, pred, credited))
    return {"credited": n_c, "credited_share": round(n_c / len(truth), 3), "abstained_share": round(1 - n_c / len(truth), 3), "false_credits": wrong,
            "credited_precision": round((n_c - wrong) / n_c, 3) if n_c else None}


def bootstrap(truth, pred, credited, groups, labels, seed=0, n=RESAMPLES):
    """Group-resampled (by author, or by speaker in smoke) samples of macro-F1 and credited precision; the same resamples for every candidate (seeded), so differences are paired."""
    rng = random.Random(seed)
    ids: dict[str, list[int]] = {}
    for i, g in enumerate(groups):
        ids.setdefault(g, []).append(i)
    keys = sorted(ids)
    f1s, precs = [], []
    for _ in range(n):
        idx = [i for g in (rng.choice(keys) for _ in keys) for i in ids[g]]
        f1s.append(macro_f1([truth[i] for i in idx], [pred[i] for i in idx], labels))
        c = [i for i in idx if credited[i]]
        precs.append(sum(truth[i] == pred[i] for i in c) / len(c) if c else None)
    return f1s, precs


def interval(xs: list[float | None]) -> list[float | None]:
    v = sorted(x for x in xs if x is not None)
    return [round(v[int(0.025 * len(v))], 3), round(v[int(0.975 * len(v)) - 1], 3)] if len(v) >= 40 else [None, None]


def vram_used_mib() -> int | None:
    try:
        return int(subprocess.run(["nvidia-smi", "--query-gpu=memory.used", "--format=csv,noheader,nounits"], capture_output=True, text=True, timeout=10).stdout.split()[0])
    except Exception:
        return None


def rss_mib() -> float | None:
    try:
        import psutil
        return round(psutil.Process().memory_info().rss / 2**20, 1)
    except Exception:
        return None


def embed(texts: list[str], model: str, url: str) -> list[list[float]]:
    import httpx
    cache_path = OUT / f"embed-cache-{model.replace(':', '_')}.json"
    cache = json.loads(cache_path.read_text(encoding="utf-8")) if cache_path.exists() else {}
    key = lambda t: hashlib.sha1(t.encode("utf-8")).hexdigest()           # noqa: E731
    todo = sorted({t for t in texts if key(t) not in cache})
    for i in range(0, len(todo), 32):
        batch = todo[i:i + 32]
        r = httpx.post(f"{url}/api/embed", json={"model": model, "input": batch, "keep_alive": "5m"}, timeout=120)
        r.raise_for_status()
        for t, v in zip(batch, r.json()["embeddings"]):
            norm = sum(x * x for x in v) ** 0.5
            cache[key(t)] = [x / norm for x in v]
    if todo:
        OUT.mkdir(parents=True, exist_ok=True)
        cache_path.write_text(json.dumps(cache), encoding="utf-8")
    return [cache[key(t)] for t in texts]


def leakage(data: dict, vectors: dict | None) -> dict:
    """Wording shared between splits. Blocking (independent mode): any near-duplicate (token overlap >= 0.8) of a dev or test sentence in training, or between dev and test. Listed for review: embedding cosine >= 0.9."""
    tr = [r["text"] for r in data["train"]]
    out = {"cut_token_overlap": DS.NEAR_DUPLICATE, "cut_embedding_cosine": COSINE_REVIEW}
    for s in ("dev", "test"):
        t = [r["text"] for r in data[s]]
        hits = DS.near_duplicates(t, tr)
        out[f"{s}_rows_with_near_duplicate_in_train"] = len({i for i, _, _ in hits})
        out[f"{s}_share_with_near_duplicate_in_train"] = round(len({i for i, _, _ in hits}) / len(t), 3)
    out["dev_test_near_duplicate_pairs"] = len(DS.near_duplicates([r["text"] for r in data["dev"]], [r["text"] for r in data["test"]]))
    if vectors:
        import numpy as np
        a = np.array(vectors["train"])
        for s in ("dev", "test"):
            sims = np.array(vectors[s]) @ a.T
            out[f"{s}_share_with_embedding_cosine_ge_{COSINE_REVIEW}_in_train"] = round(float((sims.max(axis=1) >= COSINE_REVIEW).mean()), 3)
    return out


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--data", choices=["smoke", "independent"], default="smoke")
    ap.add_argument("--qwen-model", default="qwen3.5:4b-q4_K_M")
    ap.add_argument("--embed-model", default="all-minilm:22m")
    ap.add_argument("--ollama", default="http://127.0.0.1:11434")
    ap.add_argument("--skip-qwen", action="store_true")
    ap.add_argument("--skip-embed", action="store_true")
    ap.add_argument("--limit", type=int, default=0, help="score only the first N dev and N test rows (a quick check)")
    ap.add_argument("--allow-test-rerun", action="store_true")
    a = ap.parse_args(argv)
    from classify import classifier as C
    from sklearn.feature_extraction.text import TfidfVectorizer
    from sklearn.linear_model import LogisticRegression
    tax = C.taxonomy()["acts"]
    labels = [x["id"] for x in tax]
    critical = critical_acts(tax)
    data = load_data(a.data, a.allow_test_rerun)
    if a.limit:
        data["dev"], data["test"] = data["dev"][:a.limit], data["test"][:a.limit]
    tr_y = [r["act"] for r in data["train"]]
    texts = {s: [r["text"] for r in data[s]] for s in ("train", "dev", "test", "asr")}
    vectors = None
    if not a.skip_embed:
        vectors = {s: embed(texts[s], a.embed_model, a.ollama) for s in texts if texts[s]}
    result = {"data": a.data, "valid_for_claims": data["valid_for_claims"], "provenance": data["provenance"], "rows": {s: len(data[s]) for s in ("train", "dev", "test", "asr")}, "labels": len(labels),
              "leakage": leakage(data, vectors), "candidates": {}}
    if a.data == "independent" and any(result["leakage"][k] for k in ("dev_rows_with_near_duplicate_in_train", "test_rows_with_near_duplicate_in_train", "dev_test_near_duplicate_pairs")):
        raise SystemExit("REFUSED: near-duplicate wording crosses splits: " + json.dumps(result["leakage"]))

    def lr_candidate(name, featurise, extra):
        """Fit logistic regression for C in (1, 10, 100) on training rows, choose C by dev macro-F1 (smaller wins ties), tune per-act credit thresholds on dev, score test once."""
        best = None
        for c in (1, 10, 100):
            t0 = time.monotonic()
            model = LogisticRegression(max_iter=3000, C=c).fit(featurise("train"), tr_y)
            dev_pred = list(model.predict(featurise("dev")))
            f1 = macro_f1([r["act"] for r in data["dev"]], dev_pred, labels)
            if best is None or f1 > best[0] + 1e-9:
                best = (f1, c, model, time.monotonic() - t0)
        _, c, model, fit_s = best

        def predict(split):
            p = model.predict_proba(featurise(split))
            classes = list(model.classes_)
            return [classes[i] for i in p.argmax(axis=1)], [float(row.max()) for row in p]
        dev_pred, dev_conf = predict("dev")
        thr = C.calibrate([(r["act"], p, cf) for r, p, cf in zip(data["dev"], dev_pred, dev_conf)], C.taxonomy(), MIN_PRECISION)
        t0 = time.monotonic()
        pred, conf = predict("test")
        lat_ms = (time.monotonic() - t0) / len(pred) * 1000
        asr_pred = predict("asr")[0] if data["asr"] else []
        return score(name, pred, conf, thr, {"C": c, "fit_seconds": round(fit_s, 2), "ms_per_sentence_batched": round(lat_ms, 3), **asr_stats(asr_pred), **extra})

    def asr_stats(asr_pred):
        if not asr_pred:
            return {}
        by_variant = {}
        for v in sorted({r["variant"] for r in data["asr"]}):
            ix = [i for i, r in enumerate(data["asr"]) if r["variant"] == v]
            by_variant[v] = {"rows": len(ix), "accuracy": round(sum(asr_pred[i] == data["asr"][i]["act"] for i in ix) / len(ix), 3)}
        return {"asr_copies": by_variant, "asr_note": "recogniser copies of test sentences, scored separately; not independent rows"}

    def score(name, pred, conf, thr, extra):
        truth = [r["act"] for r in data["test"]]
        credited = [p not in critical and p != "NONE" and thr.get(p) is not None and cf >= thr[p] for p, cf in zip(pred, conf)]
        groups = [r["group"] for r in data["test"]]
        cand = {"thresholds_tuned_on_dev": thr, "macro_f1": round(macro_f1(truth, pred, labels), 3), "accuracy": round(sum(t == p for t, p in zip(truth, pred)) / len(truth), 3), **credited_stats(truth, pred, credited),
                "critical_acts_credited_from_speech": sum(c and p in critical for p, c in zip(pred, credited)),
                "critical_said_but_credited_as_other_act": sum(c and t in critical for t, c in zip(truth, credited)), **extra}
        per = f1_scores(truth, pred, labels)
        cand["per_class"] = {l: {"f1": round(v[0], 3), "precision": round(v[1], 3), "recall": round(v[2], 3), "support": v[3], "false_positives": sum(t != l and p == l for t, p in zip(truth, pred))} for l, v in per.items()}
        buckets = {}
        for b in sorted({r["bucket"] for r in data["test"]}):
            ix = [i for i, r in enumerate(data["test"]) if r["bucket"] == b]
            buckets[b] = {"rows": len(ix), "accuracy": round(sum(truth[i] == pred[i] for i in ix) / len(ix), 3)}
        cand["by_bucket"] = buckets
        main_ix = [i for i, r in enumerate(data["test"]) if r["bucket"] != "ambiguous"]
        cand["macro_f1_without_ambiguous_rows"] = round(macro_f1([truth[i] for i in main_ix], [pred[i] for i in main_ix], labels), 3)
        f1s, precs = bootstrap(truth, pred, credited, groups, labels)
        cand["macro_f1_ci95"], cand["credited_precision_ci95"] = interval(f1s), interval(precs)
        boots[name] = f1s
        result["candidates"][name] = cand

    boots: dict[str, list[float]] = {}
    tf = TfidfVectorizer(ngram_range=(1, 2), lowercase=True, sublinear_tf=True).fit(texts["train"])
    mats = {s: tf.transform(texts[s]) for s in texts if texts[s]}
    lr_candidate("tfidf_logreg", lambda s: mats[s], {"features": "TF-IDF word 1-2 grams"})
    if vectors:
        import numpy as np
        arr = {s: np.array(v) for s, v in vectors.items() if v}
        singles = []
        for t in texts["test"][:20]:
            t0 = time.monotonic()
            embed([t + " "], a.embed_model, a.ollama)                       # uncached single call: the real per-sentence cost
            singles.append((time.monotonic() - t0) * 1000)
        lr_candidate("embeddings_logreg", lambda s: arr[s], {"embedding_model": a.embed_model, "dimension": arr["train"].shape[1], "ms_per_sentence_single_call_p50": round(statistics.median(singles), 1),
                                                           "ms_per_sentence_single_call_p95": round(sorted(singles)[int(0.95 * len(singles)) - 1], 1)})
    if not a.skip_qwen:
        from llm import client as L
        L.SAMPLING = {"temperature": 0, "top_p": 1.0, "top_k": 1, "seed": 0}          # this process only: deterministic decoding for the benchmark
        client = L.OllamaClient(a.ollama)
        if a.qwen_model not in client.models():
            raise SystemExit(f"{a.qwen_model} is not installed")
        clf = C.ActClassifier(client, model=a.qwen_model, timeout=60.0, thresholds={"per_act": {}})
        vram0 = vram_used_mib()

        def run(rows):
            res, ms = [], []
            for r in rows:
                t0 = time.monotonic()
                res.append(clf.classify(r["text"]))
                ms.append((time.monotonic() - t0) * 1000)
            return res, ms
        dres, _ = run(data["dev"])
        thr = C.calibrate([(r["act"], x.act, x.confidence) for r, x in zip(data["dev"], dres)], C.taxonomy(), MIN_PRECISION)
        tres, ms = run(data["test"])
        ares = run(data["asr"])[0] if data["asr"] else []
        score("qwen_3_5_4b", [x.act for x in tres], [x.confidence for x in tres], thr,
              {**asr_stats([x.act for x in ares]), "model": a.qwen_model, "unavailable": sum(x.source == "unavailable" for x in tres), "ms_p50": round(statistics.median(ms)), "ms_p95": round(sorted(ms)[int(0.95 * len(ms)) - 1]),
               "vram_used_mib_before_and_after": [vram0, vram_used_mib()], "decoding": "temperature 0, top_k 1, seed 0 (benchmark process only)"})
    result["process_rss_mib"] = rss_mib()
    names = list(result["candidates"])
    diffs = {}
    for i, x in enumerate(names):
        for y in names[i + 1:]:
            d = [p - q for p, q in zip(boots[x], boots[y])]
            diffs[f"{x} minus {y}"] = {"macro_f1_difference": round(result["candidates"][x]["macro_f1"] - result["candidates"][y]["macro_f1"], 3), "ci95": interval(d)}
    result["paired_macro_f1_differences"] = diffs
    result["verdict"] = verdict(result)
    if a.data == "independent" and not a.limit:
        (OUT / "classifier-bench-test-used.json").write_text(json.dumps({"manifest_sha": hashlib.sha256((INDEP / "manifest.json").read_bytes()).hexdigest(), "candidates": names}) + "\n", encoding="utf-8")
    OUT.mkdir(parents=True, exist_ok=True)
    path = OUT / f"classifier-bench-{a.data}{'-limit' if a.limit else ''}.json"
    path.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({k: v for k, v in result.items() if k != "candidates"}, indent=1))
    for n, c in result["candidates"].items():
        print(f"{n:20} macro-F1 {c['macro_f1']} {c['macro_f1_ci95']}  credited {c['credited']} precision {c['credited_precision']} {c['credited_precision_ci95']}  critical credited {c['critical_acts_credited_from_speech']}")
    print("wrote", path)
    return 0


def verdict(result: dict) -> str:
    if not result["valid_for_claims"]:
        return "NOT A VALID EVALUATION: smoke data (shared templates, one generator, self-labelled). The numbers show the pipeline runs and nothing else."
    cands = result["candidates"]
    if len(cands) < 3:
        return "incomplete run: fewer than the three candidates, no winner declared"
    order = sorted(cands, key=lambda n: -cands[n]["macro_f1"])
    best, second = order[0], order[1]
    d = result["paired_macro_f1_differences"].get(f"{best} minus {second}") or {"ci95": [None, None]}
    lo = cands[best]["credited_precision_ci95"][0]
    if d["ci95"][0] is not None and d["ci95"][0] > 0 and lo is not None and lo >= MIN_PRECISION and cands[best]["critical_acts_credited_from_speech"] == 0:
        return f"{best} is ahead of {second} (paired macro-F1 interval excludes 0) and its credited-precision lower bound meets {MIN_PRECISION}"
    return "no clear winner: the best two are not separated by the paired macro-F1 interval, or credited precision does not clearly meet the target; collect more independent data"


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
