"""Deterministic validation of the direct synthetic development set (tests/synthetic_dev_v1).

Joins the frozen specifications (specs.json, hash in manifest.json) with the written utterances (utterances/*.jsonl, no label field) and the discard log (discards.jsonl), then checks:
  schema and provenance (every utterance has a spec; the specs still match the manifest; utterances carry no label), cue checks derived from the specification (question mark for questions, negation / future / conditional / reporting / other-actor cues,
  no cue of a non-performed meaning in a performed item), length bands, exact and near duplicates, leakage against every historical sentence of the repository (exact, Jaccard, local embedding cosine), diversity (distinct-n, self-BLEU,
  opening and skeleton concentration), contrast-set completeness, coverage against the planned matrix.
Cue findings are WARNINGS for the author's semantic review (review.jsonl records the decision per id); only hard failures stop the build.
Usage: uv run --project tools tools/synth_dev/validate.py [--warnings] [--build] [--check]"""
from __future__ import annotations

import collections
import hashlib
import json
import re
import sys
import urllib.request
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import diversity as D
import gold as G
import plan as P

OUT = P.OUT
NEG = re.compile(r"\b(haven't|have not|hasn't|has not|didn't|did not|don't|do not|never|not yet|isn't|wasn't|won't|no one|nobody)\b", re.I)
FUT = re.compile(r"(\b(will|shall|going to|about to|later|soon|in a (minute|moment|sec|bit)|once|after|next|when)\b|'ll\b)", re.I)
HYP = re.compile(r"\b(if|were|would|suppose|imagine|what if)\b|'d\b", re.I)
REP = re.compile(r"\b(said|says|told|mentioned|stated|asked|noted|explained|reported)\b", re.I)
OTHER = re.compile(r"\b(nurse|doctor|dr|registrar|sister|consultant|surgeon|anaesthetist|porter|pharmacist|assistant|clerk|staff nurse|sam|jo|dan|dev|priya)\b", re.I)
LABEL_KEYS = {"objective", "situation", "gold", "label", "credit", "act", "cls"}


def load():
    specs = json.loads((OUT / "specs.json").read_text(encoding="utf-8"))
    by_id = {s["id"]: s for s in specs}
    utts, dup = {}, []
    for p in sorted((OUT / "utterances").glob("b*.jsonl")):
        for line in p.read_text(encoding="utf-8").splitlines():
            if not line.strip():
                continue
            r = json.loads(line)
            if r["id"] in utts:
                dup.append(r["id"])
            utts[r["id"]] = r
    discards = {}
    dp = OUT / "discards.jsonl"
    if dp.exists():
        for line in dp.read_text(encoding="utf-8").splitlines():
            if line.strip():
                r = json.loads(line)
                discards[r["id"]] = r
    review = {}
    rp = OUT / "review.jsonl"
    if rp.exists():
        for line in rp.read_text(encoding="utf-8").splitlines():
            if line.strip():
                r = json.loads(line)
                review[r["id"]] = r
    return specs, by_id, utts, dup, discards, review


def words(t):
    return len(re.findall(r"[A-Za-z0-9']+", t))


def cue_warnings(s: dict, text: str) -> list[str]:
    w = []
    sit, form, n = s["situation"], s["form"], words(text)
    ends_q = text.rstrip().endswith("?")
    if form == "question" and "?" not in text:
        w.append("form_question_without_mark")
    if form in ("statement", "request_or_instruction") and ends_q:
        w.append("%s_ends_with_question_mark" % form)
    if s["length"] == "short" and n > 12:
        w.append("short_but_%d_words" % n)
    if s["length"] == "long" and n < 18:
        w.append("long_but_%d_words" % n)
    if s["length"] == "medium" and (n < 4 or n > 40):
        w.append("medium_but_%d_words" % n)
    if sit == "negated" and not NEG.search(text):
        w.append("negated_without_negation_cue")
    if sit == "intended_later" and not FUT.search(text):
        w.append("intended_without_future_cue")
    if sit == "hypothetical" and not HYP.search(text):
        w.append("hypothetical_without_conditional_cue")
    if sit == "reported_about_someone_else" and not REP.search(text):
        w.append("reported_without_reporting_cue")
    if sit == "other_actor_does_it" and not OTHER.search(text):
        w.append("other_actor_without_other_subject")
    if sit == "performed" and s["cls"] in ("A", "B", "D"):
        if re.match(r"\s*(if|suppose|imagine|what if)\b", text, re.I):
            w.append("performed_starts_conditional")
        if NEG.search(text) and "?" not in text:
            w.append("performed_with_negation_cue")
        if re.search(r"\b(said|told me)\b", text, re.I):
            w.append("performed_with_reporting_cue")
    if "i mean" in text.lower() or "or rather" in text.lower():
        w.append("stock_phrase")
    return w


