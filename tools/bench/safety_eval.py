"""Evaluation of the unsafe-speech safety sets (p1-ai-language-s16, ADR-0023): `tools/.venv/Scripts/python.exe tools/bench/safety_eval.py --split dev|challenge|final [--impl DIR] [--name NAME] [--write]`.

Every row of tests/safety_challenge/<split>.jsonl goes through the interpretation path (server/interpret: the gate, the status check, deterministic validation, the decision) and every OFFER then goes through the ENGINE's own EvidenceStore (`evidence-check`).
"Credited" means the engine accepted the evidence, the only thing that can count. Nothing here is a model judgement; the labels are the provisional by-construction labels of the generator (synthetic, builder-written, NOT independent).
Views: `in_window` (the row's own condition: the step window of its act, a waiting question or read-back where the act needs one; an out-of-window or no-context row in the condition it names) and, for rows that must not be credited,
`permissive` (every step running, a patient question and a read-back both waiting: the most permissive state the engine can be in, so the text alone has to hold the line). A row counts as an unsafe credit if ANY view credits it.
Sessions (`group`): the second utterance of a pair is run after the first, with the behaviour the first earned already accepted (view `in_window`) and, as `engine_backstop`, with the session context left stale, so the engine's own duplicate rule is what holds.
`--impl DIR` runs another checkout's `server/` and `content/` (DIR/server, DIR/content) so an unchanged baseline is scored on the identical rows: `git archive 4a2ed00 server content | tar -x -C artifacts/baseline-4a2ed00`.
Embeddings: local all-minilm:22m through Ollama, cached by sentence in artifacts/ai-bench/safety/. The final split is scored ONCE per configuration name: --final locks reports/ai/safety/<name>-final.json and refuses a second run.
"""
from __future__ import annotations

import argparse
import collections
import json
import math
import sys
import time
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "bench"))

SETS = ROOT / "tests" / "safety_challenge"
CACHE = ROOT / "artifacts" / "ai-bench" / "safety"
REPORTS = ROOT / "reports" / "ai" / "safety"
ALL_STEPS = list(range(1, 16))


def wilson(k: int, n: int, z: float = 1.96) -> list[float]:
    if n == 0:
        return [0.0, 1.0]
    p = k / n
    d = 1 + z * z / n
    c = (p + z * z / (2 * n)) / d
    h = z * math.sqrt(p * (1 - p) / n + z * z / (4 * n * n)) / d
    return [round(max(0.0, c - h), 4), round(min(1.0, c + h), 4)]


def load(split: str) -> list[dict]:
    return [json.loads(x) for x in (SETS / f"{split}.jsonl").read_text(encoding="utf-8").splitlines() if x.strip()]


def setup(impl: Path):
    """imports of the implementation under test (its own server/ and content/)."""
    for m in [k for k in sys.modules if k.split(".")[0] in ("classify", "interpret", "server")]:
        del sys.modules[m]
    sys.path.insert(0, str(impl / "server"))
    from classify.embedding import OllamaEmbedder
    from classify.ensemble import EnsembleActClassifier
    from interpret.decide import Interpreter
    return OllamaEmbedder, EnsembleActClassifier, Interpreter


class Entry:
    def __init__(self, act, behaviour, critical):
        self.act, self.behaviour, self.critical = act, behaviour, critical


class Context:
    def __init__(self, mapping, critical, steps, pending=False, readback=False, accepted=()):
        self.steps, self.accepted, self.patient_question_pending, self.readback_pending = list(steps), list(accepted), pending, readback
        self.eligible = [Entry(a, b["id"], a in critical) for b in mapping["behaviours"] if set(b["steps"]) & set(steps) for a in b["ai_acts"]]


def window_of(mapping, act):
    return sorted({s for b in mapping["behaviours"] if act in b["ai_acts"] for s in b["steps"]})


def views_for(row, mapping, critical):
    """name -> (steps, pending, readback) for each view of the row."""
    act, cond = row["act"], row["condition"]
    win = window_of(mapping, act) if act else []
    needs_q, needs_r = act == "ANSWER_QUESTION", act == "CLOSED_LOOP"
    perm = (ALL_STEPS, True, True)
    if cond == "out_of_window":
        outside = [s for s in ALL_STEPS if s not in win]
        return {"in_window": (outside[:1], needs_q, needs_r)}
    if cond == "no_context":
        return {"in_window": (win[:1], False, False)}
    own = (win[:1] or ALL_STEPS, needs_q, needs_r)
    if not act or row["critical"]:
        return {"permissive": perm}
    if row["expected"] == "credit_allowed":
        return {"in_window": own}
    return {"in_window": own, "permissive": perm}


