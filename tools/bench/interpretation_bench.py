"""Context-conditioned benchmark of the speech interpretation path against the classifier route on the SAME rows (p1-ai-language-s14, ADR-0022): `tools/.venv/Scripts/python.exe tools/bench/interpretation_bench.py --split dev|test|test2 [--write]`.

Rows: tests/action_bench_v1 (frozen hashes; synthetic, one author family, labels by construction; NOT independent). The benchmark rows carry no scenario state, so this adapter gives each row a state taken from the engine's own rule, never from a model:
  in window     the first workflow step of a behaviour window (content/assessment/mapping.json) that lists the row's act; an act that no behaviour lists as an AI act (the critical acts other than SBAR) has no window, so every step is running;
                a waiting patient question for ANSWER_QUESTION rows and a waiting read-back for CLOSED_LOOP rows (the context those acts need).
  out of window the first step that is in no window of the row's act (legitimate rows only; skipped when the act has no such step).
  no context    in window, but with no waiting question or read-back (ANSWER_QUESTION and CLOSED_LOOP legitimate rows only).
Systems on identical rows: the classifier route as served (recorded decisions of tools/bench/nli_baseline.py), the same decisions after the engine's window rule (what EvidenceStore.Submit would do with the step), and the interpretation path.
An offer counts as an OFFER of the act it names. Legitimate recall = legitimate rows offered under the right act; credited precision = offers that were right; false credit = an offer for a critical row or for a row that does not perform the act.
Embeddings come from the local all-minilm:22m through Ollama and are cached in artifacts/ai-bench/interp/ (git-ignored); no language model is used. Nothing is tuned here: the rules and limits of server/interpret were fixed before test and test2 were scored; dev is where they may change.
"""
from __future__ import annotations

import argparse
import collections
import json
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools" / "bench"))
from classify.embedding import OllamaEmbedder  # noqa: E402
from classify.ensemble import EnsembleActClassifier  # noqa: E402
from interpret.decide import Interpreter  # noqa: E402
import engine_acceptance as E  # noqa: E402
import nli_compare as NC  # noqa: E402
import nli_zero_shot as N  # noqa: E402

CACHE = ROOT / "artifacts" / "ai-bench" / "interp"
MAPPING = json.loads((ROOT / "content" / "assessment" / "mapping.json").read_text(encoding="utf-8"))
CRITICAL = set(MAPPING["critical_acts"])
ALL_STEPS = list(range(1, 16))
LEGIT = ("paraphrase", "asr_error")


class Entry:
    def __init__(self, act, behaviour, critical):
        self.act, self.behaviour, self.critical = act, behaviour, critical


class Context:
    def __init__(self, steps, pending=False, readback=False):
        self.steps, self.accepted, self.patient_question_pending, self.readback_pending = steps, [], pending, readback
        self.eligible = [Entry(a, b["id"], a in CRITICAL) for b in MAPPING["behaviours"] if set(b["steps"]) & set(steps) for a in b["ai_acts"]]


def window_of(act: str) -> list[int]:
    return sorted({s for b in MAPPING["behaviours"] if act in b["ai_acts"] for s in b["steps"]})


def conditions(row: dict) -> dict:
    act = row["act"]
    win = window_of(act)
    needs = act in ("ANSWER_QUESTION", "CLOSED_LOOP")
    out = {"in_window": Context(win[:1] or ALL_STEPS, pending=act == "ANSWER_QUESTION", readback=act == "CLOSED_LOOP")}
    outside = [s for s in ALL_STEPS if s not in win]
    if win and outside:
        out["out_of_window"] = Context(outside[:1], pending=act == "ANSWER_QUESTION", readback=act == "CLOSED_LOOP")
    if needs and win:
        out["no_context"] = Context(win[:1])
    return out


class CachedEmbedder:
    def __init__(self, split: str, url: str):
        CACHE.mkdir(parents=True, exist_ok=True)
        self.path, self.inner = CACHE / f"emb-{split}.json", OllamaEmbedder(url)
        self.cache = json.loads(self.path.read_text(encoding="utf-8")) if self.path.exists() else {}
        self.model = getattr(self.inner, "model", "embeddings")

    def __call__(self, text):
        if text not in self.cache:
            v = self.inner(text)
            if v is None:
                return None
            self.cache[text] = v
        return self.cache[text]

    def save(self):
        self.path.write_text(json.dumps(self.cache), encoding="utf-8")


def offered(out: dict) -> str | None:
    return out["evidence"]["act"] if out["decision"] == "OFFER_EVIDENCE" else None


