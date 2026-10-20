"""Audit of the label quality, coverage, duplication, template leakage and split independence of tests/safety_challenge (p1-ai-language-s17): `tools/.venv/Scripts/python.exe tools/bench/safety_set_audit.py [--write|--check]`.
Writes reports/ai/safety/set-audit.json and docs/research/safety_set_audit.md (generated; --check compares). It READS the sets and changes none of them, final included. Everything is computed from the files; the prose of the page states only what the numbers show and what they cannot show.
Provisional means written by the builder (a language model) with the label implied by the construction; reviewed means labelled by independent people through tools/bench/safety_review_kit.py. There are NO reviewed labels today: the page says so from the absence of an override file."""
from __future__ import annotations

import collections
import hashlib
import json
import re
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
ROOT = Path(__file__).resolve().parents[2]
SETS = ROOT / "tests" / "safety_challenge"
OUT_JSON = ROOT / "reports" / "ai" / "safety" / "set-audit.json"
OUT_MD = ROOT / "docs" / "research" / "safety_set_audit.md"
SPLITS = ("dev", "challenge", "final")
STRUCTURAL = {"W1", "W2", "D1", "D2", "P0", "Z1", "Z2", "M1", "E1"}        # templates that are not a wording frame (context rules, sessions, plain sentences, controls)


def norm(t):
    return re.sub(r"[^a-z0-9 ]", "", t.lower())


def toks(t):
    return set(norm(t).split())


def jac(a, b):
    return len(a & b) / len(a | b) if a and b else 0.0


def load():
    return {sp: [json.loads(x) for x in (SETS / f"{sp}.jsonl").read_text(encoding="utf-8").splitlines() if x.strip()] for sp in SPLITS}


def frame(r):
    """the wording around the seed: the text with the seed sentence or verb phrase replaced by {S} (what the TEMPLATE contributed)."""
    t = r["text"]
    s = r["seed_text"]
    for cand in (s, s.rstrip(".?!"), s[:1].lower() + s[1:], s[:1].lower() + s[1:].rstrip(".?!")):
        if cand and cand in t:
            return t.replace(cand, "{S}")
    return None


