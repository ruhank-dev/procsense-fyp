"""Zero-shot natural-language-inference classifier for the ProcSense communication acts (EXPERIMENT; no fine-tuning; not part of the application): `artifacts/nli-env/Scripts/python tools/bench/nli_zero_shot.py score --split dev|test|test2`.

Candidate under test ("Option A"): `facebook/bart-large-mnli` through Hugging Face Transformers, used as an entailment scorer. The utterance is the PREMISE; each of the 20 labels of content/assessment/taxonomy.json is a HYPOTHESIS built from that label's own definition, and every label is scored (no shortlist from any other classifier).
Nothing is trained or tuned here: the model, the hypothesis wording and the decision rule are fixed in tools/bench/nli_config.json before any split is scored.

Ontology and mode. The 20 labels are mutually exclusive: the annotation rubric (tests/classifier_synthetic_v2/rubric.md) says "One label per sentence. Apply the FIRST rule that fits", and the existing classifier is a single-label argmax. The experiment therefore uses SINGLE-LABEL mode (softmax over the
entailment logits of the 20 hypotheses, argmax = the label), as the Hugging Face zero-shot pipeline does with `multi_label=False`. The mode was fixed from the ontology, not from results. The raw logits of all three NLI classes for every label are written out; the softmax scores are ranking scores, NOT calibrated probabilities.

Context. The production classifier receives only the target sentence (`ClassifyIn.text`, server/routers/classify.py); no prior dialogue or scenario text exists at that point, so none is given. The script accepts optional `context` lines per row and keeps them in a separate field, but the benchmark rows have none and the premise is the target utterance alone.

Labels with no usable definition. NONE is defined as "Speech that shows none of the acts above (small talk, thinking aloud, noise)", which has no meaning as an entailment hypothesis ("above"); it is worded from its own parenthesis ("The speech is small talk, thinking aloud or noise."), recorded in nli_config.json and listed as a limitation. No other definition was changed.

Writes artifacts/ai-bench/nli/<split>.json (git-ignored). Weights are cached outside the repository (HF_HOME below).
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import statistics
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CONFIG = ROOT / "tools" / "bench" / "nli_config.json"
OUT = ROOT / "artifacts" / "ai-bench" / "nli"
os.environ.setdefault("HF_HOME", str(ROOT / "artifacts" / "hf-cache"))
BENCH = ROOT / "tests" / "action_bench_v1"


def load_config() -> dict:
    return json.loads(CONFIG.read_text(encoding="utf-8"))


def hypotheses(config: dict | None = None, style: str = "verbatim") -> dict[str, str]:
    """One hypothesis per label of the taxonomy, from the label's own definition (verbatim apart from the person and the first letter), except where the config overrides a label."""
    config = config or load_config()
    tax = json.loads((ROOT / "content" / "assessment" / "taxonomy.json").read_text(encoding="utf-8"))["acts"]
    out = {}
    for a in tax:
        if a["id"] in config["hypothesis_overrides"]:
            out[a["id"]] = config["hypothesis_overrides"][a["id"]]
        else:
            d = a["definition"].rstrip(".")
            if style == "no_parentheses":
                import re
                d = re.sub(r"\s*\([^)]*\)", "", d)
            out[a["id"]] = config["hypothesis_template"].format(definition=d[0].lower() + d[1:])
    return out


def softmax(xs: list[float]) -> list[float]:
    import math
    m = max(xs)
    e = [math.exp(x - m) for x in xs]
    s = sum(e)
    return [v / s for v in e]


