"""Candidate fixes for legitimate speech recall, judged on identical rows and by what the ENGINE accepts (ADR-0022 follow-up): `tools/.venv/Scripts/python.exe tools/bench/recall_eval.py --split dev [--write]`; held-out splits need `--final`.

A candidate is a pair: the head's critical-mass cap (`cap`) and the engine's per-act evidence thresholds (`engine`: "default" is the engine as it was (an empty per-act table: 0.8 for every act), "calibrated" the classifier head's own per-act thresholds floored to three decimals, as tools/content/build_scoring.py now exports them).
Nothing else differs between candidates. Every offer the interpretation path makes is run through the engine's EvidenceStore (CLI `evidence-check`), so every count below is of ACCEPTED evidence, not of offers. Rows:
  bench   tests/action_bench_v1 (the split named): legitimate rows (paraphrase, asr_error), adversarial rows (denied, intended, imagined, reported, other speaker, broken off) and critical rows, each in the state derived from the engine's windows; the legitimate rows are also put OUTSIDE their window.
  v2      the ordinary-act rows (plain and recogniser-style) of the AI-judged synthetic v2 dev/test split, a second author family with labels from AI judges (not by construction): a corroboration of recall and of wrong-act credit, not a gate.
  panel   every existing set of evasive critical sentences and non-performed speech (tools/bench/recall_safety_panel.py), in the most permissive context; a leak counts only if the ENGINE accepts it.
Held-out discipline: the cap and the thresholds are chosen on `dev` (and the panel sets marked tuned-on); `test` and `test2` and the v2 test split are scored once per frozen candidate with `--final` and the result file is locked. Synthetic, preliminary, not independent.
"""
from __future__ import annotations

import argparse
import collections
import copy
import json
import math
import statistics
import sys
import time
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools" / "bench"))
from classify.embedding import OllamaEmbedder  # noqa: E402
from classify.ensemble import EnsembleActClassifier  # noqa: E402
from interpret.decide import Interpreter  # noqa: E402
import engine_acceptance as E  # noqa: E402
import interpretation_bench as B  # noqa: E402
import nli_zero_shot as N  # noqa: E402
import recall_safety_panel as P  # noqa: E402

CACHE = ROOT / "artifacts" / "ai-bench" / "interp"
LEGIT = ("paraphrase", "asr_error")
HEAD = json.loads((ROOT / "content" / "assessment" / "act_head_hybrid.json").read_text(encoding="utf-8"))
CALIBRATED = {a: math.floor(t * 1000) / 1000 for a, t in HEAD["thresholds"].items() if t is not None}       # what tools/content/build_scoring.py exports to mapping.json
CANDIDATES = {
    "baseline: cap 0.03, engine default 0.8": {"cap": 0.03, "engine": "default"},
    "engine calibrated only": {"cap": 0.03, "engine": "calibrated"},
    "cap 0.04, engine calibrated": {"cap": 0.04, "engine": "calibrated"},
    "cap 0.05, engine calibrated": {"cap": 0.05, "engine": "calibrated"},
    "cap 0.05, engine default": {"cap": 0.05, "engine": "default"},
}


class Embedder:
    """Cached embeddings: the benchmark caches, the panel cache and a v2 cache; a miss asks the local all-minilm:22m through Ollama."""
    def __init__(self, url: str = "http://127.0.0.1:11434"):
        self.inner = OllamaEmbedder(url)
        self.files = [CACHE / f"emb-{s}.json" for s in ("dev", "test", "test2")] + [P.CACHE, CACHE / "emb-v2.json"]
        self.c = {}
        for f in self.files:
            if f.exists():
                self.c.update(json.loads(f.read_text(encoding="utf-8")))
        self.new = {}

    def __call__(self, text):
        if text not in self.c:
            v = self.inner(text)
            if v is None:
                return None
            self.c[text] = self.new[text] = v
        return self.c[text]

    def save(self):
        if self.new:
            f = CACHE / "emb-v2.json"
            old = json.loads(f.read_text(encoding="utf-8")) if f.exists() else {}
            old.update(self.new)
            f.write_text(json.dumps(old), encoding="utf-8")


def interpreter(emb, cap) -> Interpreter:
    c = EnsembleActClassifier(emb)
    c.embedding.head = copy.copy(c.embedding.head)
    c.embedding.head["critical_mass_max"] = cap
    return Interpreter(c)


def offer_of(interp, row_id, text, ctx):
    t = time.perf_counter()
    o = interp.interpret(ctx, text, row_id, 0)
    ms = (time.perf_counter() - t) * 1000
    return o, ms


def judge(items: list[dict], engine: str) -> list[dict]:
    """items: {id, act, confidence, steps}. Returns the engine's verdict for each, in order."""
    return E.accept([{k: i[k] for k in ("id", "act", "confidence", "steps")} for i in items], {} if engine == "default" else CALIBRATED)        # "default": the engine as it was before 20 Oct 2026 (empty per-act table, 0.8 for every act)