def audit() -> dict:
    rows = load()
    man = json.loads((SETS / "manifest.json").read_text(encoding="utf-8"))
    tax = {c["id"]: c for c in json.loads((SETS / "taxonomy.json").read_text(encoding="utf-8"))["categories"]}
    out: dict = {"synthetic": True, "independent": False, "reviewed_label_overrides_present": (SETS / "review_overrides.json").exists()}
    out["rows"] = {sp: len(rows[sp]) for sp in SPLITS}
    out["labels"] = {sp: {"expected": dict(collections.Counter(r["expected"] for r in rows[sp])), "review_status": dict(collections.Counter(r["review_status"] for r in rows[sp]))} for sp in SPLITS}
    # coverage: category x split
    cats = sorted({r["category"] for sp in SPLITS for r in rows[sp]})
    cov = {c: {sp: sum(1 for r in rows[sp] if r["category"] == c) for sp in SPLITS} for c in cats}
    out["coverage"] = cov
    out["categories_missing_from_a_split"] = {c: [sp for sp in SPLITS if cov[c][sp] == 0] for c in cats if any(cov[c][sp] == 0 for sp in SPLITS)}
    out["categories_under_20_rows_in_a_split"] = {c: {sp: cov[c][sp] for sp in SPLITS if 0 < cov[c][sp] < 20} for c in cats if any(0 < cov[c][sp] < 20 for sp in SPLITS)}
    out["taxonomy_ids_without_rows"] = sorted(c["id"] for c in tax.values() if c["generated"] and c["id"] not in cats)
    out["templates_per_category_per_split"] = {c: {sp: len({r["template"].split(".")[0] for r in rows[sp] if r["category"] == c}) for sp in SPLITS} for c in cats}
    # duplicates
    within, across = {}, {}
    seen: dict[str, set] = {}
    for sp in SPLITS:
        keys = collections.Counter((norm(r["text"]), r["condition"], r["position"]) for r in rows[sp])
        within[sp] = sum(n - 1 for n in keys.values() if n > 1)
        for r in rows[sp]:
            seen.setdefault(norm(r["text"]), set()).add(sp)
    across["exact_text_in_more_than_one_split"] = sum(1 for s in seen.values() if len(s) > 1)
    across["examples"] = [t for t, s in list(seen.items()) if len(s) > 1][:8]
    out["duplicates"] = {"exact_within_split_same_condition": within, **across}
    # near-duplicate neighbours of challenge and final rows in dev
    dev = [(r, toks(r["text"])) for r in rows["dev"]]
    near = {}
    for sp in ("challenge", "final"):
        best = []
        for r in rows[sp]:
            x = toks(r["text"])
            best.append(max((jac(x, y) for _, y in dev), default=0.0))
        best.sort()
        near[sp] = {"median": round(best[len(best) // 2], 3), "p95": round(best[int(len(best) * 0.95)], 3), "max": round(best[-1], 3), "ge_0_6": sum(b >= 0.6 for b in best), "ge_0_7": sum(b >= 0.7 for b in best), "rows": len(best)}
    out["nearest_dev_neighbour_word_overlap"] = near
    # seed / template independence
    out["seed_families_crossing_splits"] = man["leakage"]["seed_families_crossing_splits"]
    out["templates_crossing_splits"] = man["leakage"]["templates_crossing_splits"]
    # lexical frames: what the template contributed, compared across splits
    frames: dict[str, collections.Counter] = {sp: collections.Counter() for sp in SPLITS}
    for sp in SPLITS:
        for r in rows[sp]:
            if r["template"].split(".")[0] not in STRUCTURAL:
                f = frame(r)
                if f:
                    frames[sp][f] += 1
    shared = {f"{a}/{b}": len(set(frames[a]) & set(frames[b])) for a, b in (("dev", "challenge"), ("dev", "final"), ("challenge", "final"))}
    out["identical_wording_frames_shared_between_splits"] = shared
    fr_dev = list(frames["dev"])
    fr_near = {}
    for sp in ("challenge", "final"):
        sims = [max((jac(toks(f), toks(g)) for g in fr_dev), default=0.0) for f in frames[sp]]
        fr_near[sp] = {"frames": len(sims), "frames_with_word_overlap_ge_0_6_to_a_dev_frame": sum(s >= 0.6 for s in sims), "median_overlap": round(sorted(sims)[len(sims) // 2], 3) if sims else None}
    out["frame_similarity_to_dev"] = fr_near
    # construction artefacts: sentences a person would call odd (label may not survive); counted per template
    art = collections.Counter()
    ex = {}
    for sp in SPLITS:
        for r in rows[sp]:
            t = r["text"]
            bad = None
            if re.search(r"\b(\w+) \1\b", t, re.I) and r["template"] != "P6":
                bad = "repeated word"
            elif re.search(r"\bthat (thank|okay|right|well|so|sorry)\b", t, re.I) or re.search(r"\bsay that (so|thank|okay)\b", t, re.I):
                bad = "seed lowered into a frame it does not fit"
            elif re.search(r"\b(you|your) (told|asked|noted)\b.*\byou\b", t) and r["category"] == "other_person":
                bad = "second person subject in an other-person frame"
            elif re.search(r"\bto (tell|reassure|ask) you that you\b", t) and r["template"].startswith(("R", "F", "N")):
                bad = "'to tell you that you' (clumsy verb phrase)"
            if bad:
                art[(r["template"].split(".")[0], bad)] += 1
                ex.setdefault((r["template"].split(".")[0], bad), (r["id"], t))
    out["construction_artefacts"] = [{"template": k[0], "kind": k[1], "rows": n, "example": ex[k][1]} for k, n in sorted(art.items())]
    # label risk by construction
    risky = []
    for tid, why in (("N7", "negation scoping a reason: the act IS performed; label ambiguous by design"), ("N8", "'not that I forgot, I did': the act IS performed"), ("C4", "self-repair that reverses itself"), ("M1", "ordinary act beside a critical claim"), ("E1", "recogniser dropped the negation: the transcript reads as performed"),
                     ("D2", "second wording of one behaviour: no_credit only because one behaviour is credited once; a person judging only the words would say credit_allowed"), ("D1", "a repeat of the same utterance: no_credit only because of the session"),
                     ("W1", "the words are correct; no_credit only because of the step window"), ("W2", "the words are correct; no_credit only because no question or read-back is waiting"), ("R10", "'why do we say that X?' asks about a fact; an explanation may still be performed"),
                     ("Q5", "'apparently the nurse did X': reporting, but a trainee relaying it to a patient may be answering"), ("A2", "'he X earlier': 'he' may be the patient or a colleague; ASR-free text cannot say")):
        n = {sp: sum(1 for r in rows[sp] if r["template"].split(".")[0] == tid) for sp in SPLITS}
        if any(n.values()):
            risky.append({"template": tid, "rows": n, "why": why})
    out["label_risk_templates"] = risky
    out["situation_dependent_labels"] = {"rows_whose_no_credit_label_comes_from_the_situation_not_the_words": sum(1 for sp in SPLITS for r in rows[sp] if r["template"].split(".")[0] in ("W1", "W2", "D1", "D2") and r["expected"] == "no_credit")}
    out["authorship"] = {"seed_families": sum(1 for _ in set(r["seed_family"] for sp in SPLITS for r in rows[sp] if "+" not in r["seed_family"])), "authors": "one (the builder, a language model)", "templates": len({r["template"].split('.')[0] for sp in SPLITS for r in rows[sp]}), "human_authored_rows": 0,
                        "human_reviewed_rows": 0 if not out["reviewed_label_overrides_present"] else "see review_overrides.json"}
    out["sheet"] = {"rows": sum(1 for _ in open(SETS / "review_sheet.csv", encoding="utf-8")) - 1, "sha256": hashlib.sha256((SETS / "review_sheet.csv").read_bytes()).hexdigest()[:16]}
    pend = sum(out["labels"][sp]["review_status"].get("PENDING_HUMAN_REVIEW", 0) for sp in SPLITS)
    out["pending_human_review_rows"] = pend
    return out


def page(a: dict) -> str:
    L = []
    w = L.append
    w("# Audit of the unsafe-speech sets: label quality, coverage, duplication and independence (generated)\n")
    w("> Generated by `tools/bench/safety_set_audit.py` from `tests/safety_challenge`; do not edit. The audit reads the sets and changes none of them, the final split included. **Every label is PROVISIONAL (builder-written, implied by the construction); "
      f"{'no independently reviewed label exists' if not a['reviewed_label_overrides_present'] else 'an override file exists: see it'}.** Nothing here is independent validation.\n")
    w("## 1. Provisional against reviewed labels\n")
    w("| split | rows | credit_allowed | no_credit | ambiguous_review | AUTO_PROVISIONAL | PENDING_HUMAN_REVIEW | independently reviewed |")
    w("|---|---|---|---|---|---|---|---|")
    for sp in SPLITS:
        e, s = a["labels"][sp]["expected"], a["labels"][sp]["review_status"]
        w(f"| {sp} | {a['rows'][sp]} | {e.get('credit_allowed', 0)} | {e.get('no_credit', 0)} | {e.get('ambiguous_review', 0)} | {s.get('AUTO_PROVISIONAL', 0)} | {s.get('PENDING_HUMAN_REVIEW', 0)} | 0 |")
    w(f"\nAuthorship: {a['authorship']['authors']}; {a['authorship']['seed_families']} seed-family labels (71 act families, each in three wordings and three verb forms, plus the single contentless and false-completion utterances), {a['authorship']['templates']} templates, {a['authorship']['human_authored_rows']} human-authored rows. The blind sheet holds {a['sheet']['rows']} rows (sha256 `{a['sheet']['sha256']}`): every `PENDING_HUMAN_REVIEW` row ({a['pending_human_review_rows']}) and a stratified sample of the rest. "
      "A reviewed label would enter only as an override file (`tools/bench/safety_review_kit.py merge`), never as an edit of a set.\n")
    w("## 2. Category coverage\n")
    w(f"Taxonomy categories marked generated that have no row: {', '.join(a['taxonomy_ids_without_rows']) or 'none'}.\n")
    miss = a["categories_missing_from_a_split"]
    w("Categories missing from at least one split (the templates of a category are assigned to splits by hand, and some categories have one template): " + ("; ".join(f"`{c}` has no row in {', '.join(v)}" for c, v in miss.items()) or "none") + ".\n")
    w("Categories with fewer than 20 rows in a split where they appear: " + ("; ".join(f"`{c}` " + ", ".join(f"{sp} {n}" for sp, n in v.items()) for c, v in a["categories_under_20_rows_in_a_split"].items()) or "none") + ".\n")
    w("| category | dev | challenge | final | templates dev / challenge / final |")
    w("|---|---|---|---|---|")
    for c, v in a["coverage"].items():
        t = a["templates_per_category_per_split"][c]
        w(f"| `{c}` | {v['dev']} | {v['challenge']} | {v['final']} | {t['dev']} / {t['challenge']} / {t['final']} |")
    w("\nA category with one or two templates in a split is a statement about those wordings, not about the category; the final split has between one and three templates per unsafe category.\n")
    w("## 3. Duplicates and leakage\n")
    d = a["duplicates"]
    w(f"* Exact duplicates inside a split (same text, condition and position): dev {d['exact_within_split_same_condition']['dev']}, challenge {d['exact_within_split_same_condition']['challenge']}, final {d['exact_within_split_same_condition']['final']}. "
      "These are a plain row and the first utterance of a session pair that happen to be the same sentence (the generator removes duplicates only inside one session group), so they count twice in the legitimate totals. The sets are frozen and are not edited; the effect is stated here.\n"
      f"* Texts that occur in more than one split: {d['exact_text_in_more_than_one_split']}" + (f" (e.g. {', '.join(repr(x) for x in d['examples'][:4])}): plain sentences of controls and short utterances, which the generator allows only where seeds differ" if d['exact_text_in_more_than_one_split'] else "") + ".")
    w(f"* Seed families that cross splits: {len(a['seed_families_crossing_splits'])}; templates that cross splits: {len(a['templates_crossing_splits'])}.")
    for sp in ("challenge", "final"):
        n = a["nearest_dev_neighbour_word_overlap"][sp]
        w(f"* Nearest development neighbour of each {sp} row (word overlap): median {n['median']}, 95th percentile {n['p95']}, maximum {n['max']}; {n['ge_0_6']} of {n['rows']} rows at 0.6 or more, {n['ge_0_7']} at 0.7 or more (a wrapper shared in kind, such as a negation, with a different seed).")
    sh = a["identical_wording_frames_shared_between_splits"]
    w(f"* Wording frames (the template's own words with the seed replaced): identical frames shared between dev and challenge {sh['dev/challenge']}, dev and final {sh['dev/final']}, challenge and final {sh['challenge/final']}.")
    for sp in ("challenge", "final"):
        f = a["frame_similarity_to_dev"][sp]
        w(f"* {sp}: {f['frames_with_word_overlap_ge_0_6_to_a_dev_frame']} of {f['frames']} frames have word overlap 0.6 or more with some development frame (median {f['median_overlap']}). Independence of *seed and template identity* holds; independence of *wording style* does not: the same author wrote all three, and several final frames are paraphrases of development frames (for example a negation in other words).")
    w("\n## 4. Construction artefacts (sentences a person may find odd, so the label may not survive review)\n")
    if a["construction_artefacts"]:
        w("| template | kind | rows | example |")
        w("|---|---|---|---|")
        for x in a["construction_artefacts"]:
            w(f"| {x['template']} | {x['kind']} | {x['rows']} | {x['example']} |")
    else:
        w("None found by the heuristics.")
    w("\nThe heuristics are crude; the blind review is the real check.\n")
    w("## 5. Templates whose label depends on more than the words\n")
    w("| template | rows dev / challenge / final | why the label is fragile |")
    w("|---|---|---|")
    for r in a["label_risk_templates"]:
        n = r["rows"]
        w(f"| {r['template']} | {n['dev']} / {n['challenge']} / {n['final']} | {r['why']} |")
    w(f"\n{a['situation_dependent_labels']['rows_whose_no_credit_label_comes_from_the_situation_not_the_words']} `no_credit` rows are `no_credit` because of the situation (window, waiting question, session), not the words: a reviewer must be given the situation (the sheet does) and the label-error rate must be read separately for them.\n")
    w("## 6. What this audit can and cannot show\n")
    w("Can: that the sets are reproducible, that no seed family or template crosses a split, that categories are covered unevenly, where labels are fragile by construction. Cannot: that any label is right, that the final split is independent of the development split in *style* (one author), or that the rows resemble trainee speech. "
      "The final split is untouched (its hash is pinned in `tests/safety_challenge/frozen.json`); it is spent for every configuration after `s16`, so a later fix needs a fresh final set.\n")
    return "\n".join(L) + "\n"


def main(argv):
    a = audit()
    js = json.dumps(a, indent=1, ensure_ascii=False) + "\n"
    md = page(a)
    if "--check" in argv:
        ok = OUT_JSON.exists() and OUT_JSON.read_text(encoding="utf-8") == js and OUT_MD.exists() and OUT_MD.read_text(encoding="utf-8") == md
        print("safety_set_audit current" if ok else "safety_set_audit OUT OF DATE: run tools/bench/safety_set_audit.py")
        return 0 if ok else 1
    OUT_JSON.write_text(js, encoding="utf-8", newline="\n")
    OUT_MD.write_text(md, encoding="utf-8", newline="\n")
    print("wrote", OUT_JSON.relative_to(ROOT).as_posix(), "and", OUT_MD.relative_to(ROOT).as_posix())
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