def load_rows(split: str) -> list[dict]:
    manifest = json.loads((BENCH / "manifest.json").read_text(encoding="utf-8"))
    p = BENCH / f"{split}.jsonl"
    if hashlib.sha256(p.read_bytes().replace(b"\r\n", b"\n")).hexdigest() != manifest["files"][f"{split}.jsonl"]:
        raise SystemExit(f"REFUSED: {p.name} does not match its frozen hash")
    return [json.loads(l) for l in p.read_text(encoding="utf-8").splitlines() if l.strip()]


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    s = sub.add_parser("score")
    s.add_argument("--split", required=True, choices=["dev", "test", "test2"])
    s.add_argument("--device", default="auto", choices=["auto", "cuda", "cpu"])
    s.add_argument("--limit", type=int, default=0)
    s.add_argument("--dtype", default="auto", choices=["auto", "float16", "float32"], help="auto: float32 (on the GTX 1660 SUPER, float16 measured 1,181 ms per utterance against 202 ms in float32: Turing 16-series cards have no fast half precision)")
    s.add_argument("--out-tag", default="", help="suffix of the output file for a latency probe (never used for the benchmark splits)")
    s.add_argument("--warmup", type=int, default=5)
    s.add_argument("--style", default="verbatim", choices=["verbatim", "no_parentheses"], help="verbatim is the pre-registered wording; any other style is a sensitivity probe on the DEV split only and writes dev_<style>.json")
    a = ap.parse_args(argv)
    import numpy  # noqa: F401
    import psutil
    import torch
    import transformers
    from huggingface_hub import snapshot_download
    from transformers import AutoModelForSequenceClassification, AutoTokenizer
    config = load_config()
    if a.style != "verbatim" and a.split != "dev":
        raise SystemExit("REFUSED: a wording other than the pre-registered one may be tried on the dev split only")
    device = ("cuda" if torch.cuda.is_available() else "cpu") if a.device == "auto" else a.device
    path = snapshot_download(config["model"], revision=config.get("revision") or None, allow_patterns=["*.json", "merges.txt", "vocab.json", "model.safetensors"])            # one weight format only: the repository also holds PyTorch, TensorFlow, Flax and Rust copies
    revision = Path(path).name
    tok = AutoTokenizer.from_pretrained(path)
    model = AutoModelForSequenceClassification.from_pretrained(path, torch_dtype=torch.float32 if a.dtype == "auto" else getattr(torch, a.dtype)).to(device).eval()
    id2 = {int(k): v.lower() for k, v in model.config.id2label.items()}
    idx = {v: k for k, v in id2.items()}
    assert {"contradiction", "neutral", "entailment"} <= set(idx), id2
    hyp = hypotheses(config, a.style)
    labels = list(hyp)
    rows = load_rows(a.split)[: a.limit or None]
    results, times = [], []
    if device == "cuda":
        torch.cuda.reset_peak_memory_stats()
    for n, r in enumerate(rows):
        premise = r["text"] if not r.get("context") else r["text"]           # context lines, if a row has any, are kept in `context` and are not part of this experiment's premise
        enc = tok([premise] * len(labels), [hyp[l] for l in labels], return_tensors="pt", padding=True, truncation=True, max_length=config["max_length"]).to(device)
        if device == "cuda":
            torch.cuda.synchronize()
        t = time.perf_counter()
        with torch.no_grad():
            logits = model(**enc).logits.float().cpu()
        if device == "cuda":
            torch.cuda.synchronize()
        ms = (time.perf_counter() - t) * 1000
        if n >= a.warmup:
            times.append(ms)
        ent = [float(logits[i, idx["entailment"]]) for i in range(len(labels))]
        probs = softmax(ent)
        results.append({"id": r["id"], "scores": {l: round(p, 6) for l, p in zip(labels, probs)},
                        "raw": {l: [round(float(logits[i, idx["contradiction"]]), 4), round(float(logits[i, idx["neutral"]]), 4), round(float(logits[i, idx["entailment"]]), 4)] for i, l in enumerate(labels)}})
    times.sort()
    meta = {"split": a.split, "rows": len(rows), "model": config["model"], "revision": revision, "device": device, "dtype": str(next(model.parameters()).dtype).replace("torch.", ""),
            "torch": torch.__version__, "transformers": transformers.__version__, "python": sys.version.split()[0], "label_order": ["contradiction", "neutral", "entailment"],
            "gpu": torch.cuda.get_device_name(0) if device == "cuda" else None, "peak_gpu_memory_mb": round(torch.cuda.max_memory_allocated() / 1e6) if device == "cuda" else None,
            "process_rss_mb": round(psutil.Process().memory_info().rss / 1e6), "latency_ms_per_utterance": {"median": round(statistics.median(times), 1), "p95": round(times[int(len(times) * 0.95) - 1], 1), "n": len(times), "warmup_excluded": a.warmup,
                                                                                                         "note": "20 premise-hypothesis pairs in one batch; the model forward pass only (tokenisation excluded)"},
            "hypotheses": hyp, "style": a.style, "config_sha256": hashlib.sha256(CONFIG.read_bytes().replace(b"\r\n", b"\n")).hexdigest()}
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / f"{a.split}{"" if a.style == "verbatim" else "_" + a.style}{a.out_tag}.json").write_text(json.dumps({"meta": meta, "results": results}, indent=1) + "\n", encoding="utf-8", newline="\n")
    print(json.dumps({k: meta[k] for k in ("split", "rows", "device", "revision", "peak_gpu_memory_mb", "latency_ms_per_utterance")}, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
