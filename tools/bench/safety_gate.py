"""Safety evaluation of the credit path of the communication classifier: `uv run --project tools tools/bench/safety_gate.py [--ollama URL] [--final] [--write]`.

Runs the production classifiers over every labelled set and reports, per set, bucket and variant, what matters for safety (all figures SYNTHETIC and AI-judged, none independent):

  false credit       a row whose true act is CRITICAL (or whose sentence is a denial, hypothetical or report) and which the classifier credited under an ordinary act: must be 0
  critical named     the classifier named the right critical act (recorded, never credited; this is "critical-action recall")
  wrong-action credit  an ordinary row credited under a different act than its label
  credited / precision  ordinary rows credited, and the share credited under the right act
  abstained          ordinary rows not credited (a guard, a threshold, NONE or a critical prediction)
  ordinary read as critical   ordinary-act rows the classifier named as a critical act (they lose credit: the cost of caution)
  95% Wilson interval for each rate.

Variants: `baseline` = the shipped embedding head and cues at the start of the session (read from git, --baseline-rev), `embedding` = the rebuilt embedding head alone, `tfidf` = the rebuilt TF-IDF head alone,
`ensemble` = what the service now runs (server/classify/ensemble.py). Sets: the v2 dev split (the heads' tuning set), the v2 test and v3 test splits (scored once before; post hoc here), the four adversarial sets
(the first three were used to train and tune; hold-out 3 is the set that exposed the failure), and the challenge set v5 (tests/classifier_challenge_v5), which only `--final` scores, once; the v4 result of the third session is carried over unchanged (a lock file in
artifacts/ai-bench records it; --allow-rerun overrides, deliberately). The 22M embedding model needs a running Ollama with all-minilm:22m.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
from classify.assertion import non_assertive  # noqa: E402
from classify.classifier import Result  # noqa: E402
from classify.embedding import EmbeddingActClassifier, OllamaEmbedder, load_head  # noqa: E402
from classify.hybrid import HybridActClassifier  # noqa: E402
from classify.ensemble import EnsembleActClassifier  # noqa: E402
from classify.tfidf import TfidfActClassifier  # noqa: E402

V2, V3, V5 = ROOT / "tests" / "classifier_synthetic_v2", ROOT / "tests" / "classifier_synthetic_v3", ROOT / "tests" / "classifier_challenge_v5"
OUT = ROOT / "artifacts" / "ai-bench"
REPORT = ROOT / "reports" / "ai" / "classifier-safety.json"
LOCK = OUT / "classifier-challenge-v5-used.json"


def jl(path: Path) -> list[dict]:
    return [json.loads(l) for l in path.read_text(encoding="utf-8").splitlines() if l.strip()]


def adv(name: str) -> list[dict]:
    return [{"id": r["id"], "text": r["text"], "act": r["act"], "bucket": "critical_evasive", "expect_credit": False} for r in json.loads((V2 / name).read_text(encoding="utf-8"))["rows"]]


def sets(final: bool) -> dict[str, list[dict]]:
    out = {"v2_dev": jl(V2 / "dev.jsonl"), "v2_test": jl(V2 / "test.jsonl"), "v3_test": jl(V3 / "test.jsonl"), "adv_tuning": adv("adversarial_critical.json"), "adv_holdout": adv("adversarial_critical_holdout.json"),
           "adv_holdout2": adv("adversarial_critical_holdout2.json"), "adv_holdout3": adv("adversarial_critical_holdout3.json")}
    if final:
        manifest = json.loads((V5 / "manifest.json").read_text(encoding="utf-8"))
        if hashlib.sha256((V5 / "challenge.json").read_bytes()).hexdigest() != manifest["files"]["challenge.json"]:
            raise SystemExit("REFUSED: challenge.json does not match its frozen hash")
        out["challenge_v5"] = json.loads((V5 / "challenge.json").read_text(encoding="utf-8"))["rows"]
    return out


def wilson(k: int, n: int) -> list[float] | None:
    if n == 0:
        return None
    z, p = 1.96, k / n
    d = 1 + z * z / n
    c = (p + z * z / (2 * n)) / d
    h = z * math.sqrt(p * (1 - p) / n + z * z / (4 * n * n)) / d
    return [round(max(0.0, c - h), 3), round(min(1.0, c + h), 3)]


def rate(k: int, n: int) -> dict:
    return {"k": k, "n": n, "rate": round(k / n, 3) if n else None, "ci95": wilson(k, n)}


def score(rows: list[dict], preds: list[Result], critical: set[str]) -> dict:
    by: dict[str, dict] = {}
    for r, p in zip(rows, preds):
        bucket = r.get("bucket", "plain")
        for key in (bucket, "ALL"):
            b = by.setdefault(key, dict(n=0, crit_rows=0, crit_named=0, false_credit=0, ord_rows=0, ord_credited=0, ord_right=0, ord_wrong=0, ord_abstained=0, ord_read_critical=0, na_rows=0, na_credited=0, unavailable=0))
            b["n"] += 1
            if p.source == "unavailable":
                b["unavailable"] += 1
                continue
            truth = r["act"]
            if r.get("expect_credit") is False and bucket == "non_assertive":
                b["na_rows"] += 1
                b["na_credited"] += p.credited
            elif truth in critical:
                b["crit_rows"] += 1
                b["crit_named"] += p.act == truth
                b["false_credit"] += p.credited
            else:
                b["ord_rows"] += 1
                b["ord_credited"] += p.credited
                b["ord_right"] += p.credited and p.act == truth
                b["ord_wrong"] += p.credited and p.act != truth
                b["ord_abstained"] += not p.credited
                b["ord_read_critical"] += p.act in critical
    out = {}
    for key, b in by.items():
        out[key] = {"rows": b["n"], "unavailable": b["unavailable"],
                    "critical_rows": b["crit_rows"], "false_credit": rate(b["false_credit"], b["crit_rows"]), "critical_named": rate(b["crit_named"], b["crit_rows"]),
                    "ordinary_rows": b["ord_rows"], "ordinary_credited": rate(b["ord_credited"], b["ord_rows"]), "credited_precision": rate(b["ord_right"], b["ord_credited"]),
                    "wrong_action_credit": rate(b["ord_wrong"], b["ord_rows"]), "abstained": rate(b["ord_abstained"], b["ord_rows"]), "ordinary_read_as_critical": rate(b["ord_read_critical"], b["ord_rows"]),
                    "non_assertive_rows": b["na_rows"], "non_assertive_credited": rate(b["na_credited"], b["na_rows"])}
    return out


def per_class(rows: list[dict], preds: list[Result]) -> dict:
    """Precision, recall and F1 of the NAMED act (credit is not applied) per act, and their macro mean over the acts that occur in the labels."""
    acts = sorted({r["act"] for r in rows})
    out = {}
    for a in acts:
        tp = sum(r["act"] == a and p.act == a for r, p in zip(rows, preds))
        fp = sum(r["act"] != a and p.act == a for r, p in zip(rows, preds))
        fn = sum(r["act"] == a and p.act != a for r, p in zip(rows, preds))
        pr, rc = (tp / (tp + fp) if tp + fp else 0.0), (tp / (tp + fn) if tp + fn else 0.0)
        out[a] = {"precision": round(pr, 3), "recall": round(rc, 3), "f1": round(2 * pr * rc / (pr + rc), 3) if pr + rc else 0.0, "support": tp + fn}
    out["_macro_f1"] = round(sum(v["f1"] for k, v in out.items()) / len(acts), 3)
    return out


def baseline_head(rev: str, path: str) -> dict | None:
    try:
        raw = subprocess.run(["git", "show", f"{rev}:{path}"], cwd=ROOT, capture_output=True, check=True).stdout
    except (OSError, subprocess.CalledProcessError):
        return None
    tmp = OUT / f"baseline-{Path(path).name}"
    OUT.mkdir(parents=True, exist_ok=True)
    tmp.write_bytes(raw)
    return load_head(tmp)


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--ollama", default="http://127.0.0.1:11434")
    ap.add_argument("--baseline-rev", default="b73d8d8", help="the commit whose heads are the 'baseline' variant (the shipped heads at the start of the third session)")
    ap.add_argument("--final", action="store_true", help="also score the frozen challenge set v5 (once)")
    ap.add_argument("--allow-rerun", action="store_true")
    ap.add_argument("--write", action="store_true")
    a = ap.parse_args(argv)
    if a.final and LOCK.exists() and not a.allow_rerun:
        raise SystemExit("REFUSED: the challenge set v4 has already been scored (it is scored once per candidate). Use --allow-rerun only deliberately.")
    cache: dict[str, list[float] | None] = {}
    base = OllamaEmbedder(a.ollama)

    def embedder(t: str):
        if t not in cache:
            cache[t] = base(t)
        return cache[t]
    embedder.model = base.model                                     # type: ignore[attr-defined]
    old = baseline_head(a.baseline_rev, "content/assessment/act_head.json")
    variants: dict[str, object] = {}
    if old is not None:
        variants["baseline"] = EmbeddingActClassifier(embedder, old)
    variants["embedding"] = EmbeddingActClassifier(embedder)
    variants["tfidf"] = TfidfActClassifier()
    variants["hybrid"] = HybridActClassifier(embedder)
    v1 = EnsembleActClassifier(embedder)
    v1.embedding = EmbeddingActClassifier(embedder)                              # the gate of the third session: embedding head and TF-IDF head
    variants["ensemble_v1"] = v1
    variants["ensemble"] = EnsembleActClassifier(embedder)
    critical = variants["ensemble"].critical
    data = sets(a.final)
    result = {"synthetic": True, "independent": False, "baseline_rev": a.baseline_rev if old is not None else None, "heads": {}, "sets": {}, "latency_ms": {}}
    for name in ("embedding", "tfidf"):
        h = variants[name].head
        result["heads"][name] = {"trained_on_rows": h.get("trained_on_rows"), "critical_mass_max": h.get("critical_mass_max"), "C": h.get("C"), "sha256": hashlib.sha256(json.dumps(h.get("coef"), sort_keys=True).encode()).hexdigest()[:16]}
    for sn, rows in data.items():
        result["sets"][sn] = {"rows": len(rows), "variants": {}}
        result["sets"][sn]["per_class"] = {}
        for vn, clf in variants.items():
            preds = [clf.classify(r["text"]) for r in rows]
            result["sets"][sn]["variants"][vn] = score(rows, preds, critical)
            if sn in ("v2_dev", "v2_test", "v3_test", "challenge_v5"):
                result["sets"][sn]["per_class"][vn] = per_class(rows, preds)
    if not a.final and REPORT.exists():                                  # the frozen challenge set is scored once: carry its recorded result over instead of dropping it from the report
        old = json.loads(REPORT.read_text(encoding="utf-8"))
        for key in ("challenge_v4", "challenge_v5"):
            if key in old.get("sets", {}):
                result["sets"][key] = old["sets"][key]
                result[key + "_scored_with"] = old.get(key + "_scored_with", "the committed heads and guards of the commit that first wrote this file")
    if a.final:
        result["challenge_v5_scored_with"] = "hybrid head and TF-IDF head built by tools/bench/build_safe_heads.py in the fourth session, assertion rules of server/classify/assertion.py, cap 0.05; v5 was written before the false-reject analysis"
    sample = [r["text"] for r in data["v2_dev"][:40]]
    for vn, clf in variants.items():
        times = []
        for t in sample:
            cache.pop(t, None)
            t0 = time.perf_counter()
            clf.classify(t)
            times.append((time.perf_counter() - t0) * 1000)
        times.sort()
        result["latency_ms"][vn] = {"median": round(times[len(times) // 2], 2), "p95": round(times[int(len(times) * 0.95) - 1], 2)}
    na = [non_assertive(r["text"]) for r in data["v2_dev"]]
    result["non_assertive_flagged_on_v2_dev"] = {"flagged": sum(bool(x) for x in na), "of": len(na)}
    lost = {f"{sn}/{vn}": v["ALL"]["unavailable"] for sn, x in result["sets"].items() for vn, v in x["variants"].items() if v["ALL"]["unavailable"] and sn != "challenge_v4"}
    if lost:
        raise SystemExit(f"REFUSED: the embedder was unavailable for some rows ({lost}); nothing written and the v5 lock untouched. Rerun with Ollama otherwise idle.")
    text = json.dumps(result, indent=1) + "\n"
    if a.final:
        OUT.mkdir(parents=True, exist_ok=True)
        LOCK.write_text(json.dumps({"scored": True, "sha256": hashlib.sha256(text.encode()).hexdigest()}) + "\n", encoding="utf-8")
    if a.write:
        REPORT.write_text(text, encoding="utf-8", newline="\n")
        print("wrote", REPORT.relative_to(ROOT).as_posix())
    for sn, s in result["sets"].items():
        for vn, v in s["variants"].items():
            al = v["ALL"]
            print(f"{sn:14s} {vn:9s} false_credit {al['false_credit']['k']}/{al['false_credit']['n']}  crit_named {al['critical_named']['rate']}  cred_prec {al['credited_precision']['rate']} ({al['credited_precision']['n']})  ord_credited {al['ordinary_credited']['rate']}  wrong {al['wrong_action_credit']['k']}  na_cred {al['non_assertive_credited']['k']}/{al['non_assertive_rows']}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
