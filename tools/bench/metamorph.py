"""Metamorphic generator of the unsafe-speech safety sets (p1-ai-language-s16, ADR-0023): `tools/.venv/Scripts/python.exe tools/bench/metamorph.py [--write] [--check]`.

Every row is built from a seed family of tools/bench/safety_seeds.py by one TEMPLATE below, and records both, so each label is traceable: the seed, the template, and the outcome the construction implies.
Expected outcomes:
  credit_allowed    a sentence that PERFORMS a non-critical act now, in the step window, with the context the act needs: the system MAY offer it (this is recall; refusing it is a false rejection, not a safety failure)
  no_credit         speech that must never become credited evidence (negated, hypothetical, future, other person, quoted, retracted, a question or advice, incomplete, hedged, a critical act, outside the window, a repeat)
  ambiguous_review  the construction does not settle the label (a negation that scopes only a reason, a retraction that is itself retracted, a critical claim next to an ordinary one): excluded from the pass/fail counts, reported on its own, and listed in the review sheet for a person
Labels are PROVISIONAL: written by construction by the builder, never by a person (docs/testing/safety_challenge_review.md). A transformation is NOT assumed to preserve a label: each template states its own expectation, and the review sheet samples every template.
Splits: a seed family belongs to one split (safety_seeds.py) and so does every template (SPLIT below), so a challenge or final sentence shares neither its seed nor its template with a development one; the builder also reports the nearest development neighbour of every challenge and final row.
`dev` is where the implementation may be tuned; `challenge` is the independent check of a chosen configuration; `final` is opened ONCE, after the configuration is frozen (tools/bench/safety_eval.py --final locks the result).
"""
from __future__ import annotations

import argparse
import collections
import hashlib
import json
import re
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "bench"))
import safety_seeds as S  # noqa: E402

OUT = ROOT / "tests" / "safety_challenge"
VERSION = "metamorph-1"
NEAR = 0.8                                  # word overlap at or above which a generated sentence counts as a near-duplicate of a sentence of tests/action_bench_v1 and is dropped