def embed(texts: list[str]) -> list[list[float]]:
    out = []
    for i in range(0, len(texts), 32):
        req = urllib.request.Request("http://localhost:11434/api/embed", data=json.dumps({"model": "all-minilm:22m", "input": texts[i:i + 32]}).encode(), headers={"Content-Type": "application/json"})
        out += json.loads(urllib.request.urlopen(req, timeout=120).read())["embeddings"]
    return out


def evaluate(with_embeddings: bool = True) -> dict:
    specs, by_id, utts, dup, discards, review = load()
    manifest = json.loads((OUT / "manifest.json").read_text(encoding="utf-8"))
    hard = []
    spec_bytes = (OUT / "specs.json").read_bytes()
    if hashlib.sha256(spec_bytes).hexdigest() != manifest["specs_sha256"]:
        hard.append("specs.json does not match manifest.json (the specifications are frozen)")
    here = Path(__file__).resolve().parent
    for n, h in manifest["plan_sources_sha256"].items():
        if hashlib.sha256((here / n).read_bytes()).hexdigest() != h:
            hard.append("plan source %s changed after the freeze" % n)
    if dup:
        hard.append("duplicate utterance ids: %s" % dup[:5])
    for i, r in utts.items():
        if i not in by_id:
            hard.append("utterance for unknown spec %s" % i)
        if set(r) - {"id", "text"}:
            hard.append("%s: utterance record has fields besides id and text" % i)
        if not r.get("text", "").strip():
            hard.append("%s: empty utterance" % i)
    for s in specs:
        if s["id"] not in utts and s["id"] not in discards:
            hard.append("%s: neither an utterance nor a discard record" % s["id"])
        if s["id"] in utts and s["id"] in discards:
            hard.append("%s: both written and discarded" % s["id"])
        if s["persona"] in P.RESERVED_PERSONAS:
            hard.append("%s: reserved persona used" % s["id"])
    accepted = [s for s in specs if s["id"] in utts and s["id"] not in discards and review.get(s["id"], {}).get("verdict", "ok") == "ok"]
    texts = {i: utts[i]["text"] for i in utts}
    warnings = {s["id"]: cue_warnings(s, texts[s["id"]]) for s in specs if s["id"] in texts}
    warnings = {k: v for k, v in warnings.items() if v}
    # duplicates and leakage
    norm = {i: D.norm(t) for i, t in texts.items()}
    exact = [i for i, n in norm.items() if list(norm.values()).count(n) > 1]
    sets = {i: frozenset(n.split()) for i, n in norm.items()}
    near = []
    ids = sorted(texts)
    for a in range(len(ids)):
        for b in range(a + 1, len(ids)):
            x, y = sets[ids[a]], sets[ids[b]]
            if len(x) >= 4 and len(y) >= 4 and len(x & y) / len(x | y) >= D.JACCARD:
                same = by_id[ids[a]]["set_id"] and by_id[ids[a]]["set_id"] == by_id[ids[b]]["set_id"]
                near.append((ids[a], ids[b], "same_contrast_set" if same else "different_items"))
    leaks = D.Leaks(vectors=[])
    leak_counts = collections.Counter()
    leak_ids = {}
    vecs = {}
    if with_embeddings:
        try:
            ev = embed([texts[i] for i in ids])
            vecs = dict(zip(ids, ev))
        except Exception as e:  # local ollama not running: embedding leak check skipped and reported as such
            vecs = {}
            hard_note = "embedding leak check skipped: %s" % type(e).__name__
        hv = D.historical_vectors()
    else:
        hv = []
    leaks.vectors = hv
    for i in ids:
        r = leaks.check(texts[i], vecs.get(i))
        if r:
            leak_counts[r] += 1
            leak_ids[i] = r
    acc_texts = [texts[s["id"]] for s in accepted]
    toks = lambda t: D.norm(t).split()
    div = {"distinct_1": D.distinct_n(acc_texts, 1), "distinct_2": D.distinct_n(acc_texts, 2), "distinct_3": D.distinct_n(acc_texts, 3), "self_bleu": D.self_bleu(acc_texts),
           "opening_3": D.concentration([D.opening(t) for t in acc_texts]), "opening_1": D.concentration([D.opening(t, 1) for t in acc_texts]), "skeleton": D.concentration([D.skeleton(t) for t in acc_texts]),
           "mean_words": round(sum(words(t) for t in acc_texts) / max(1, len(acc_texts)), 2), "stock_phrase_share": round(sum(1 for t in acc_texts if "i mean" in t.lower() or "or rather" in t.lower()) / max(1, len(acc_texts)), 4)}
    # contrast completeness
    sets_all = collections.defaultdict(list)
    for s in specs:
        if s["set_id"]:
            sets_all[s["set_id"]].append(s)
    acc_ids = {s["id"] for s in accepted}
    complete = sum(1 for v in sets_all.values() if all(x["id"] in acc_ids for x in v))
    cov = P.coverage(accepted)
    return {"hard_failures": hard, "n_specs": len(specs), "n_written": len(utts), "n_discarded": len(discards), "n_accepted": len(accepted), "warnings": warnings, "exact_duplicates": exact, "near_duplicates": near,
            "leak_counts": dict(leak_counts), "leak_ids": leak_ids, "embedding_check": bool(vecs), "diversity": div, "contrast_sets_total": len(sets_all), "contrast_sets_complete": complete,
            "contrast_items_accepted": sum(1 for s in accepted if s["set_id"]), "coverage_accepted": cov, "discards": {k: v["reason"] for k, v in discards.items()},
            "accepted_ids": sorted(acc_ids)}


