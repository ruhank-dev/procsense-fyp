"""The shipped heads on exactly the rows the language-model classifiers were scored on (tools/bench/llm_classifier_compare.py): `uv run --project tools tools/bench/heads_same_rows.py` (needs Ollama with all-minilm:22m).

Accuracy and macro-F1 of the predicted act (credit is not applied), and how often a critical-content sentence is named as an ORDINARY act before and after the credit gate, for the embedding head, the TF-IDF head and the
ensemble gate, so the 4B and 9B figures in docs/research/classifier_benchmark/safety_gate.md can be read against the production path on the same sentences. Writes reports/ai/classifier-heads-same-rows.json.
Synthetic and AI-judged, not independent; the v3 hold-out was scored once for the heads before (post hoc here).
"""
from __future__ import annotations

import json
import statistics
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
for sub in ("server", "tools/ai", "tools/bench"):
    sys.path.insert(0, str(ROOT / sub))
from classify import classifier as C  # noqa: E402
from classify.embedding import EmbeddingActClassifier, OllamaEmbedder  # noqa: E402
from classify.ensemble import EnsembleActClassifier  # noqa: E402
from classify.tfidf import TfidfActClassifier  # noqa: E402
from eval_classifier import f1_scores  # noqa: E402
from llm_classifier_compare import sample_rows  # noqa: E402


def main() -> int:
    tax = C.taxonomy()["acts"]
    labels, critical = [t["id"] for t in tax], {t["id"] for t in tax if t["critical"]}
    emb = OllamaEmbedder("http://127.0.0.1:11434")
    variants = {"embedding_head": EmbeddingActClassifier(emb), "tfidf_head": TfidfActClassifier(), "ensemble_gate": EnsembleActClassifier(emb)}
    sample, adv = sample_rows()
    out = {"synthetic": True, "independent": False, "rows": {"v3_test_sample": len(sample), "adversarial_holdout3": len(adv)}}
    for vn, clf in variants.items():
        out[vn] = {}
        for name, data in (("v3_test_sample", sample), ("adversarial_holdout3", adv)):
            res = [clf.classify(r["text"]) for r in data]
            truth, pred = [r["act"] for r in data], [x.act for x in res]
            f1 = f1_scores(truth, pred, labels)
            out[vn][name] = {"accuracy": round(sum(t == p for t, p in zip(truth, pred)) / len(data), 3), "macro_f1": round(statistics.mean(v[0] for v in f1.values() if v[3]), 3),
                             "critical_rows_named_ordinary": sum(t in critical and p not in critical for t, p in zip(truth, pred)), "critical_rows_credited": sum(t in critical and x.credited for t, x in zip(truth, res))}
        print(vn, json.dumps(out[vn]))
    (ROOT / "reports" / "ai" / "classifier-heads-same-rows.json").write_text(json.dumps(out, indent=1) + "\n", encoding="utf-8", newline="\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