def run(split: str, url: str) -> dict:
    rows = N.load_rows(split)
    base = {x["id"]: x for x in json.loads((NC.NLI / f"baseline-{split}.json").read_text(encoding="utf-8"))["results"]}
    emb = CachedEmbedder(split, url)
    interp = Interpreter(EnsembleActClassifier(emb))
    res = []
    for r in rows:
        conds = conditions(r)
        b = base[r["id"]]
        route = b["act"] if b["credited"] else None
        item = {"id": r["id"], "text": r["text"], "act": r["act"], "status": r["status"], "category": r["category"], "expect_credit": r["expect_credit"], "critical": r["critical"], "route": route}
        win = set(window_of(r["act"]))
        item["route_after_window_rule"] = route if (route is not None and (route in CRITICAL or not window_of(route) or set(window_of(route)) & set(conds["in_window"].steps))) else None
        for name, c in conds.items():
            o = interp.interpret(c, r["text"], r["id"], 0)
            item[name] = {"decision": o["decision"], "offered_by_service": offered(o), "offer": offered(o), "reasons": o["reasons"], "clarify": o["options"], "points_to": o["points_to"],
                          "confidence": o["evidence"]["confidence"] if o["decision"] == "OFFER_EVIDENCE" else None, "steps": c.steps}
        if "out_of_window" in conds and route is not None:
            item["route_out_of_window"] = route if (route in CRITICAL or not window_of(route) or set(window_of(route)) & set(conds["out_of_window"].steps)) else None
        res.append(item)
    emb.save()
    # what the ENGINE accepts: every offer is run through the engine's own EvidenceStore (CLI evidence-check); from here on `offer` means accepted evidence, `offered_by_service` what the service offered
    pend = [(it, name) for it in res for name in ("in_window", "out_of_window", "no_context") if name in it and it[name]["offered_by_service"]]
    verdicts = E.accept([{"id": f"{it['id']}|{name}", "act": it[name]["offered_by_service"], "confidence": it[name]["confidence"], "steps": it[name]["steps"]} for it, name in pend])
    for (it, name), v in zip(pend, verdicts):
        it[name]["engine"] = "accepted" if v["accepted"] else v["reason"]
        if not v["accepted"]:
            it[name]["offer"] = None
    return {"rows": res, "emb_model": emb.model}


def rate(a: int, b: int) -> str:
    return f"{a}/{b}"


