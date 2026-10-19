"""The ProcSense act benchmark: five ways to recognise a trainee's communication act, scored on the SAME sentences (ADR-0020): `uv run --project tools --with scikit-learn tools/bench/act_benchmark.py [--final] [--write]`.

Data: tests/action_bench_v1 (tools/bench/build_act_dataset.py; SYNTHETIC, labelled by construction, split by family and wrapper template). `dev` may be used to tune and to train; `test` is scored ONCE per run with --final and the run refuses a second look.

Approaches
  A  rules                  server/classify/rules.py: deterministic phrases, one act only, critical-adjacent topics and non-assertive sentences refused.
  B  retrieval + status     the hybrid embedding head names the candidate act (its own thresholds, critical-mass cap and critical-content cues); a SEPARATE status detector (word and character TF-IDF logistic regression trained on the dev rows' construction labels:
                            actual / denied / intended / hypothetical / reported / other_actor / incomplete / none) must say `actual`. No second head and no regular-expression assertion check, to isolate the status detector.
  C  model + validation     qwen3.5:4b-q4_K_M (prompt classify.interpret@1) proposes act, assertion, actor and evidence; credited only if every deterministic check passes (server/classify/llm_hybrid.py `validate`). The rules are NOT consulted.
  C2 rules, then the model  server/classify/llm_hybrid.py as built: a rule credit stands, a rule refusal on safety grounds is final, only an undecided sentence goes to the model.
  F  E + status detector     the existing gate, and the status detector must also say `actual` (registered before the test split was scored; it can only remove credit).
  E  the existing gate      server/classify/ensemble.py (hybrid head + TF-IDF head + critical-mass cap + cues + regular-expression assertion check).
  D  a small task-specific classifier (SetFit) was NOT run: the benchmark has 488 development sentences from 28 families; a few-shot contrastive fine-tune on that would learn the templates, needs torch and sentence-transformers (several GB) and has no valid held-out data to beat the baseline on. See ADR-0020.

Neither the embedding head, the model nor the status detector can award credit alone, and none of them reaches the scoring engine (ADR-0005): the result is evidence for the debrief. Reported per approach and per category (paraphrase, asr_error, negation, intention, hypothetical, reported, actor_change, incomplete, critical, critical_asr, none):
for categories where credit is wrong (everything except paraphrase and asr_error) the credit rate (false credit; critical categories must be 0), for the two where credit is right the recall (credited under the right act), credited precision, wrong-act credit, abstention, latency, model calls and memory.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import statistics
import sys
import time
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools" / "bench"))
import safety_gate as G  # noqa: E402
from classify.classifier import Result  # noqa: E402
from classify.embedding import OllamaEmbedder  # noqa: E402
from classify.ensemble import EnsembleActClassifier  # noqa: E402
from classify.guard import critical_cue  # noqa: E402
from classify.hybrid import HybridActClassifier  # noqa: E402
from classify.llm_hybrid import RulesThenModel  # noqa: E402
from classify.rules import RuleActClassifier  # noqa: E402

BENCH = ROOT / "tests" / "action_bench_v1"
REPORT = ROOT / "reports" / "ai" / "act-benchmark.json"
LOCK = ROOT / "artifacts" / "ai-bench" / "act-benchmark-test-used.json"
WRONG_CREDIT = {"negation", "intention", "hypothetical", "reported", "actor_change", "incomplete", "critical", "critical_asr", "none"}
RIGHT_CREDIT = {"paraphrase", "asr_error"}


def load(split: str) -> list[dict]:
    manifest = json.loads((BENCH / "manifest.json").read_text(encoding="utf-8"))
    p = BENCH / f"{split}.jsonl"
    if hashlib.sha256(p.read_bytes().replace(b"\r\n", b"\n")).hexdigest() != manifest["files"][f"{split}.jsonl"]:
        raise SystemExit(f"REFUSED: {p.name} does not match its frozen hash")
    return [json.loads(l) for l in p.read_text(encoding="utf-8").splitlines() if l.strip()]


class StatusDetector:
    """Dedicated assertion/status detector: TF-IDF word and character n-grams + logistic regression, trained on the dev construction labels only."""

    def __init__(self, dev: list[dict]):
        from scipy.sparse import hstack
        from sklearn.feature_extraction.text import TfidfVectorizer
        from sklearn.linear_model import LogisticRegression
        self.w = TfidfVectorizer(ngram_range=(1, 3), sublinear_tf=True, lowercase=True).fit([r["text"] for r in dev])
        self.c = TfidfVectorizer(analyzer="char_wb", ngram_range=(3, 5), sublinear_tf=True, lowercase=True).fit([r["text"] for r in dev])
        self._h = hstack
        x = self._x([r["text"] for r in dev])
        self.m = LogisticRegression(max_iter=4000, C=20).fit(x, [r["status"] for r in dev])

    def _x(self, texts):
        return self._h([self.w.transform(texts), self.c.transform(texts)]).tocsr()

    def status(self, text: str) -> str:
        return str(self.m.predict(self._x([text]))[0])


class RetrievalPlusStatus:
    def __init__(self, embedder, dev):
        self.head = HybridActClassifier(embedder)
        self.status = StatusDetector(dev)
        self.critical = self.head.critical

    def classify(self, sentence: str) -> Result:
        r = self.head.classify(sentence)
        if r.source == "unavailable":
            return r
        if not r.credited:
            return Result(r.act, r.confidence, False, "retrieval+status", r.critical, "", "HEAD_REFUSED")
        st = self.status.status(sentence)
        return Result(r.act, r.confidence, st == "actual", "retrieval+status", r.critical, "", "" if st == "actual" else f"STATUS:{st}")


class GatePlusStatus:
    """Approach F (registered before the test split was scored): the existing gate E, and in addition the dedicated status detector must say `actual`. It can only remove credit E would give."""

    def __init__(self, embedder, dev):
        self.gate = EnsembleActClassifier(embedder)
        self.status = StatusDetector(dev)
        self.critical = self.gate.critical

    def classify(self, sentence: str) -> Result:
        r = self.gate.classify(sentence)
        if r.source == "unavailable" or not r.credited:
            return r
        st = self.status.status(sentence)
        return Result(r.act, r.confidence, st == "actual", "ensemble+status", r.critical, "", "" if st == "actual" else f"STATUS:{st}")


class ModelOnly(RulesThenModel):
    """Approach C: the model proposes, the deterministic validation disposes; the rules are not asked."""

    def classify(self, sentence: str) -> Result:
        if self.client is None:
            return Result("NONE", 0.0, False, "unavailable", False, "NO_MODEL")
        from llm import gateway
        g = gateway.run(self.client, "classify.interpret", {"acts": self.listing, "sentence": sentence}, schema=self.schema(), model=self.model, timeout=self.timeout, retries=0, task="classify_interpret")
        if not g.ok:
            return Result("NONE", 0.0, False, "unavailable", False, "MODEL_UNAVAILABLE")
        why = self.validate(sentence, g.value)
        return Result(g.value["act"], 0.0, not why, "model", g.value["act"] in self.critical, "", why)


def judge(rows: list[dict], preds: list[Result]) -> dict:
    cats: dict[str, dict] = defaultdict(lambda: {"n": 0, "credited": 0, "right_act": 0})
    for r, p in zip(rows, preds):
        c = cats[r["category"]]
        c["n"] += 1
        c["credited"] += p.credited
        c["right_act"] += p.credited and p.act == r["act"]
    out = {}
    for k, c in sorted(cats.items()):
        d = {"n": c["n"], "credited": c["credited"], "abstained": c["n"] - c["credited"]}
        if k in RIGHT_CREDIT:
            d.update({"recall": round(c["right_act"] / c["n"], 3), "wrong_act_credit": c["credited"] - c["right_act"]})
        else:
            d.update({"false_credit": c["credited"], "false_credit_rate": round(c["credited"] / c["n"], 3)})
        out[k] = d
    right = [r for r in rows if r["category"] in RIGHT_CREDIT]
    cr = [(r, p) for r, p in zip(rows, preds) if p.credited]
    tot = {"critical_false_credit": sum(d.get("false_credit", 0) for k, d in out.items() if k.startswith("critical")), "critical_rows": sum(d["n"] for k, d in out.items() if k.startswith("critical")),
           "non_assertive_false_credit": sum(d.get("false_credit", 0) for k, d in out.items() if k in ("negation", "intention", "hypothetical", "reported", "actor_change", "incomplete")),
           "non_assertive_rows": sum(d["n"] for k, d in out.items() if k in ("negation", "intention", "hypothetical", "reported", "actor_change", "incomplete")),
           "legitimate_recall": round(sum(out[k]["recall"] * out[k]["n"] for k in RIGHT_CREDIT if k in out) / max(1, len(right)), 3),
           "legitimate_credited": sum(out[k]["credited"] for k in RIGHT_CREDIT if k in out), "legitimate_rows": len(right),
           "credited_precision": round(sum(p.act == r["act"] and r["expect_credit"] for r, p in cr) / len(cr), 3) if cr else None, "credited_total": len(cr),
           "abstention_rate_on_legitimate": round(1 - sum(out[k]["credited"] for k in RIGHT_CREDIT if k in out) / max(1, len(right)), 3),
           "status_errors": sum(1 for r, p in zip(rows, preds) if r["status"] != "actual" and p.credited)}
    return {"categories": out, "totals": tot}


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--ollama", default="http://127.0.0.1:11434")
    ap.add_argument("--final", action="store_true", help="score a frozen split (once per split)")
    ap.add_argument("--split", default="test", choices=["test", "test2"], help="which frozen split --final scores (test2 was written after test had been scored)")
    ap.add_argument("--approaches", default="A,B,C,C2,E")
    ap.add_argument("--write", action="store_true")
    a = ap.parse_args(argv)
    lock = LOCK if a.split == "test" else LOCK.with_name(f"act-benchmark-{a.split}-used.json")
    if a.final and lock.exists():
        raise SystemExit("REFUSED: the test split was already scored; a second look makes it a development set")
    split = a.split if a.final else "dev"
    rows = load(split)
    dev = load("dev")
    cache: dict[str, list[float] | None] = {}
    base = OllamaEmbedder(a.ollama)

    def emb(t: str):
        if t not in cache:
            cache[t] = base(t)
        return cache[t]
    emb.model = base.model                                              # type: ignore[attr-defined]
    approaches: dict[str, object] = {}
    want = a.approaches.split(",")
    calls = {"n": 0}
    if "A" in want:
        approaches["A_rules"] = RuleActClassifier()
    if "B" in want:
        approaches["B_retrieval_plus_status"] = RetrievalPlusStatus(emb, dev)
    client = None
    if "C" in want or "C2" in want:
        from llm import client as C
        client = C.OllamaClient(a.ollama)
        orig = client.chat

        def counting(*x, **k):
            calls["n"] += 1
            return orig(*x, **k)
        client.chat = counting                                          # type: ignore[method-assign]
    if "C" in want:
        approaches["C_model_validated"] = ModelOnly(client, timeout=90.0)
    if "C2" in want:
        approaches["C2_rules_then_model"] = RulesThenModel(client, timeout=90.0)
    if "E" in want:
        approaches["E_existing_gate"] = EnsembleActClassifier(emb)
    if "F" in want:
        approaches["F_existing_gate_plus_status"] = GatePlusStatus(emb, dev)
    out = {"synthetic": True, "independent": False, "split": split, "rows": len(rows), "dataset_manifest_sha256": hashlib.sha256((BENCH / "manifest.json").read_bytes()).hexdigest()[:16], "approaches": {}}
    for name, clf in approaches.items():
        before = calls["n"]
        preds, lat = [], []
        for r in rows:
            if name.startswith(("B_", "E_", "F_")):
                emb(r["text"])                                          # time the gate, not the embedder
            t = time.perf_counter()
            p = clf.classify(r["text"])
            for _ in range(2):                                          # a model call can time out once under load; two more tries, and only then the row counts as unavailable and the run is refused
                if p.source != "unavailable":
                    break
                time.sleep(3)
                p = clf.classify(r["text"])
            preds.append(p)
            lat.append((time.perf_counter() - t) * 1000)
        bad = sum(p.source == "unavailable" for p in preds)
        if bad or len(preds) != len(rows):
            raise SystemExit(f"REFUSED: {name} was unavailable for {bad} of {len(rows)} rows; nothing written")
        lat.sort()
        res = judge(rows, preds)
        res["latency_ms"] = {"median": round(statistics.median(lat), 2), "p95": round(lat[int(len(lat) * 0.95) - 1], 2)}
        res["model_calls"] = calls["n"] - before
        res["refusal_reasons"] = dict(Counter((p.refused or "credited" if p.credited else p.refused or "NO_ACT").split(":")[0] for p in preds).most_common(8))
        out["approaches"][name] = res
        t = res["totals"]
        print(f"{split:5s} {name:26s} critical false credit {t['critical_false_credit']}/{t['critical_rows']}  non-assertive credited {t['non_assertive_false_credit']}/{t['non_assertive_rows']}  legit recall {t['legitimate_recall']} ({t['legitimate_credited']}/{t['legitimate_rows']})  precision {t['credited_precision']}  p50 {res['latency_ms']['median']} ms  model calls {res['model_calls']}", flush=True)
    try:
        import httpx
        ps = httpx.get(a.ollama + "/api/ps", timeout=5).json().get("models", [])
        out["ollama_resident_models"] = [{"name": m["name"], "size_mb": round(m["size"] / 1e6), "vram_mb": round(m.get("size_vram", 0) / 1e6)} for m in ps]
    except Exception:
        out["ollama_resident_models"] = []
    try:
        import psutil
        out["python_rss_mb"] = round(psutil.Process().memory_info().rss / 1e6)
    except Exception:
        out["python_rss_mb"] = None
    if a.write:
        path = (REPORT if a.split == "test" else REPORT.with_name(f"act-benchmark-{a.split}.json")) if a.final else REPORT.with_name("act-benchmark-dev.json")
        path.write_text(json.dumps(out, indent=1) + "\n", encoding="utf-8", newline="\n")
        if a.final:
            lock.parent.mkdir(parents=True, exist_ok=True)
            lock.write_text(json.dumps({"scored": True}) + "\n", encoding="utf-8")
        print("wrote", path.relative_to(ROOT).as_posix())
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