def build_dataset(res: dict) -> None:
    specs, by_id, utts, dup, discards, review = load()
    rows = []
    for i in res["accepted_ids"]:
        s = by_id[i]
        rows.append({"id": i, "utterance": utts[i]["text"], "prior_turn": s["prior_turn"], "prior_speaker": s["prior_speaker"], "spec": {k: v for k, v in s.items() if k not in ("gold",)}, "gold": s["gold"],
                     "provenance": {"generation": "AI-assisted (an AI assistant), no cloud call", "review": "AI-assisted (same model), not independent human validation", "kind": "synthetic DEVELOPMENT data"}})
    (OUT / "dataset.jsonl").write_text("\n".join(json.dumps(r, ensure_ascii=False) for r in rows) + "\n", encoding="utf-8", newline="\n")
    (OUT / "report.json").write_text(json.dumps({k: v for k, v in res.items() if k not in ("accepted_ids",)}, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    h = hashlib.sha256((OUT / "dataset.jsonl").read_bytes()).hexdigest()
    m = json.loads((OUT / "manifest.json").read_text(encoding="utf-8"))
    m["dataset_sha256"] = h
    m["dataset_items"] = len(rows)
    (OUT / "manifest.json").write_text(json.dumps(m, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")


def main(argv):
    res = evaluate()
    if "--warnings" in argv:
        for i, w in sorted(res["warnings"].items()):
            print(i[4:], ",".join(w))
        return 0
    print(json.dumps({k: v for k, v in res.items() if k not in ("warnings", "accepted_ids", "leak_ids", "near_duplicates", "coverage_accepted")}, indent=1)[:6000])
    print("warnings:", len(res["warnings"]), "near-duplicates:", len(res["near_duplicates"]))
    if "--build" in argv and not res["hard_failures"]:
        build_dataset(res)
        print("built dataset.jsonl, report.json and the dataset hash in manifest.json")
    return 1 if res["hard_failures"] else 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