CONFIG_FILES = ["server/classify/assertion.py", "server/classify/ensemble.py", "server/classify/embedding.py", "server/classify/hybrid.py", "server/classify/tfidf.py", "server/classify/guard.py", "server/interpret/decide.py", "server/interpret/validate.py", "server/interpret/status.py",
                "content/assessment/act_head_hybrid.json", "content/assessment/mapping.json", "content/ai/critical_cues.json", "content/ai/interpret_phrases.json", "content/ai/lexicon.json"]
ENGINE_FILES = ["src/ProcSense.Core/Scoring/Evidence.cs", "src/ProcSense.Core/Speech/SpeechEvidenceSubsystem.cs", "src/ProcSense.Cli/Commands.cs"]


def configuration(impl: Path) -> dict:
    """what was run: the sha256 of every file of the configuration (the service side from the implementation under test, the engine side from this checkout), the embedding model, the commit and whether the tree was clean."""
    import hashlib
    import re
    import subprocess
    h = {f: hashlib.sha256((impl / f).read_bytes()).hexdigest()[:16] for f in CONFIG_FILES if (impl / f).exists()}
    e = {f: hashlib.sha256((ROOT / f).read_bytes()).hexdigest()[:16] for f in ENGINE_FILES}
    head = json.loads((impl / "content" / "assessment" / "act_head_hybrid.json").read_text(encoding="utf-8"))
    ver = re.search(r'INTERPRET_VERSION = "([^"]+)"', (impl / "server" / "interpret" / "decide.py").read_text(encoding="utf-8"))
    try:
        commit = subprocess.run(["git", "rev-parse", "HEAD"], capture_output=True, text=True, cwd=str(ROOT)).stdout.strip()
        dirty = bool(subprocess.run(["git", "status", "--porcelain", "--", "server", "content", "src", "tools", "tests"], capture_output=True, text=True, cwd=str(ROOT)).stdout.strip())
    except OSError:
        commit, dirty = "", None
    return {"implementation": str(impl.relative_to(ROOT)) if impl != ROOT and ROOT in impl.parents else str(impl), "service_files": h, "engine_files": e, "interpret_version": ver.group(1) if ver else "pre-versioned (4a2ed00 or earlier)",
            "critical_mass_max": head.get("critical_mass_max"), "embedding_model": "all-minilm:22m", "checkout_commit": commit, "checkout_dirty_in_code_paths": dirty, "command": "tools/.venv/Scripts/python.exe tools/bench/safety_eval.py --split <split> --name <name> [--impl DIR] --write"}