def summarise(split: str, data: dict) -> dict:
    rows = data["rows"]
    legit = [r for r in rows if r["category"] in LEGIT]
    adversarial = [r for r in rows if r["category"] in NC.ADVERSARIAL]
    critical = [r for r in rows if r["category"].startswith("critical")]
    core = [r for r in rows if r["category"] in ("paraphrase", "asr_error", "critical", "critical_asr", "none")]
    out: dict = {"split": split, "rows": len(rows), "synthetic": True, "independent": False, "counts": {"legitimate": len(legit), "adversarial": len(adversarial), "critical": len(critical)}}

    def system(name, pick):
        off = [(r, pick(r)) for r in rows]
        offers = [(r, o) for r, o in off if o]
        right = [(r, o) for r, o in offers if r["expect_credit"] and o == r["act"]]
        by_cat = collections.Counter(r["category"] for r, o in offers if r["category"] in NC.ADVERSARIAL)
        out[name] = {"offers": len(offers), "offers_right": len(right), "credited_precision": rate(len(right), len(offers)),
                     "legitimate_recall": rate(sum(1 for r in legit if pick(r) == r["act"]), len(legit)), "legitimate_offered_under_another_act": sum(1 for r in legit if pick(r) and pick(r) != r["act"]),
                     "critical_false_credit": rate(sum(1 for r in critical if pick(r)), len(critical)), "non_assertive_false_credit": rate(sum(by_cat.values()), len(adversarial)), "non_assertive_by_category": dict(by_cat),
                     "label_view_core": {k: v for k, v in NC.prf([(NC.truth(r), pick(r) or "ABSTAIN") for r in core]).items() if k != "per_class"}}
        out[name]["per_class"] = NC.prf([(NC.truth(r), pick(r) or "ABSTAIN") for r in core])["per_class"]
        out[name]["confusion"] = NC.confusion([(NC.truth(r), pick(r) or "ABSTAIN") for r in core])
    system("classifier_route_as_served", lambda r: r["route"])
    system("classifier_route_after_window_rule", lambda r: r["route_after_window_rule"])
    system("interpretation_in_window", lambda r: r["in_window"]["offer"])
    # decisions on the legitimate rows: coverage, abstention, clarification, legitimate rejection
    dec = collections.Counter(r["in_window"]["decision"] for r in legit)
    out["interpretation_in_window"]["legitimate_decisions"] = dict(dec)
    out["interpretation_in_window"]["clarification_rate_legit"] = rate(dec["CLARIFY"], len(legit))
    out["interpretation_in_window"]["abstention_rate_legit"] = rate(dec["ABSTAIN"], len(legit))
    out["interpretation_in_window"]["menu_rate_legit"] = rate(dec["MENU"], len(legit))
    out["interpretation_in_window"]["offered_by_service_but_refused_by_engine"] = {"legitimate": sum(1 for r in legit if r["in_window"]["offered_by_service"] and not r["in_window"]["offer"]), "all_rows": sum(1 for r in rows if r["in_window"]["offered_by_service"] and not r["in_window"]["offer"])}
    out["interpretation_in_window"]["legitimate_rejection_rate"] = rate(len(legit) - dec["OFFER_EVIDENCE"] - dec["CLARIFY"], len(legit))
    out["interpretation_in_window"]["reasons_for_missed_legit"] = dict(collections.Counter(x for r in legit if r["in_window"]["offer"] != r["act"] for x in r["in_window"]["reasons"]).most_common(8))
    # context rules
    oow = [r for r in legit if "out_of_window" in r]
    out["out_of_window"] = {"legitimate_rows_with_an_outside_step": len(oow), "interpretation_offered_the_rows_own_act": sum(1 for r in oow if r["out_of_window"]["offer"] == r["act"]),
                            "interpretation_offered_any_act": sum(1 for r in oow if r["out_of_window"]["offer"]), "route_as_served_would_offer_its_credited_act": sum(1 for r in oow if r["route"]),
                            "route_after_window_rule_offered_the_rows_own_act": sum(1 for r in oow if r.get("route_out_of_window") == r["act"])}
    nc = [r for r in legit if "no_context" in r]
    out["without_pending_question_or_readback"] = {"rows": len(nc), "interpretation_offered_the_rows_own_act": sum(1 for r in nc if r["no_context"]["offer"] == r["act"]),
                                                   "with_the_context_offered_the_rows_own_act": sum(1 for r in nc if r["in_window"]["offer"] == r["act"]), "route_credited_the_rows_own_act": sum(1 for r in nc if r["route"] == r["act"])}
    out["critical"] = {"offered_in_any_condition": sum(1 for r in critical for c in ("in_window", "out_of_window", "no_context") if c in r and r[c]["offer"]),
                       "menu_with_verified_action_pointer": sum(1 for r in critical if r["in_window"]["decision"] == "MENU" and r["in_window"]["points_to"]), "rows": len(critical)}
    ip, route = out["interpretation_in_window"], out["classifier_route_as_served"]
    adv_off = sum(1 for r in adversarial if r["in_window"]["offer"])
    out["gate_inputs"] = {"note": "numbers the speech_interpretation gate reads (content/ai/thresholds.json); on this synthetic split they are recorded for information only: the gate reads interpretation-bench-independent.json, which does not exist until the independent set is scored (EX-01)",
                          "critical_false_credit": out["critical"]["offered_in_any_condition"],
                          "out_of_window_false_credit": out["out_of_window"]["interpretation_offered_the_rows_own_act"],
                          "non_assertive_false_credit_rate": round(adv_off / len(adversarial), 4) if adversarial else None,
                          "credited_precision": round(ip["offers_right"] / ip["offers"], 4) if ip["offers"] else 0.0,
                          "legitimate_recall": round(sum(1 for r in legit if r["in_window"]["offer"] == r["act"]) / len(legit), 4),
                          "route_legitimate_recall": round(sum(1 for r in legit if r["route"] == r["act"]) / len(legit), 4), "valid_for_claims": False}
    wrong = [r for r in rows if r["in_window"]["offer"] and not (r["expect_credit"] and r["in_window"]["offer"] == r["act"])]
    missed = [r for r in legit if r["in_window"]["offer"] != r["act"]]
    def short(r, c="in_window"):
        return {"id": r["id"], "text": r["text"], "act": r["act"], "category": r["category"], "route": r["route"], **{k: r[c][k] for k in ("decision", "offer", "reasons", "clarify")}}
    out["examples"] = {"wrong_offers": [short(r) for r in wrong[:8]], "missed_legitimate": [short(r) for r in missed[:8]]}
    out["example_counts"] = {"wrong_offers": len(wrong), "missed_legitimate": len(missed)}
    return out


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--split", required=True, choices=["dev", "test", "test2"])
    ap.add_argument("--ollama", default="http://127.0.0.1:11434")
    ap.add_argument("--write", action="store_true")
    a = ap.parse_args(argv)
    data = run(a.split, a.ollama)
    out = summarise(a.split, data)
    out["embedding_model"] = data["emb_model"]
    (CACHE / f"rows-{a.split}.json").write_text(json.dumps(data["rows"], indent=1) + "\n", encoding="utf-8")
    if a.write:
        p = ROOT / "reports" / "ai" / f"interpretation-bench-{a.split}.json"
        p.write_text(json.dumps(out, indent=1) + "\n", encoding="utf-8", newline="\n")
        print("wrote", p.relative_to(ROOT).as_posix())
    brief = {k: {x: v[x] for x in ("offers", "credited_precision", "legitimate_recall", "critical_false_credit", "non_assertive_false_credit")} for k, v in out.items() if isinstance(v, dict) and "offers" in v}
    print(json.dumps({"counts": out["counts"], **brief, "out_of_window": out["out_of_window"], "without_context": out["without_pending_question_or_readback"], "critical": out["critical"],
                      "decisions": out["interpretation_in_window"]["legitimate_decisions"], "example_counts": out["example_counts"]}, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