def v2_rows(split: str) -> list[dict]:
    crit = set(B.CRITICAL)
    rows = [json.loads(l) for l in (ROOT / "tests" / "classifier_synthetic_v2" / f"{split}.jsonl").read_text(encoding="utf-8").splitlines() if l.strip()]
    return [r for r in rows if r["act"] not in crit and r["act"] != "NONE" and r["bucket"] in ("plain", "asr_style")]


def evaluate(name: str, cand: dict, emb, bench_split: str, with_v2: bool, with_panel: bool) -> dict:
    interp = interpreter(emb, cand["cap"])
    rows = N.load_rows(bench_split)
    out: dict = {"candidate": name, **cand}
    # ---- bench rows in window
    items, lat = [], []
    decisions = collections.Counter()
    for r in rows:
        ctx = B.conditions(r)["in_window"]
        o, ms = offer_of(interp, r["id"], r["text"], ctx)
        lat.append(ms)
        if r["category"] in LEGIT:
            decisions[o["decision"]] += 1
        if o["decision"] == "OFFER_EVIDENCE":
            items.append({"id": r["id"], "act": o["evidence"]["act"], "confidence": o["evidence"]["confidence"], "steps": ctx.steps, "row": r})
    ver = judge(items, cand["engine"])
    accepted = {i["id"]: (i, v) for i, v in zip(items, ver) if v["accepted"]}
    refused = {i["id"]: (i, v) for i, v in zip(items, ver) if not v["accepted"]}
    legit = [r for r in rows if r["category"] in LEGIT]
    adv = [r for r in rows if r["category"] in B.NC.ADVERSARIAL]
    crit = [r for r in rows if r["category"].startswith("critical")]
    tp = [r for r in legit if r["id"] in accepted and accepted[r["id"]][0]["act"] == r["act"]]
    fp_legit = [r for r in legit if r["id"] in accepted and accepted[r["id"]][0]["act"] != r["act"]]
    offered_right_refused = [r for r in legit if r["id"] in refused and refused[r["id"]][0]["act"] == r["act"]]
    out["bench"] = {"split": bench_split, "legit_rows": len(legit), "true_positive_accepted": len(tp), "false_negative": len(legit) - len(tp) - len(fp_legit), "wrong_act_accepted": len(fp_legit),
                    "right_act_offered_but_refused_by_engine": len(offered_right_refused), "refusal_reasons": dict(collections.Counter(v["reason"] for _, v in refused.values())),
                    "accepted_total": len(accepted), "credited_precision": f"{len(tp)}/{len(accepted)}" if accepted else "0/0",
                    "critical_accepted": f"{sum(1 for r in crit if r['id'] in accepted)}/{len(crit)}", "non_assertive_accepted": f"{sum(1 for r in adv if r['id'] in accepted)}/{len(adv)}",
                    "non_assertive_by_category": dict(collections.Counter(r["category"] for r in adv if r["id"] in accepted)),
                    "legit_decisions": dict(decisions), "latency_ms": {"median": round(statistics.median(lat), 1), "p95": round(sorted(lat)[int(len(lat) * 0.95) - 1], 1)}}
    by_act = collections.defaultdict(lambda: [0, 0])
    for r in legit:
        by_act[r["act"]][0] += 1
        by_act[r["act"]][1] += r in tp
    out["bench"]["recall_by_act"] = {a: f"{v[1]}/{v[0]}" for a, v in sorted(by_act.items())}
    out["bench"]["recall_by_category"] = {c: f"{sum(1 for r in tp if r['category'] == c)}/{sum(1 for r in legit if r['category'] == c)}" for c in LEGIT}
    out["_accepted_ids"] = sorted(accepted)
    out["_tp_ids"] = sorted(r["id"] for r in tp)
    out["_wrong"] = [{"id": r["id"], "text": r["text"], "label": r["act"], "accepted_as": accepted[r["id"]][0]["act"]} for r in fp_legit]
    # ---- out of window
    oo, items2 = [r for r in legit if "out_of_window" in B.conditions(r)], []
    for r in oo:
        ctx = B.conditions(r)["out_of_window"]
        o, _ = offer_of(interp, r["id"], r["text"], ctx)
        if o["decision"] == "OFFER_EVIDENCE":
            items2.append({"id": r["id"], "act": o["evidence"]["act"], "confidence": o["evidence"]["confidence"], "steps": ctx.steps, "row": r})
    ver2 = judge(items2, cand["engine"])
    out["bench"]["out_of_window_rows"] = len(oo)
    out["bench"]["out_of_window_own_act_accepted"] = sum(1 for i, v in zip(items2, ver2) if v["accepted"] and i["act"] == i["row"]["act"])
    # ---- v2
    if with_v2:
        v2 = {}
        for sp in ("dev", "test"):
            rs, its = v2_rows(sp), []
            for r in rs:
                ctx = B.conditions(r)["in_window"]
                o, _ = offer_of(interp, r["id"], r["text"], ctx)
                if o["decision"] == "OFFER_EVIDENCE":
                    its.append({"id": r["id"], "act": o["evidence"]["act"], "confidence": o["evidence"]["confidence"], "steps": ctx.steps, "row": r})
            vs = judge(its, cand["engine"])
            acc = [(i, v) for i, v in zip(its, vs) if v["accepted"]]
            v2[sp] = {"rows": len(rs), "true_positive_accepted": sum(1 for i, v in acc if i["act"] == i["row"]["act"]), "wrong_act_accepted": sum(1 for i, v in acc if i["act"] != i["row"]["act"])}
        out["v2_ordinary"] = v2
    # ---- panel
    if with_panel:
        panel = {}
        for sname, rs in P.sets().items():
            its = []
            for k, r in enumerate(rs):
                o, _ = offer_of(interp, f"p{k}", r["text"], P.PERMISSIVE)
                if o["decision"] == "OFFER_EVIDENCE":
                    its.append({"id": f"p{k}", "act": o["evidence"]["act"], "confidence": o["evidence"]["confidence"], "steps": P.PERMISSIVE.steps, "text": r["text"]})
            vs = judge(its, cand["engine"])
            panel[sname] = {"rows": len(rs), "offered": len(its), "accepted_by_engine": sum(1 for v in vs if v["accepted"]), "examples": [i["text"] for i, v in zip(its, vs) if v["accepted"]][:3]}
        out["panel"] = panel
    return out


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--split", default="dev", choices=["dev", "test", "test2"])
    ap.add_argument("--final", action="store_true", help="a held-out split: scored once per frozen candidate set; the result file is locked")
    ap.add_argument("--candidates", default="", help="semicolon-separated candidate names (default: all)")
    ap.add_argument("--confirm", action="store_true", help="a SECOND LOOK at a held-out split with the shipped configuration (the frozen candidate plus the assertion fixes made after the freeze); writes recall-confirm-<split>.json and leaves the locked frozen-run file alone")
    ap.add_argument("--write", action="store_true")
    a = ap.parse_args(argv)
    out_path = ROOT / "reports" / "ai" / (f"recall-confirm-{a.split}.json" if a.confirm else f"recall-eval-{a.split}.json")
    if a.confirm:
        a.final = True
        a.candidates = a.candidates or json.loads((ROOT / "tools" / "bench" / "recall_frozen.json").read_text(encoding="utf-8"))["chosen"]
    if a.split != "dev" and not a.confirm:
        if not a.final:
            raise SystemExit("REFUSED: test and test2 are held out; pass --final to score a frozen candidate set once")
        if out_path.exists():
            raise SystemExit(f"REFUSED: {out_path.name} already exists: a held-out split is scored once per frozen candidate set")
    names = [n.strip() for n in a.candidates.split(";") if n.strip()] or list(CANDIDATES)
    emb = Embedder()
    results = [evaluate(n, CANDIDATES[n], emb, a.split, with_v2=True, with_panel=True) for n in names]
    emb.save()
    base = next((r for r in results if r["candidate"].startswith("baseline")), results[0])
    for r in results:
        r["vs_baseline"] = {"newly_accepted_right": sorted(set(r["_tp_ids"]) - set(base["_tp_ids"])), "lost_right": sorted(set(base["_tp_ids"]) - set(r["_tp_ids"]))}
    for r in results:
        b = r["bench"]
        pan = sum(v["accepted_by_engine"] for v in r["panel"].values())
        print(f"{r['candidate']:<42} TP {b['true_positive_accepted']:>3}/{b['legit_rows']} FN {b['false_negative']:>3} wrong-act {b['wrong_act_accepted']} precision {b['credited_precision']:<8} refused-by-engine {b['right_act_offered_but_refused_by_engine']:>2} "
              f"crit {b['critical_accepted']} nonassert {b['non_assertive_accepted']} oow {b['out_of_window_own_act_accepted']} panel-leaks {pan} v2dev {r['v2_ordinary']['dev']['true_positive_accepted']}/{r['v2_ordinary']['dev']['rows']}+{r['v2_ordinary']['dev']['wrong_act_accepted']}w v2test {r['v2_ordinary']['test']['true_positive_accepted']}/{r['v2_ordinary']['test']['rows']}+{r['v2_ordinary']['test']['wrong_act_accepted']}w")
    if a.write:
        out_path.write_text(json.dumps({"synthetic": True, "independent": False, "split": a.split, "final": a.final, "confirm_second_look": a.confirm, "candidates": results}, indent=1) + "\n", encoding="utf-8", newline="\n")
        print("wrote", out_path.relative_to(ROOT).as_posix())
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
