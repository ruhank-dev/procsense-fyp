"""Metrics for the production Gemma evaluator on tests/synthetic_dev_v1 (development evidence only, AI-written data): semantic understanding first (objective, performed vs not, per act, modality), then the graded credit that results when the engine's rules are applied.
Scoring here is a Python MIRROR of the engine's formula (src/ProcSense.Core/Scoring/CommunicationScorer.cs, content/scoring/rubric.json "communication"); the authoritative numbers are the engine's, and tools/ci/test_evaluate.py pins the two together.
Usage: server/.venv/Scripts/python.exe tools/synth_dev/evaluator_metrics.py <run.json> [--engine] [--write <summary.json>]   (--engine takes every credit from the engine itself through the CLI)"""
from __future__ import annotations

import collections
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
import os  # noqa: E402

OUT = Path(os.environ["SYNTH_SET_DIR"]) if os.environ.get("SYNTH_SET_DIR") else ROOT / "tests" / "synthetic_dev_v1"
MAPPING = json.loads((ROOT / "content" / "assessment" / "mapping.json").read_text(encoding="utf-8"))
RUBRIC = json.loads((ROOT / "content" / "scoring" / "rubric.json").read_text(encoding="utf-8"))
COMM = RUBRIC["communication"]
CRIT = set(MAPPING["critical_acts"])
WINDOW: dict[str, set[int]] = {}
for _b in MAPPING["behaviours"]:
    for _a in _b.get("ai_acts") or []:
        WINDOW.setdefault(_a, set()).update(_b["steps"])
for _act, _e in MAPPING["critical_act_evidence"].items():          # a critical objective is spoken for the behaviour that its verified actions belong to
    WINDOW.setdefault(_act, set()).update(next(b for b in MAPPING["behaviours"] if b["id"] == _e["behaviour"])["steps"])
NEEDS = {"ANSWER_QUESTION": "patient_question_pending", "CLOSED_LOOP": "readback_pending"}
SIT_ASSERT = {"negated": "negated", "intended_later": "intended_later", "hypothetical": "hypothetical", "reported_about_someone_else": "reported", "other_actor_does_it": "other_actor"}


def grade(a: dict) -> float:
    r, c, k = a["relevance"], a["correctness"], a["completeness"]
    if r == 0 or c == 0:
        return 0.0
    f, n = COMM["completeness_floor"], COMM["grade_levels"]
    return (r / n) * (c / n) * (f + (1 - f) * k / n)


def score_act(a: dict, spec: dict) -> float:
    """communication credit of one evaluated act under the engine's rules (a mirror): performed by the trainee now, inside the window, with the state it needs, graded, floored."""
    if not a["performed_now"]:
        return 0.0
    steps = [spec["step"]] if spec.get("step") else list(range(1, 16))
    if a["objective"] in WINDOW and not (WINDOW[a["objective"]] & set(steps)):
        return 0.0
    need = NEEDS.get(a["objective"])
    if need and not spec.get(need):
        return 0.0
    g = grade(a)
    g = g if g >= COMM["credit_floor"] else 0.0
    return round(g * (COMM["critical_weight"] if a["objective"] in CRIT else COMM["ordinary_weight"]), 4)


def pct(a, b):
    return round(a / b, 4) if b else None


