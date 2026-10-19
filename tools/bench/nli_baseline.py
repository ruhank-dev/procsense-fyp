"""The current production classifier on the same sentences as the NLI experiment: `tools/.venv/Scripts/python.exe tools/bench/nli_baseline.py --split dev|test|test2`.

Runs the credit gate the service runs (server/classify/ensemble.py EnsembleActClassifier: hybrid embedding head + TF-IDF head + critical-mass cap + critical-content cues + assertion check) over the rows of tests/action_bench_v1/<split>.jsonl, unchanged, with the embedding model (all-minilm:22m through Ollama) called for EVERY sentence
(no cache), and records per row the label the head names (`act`), whether the gate credits it, the reason it did not, and the time of the call. Nothing is trained or tuned here; the heads were built before this benchmark existed and never saw it. Writes artifacts/ai-bench/nli/baseline-<split>.json (git-ignored).
"""
from __future__ import annotations

import argparse
import json
import statistics
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools" / "bench"))
from classify.embedding import OllamaEmbedder  # noqa: E402
from classify.ensemble import EnsembleActClassifier  # noqa: E402
import nli_zero_shot as N  # noqa: E402

OUT = ROOT / "artifacts" / "ai-bench" / "nli"


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--split", required=True, choices=["dev", "test", "test2"])
    ap.add_argument("--ollama", default="http://127.0.0.1:11434")
    a = ap.parse_args(argv)
    rows = N.load_rows(a.split)
    clf = EnsembleActClassifier(OllamaEmbedder(a.ollama))
    clf.classify("warm up")
    out, times = [], []
    for r in rows:
        t = time.perf_counter()
        p = clf.classify(r["text"])
        times.append((time.perf_counter() - t) * 1000)
        if p.source == "unavailable":
            raise SystemExit(f"REFUSED: the embedder was unavailable for {r['id']}; nothing written")
        out.append({"id": r["id"], "act": p.act, "credited": bool(p.credited), "refused": p.refused})
    times.sort()
    meta = {"split": a.split, "rows": len(rows), "classifier": "ensemble gate (hybrid head + TF-IDF head + guards), as served", "embedding_model": "all-minilm:22m",
            "latency_ms_per_utterance": {"median": round(statistics.median(times), 1), "p95": round(times[int(len(times) * 0.95) - 1], 1), "n": len(times), "note": "includes the call to the embedding model; measured on the same desktop"}}
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / f"baseline-{a.split}.json").write_text(json.dumps({"meta": meta, "results": out}, indent=1) + "\n", encoding="utf-8", newline="\n")
    print(json.dumps(meta, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