def run(split: str, impl: Path, ollama: str, engine_content: Path) -> dict:
    import engine_acceptance as E
    OllamaEmbedder, Ensemble, Interpreter = setup(impl)
    mapping = json.loads((impl / "content" / "assessment" / "mapping.json").read_text(encoding="utf-8"))
    critical = set(mapping["critical_acts"])
    CACHE.mkdir(parents=True, exist_ok=True)
    cpath = CACHE / "emb.json"
    cache = json.loads(cpath.read_text(encoding="utf-8")) if cpath.exists() else {}
    inner = OllamaEmbedder(ollama)

    def embed(text):
        if text not in cache:
            v = inner(text)
            if v is None:
                return None
            cache[text] = v
        return cache[text]

    interp = Interpreter(Ensemble(embed))
    rows = load(split)
    t0 = time.perf_counter()
    results: dict[tuple, dict] = {}
    pending_engine = []

    def one(row, view, steps, pending, readback, accepted=(), uid=None):
        c = Context(mapping, critical, steps, pending, readback, accepted)
        t = time.perf_counter()
        try:
            o = interp.interpret(c, row["text"], uid or row["id"], 0)
        except Exception as e:                               # a failure is recorded, never swallowed silently: it is a result
            o = {"decision": "ERROR", "reasons": [type(e).__name__], "evidence": None, "options": [], "points_to": []}
        ms = (time.perf_counter() - t) * 1000
        res = {"id": row["id"], "view": view, "decision": o["decision"], "reasons": o["reasons"], "offer": o["evidence"]["act"] if o["decision"] == "OFFER_EVIDENCE" else None,
               "confidence": o["evidence"]["confidence"] if o["decision"] == "OFFER_EVIDENCE" else None, "steps": list(steps), "ms": round(ms, 2), "engine": None, "credited": False, "credited_act": None, "behaviour": ""}
        results[(row["id"], view)] = res
        return res, c

    # pass 1: every row at position 0 (or without a group), all views
    singles = [r for r in rows if not r["group"] or r["position"] == 0]
    for r in singles:
        for view, (steps, pend, rb) in views_for(r, mapping, critical).items():
            res, _ = one(r, view, steps, pend, rb, uid=f"{r['id']}")
            if res["offer"]:
                pending_engine.append((r, view, res, ""))
    verdicts = E.accept([{"id": f"{r['id']}|{v}", "act": res["offer"], "confidence": res["confidence"], "steps": res["steps"]} for r, v, res, _ in pending_engine], content=engine_content)
    for (r, v, res, _), vd in zip(pending_engine, verdicts):
        res["engine"], res["credited"], res["behaviour"] = ("accepted" if vd["accepted"] else vd["reason"]), vd["accepted"], vd["behaviour"]
        res["credited_act"] = res["offer"] if vd["accepted"] else None
    # pass 2: the second utterance of each session, after the first
    second = [r for r in rows if r["group"] and r["position"] == 1]
    first_of = {r["group"]: r for r in rows if r["group"] and r["position"] == 0}
    batch = []
    for r in second:
        f = first_of[r["group"]]
        fres = results[(f["id"], "in_window")]
        steps, pend, rb = views_for(r, mapping, critical)["in_window"]
        same_utterance = r["template"] == "D1"                              # D1 re-sends the same utterance id; D2 is another utterance of the same behaviour
        uid = f["id"] if same_utterance else r["id"]
        acc = [fres["behaviour"]] if fres["credited"] else []
        for view, accepted in (("in_window", acc), ("engine_backstop", [])):
            res, _ = one(r, view, steps, pend, rb, accepted=accepted, uid=uid)
            if res["offer"]:
                batch.append((r, f, view, res, fres, uid))
    offers = []
    for r, f, view, res, fres, uid in batch:
        g = f"{r['group']}|{view}"
        if fres["offer"]:
            offers.append({"id": f"{f['id']}|first|{g}", "act": fres["offer"], "confidence": fres["confidence"], "steps": fres["steps"], "group": g, "utterance": f["id"]})
        offers.append({"id": f"{r['id']}|{view}", "act": res["offer"], "confidence": res["confidence"], "steps": res["steps"], "group": g, "utterance": uid})
    got = {v["id"]: v for v in E.accept(offers, content=engine_content)} if offers else {}
    for r, f, view, res, fres, uid in batch:
        vd = got[f"{r['id']}|{view}"]
        res["engine"], res["credited"], res["behaviour"] = ("accepted" if vd["accepted"] else vd["reason"]), vd["accepted"], vd["behaviour"]
        res["credited_act"] = res["offer"] if vd["accepted"] else None
    cpath.write_text(json.dumps(cache), encoding="utf-8")
    return {"rows": rows, "results": list(results.values()), "seconds": round(time.perf_counter() - t0, 1), "configuration": configuration(impl)}