def stats(v):
    if not v:
        return {"n": 0}
    s = sorted(v)
    bins = [0.0, 0.01, 0.1, 0.25, 0.5, 0.75, 0.9, 1.0001]
    return {"n": len(v), "mean": round(sum(v) / len(v), 3), "median": round(s[len(s) // 2], 3), "min": round(s[0], 3), "max": round(s[-1], 3), "zero": sum(1 for x in v if x == 0), "ge_0.5": sum(1 for x in v if x >= 0.5), "ge_0.9": sum(1 for x in v if x >= 0.9),
            "hist[0,.01,.1,.25,.5,.75,.9,1]": [sum(1 for x in v if lo <= x < hi) for lo, hi in zip(bins, bins[1:])]}


def engine_scores(d: dict, rows: dict) -> dict:
    """the AUTHORITATIVE per-act credit: every item's judged acts run through the engine's own evidence store and communication scorer (CLI speech-score), with the item's frozen scenario state. Returns id -> {objective: credit}."""
    sys.path.insert(0, str(Path(__file__).resolve().parent))
    import engine_score as ES
    from evaluator_run import context_of
    items = []
    for i, res in d["results"].items():
        ev = res["evaluation"]
        c = context_of(rows[i]["spec"]) if i in rows else rows_extra[i]
        acts = [] if not ev else [{"act": a["objective"], "assertion": a["assertion"], "actor": a["actor"], "form": a["form"], "relevance": a["relevance"], "correctness": a["correctness"], "completeness": a["completeness"], "span": a["evidence"] or a["objective"], "claims_physical": bool(ev["claims_physical_action"])} for a in ev["acts"]]
        items.append({"id": i, "steps": c["steps"], "patient_question_pending": c["patient_question_pending"], "readback_pending": c["readback_pending"], "verified": [], "acts": acts})
    out = {}
    for r in ES.score(items):
        out[r["id"]] = {a["act"]: round(a["grade"] * (0.25 if a["act"] in CRIT else 1.0), 4) for a in r["acts"]}
    return out


rows_extra: dict = {}


def load(run_path: str, engine: bool = False):
    d = json.loads(Path(run_path).read_text(encoding="utf-8"))
    rows = {json.loads(l)["id"]: json.loads(l) for l in (OUT / "dataset.jsonl").read_text(encoding="utf-8").splitlines() if l.strip()}
    base = {json.loads(l)["id"]: json.loads(l) for l in (OUT / "baseline_results.jsonl").read_text(encoding="utf-8").splitlines() if l.strip()} if (OUT / "baseline_results.jsonl").exists() else {}      # the retired baseline exists for the development set only
    items = []
    eng = engine_scores(d, rows) if engine else None
    for i, res in d["results"].items():
        r = rows[i]
        s, g = r["spec"], r["gold"]
        acts = (res["evaluation"] or {"acts": []})["acts"]
        scored = [(a, eng[i].get(a["objective"], 0.0) if eng is not None else score_act(a, s)) for a in acts]
        perf_gold = {s["objective"]} if s["cls"] in ("A", "B", "D") else ({a["act"] for a in s["acts"] if a["situation"] == "performed"} if s["cls"] == "G" else set())
        items.append({"id": i, "text": r["utterance"], "spec": s, "gold": g, "cls": s["cls"], "objective": s["objective"], "situation": s["situation"], "set_id": s["set_id"], "set_role": s["set_role"], "form": s["form"], "tags": s["tags"],
                      "res": res, "acts": acts, "scored": scored, "gold_performed": perf_gold, "perf": {a["objective"] for a in acts if a["performed_now"]}, "b": base.get(i)})
    return d, items


def credited(x, th=0.5):
    return {a["objective"] for a, sc in x["scored"] if sc >= th and a["objective"] not in CRIT}


def ordmax(x):
    return max([sc for a, sc in x["scored"] if a["objective"] not in CRIT] or [0.0])


def exp_score(x):
    return next((sc for a, sc in x["scored"] if a["objective"] == x["objective"]), 0.0)


def false_category(x, o) -> str:
    """why an act that earns >= 0.5 is not in the gold credit set: the semantic category of the failure."""
    s = x["spec"]
    if x["cls"] == "C":
        return "non-performed read as performed: " + s["situation"]
    if x["cls"] == "B":
        return "scenario-ineligible speech credited (a different in-window objective)"
    if x["cls"] in ("D", "E") or x["gold"]["critical"]:
        return "ordinary objective credited on critical content"
    if x["cls"] == "F":
        return "no-act speech credited"
    if x["cls"] == "G":
        return "multi-intent: extra objective"
    return "extra objective on an eligible item"


def analyse(d, items, with_baseline=True):
    with_baseline = with_baseline and all(x["b"] for x in items)
    n = len(items)
    out = {"n": n, "prompt_version": d["prompt_version"], "prompt_hash": d["prompt_hash"], "model": d["model"]}
    st = collections.Counter(x["res"]["status"] for x in items)
    walls = sorted(l["wall_ms"] for l in d["log"] if not l["error"])
    codes = collections.Counter(c.split(":")[0] for x in items for c in x["res"]["codes"])
    out["reader"] = {"status": dict(st), "valid_rate": pct(st["VALID"], n), "codes": dict(codes), "retried_items": sum(1 for x in items if x["res"]["attempts"] > 1), "calls": d["calls"], "tokens": d["tokens"], "tokens_per_item": round(d["tokens"] / n), "stopped": d["stopped"],
                     "prompt_tokens": sum(l["prompt_tokens"] for l in d["log"]), "output_tokens": sum(l["output_tokens"] for l in d["log"]), "wall_ms_per_call": {"median": walls[len(walls) // 2], "p95": walls[int(len(walls) * 0.95)], "max": walls[-1]}, "run_wall_s": d["wall_s"], "workers": d["workers"]}
    P_items = [x for x in items if x["gold_performed"]]
    tp = sum(len(x["gold_performed"] & x["perf"]) for x in P_items)
    out["1_objective_accuracy"] = {"exact_set_match": [sum(1 for x in P_items if x["gold_performed"] == x["perf"]), len(P_items)], "per_act_recall": [tp, sum(len(x["gold_performed"]) for x in P_items)],
                                   "per_act_precision": [sum(len(x["gold_performed"] & x["perf"]) for x in items), sum(len(x["perf"]) for x in items)],
                                   "confusions": [[",".join(sorted(k[0])), "+".join(sorted(k[1])) or "none", v] for k, v in collections.Counter((frozenset(x["gold_performed"]), frozenset(x["perf"])) for x in P_items if x["gold_performed"] != x["perf"]).most_common(30)]}
    tpn = sum(1 for x in items if x["gold"]["performs_now"] and x["perf"])
    fn = sum(1 for x in items if x["gold"]["performs_now"] and not x["perf"])
    fp = sum(1 for x in items if not x["gold"]["performs_now"] and x["perf"])
    out["2_performed_vs_not"] = {"accuracy": pct(n - fn - fp, n), "performed_found": [tpn, tpn + fn], "non_performed_called_performed": [fp, sum(1 for x in items if not x["gold"]["performs_now"])],
                                 "by_situation_called_performed": {s: [sum(1 for x in items if x["situation"] == s and x["perf"]), sum(1 for x in items if x["situation"] == s)] for s in sorted({x["situation"] for x in items})}}
    mod = collections.defaultdict(collections.Counter)
    for x in items:
        if x["cls"] == "C":
            a = next((a for a in x["acts"] if a["objective"] == x["objective"]), None)
            mod[x["situation"]]["invalid" if x["res"]["status"] != "VALID" else (a["assertion"] if a else "no finding")] += 1
    out["3_modality_for_the_items_own_objective"] = {k: dict(v.most_common()) for k, v in mod.items()}
    el = [x for x in items if x["gold"]["credit_eligible"]]
    nc = [x for x in items if not x["gold"]["credit_eligible"]]
    gold_acts = lambda x: set(x["gold"]["credit_acts"])

    def view(th):
        c = lambda x: credited(x, th)
        hits = [(x["id"], o) for x in items for o in c(x)]
        gd = sum(1 for i, o in hits if o in gold_acts(next(y for y in items if y["id"] == i)))
        return {"threshold": th, "4_legitimate_credit_recall": [sum(1 for x in el if c(x) & gold_acts(x)), len(el)], "per_act_recall": [sum(len(c(x) & gold_acts(x)) for x in el), sum(len(gold_acts(x)) for x in el)], "5_precision": [gd, len(hits), pct(gd, len(hits))],
                "6_false_credit_items_on_no_credit": sum(1 for x in nc if c(x)), "7_wrong_or_extra_acts_on_eligible": sum(len(c(x) - gold_acts(x)) for x in el), "no_credit_rejection": [sum(1 for x in nc if not c(x)), len(nc)]}
    out["credit"] = {"meaningful_ge_0.5": view(0.5), "full_ge_0.9": view(0.9), "any_positive": view(1e-9)}
    fc = collections.Counter()
    for x in items:
        for o in credited(x) - gold_acts(x):
            fc[false_category(x, o)] += 1
    out["6_false_credits_by_category"] = dict(fc.most_common())
    crit = [x for x in items if x["gold"]["critical"]]
    out["8_critical"] = {"critical_items": len(crit), "physical_completion_credit": 0.0, "model_claims_physical_action": [sum(1 for x in items if x["res"]["evaluation"] and x["res"]["evaluation"]["claims_physical_action"]), n], "ordinary_credit_on_critical_items": sum(1 for x in crit if credited(x)),
                         "critical_flag": [sum(1 for x in crit if x["res"]["evaluation"] and x["res"]["evaluation"]["critical_topic"]), len(crit)], "critical_flag_on_non_critical": [sum(1 for x in items if not x["gold"]["critical"] and x["res"]["evaluation"] and x["res"]["evaluation"]["critical_topic"]), n - len(crit)],
                         "class_D_critical_act_named_performed": [sum(1 for x in items if x["cls"] == "D" and x["objective"] in x["perf"]), sum(1 for x in items if x["cls"] == "D")], "class_E_called_performed": [sum(1 for x in items if x["cls"] == "E" and x["perf"]), sum(1 for x in items if x["cls"] == "E")]}
    G_ = [x for x in items if x["cls"] == "G"]
    out["9_multi_intent"] = {"items": len(G_), "credit_acts": sum(len(gold_acts(x)) for x in G_), "found_ge_0.5": sum(len(credited(x) & gold_acts(x)) for x in G_), "performed_set_exact": sum(1 for x in G_ if x["gold_performed"] == x["perf"]), "items_with_extra_credit": sum(1 for x in G_ if credited(x) - gold_acts(x))}
    sets = collections.defaultdict(list)
    for x in items:
        if x["set_id"]:
            sets[x["set_id"]].append(x)
    ok = {}
    for sid, L in sets.items():
        perf = next(y for y in L if y["set_role"] == "performed")
        non = [y for y in L if y["set_role"] != "performed"]
        if perf["objective"] in CRIT:
            ok[sid] = perf["objective"] in perf["perf"] and all(perf["objective"] not in m["perf"] for m in non) and not any(credited(m, 0.25) for m in L)
        else:
            ok[sid] = bool(credited(perf) & gold_acts(perf)) and all(ordmax(m) < 0.25 for m in non)
    out["10_contrast_sets"] = {"correct": sum(ok.values()), "n": len(ok), "ordinary": [sum(v for k, v in ok.items() if k.split("-")[1] not in CRIT), sum(1 for k in ok if k.split("-")[1] not in CRIT)], "critical": [sum(v for k, v in ok.items() if k.split("-")[1] in CRIT), sum(1 for k in ok if k.split("-")[1] in CRIT)],
                               "failing": sorted(k.replace("SET-", "") for k, v in ok.items() if not v)}
    B_ = [x for x in items if x["cls"] == "B"]
    out["11_window_state"] = {"n": len(B_), "model_names_the_performed_act": sum(1 for x in B_ if x["perf"] == {x["objective"]}), "credited (>=0.5)": sum(1 for x in B_ if credited(x)), "out_of_window": [sum(1 for x in B_ if x["spec"]["window"] == "out_of_window" and credited(x)), sum(1 for x in B_ if x["spec"]["window"] == "out_of_window")],
                              "state_absent": [sum(1 for x in B_ if "state_absent" in x["tags"] and credited(x)), sum(1 for x in B_ if "state_absent" in x["tags"])]}
    rcc = {}
    for c in "ABCDEFG":
        accs = [a for x in items if x["cls"] == c for a in x["acts"] if a["objective"] == x["objective"]]
        rcc[c] = {"with_finding": len(accs), "relevance_levels": dict(collections.Counter(a["relevance"] for a in accs)), "correctness_levels": dict(collections.Counter(a["correctness"] for a in accs)), "completeness_levels": dict(collections.Counter(a["completeness"] for a in accs))}
    out["12_grade_distributions_by_class"] = rcc
    D = collections.OrderedDict()
    D["legitimate performed (A): the objective's score"] = stats([exp_score(x) for x in items if x["cls"] == "A"])
    D["mixed multi-intent: the performed act's score"] = stats([exp_score(x) for x in items if "mixed_performed" in x["tags"]])
    D["no-act (F): highest ordinary score"] = stats([ordmax(x) for x in items if x["cls"] == "F"])
    for s, lab in (("negated", "negated"), ("intended_later", "intended-later"), ("hypothetical", "hypothetical"), ("reported_about_someone_else", "reported"), ("other_actor_does_it", "another actor")):
        D[lab + " (C): highest ordinary score"] = stats([ordmax(x) for x in items if x["cls"] == "C" and x["situation"] == s])
    D["critical-topic discussion (E): highest ordinary score"] = stats([ordmax(x) for x in items if x["cls"] == "E"])
    D["genuine critical claims without simulator evidence (D): total communication credit"] = stats([sum(sc for a, sc in x["scored"]) for x in items if x["cls"] == "D"])
    D["scenario-ineligible performed speech (B): highest ordinary score"] = stats([ordmax(x) for x in items if x["cls"] == "B"])
    out["13_score_distributions"] = D
    ls = [exp_score(x) for x in items if x["cls"] == "A"]
    ns = [ordmax(x) for x in items if x["cls"] in ("C", "F")]
    out["13_separation"] = {"auc": round(sum((a > b) + 0.5 * (a == b) for a in ls for b in ns) / (len(ls) * len(ns)), 4), "non_performed_or_irrelevant_ge_0.25": [sum(1 for v in ns if v >= 0.25), len(ns)], "legitimate_lt_0.5": [sum(1 for v in ls if v < 0.5), len(ls)]}
    def grp(keyf, L):
        g = collections.defaultdict(list)
        for x in L:
            g[keyf(x)].append(x)
        res = {}
        for k, v in sorted(g.items(), key=lambda kv: str(kv[0])):
            e = [y for y in v if y["gold"]["credit_eligible"]]
            res[str(k)] = {"n": len(v), "eligible": len(e), "recall": [sum(1 for y in e if credited(y) & gold_acts(y)), len(e)], "false_credit_items": sum(1 for y in v if credited(y) - gold_acts(y)), "exact_performed_set": sum(1 for y in v if y["gold_performed"] == y["perf"])}
            if with_baseline:
                res[str(k)]["baseline_recall"] = [sum(1 for y in e if y["b"]["credited"] and y["b"]["offer"] in y["gold"]["credit_acts"]), len(e)]
        return res
    out["per_objective"] = grp(lambda x: x["objective"], items)
    out["per_situation"] = grp(lambda x: x["situation"], items)
    out["per_class"] = grp(lambda x: x["cls"], items)
    out["failures"] = {"eligible_misses": [x["id"][4:] for x in el if not (credited(x) & gold_acts(x))], "false_credit_items": [x["id"][4:] for x in items if credited(x) - gold_acts(x)]}
    return out


def main(argv):
    d, items = load(argv[0], engine="--engine" in argv)
    out = analyse(d, items)
    if "--write" in argv:
        Path(argv[argv.index("--write") + 1]).write_text(json.dumps(out, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    for k in ("reader", "1_objective_accuracy", "2_performed_vs_not", "credit", "6_false_credits_by_category", "8_critical", "9_multi_intent", "10_contrast_sets", "11_window_state", "13_separation"):
        v = out[k]
        if k == "1_objective_accuracy":
            v = {a: b for a, b in v.items() if a != "confusions"}
        print(k, json.dumps(v, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
