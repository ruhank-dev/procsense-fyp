"""The three ways to recognise a trainee's communication act, on the same sentences with the same scoring: `uv run --project tools tools/bench/approach_compare.py [--approaches A,B,C] [--sets ...] [--final] [--write]` (ADR-0020).

  A  rules only                   server/classify/rules.py
  B  rules first, the 4B model    server/classify/llm_hybrid.py (needs a running Ollama with qwen3.5:4b-q4_K_M)
  C  the existing gate            server/classify/ensemble.py (hybrid embedding head + TF-IDF head + guards; needs all-minilm:22m)

Every approach is scored by tools/bench/safety_gate.py's `score` (false credit on critical or non-assertive sentences, ordinary credited, credited precision, wrong-action credit, abstention) and `per_class` (precision, recall, F1 of the NAMED act), per set and per bucket
(negation, correction, incomplete, ambiguous, asr_style, plain, critical_evasive, ordinary_hard, non_assertive), with Wilson intervals, latency and the number of model calls. The rows of a set are counted: a run in which an approach scored fewer rows than the set holds is refused and nothing
is written. Synthetic and AI-judged; none of the sets is independent. `--final` also scores the frozen challenge set v7 (written after the approaches were frozen), once.
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
import safety_gate as G  # noqa: E402
from classify.embedding import OllamaEmbedder  # noqa: E402
from classify.ensemble import EnsembleActClassifier  # noqa: E402
from classify.llm_hybrid import RulesThenModel  # noqa: E402
from classify.rules import RuleActClassifier  # noqa: E402

REPORT = ROOT / "reports" / "ai" / "classifier-approach-compare.json"
V7 = ROOT / "tests" / "classifier_challenge_v7"
V7_REPORT = ROOT / "reports" / "ai" / "classifier-approach-compare-v7.json"
LOCK = ROOT / "artifacts" / "ai-bench" / "classifier-challenge-v7-used.json"


def sets_for(names: list[str], final: bool, only_final: bool = False) -> dict[str, list[dict]]:
    base = G.sets(False)
    out = {k: base[k] for k in ("v2_dev", "v2_test", "v3_test", "adv_holdout3", "challenge_v4_dev", "challenge_v5_dev") if k in base}
    out["challenge_v6"] = json.loads((G.V6 / "challenge.json").read_text(encoding="utf-8"))["rows"]            # a development set since the fifth session's report
    if final:
        if only_final:
            out = {}
        out["challenge_v7"] = json.loads((V7 / "challenge.json").read_text(encoding="utf-8"))["rows"]
    return {k: v for k, v in out.items() if not names or k in names}


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--approaches", default="A,B,C")
    ap.add_argument("--sets", default="")
    ap.add_argument("--limit", type=int, default=0, help="score only the first N rows of each set (a smoke run; never written)")
    ap.add_argument("--final", action="store_true")
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--only-final", action="store_true", help="with --final: score only the frozen v7 set (the development sets are in the other result file)")
    ap.add_argument("--ollama", default="http://127.0.0.1:11434")
    a = ap.parse_args(argv)
    if a.final and LOCK.exists():
        raise SystemExit("REFUSED: the frozen set v7 was already scored; a second look makes it a development set")
    data = sets_for([s for s in a.sets.split(",") if s], a.final, a.only_final)
    if a.limit:
        data = {k: v[: a.limit] for k, v in data.items()}
    clfs: dict[str, object] = {}
    cache: dict[str, list[float] | None] = {}
    base = OllamaEmbedder(a.ollama)

    def emb(t: str):
        if t not in cache:
            cache[t] = base(t)
        return cache[t]
    emb.model = base.model                                       # type: ignore[attr-defined]
    calls = {"model": 0}
    if "A" in a.approaches:
        clfs["A_rules"] = RuleActClassifier()
    if "B" in a.approaches:
        from llm import client as C
        client = C.OllamaClient(a.ollama)
        b = RulesThenModel(client, timeout=60.0)
        orig = b.client.chat

        def counting(*x, **k):
            calls["model"] += 1
            return orig(*x, **k)
        b.client.chat = counting                                 # type: ignore[method-assign]
        clfs["B_rules_then_4b"] = b
    if "C" in a.approaches:
        clfs["C_existing_ensemble"] = EnsembleActClassifier(emb)
    critical = EnsembleActClassifier(emb).critical
    out = {"synthetic": True, "independent": False, "approaches": list(clfs), "sets": {}, "latency_ms": {}, "model_calls": {}}
    lat: dict[str, list[float]] = {k: [] for k in clfs}
    for sn, rows in data.items():
        out["sets"][sn] = {"rows": len(rows), "variants": {}, "per_class": {}}
        for name, clf in clfs.items():
            before = calls["model"]
            preds = []
            for r in rows:
                if name.startswith("C_"):
                    emb(r["text"])                               # warm the cache so the timing is the gate, not the embedder
                t = time.perf_counter()
                preds.append(clf.classify(r["text"]))
                lat[name].append((time.perf_counter() - t) * 1000)
            if any(p.source == "unavailable" for p in preds):
                raise SystemExit(f"REFUSED: {name} was unavailable for {sum(p.source == 'unavailable' for p in preds)} rows of {sn}; nothing written (a saturated or stopped Ollama gives valid-looking numbers)")
            sc = G.score(rows, preds, critical)
            if sc["ALL"]["rows"] != len(rows):
                raise SystemExit(f"REFUSED: {name} scored {sc['ALL']['rows']} rows of {len(rows)} in {sn}")
            out["sets"][sn]["variants"][name] = sc
            out["sets"][sn]["per_class"][name] = G.per_class(rows, preds)
            out["model_calls"].setdefault(name, 0)
            out["model_calls"][name] += calls["model"] - before
    for name, v in lat.items():
        v.sort()
        out["latency_ms"][name] = {"median": round(statistics.median(v), 2), "p95": round(v[int(len(v) * 0.95) - 1], 2), "calls": len(v)}
    for sn, s in out["sets"].items():
        for name, v in s["variants"].items():
            al = v["ALL"]
            print(f"{sn:17s} {name:20s} false_credit {al['false_credit']['k']}/{al['false_credit']['n']}  na_cred {al['non_assertive_credited']['k']}/{al['non_assertive_rows']}  ord_credited {al['ordinary_credited']['rate']} ({al['ordinary_credited']['k']}/{al['ordinary_credited']['n']})  prec {al['credited_precision']['rate']}  wrong {al['wrong_action_credit']['k']}")
    print(json.dumps(out["latency_ms"]), json.dumps(out["model_calls"]))
    if a.write and not a.limit:
        (V7_REPORT if a.only_final else REPORT).write_text(json.dumps(out, indent=1) + "\n", encoding="utf-8", newline="\n")
        if a.final:
            LOCK.parent.mkdir(parents=True, exist_ok=True)
            LOCK.write_text(json.dumps({"scored": True}) + "\n", encoding="utf-8")
        print("wrote", (V7_REPORT if a.only_final else REPORT).relative_to(ROOT).as_posix())
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