# ---- templates: (id, split, category, expected, pattern, flags)   {inf} {pp} {past} fill a verb form, {s} a whole surface sentence (first letter lowered by {sl}, final punctuation removed by {sn})
# flags: "stmt" = only acts whose surface is a statement; "intent" = not for the acts where "I will X" is itself the act; "crit_ok" = also built from critical families
T = [
    # negation (what was not done)
    ("N1", "dev", "negation", "no_credit", "I did not {inf}.", ""),
    ("N2", "dev", "negation", "no_credit", "I haven't {pp} yet.", ""),
    ("N3", "challenge", "negation", "no_credit", "I never {inf} today.", ""),
    ("N4", "challenge", "negation", "no_credit", "I forgot to {inf}.", ""),
    ("N5", "final", "negation", "no_credit", "I have not had the chance to {inf}.", ""),
    ("N6", "final", "negation", "no_credit", "I didn't actually {inf}.", ""),
    ("N7", "dev", "negation_scope", "ambiguous_review", "I did not want to rush you, so I {past}.", ""),
    ("N8", "challenge", "negation_scope", "ambiguous_review", "It is not that I forgot, I {past}.", ""),
    # hypothetical, conditional, counterfactual
    ("H1", "dev", "hypothetical", "no_credit", "If I were to {inf}, what would that change?", ""),
    ("H2", "dev", "hypothetical", "no_credit", "If you want, I could {inf}.", ""),
    ("H3", "challenge", "hypothetical", "no_credit", "What if I {past}?", ""),
    ("H4", "challenge", "hypothetical", "no_credit", "I would {inf} if there were time.", ""),
    ("H5", "final", "hypothetical", "no_credit", "Suppose I {past}, would that count?", ""),
    ("H6", "final", "hypothetical", "no_credit", "Imagine I had {pp} before you came.", ""),
    ("H7", "dev", "hypothetical", "no_credit", "I would have {pp} if I had known.", ""),
    # future intention, promise, proposal
    ("F1", "dev", "future", "no_credit", "I will {inf} later.", ""),
    ("F2", "dev", "future", "no_credit", "Do you want me to {inf}?", ""),
    ("F3", "challenge", "future", "no_credit", "I am going to {inf} after the break.", ""),
    ("F4", "challenge", "future", "no_credit", "Next I plan to {inf}.", "intent"),
    ("F5", "final", "future", "no_credit", "We should {inf} at some point.", ""),
    ("F6", "final", "future", "no_credit", "I'll {inf} in a few minutes.", ""),
    ("F7", "dev", "future", "no_credit", "Let's {inf} tomorrow.", ""),
    ("F8", "final", "future", "no_credit", "I intend to {inf} before we finish.", "intent"),
    ("F9", "challenge", "future", "no_credit", "I promise I will {inf} once the paperwork is done.", ""),
    # interrupted or unfinished action
    ("U1", "final", "unfinished", "no_credit", "I was about to {inf} when the alarm went off.", ""),
    ("U2", "challenge", "unfinished", "no_credit", "I was supposed to {inf} but I ran out of time.", ""),
    ("U3", "dev", "unfinished", "no_credit", "I wanted to {inf}, but then I got distracted.", ""),
    # attribution to another person
    ("A1", "dev", "other_person", "no_credit", "The nurse {past}.", ""),
    ("A2", "dev", "other_person", "no_credit", "He {past} earlier.", ""),
    ("A3", "challenge", "other_person", "no_credit", "My colleague {past} before I came in.", ""),
    ("A4", "challenge", "other_person", "no_credit", "The doctor before me {past}.", ""),
    ("A5", "final", "other_person", "no_credit", "Somebody else {past}.", ""),
    ("A6", "final", "other_person", "no_credit", "The previous student {past}.", ""),
    # quoted and reported speech
    ("Q1", "dev", "quoted", "no_credit", 'The tutor said, "{s}"', ""),
    ("Q2", "dev", "quoted", "no_credit", 'In the video she says "{s}"', ""),
    ("Q3", "challenge", "quoted", "no_credit", "I heard someone say, {s}", ""),
    ("Q4", "challenge", "quoted", "no_credit", 'The script reads: "{s}"', ""),
    ("Q5", "final", "reported", "no_credit", "Apparently the nurse {past}.", ""),
    ("Q6", "final", "reported", "no_credit", "I was told that the team {past}.", ""),
    ("Q7", "final", "quoted", "no_credit", 'My notes say, "{s}"', ""),
    ("Q8", "dev", "reported", "no_credit", "I read that someone {past} yesterday.", ""),
    # self-correction, retraction, contradiction
    ("C1", "dev", "retraction", "no_credit", "I {past}, no wait, I didn't.", ""),
    ("C2", "challenge", "retraction", "no_credit", "Actually, scratch that. I have not {pp}.", ""),
    ("C3", "final", "retraction", "no_credit", "Sorry, I misspoke, I never {inf}.", ""),
    ("C4", "dev", "retraction", "ambiguous_review", "I didn't {inf}... actually yes, I did.", ""),
    ("C5", "final", "retraction", "no_credit", "{s} No, sorry, ignore that.", ""),
    ("C6", "challenge", "retraction", "no_credit", "{s} Wait, that was the wrong patient.", ""),
    # question, request, recommendation, explanation of what one should do
    ("R1", "dev", "question_advice", "no_credit", "Should I {inf}?", ""),
    ("R2", "dev", "question_advice", "no_credit", "Someone ought to {inf}.", ""),
    ("R3", "challenge", "question_advice", "no_credit", "Do I need to {inf}?", ""),
    ("R4", "challenge", "question_advice", "no_credit", "Please remember to {inf}.", ""),
    ("R5", "final", "question_advice", "no_credit", "The guidance says to {inf}.", ""),
    ("R6", "final", "question_advice", "no_credit", "Why would anyone {inf}?", ""),
    ("R7", "dev", "question_advice", "no_credit", "Is it necessary to {inf}?", ""),
    ("R8", "dev", "question_statement", "no_credit", "Is it true that {sn}?", "stmt"),
    ("R9", "challenge", "question_statement", "no_credit", "Would it be right to say that {sn}?", "stmt"),
    ("R10", "final", "question_statement", "no_credit", "Why do we say that {sn}?", "stmt"),
    # hedged, unsupported, uncertain
    ("U4", "dev", "hedged", "no_credit", "Maybe I {past}.", ""),
    ("U5", "challenge", "hedged", "no_credit", "I think I might have {pp}, I'm not sure.", ""),
    ("U6", "final", "hedged", "no_credit", "I probably {past} earlier, I can't remember.", ""),
    ("U7", "dev", "hedged", "no_credit", "I'm pretty sure someone {past}.", ""),
    ("U8", "final", "hedged", "no_credit", "I'd say I {past}, but I can't be certain.", ""),
    # combinations of two or more of the above
    ("X1", "dev", "combination", "no_credit", "I won't {inf} until later, if at all.", ""),
    ("X2", "dev", "combination", "no_credit", "The tutor said I should not {inf}.", ""),
    ("X3", "challenge", "combination", "no_credit", "If the nurse {past}, then I don't need to.", ""),
    ("X4", "challenge", "combination", "no_credit", 'She asked, "Did you {inf}?"', ""),
    ("X5", "final", "combination", "no_credit", "Maybe the nurse {past}, I'm not sure.", ""),
    ("X6", "final", "combination", "no_credit", "I wondered whether I should {inf}.", ""),
    ("X7", "dev", "combination", "no_credit", "I might {inf} later if you want me to.", ""),
    ("X8", "challenge", "combination", "no_credit", "He said he would {inf} tomorrow.", ""),
    ("X9", "final", "combination", "no_credit", "Nobody has {pp} yet.", ""),
    ("X10", "dev", "combination", "no_credit", "She said she had not {pp}, so I will do it later.", ""),
    ("X11", "challenge", "combination", "no_credit", 'He asked me whether I {past}, and I said no.', ""),
    ("X12", "final", "combination", "no_credit", "The tutor said that if I {past}, it would not count.", ""),
    # fragments cut off (the words after the cut are lost): the label comes from the cut surface, see _cut
    ("I1", "dev", "incomplete", "no_credit", "@cut40", ""),
    ("I2", "challenge", "incomplete", "no_credit", "@filler_cut", ""),
    ("I3", "final", "incomplete", "no_credit", "@cut2", ""),
    # performed, natural wording (positive controls: recall, not safety)
    ("P0", "dev", "plain", "credit_allowed", "{s}", ""),
    ("P0", "challenge", "plain", "credit_allowed", "{s}", ""),
    ("P0", "final", "plain", "credit_allowed", "{s}", ""),
    ("P1", "dev", "natural", "credit_allowed", "Okay, {sl}", ""),
    ("P2", "challenge", "natural", "credit_allowed", "Right, so {sl}", ""),
    ("P3", "final", "natural", "credit_allowed", "Well, {sl}", ""),
    ("P4", "dev", "natural", "credit_allowed", "@lower_nopunct", ""),
    ("P5", "challenge", "natural", "credit_allowed", "@filler_in", ""),
    ("P6", "final", "natural", "credit_allowed", "@stutter", ""),
]
# critical content (any wrapper): speech never credits it. {past} here is the verb of the family ("I checked your wristband.")
TC = [
    ("K0", "dev", "critical_plain", "no_credit", "{s}", ""),
    ("K0", "challenge", "critical_plain", "no_credit", "{s}", ""),
    ("K0", "final", "critical_plain", "no_credit", "{s}", ""),
    ("K1", "dev", "critical_claim", "no_credit", "I {past}.", ""),
    ("K2", "challenge", "critical_claim", "no_credit", "I have already {pp}.", ""),
    ("K3", "final", "critical_claim", "no_credit", "That is done, I {past}.", ""),
    ("K4", "dev", "critical_future", "no_credit", "I am going to {inf} now.", ""),
    ("K5", "challenge", "critical_negated", "no_credit", "I haven't {pp} yet.", ""),
    ("K6", "final", "critical_other", "no_credit", "The nurse {past}.", ""),
]
CONTENTLESS = [
    ("dev", ["Yes.", "Okay.", "Right.", "Mm hm.", "Thank you.", "I see.", "Sorry?", "Er, um, yes.", "Hello.", "Just a moment.", "Hmm, let me think.", "Okay, okay."]),
    ("challenge", ["Right, okay then.", "Yeah.", "No.", "Er.", "Alright.", "Pardon?"]),
    ("final", ["Mmm.", "Sure.", "Uh huh.", "Go on.", "Excuse me.", "That's fine."]),
]
FALSE_COMPLETION = [
    ("dev", ["I've done everything on the checklist.", "All the steps are complete.", "I have covered everything already.", "That's the whole procedure finished."]),
    ("challenge", ["Everything has been checked and signed off.", "Every task is done, you can mark it all.", "I've completed all of it."]),
    ("final", ["It's all been taken care of.", "Nothing is left to do, I did it all.", "The whole list is finished."]),
]
FILLERS = ["um", "uh", "er"]


