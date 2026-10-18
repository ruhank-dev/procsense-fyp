"""Scores an embedding head, exactly as the service would use it, on a frozen synthetic set: `uv run --project tools tools/bench/score_head.py --data v3 [--head PATH] [--out reports/ai/FILE.json]`.

Used for the fresh hold-out (tests/classifier_synthetic_v3) that was written and judged AFTER the v2 test split had been scored once, to measure the changes made after that look (a retrained head and the
critical-mass guard). Nothing is tuned here: the head, its per-act thresholds and its `critical_mass_max` are taken as they are, every sentence goes through `EmbeddingActClassifier.classify` (the Ollama
embedding, the head, the regular-expression guard and the critical-mass guard), and the set is refused unless its files match the hashes in its manifest. A set is scored once per head; the result file says
which head (a hash of its weights) and which set (the manifest hash). The result carries `valid_for_claims: false` and `independent: false`: the set is model-written and AI-judged.
Reported: macro-F1, accuracy, what was credited and its precision, false credits, the share not credited, critical acts credited from speech (must be 0), critical sentences credited under an ordinary act
(must be 0), per-class precision, recall and false positives, and the same on the rows both judges agreed with.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
for sub in ("server", "tools/ai", "tools/bench"):
    sys.path.insert(0, str(ROOT / sub))
import bench_classifier as BC  # noqa: E402
from classify.embedding import EmbeddingActClassifier, OllamaEmbedder, load_head  # noqa: E402
from eval_classifier import f1_scores  # noqa: E402

SETS = {"v3": ROOT / "tests" / "classifier_synthetic_v3", "v2test": BC.SYNTH2}


def frozen_rows(folder: Path, split: str) -> tuple[list[dict], str]:
    manifest_path = folder / "manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    for name, digest in manifest["files"].items():
        if hashlib.sha256((folder / name).read_bytes()).hexdigest() != digest:
            raise SystemExit(f"REFUSED: {name} does not match the frozen hash in {manifest_path.relative_to(ROOT).as_posix()}")
    return BC.rows_of(folder / f"{split}.jsonl"), hashlib.sha256(manifest_path.read_bytes()).hexdigest()


def score(clf: EmbeddingActClassifier, rows: list[dict]) -> dict:
    labels = [a["id"] for a in json.loads((ROOT / "content/assessment/taxonomy.json").read_text(encoding="utf-8"))["acts"]]
    results = [clf.classify(r["text"]) for r in rows]
    if any(x.source == "unavailable" for x in results):
        raise SystemExit("the embedding service did not answer for every sentence: no score written")
    truth, pred = [r["act"] for r in rows], [x.act for x in results]
    credited = [x.credited for x in results]
    n_c = sum(credited)
    wrong = sum(c and t != p for t, p, c in zip(truth, pred, credited))
    per = f1_scores(truth, pred, labels)
    certain = [i for i, r in enumerate(rows) if r.get("judge_status") == "certain"]
    out = {"rows": len(rows), "macro_f1": round(BC.macro_f1(truth, pred, labels), 3), "accuracy": round(sum(t == p for t, p in zip(truth, pred)) / len(rows), 3),
           "credited": n_c, "credited_precision": round((n_c - wrong) / n_c, 3) if n_c else None, "false_credits": wrong, "not_credited_share": round(1 - n_c / len(rows), 3),
           "critical_acts_credited_from_speech": sum(c and p in clf.critical for p, c in zip(pred, credited)),
           "critical_said_but_credited_as_other_act": sum(c and t in clf.critical for t, c in zip(truth, credited)),
           "critical_said_total": sum(t in clf.critical for t in truth),
           "per_class": {lab: {"f1": round(v[0], 3), "precision": round(v[1], 3), "recall": round(v[2], 3), "support": v[3], "false_positives": sum(t != lab and p == lab for t, p in zip(truth, pred))} for lab, v in per.items()},
           "by_bucket": {b: {"rows": sum(1 for r in rows if r["bucket"] == b), "accuracy": round(sum(truth[i] == pred[i] for i, r in enumerate(rows) if r["bucket"] == b) / sum(1 for r in rows if r["bucket"] == b), 3)} for b in sorted({r["bucket"] for r in rows})}}
    if certain:
        out["macro_f1_on_rows_both_judges_agreed_with"] = round(BC.macro_f1([truth[i] for i in certain], [pred[i] for i in certain], labels), 3)
        out["rows_both_judges_agreed_with"] = len(certain)
    return out


class TfidfActClassifier:
    """The TF-IDF candidate of the benchmark, trained and calibrated the way tools/bench/bench_classifier.py does (word 1 to 2 grams, C chosen on the v2 dev split, per-act thresholds tuned on v2 dev), so it can be
    scored on the same frozen hold-out as the embedding head. It is a benchmark object, not a service: it needs scikit-learn (`uv run --with scikit-learn`)."""

    def __init__(self):
        from classify import classifier as C
        from sklearn.feature_extraction.text import TfidfVectorizer
        from sklearn.linear_model import LogisticRegression
        data = BC.load_data("synthetic2", True)
        tax = C.taxonomy()["acts"]
        labels = [a["id"] for a in tax]
        self.critical = BC.critical_acts(tax)
        self.vec = TfidfVectorizer(ngram_range=(1, 2), lowercase=True, sublinear_tf=True).fit([r["text"] for r in data["train"]])
        best = None
        for c in (1, 10, 100):
            m = LogisticRegression(max_iter=3000, C=c).fit(self.vec.transform([r["text"] for r in data["train"]]), [r["act"] for r in data["train"]])
            f1 = BC.macro_f1([r["act"] for r in data["dev"]], list(m.predict(self.vec.transform([r["text"] for r in data["dev"]]))), labels)
            if best is None or f1 > best[0] + 1e-9:
                best = (f1, c, m)
        self.model, self.C = best[2], best[1]
        p = self.model.predict_proba(self.vec.transform([r["text"] for r in data["dev"]]))
        classes = list(self.model.classes_)
        self.thresholds = C.calibrate([(r["act"], classes[i], float(row.max())) for r, i, row in zip(data["dev"], p.argmax(axis=1), p)], C.taxonomy(), BC.MIN_PRECISION)
        self.head = {"trained_on_rows": len(data["train"]), "critical_mass_max": None}

    def classify(self, text: str):
        from classify.classifier import Result
        from classify.guard import critical_cue
        p = self.model.predict_proba(self.vec.transform([text]))[0]
        i = int(p.argmax())
        act, conf = str(self.model.classes_[i]), float(p[i])
        ok = act not in self.critical and act != "NONE" and self.thresholds.get(act) is not None and conf >= self.thresholds[act] and not critical_cue(text)
        return Result(act, conf, ok, "tfidf", act in self.critical)


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--data", choices=sorted(SETS), default="v3")
    ap.add_argument("--split", default="test")
    ap.add_argument("--head", default="")
    ap.add_argument("--ollama", default="http://127.0.0.1:11434")
    ap.add_argument("--out", default="")
    ap.add_argument("--candidate", choices=["embeddings", "tfidf", "tfidf_head"], default="embeddings", help="tfidf: the benchmark's scikit-learn candidate; tfidf_head: the plain-Python head the service runs (content/assessment/act_head_tfidf.json or --head)")
    a = ap.parse_args(argv)
    rows, manifest_sha = frozen_rows(SETS[a.data], a.split)
    if a.candidate == "tfidf":
        clf = TfidfActClassifier()
        out = {"set": a.data, "split": a.split, "set_manifest_sha256": manifest_sha, "candidate": "tfidf_logreg", "C": clf.C, "head_trained_on_rows": clf.head["trained_on_rows"], "valid_for_claims": False,
               "independent": False, "provenance": "model-written, AI-judged synthetic hold-out; not independent and not real-world validation"}
        out.update(score(clf, rows))
        print(json.dumps({k: v for k, v in out.items() if k not in ("per_class", "by_bucket")}, indent=1))
        if a.out:
            Path(a.out).write_text(json.dumps(out, indent=1) + "\n", encoding="utf-8", newline="\n")
        return 0
    head_path = Path(a.head) if a.head else BC.ROOT / "content" / "assessment" / ("act_head_tfidf.json" if a.candidate == "tfidf_head" else "act_head.json")
    head = load_head(head_path)
    out = {"set": a.data, "split": a.split, "set_manifest_sha256": manifest_sha, "head_file": head_path.relative_to(ROOT).as_posix() if head_path.is_relative_to(ROOT) else str(head_path),
           "head_sha256": hashlib.sha256(head_path.read_bytes()).hexdigest(), "head_trained_on_rows": head.get("trained_on_rows"), "critical_mass_max": head.get("critical_mass_max"),
           "valid_for_claims": False, "independent": False, "provenance": "model-written, AI-judged synthetic hold-out; not independent and not real-world validation"}
    from classify.tfidf import TfidfActClassifier as PlainTfidf
    out["candidate"] = a.candidate
    out.update(score(PlainTfidf(head) if a.candidate == "tfidf_head" else EmbeddingActClassifier(OllamaEmbedder(a.ollama), head), rows))
    print(json.dumps({k: v for k, v in out.items() if k not in ("per_class", "by_bucket")}, indent=1))
    if a.out:
        Path(a.out).write_text(json.dumps(out, indent=1) + "\n", encoding="utf-8", newline="\n")
        print("wrote", a.out)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