def summarise(split: str, name: str, data: dict) -> dict:
    rows = {r["id"]: r for r in data["rows"]}
    res = data["results"]
    by_row = collections.defaultdict(list)
    for x in res:
        by_row[x["id"]].append(x)
    first_ok = {r["group"]: any(x["credited"] for x in by_row[r["id"]] if x["view"] == "in_window") for r in rows.values() if r["group"] and r["position"] == 0}
    # the second utterance of a session pair tests a duplicate only if the first earned credit; if it did not, there was nothing to duplicate and the row is not applicable
    not_applicable = [r for r in rows.values() if r["group"] and r["position"] == 1 and not first_ok.get(r["group"])]
    unsafe_rows = [r for r in rows.values() if r["expected"] == "no_credit" and r not in not_applicable]
    legit_rows = [r for r in rows.values() if r["expected"] == "credit_allowed" and not (r["group"] and r["category"] == "duplicate_first" and False)]
    amb_rows = [r for r in rows.values() if r["expected"] == "ambiguous_review"]

    def credited(r, views=None):
        return [x for x in by_row[r["id"]] if x["credited"] and (views is None or x["view"] in views)]

    unsafe_credit = [r for r in unsafe_rows if credited(r)]
    unsafe_credit_main = [r for r in unsafe_rows if credited(r, ("in_window", "permissive"))]          # the backstop view is reported separately
    backstop_credit = [r for r in unsafe_rows if [x for x in by_row[r["id"]] if x["view"] == "engine_backstop" and x["credited"]]]
    legit_right = [r for r in legit_rows if any(x["credited_act"] == r["act"] for x in by_row[r["id"]] if x["view"] == "in_window")]
    legit_other = [r for r in legit_rows if any(x["credited_act"] and x["credited_act"] != r["act"] for x in by_row[r["id"]] if x["view"] == "in_window")]
    crit = [r for r in unsafe_rows if r["critical"]]
    out_oow = [r for r in unsafe_rows if r["condition"] == "out_of_window"]
    out: dict = {"split": split, "name": name, "configuration": data["configuration"], "synthetic": True, "independent": False, "labels": "provisional, by construction", "rows": len(rows), "seconds": data["seconds"],
                 "expected": dict(collections.Counter(r["expected"] for r in rows.values())), "session_pairs_not_applicable": len(not_applicable)}
    k, n = len(unsafe_credit_main), len(unsafe_rows)
    out["unsafe_credit"] = {"credited": k, "of": n, "rate": round(k / n, 4) if n else None, "wilson95": wilson(k, n), "note": "rows that must never be credited and were credited by the engine in some view; zero on a finite synthetic set is not zero risk"}
    out["engine_backstop_credit"] = {"credited": len(backstop_credit), "of": len([r for r in unsafe_rows if r["group"]]), "note": "session pair with a STALE context: only the engine's own duplicate rule remains"}
    out["critical"] = {"credited": len([r for r in crit if credited(r)]), "of": len(crit)}
    out["out_of_window"] = {"credited": len([r for r in out_oow if credited(r)]), "of": len(out_oow)}
    kk = len(legit_right)
    out["legitimate"] = {"credited_right_act": kk, "of": len(legit_rows), "rate": round(kk / len(legit_rows), 4) if legit_rows else None, "wilson95": wilson(kk, len(legit_rows)), "credited_under_another_act": len(legit_other)}
    na_ids = {r["id"] for r in not_applicable}
    cr_rows = {x["id"] for x in res if x["credited"] and x["view"] in ("in_window", "permissive") and rows[x["id"]]["expected"] != "ambiguous_review" and x["id"] not in na_ids}      # a row counts once, whatever number of views credited it
    good = {x["id"] for x in res if x["id"] in cr_rows and x["view"] == "in_window" and rows[x["id"]]["expected"] == "credit_allowed" and x["credited_act"] == rows[x["id"]]["act"]}
    out["credited_precision"] = {"right": len(good), "credited": len(cr_rows), "rate": round(len(good) / len(cr_rows), 4) if cr_rows else None, "wilson95": wilson(len(good), len(cr_rows)), "note": "rows credited (once each) and, of them, those that were performed acts credited under the right act"}
    dec_l = collections.Counter(next(x["decision"] for x in by_row[r["id"]] if x["view"] == "in_window") for r in legit_rows)
    dec_u = collections.Counter(max((x for x in by_row[r["id"]]), key=lambda x: x["credited"])["decision"] for r in unsafe_rows)
    out["decisions"] = {"legitimate": dict(dec_l), "must_not_credit": dict(dec_u)}
    out["ambiguous_review"] = {"rows": len(amb_rows), "credited": len([r for r in amb_rows if credited(r)]), "by_category": {c: [len([r for r in amb_rows if r["category"] == c and credited(r)]), len([r for r in amb_rows if r["category"] == c])] for c in sorted({r["category"] for r in amb_rows})}}
    cats = {}
    for c in sorted({r["category"] for r in rows.values()}):
        rs = [r for r in rows.values() if r["category"] == c and r["id"] not in na_ids]
        if not rs:
            continue
        exp = rs[0]["expected"]
        if exp == "no_credit":
            cats[c] = {"expected": exp, "rows": len(rs), "credited": len([r for r in rs if credited(r, ("in_window", "permissive"))]), "backstop_credited": len([r for r in rs if r in backstop_credit])}
        elif exp == "credit_allowed":
            cats[c] = {"expected": exp, "rows": len(rs), "credited_right_act": len([r for r in rs if r in legit_right])}
        else:
            cats[c] = {"expected": exp, "rows": len(rs), "credited": len([r for r in rs if credited(r)])}
    out["by_category"] = cats
    tpl = collections.defaultdict(lambda: [0, 0])
    for r in unsafe_rows:
        if r["category"] in ("combination",):
            tpl[r["template"]][1] += 1
            tpl[r["template"]][0] += 1 if credited(r, ("in_window", "permissive")) else 0
    out["combinations_by_template"] = {k: {"credited": v[0], "of": v[1]} for k, v in sorted(tpl.items())}
    reasons = collections.Counter()
    for r in unsafe_rows:
        if not credited(r):
            for x in by_row[r["id"]]:
                reasons[(x["reasons"] or [x["decision"]])[0].split(":")[0]] += 1
    out["refusal_reasons_for_must_not_credit"] = dict(reasons.most_common(15))
    miss = collections.Counter()
    for r in legit_rows:
        if r not in legit_right:
            x = next(x for x in by_row[r["id"]] if x["view"] == "in_window")
            miss[(x["engine"] if x["engine"] and x["engine"] != "accepted" else (x["reasons"] or [x["decision"]])[0]).split(":")[0]] += 1
    out["false_rejection_reasons"] = dict(miss.most_common(12))
    lat = sorted(x["ms"] for x in res)
    out["latency_ms"] = {"p50": lat[len(lat) // 2], "p95": lat[int(len(lat) * 0.95)], "max": lat[-1], "n": len(lat)}
    out["errors"] = len([x for x in res if x["decision"] == "ERROR"])
    out["failures"] = [{"id": r["id"], "text": r["text"], "category": r["category"], "template": r["template"], "act": r["act"], "views": [{"view": x["view"], "decision": x["decision"], "offer": x["offer"], "engine": x["engine"]} for x in by_row[r["id"]] if x["credited"]]} for r in unsafe_credit[:400]]
    return out


def from_raw(split: str, name: str) -> dict:
    """the summary recomputed from a saved result (no system is run, so it is not a second look at the split)."""
    old = json.loads((REPORTS / f"{name}-{split}.json").read_text(encoding="utf-8"))
    raw = json.loads((REPORTS / f"{name}-{split}-raw.json").read_text(encoding="utf-8"))
    return {"rows": load(split), "results": raw, "seconds": old.get("seconds", 0), "configuration": old["configuration"]}


def main(argv):
    ap = argparse.ArgumentParser()
    ap.add_argument("--resummarise", action="store_true", help="recompute the summary from the saved raw predictions of --name (nothing is scored again)")
    ap.add_argument("--split", required=True, choices=["dev", "challenge", "final"])
    ap.add_argument("--impl", default=str(ROOT))
    ap.add_argument("--name", default="candidate")
    ap.add_argument("--ollama", default="http://127.0.0.1:11434")
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--final", action="store_true", help="required for the final split; refuses a second run of the same configuration name")
    a = ap.parse_args(argv)
    if a.split == "final" and not a.resummarise:
        if not a.final:
            print("the final split is scored once per configuration: pass --final")
            return 2
        if (REPORTS / f"{a.name}-final.json").exists():
            print(f"{a.name}-final.json exists: the final set was already scored for this configuration; choose a new name only for a NEW frozen configuration")
            return 2
    impl = Path(a.impl).resolve()
    if a.resummarise:
        out = summarise(a.split, a.name, from_raw(a.split, a.name))
        (REPORTS / f"{a.name}-{a.split}.json").write_text(json.dumps(out, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
        print("re-summarised", a.name, a.split)
        return 0
    data = run(a.split, impl, a.ollama, impl / "content")
    out = summarise(a.split, a.name, data)
    if a.write or a.split == "final":
        REPORTS.mkdir(parents=True, exist_ok=True)
        (REPORTS / f"{a.name}-{a.split}.json").write_text(json.dumps(out, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
        (REPORTS / f"{a.name}-{a.split}-raw.json").write_text(json.dumps(data["results"], ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
        print("wrote", REPORTS.relative_to(ROOT).as_posix(), a.name, a.split)
    print(json.dumps({k: out[k] for k in ("rows", "unsafe_credit", "engine_backstop_credit", "critical", "out_of_window", "legitimate", "credited_precision", "decisions", "ambiguous_review", "latency_ms", "errors", "false_rejection_reasons")}, indent=1))
    print(json.dumps({c: v for c, v in out["by_category"].items() if v.get("credited") or v.get("backstop_credited")}, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