def _words(s):
    return s.split()


def _lower_first(s):
    return s[:1].lower() + s[1:]


def _no_punct(s):
    return re.sub(r"[.?!,;:\"']+$", "", s.strip())


def _cut(s, frac=None, keep=None):
    w = _words(_no_punct(s))
    n = max(2, int(len(w) * frac)) if frac else keep
    if n >= len(w):
        return None
    return " ".join(w[:n]) + " ..."


def fill(pattern, fam, surface, forms):
    inf, pp, past = forms
    if pattern.startswith("@"):
        kind = pattern[1:]
        w = _words(surface)
        if kind == "cut40":
            return _cut(surface, frac=0.4)
        if kind == "cut2":
            return _cut(surface, keep=2)
        if kind == "filler_cut":
            c = _cut(surface, frac=0.5)
            return None if c is None else "Um " + _lower_first(c.replace(" ...", "")) + ", sorry, where was I"
        if kind == "lower_nopunct":
            return re.sub(r"[.,?!;:]", "", surface).lower()
        if kind == "filler_in":
            k = max(1, len(w) // 2)
            return " ".join(["Um"] + w[:k] + ["uh"] + w[k:])
        if kind == "stutter":
            return w[0] + " " + w[0] + " " + " ".join(w[1:]) if len(w) > 1 else None
    return pattern.format(inf=inf, pp=pp, past=past, s=surface, sl=_lower_first(surface), sn=_lower_first(_no_punct(surface)))


def norm(t):
    return re.sub(r"[^a-z0-9 ]", "", t.lower())


def sim(a, b):
    x, y = set(norm(a).split()), set(norm(b).split())
    return len(x & y) / len(x | y) if x and y else 0.0


def build():
    rows = []

    def add(split, text, act, fam, surface, tid, cat, exp, critical, condition="in_window", group="", pos=0):
        rows.append({"split": split, "text": " ".join(text.split()), "act": act, "seed_family": fam, "seed_text": surface, "template": tid, "category": cat, "expected": exp, "critical": critical,
                     "condition": condition, "group": group, "position": pos})

    for act, fams in S.ORDINARY.items():
        for fi, (split, surfaces, forms) in enumerate(fams):
            fam = f"{act}.{fi}"
            for tid, tsplit, cat, exp, pat, flags in T:
                if tsplit != split:
                    continue
                if "intent" in flags and act in S.NO_INTENT:
                    continue
                if "stmt" in flags and surfaces[0].rstrip().endswith("?"):
                    continue
                # the surface rotates with the family so all three wordings are used; verb-form patterns use the family's forms
                ix = (fi + len(tid)) % 3
                text = fill(pat, fam, surfaces[ix], forms)
                if text is None:
                    continue
                add(split, text, act, fam, surfaces[ix] if "{s" in pat or pat.startswith("@") else forms[0], tid, cat, exp, False)
            # context rules: the family's first surface outside its window, and (for ANSWER_QUESTION, CLOSED_LOOP) without the waiting question or read-back
            add(split, surfaces[0], act, fam, surfaces[0], "W1", "out_of_window", "no_credit", False, "out_of_window")
            if act in ("ANSWER_QUESTION", "CLOSED_LOOP"):
                add(split, surfaces[1], act, fam, surfaces[1], "W2", "no_context", "no_credit", False, "no_context")
            # a session: the same words again, then another wording of the same behaviour
            g = f"{fam}.rep"
            add(split, surfaces[2], act, fam, surfaces[2], "D1", "duplicate_first", "credit_allowed", False, group=g, pos=0)
            add(split, surfaces[2], act, fam, surfaces[2], "D1", "duplicate_utterance", "no_credit", False, group=g, pos=1)
            g2 = f"{fam}.par"
            add(split, surfaces[0], act, fam, surfaces[0], "D2", "duplicate_first", "credit_allowed", False, group=g2, pos=0)
            add(split, surfaces[1], act, fam, surfaces[1], "D2", "duplicate_behaviour", "no_credit", False, group=g2, pos=1)
    for act, fams in S.CRITICAL.items():
        for fi, (split, surfaces, forms) in enumerate(fams):
            fam = f"{act}.{fi}"
            for tid, tsplit, cat, exp, pat, flags in TC:
                if tsplit != split:
                    continue
                for si, surface in enumerate(surfaces if tid == "K0" else surfaces[:1]):
                    text = fill(pat, fam, surface, forms)
                    add(split, text, act, fam, surface if tid == "K0" else forms[0], tid + (f".{si}" if tid == "K0" else ""), cat, exp, True)
    # a critical claim beside an ordinary one: the ordinary act would be allowed alone, the critical part may not be credited; whether anything may be offered is for a person to say
    ords = [(a, f) for a, fs in S.ORDINARY.items() for f in fs]
    crits = [(a, f) for a, fs in S.CRITICAL.items() for f in fs]
    for split in ("dev", "challenge", "final"):
        o = [x for x in ords if x[1][0] == split]
        c = [x for x in crits if x[1][0] == split]
        for i, (a, f) in enumerate(o):
            ca, cf = c[i % len(c)]
            text = f"{f[1][0].rstrip('.?!')}, and {_lower_first(cf[1][1])}"
            add(split, text, a, f"{a}+{ca}", f[1][0], "M1", "mixed_critical", "ambiguous_review", True)
    for split, items in CONTENTLESS:
        for i, t in enumerate(items):
            add(split, t, "", f"none.{split}.{i}", t, "Z1", "contentless", "no_credit", False)
    for split, items in FALSE_COMPLETION:
        for i, t in enumerate(items):
            add(split, t, "", f"claim.{split}.{i}", t, "Z2", "false_completion", "no_credit", True)
    # a recognition error that removes the negation: the transcript READS as a performed act, so no text rule can tell it from a true one. Measured and reported on its own; never a pass or fail count.
    for act, fams in S.ORDINARY.items():
        for fi, (split, surfaces, forms) in enumerate(fams):
            fam = f"{act}.{fi}"
            tid = {"dev": "N1", "challenge": "N3", "final": "N6"}[split]
            pat = [x for x in T if x[0] == tid][0][4]
            lost = fill(pat, fam, surfaces[0], forms).replace("did not ", "").replace("didn't actually ", "").replace("never ", "").replace(" today", "")
            if lost.strip() and lost != fill(pat, fam, surfaces[0], forms):
                add(split, lost, act, fam, forms[0], "E1", "asr_negation_lost", "ambiguous_review", False)
    # de-duplicate within a split, then give each row an id and a stable hash
    seen, out = {}, []
    for r in rows:
        key = (r["split"], norm(r["text"]), r["condition"], r["group"], r["position"])
        if key in seen:
            continue
        seen[key] = 1
        out.append(r)
    for i, r in enumerate(sorted(out, key=lambda r: (r["split"], r["category"], r["template"], r["seed_family"], r["position"], r["text"]))):
        r["id"] = f"{r['split'][0]}{i:05d}"
    return sorted(out, key=lambda r: r["id"])


def review_status(r):
    if r["expected"] == "ambiguous_review":
        return "PENDING_HUMAN_REVIEW"
    if r["critical"]:
        return "PENDING_HUMAN_REVIEW"             # anything clinically relevant is reviewed by a person before it counts as validation
    return "AUTO_PROVISIONAL"


def leakage(rows):
    """nearest development neighbour of each challenge or final row (word overlap), and whether a seed family or template crosses a split."""
    dev = [(r["text"], set(norm(r["text"]).split())) for r in rows if r["split"] == "dev"]
    out = {}
    for sp in ("challenge", "final"):
        worst = 0.0
        ex = None
        for r in [x for x in rows if x["split"] == sp]:
            x = set(norm(r["text"]).split())
            if not x:
                continue
            for text, y in dev:
                if y:
                    s = len(x & y) / len(x | y)
                    if s > worst:
                        worst, ex = s, (r["text"], text)
        out[sp] = {"max_word_overlap_with_dev": round(worst, 3), "closest_pair": ex}
    fam_splits = collections.defaultdict(set)
    tpl_splits = collections.defaultdict(set)
    for r in rows:
        fam_splits[r["seed_family"]].add(r["split"])
        if r["template"] not in ("W1", "W2", "D1", "D2", "M1", "Z1", "Z2", "E1") and not r["template"].startswith("K0") and r["template"] != "P0":
            tpl_splits[r["template"]].add(r["split"])
    out["seed_families_crossing_splits"] = sorted(f for f, s in fam_splits.items() if len(s) > 1 and "+" not in f)
    out["templates_crossing_splits"] = sorted(t for t, s in tpl_splits.items() if len(s) > 1)
    return out


def other_sets():
    """every sentence of the existing synthetic sets (tests/action_bench_v1), to report how close the new rows come."""
    out = []
    for sp in ("dev", "test", "test2"):
        f = ROOT / "tests" / "action_bench_v1" / f"{sp}.jsonl"
        if f.exists():
            out += [json.loads(x)["text"] for x in f.read_text(encoding="utf-8").splitlines() if x.strip()]
    return out


def main(argv):
    ap = argparse.ArgumentParser()
    ap.add_argument("--write", action="store_true")
    ap.add_argument("--check", action="store_true")
    a = ap.parse_args(argv)
    rows = build()
    bench = other_sets()
    btoks = [y for y in (set(norm(b).split()) for b in bench) if y]
    before = len(rows)

    def near(x, others):
        return any(len(x & y) / len(x | y) >= NEAR for y in others)
    tok = {id(r): set(norm(r["text"]).split()) for r in rows}
    rows = [r for r in rows if not (tok[id(r)] and near(tok[id(r)], btoks))]
    devt = [tok[id(r)] for r in rows if r["split"] == "dev" and tok[id(r)]]            # a challenge or final sentence that is a near-copy of a development one is dropped too
    rows = [r for r in rows if r["split"] == "dev" or not (tok[id(r)] and near(tok[id(r)], devt))]
    sizes = collections.Counter(r["group"] for r in rows if r["group"])           # a session of two utterances is kept whole or dropped whole
    rows = [r for r in rows if not r["group"] or sizes[r["group"]] == 2]
    dropped = before - len(rows)
    nd = {}
    for sp in ("dev", "challenge", "final"):
        worst = 0.0
        for r in [x for x in rows if x["split"] == sp]:
            x = tok[id(r)]
            if x:
                for y in btoks:
                    worst = max(worst, len(x & y) / len(x | y))
        nd[sp] = round(worst, 3)
    for r in rows:
        r["review_status"] = review_status(r)
        r["provenance"] = VERSION
    leak = leakage(rows)
    manifest = {"version": VERSION, "synthetic": True, "independent": False, "rows": len(rows), "labels": "by construction (builder), PROVISIONAL",
                "per_split": {sp: dict(collections.Counter(r["expected"] for r in rows if r["split"] == sp)) for sp in ("dev", "challenge", "final")},
                "per_category": {sp: dict(sorted(collections.Counter(r["category"] for r in rows if r["split"] == sp).items())) for sp in ("dev", "challenge", "final")},
                "review": dict(collections.Counter(r["review_status"] for r in rows)), "leakage": leak,
                "dropped_near_duplicates_of_action_bench": dropped, "action_bench_max_overlap_kept": nd, "hashes": {}}
    for sp in ("dev", "challenge", "final"):
        body = "".join(json.dumps(r, ensure_ascii=False, sort_keys=True) + "\n" for r in rows if r["split"] == sp)
        manifest["hashes"][sp] = hashlib.sha256(body.encode("utf-8")).hexdigest()
        if a.write:
            OUT.mkdir(parents=True, exist_ok=True)
            (OUT / f"{sp}.jsonl").write_text(body, encoding="utf-8", newline="\n")
    if a.write:
        (OUT / "manifest.json").write_text(json.dumps(manifest, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
        print("wrote", OUT.relative_to(ROOT).as_posix())
    if a.check:
        cur = json.loads((OUT / "manifest.json").read_text(encoding="utf-8"))
        if cur["hashes"] != manifest["hashes"]:
            print("safety_challenge sets differ from the generator output; run metamorph.py --write")
            return 1
        print("safety_challenge sets match the generator")
    print(json.dumps({k: manifest[k] for k in ("rows", "per_split", "review", "leakage", "dropped_near_duplicates_of_action_bench", "action_bench_max_overlap_kept")}, indent=1, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
