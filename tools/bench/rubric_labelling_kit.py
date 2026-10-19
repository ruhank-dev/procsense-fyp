"""Annotation kit for a small, independently human-labelled set of new fictional ProcSense dialogue (rubric-evaluator evaluation; protocol in docs/testing/independent_labelling_protocol.md):
`uv run --project tools tools/bench/rubric_labelling_kit.py template|check|agree|merge ...`.

  template --out DIR                      writes DIR/annotation_template.csv (header only: no example rows, no labels).
  check    --file F [--annotator NAME]    validates ONE annotator's file: columns, allowed values, evidence found in the utterance, duplicate item ids, an utterance already present in a repository benchmark set (leak), and that `credit_eligible`
                                          agrees with the approved rule (an ordinary act performed now by the trainee). Disagreement with the rule is REPORTED for adjudication, not corrected.
  agree    --a FILE --b FILE              two annotators' files on the same item ids: per field raw agreement and Cohen's kappa, and the list of items on which they differ (written to stdout).
  merge    --a FILE --b FILE --adjudicated FILE --out F   writes the final labelled set: items where both agree keep that label; differing items take the adjudicator's row; items flagged `ambiguous` by either annotator or the adjudicator are written to a separate list and
                                          are NOT part of the scored set. It never invents a label.

Every value is a person's. Nothing here labels, scores or generates a sentence; the output is `independent: true` only if the owner records who wrote and who labelled it (the tool cannot verify that).
"""
from __future__ import annotations

import argparse
import collections
import csv
import json
import re
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
ROOT = Path(__file__).resolve().parents[2]
TAX = json.loads((ROOT / "content" / "assessment" / "taxonomy.json").read_text(encoding="utf-8"))["acts"]
ACTS = [a["id"] for a in TAX]
CRITICAL = {a["id"] for a in TAX if a["critical"]}
ACTORS = {"trainee", "other", "unclear"}
MODALITIES = {"actual", "negated", "intended", "hypothetical", "reported", "incomplete"}
COLUMNS = ["item_id", "scenario_step", "scenario_context", "prior_turns", "speaker_role", "utterance", "acts_performed", "actor", "modality", "evidence", "credit_eligible", "ambiguous", "note"]
LABEL_FIELDS = ["acts_performed", "actor", "modality", "credit_eligible", "ambiguous"]


def norm(t: str) -> str:
    return re.sub(r"[^a-z0-9 ]+", "", t.lower()).strip()


def read(path: str) -> list[dict]:
    with open(path, encoding="utf-8", newline="") as fh:
        return list(csv.DictReader(fh))


def acts_of(row: dict) -> frozenset:
    return frozenset(x.strip() for x in row.get("acts_performed", "").split(";") if x.strip())


def rule_credit(row: dict) -> str:
    """The approved rule as the repository states it: an ordinary (non-critical) act performed now by the trainee. Critical acts are never credited from speech."""
    a = acts_of(row) - {"NONE"}
    return "yes" if (a - CRITICAL) and row.get("actor") == "trainee" and row.get("modality") == "actual" else "no"


def known_sentences() -> set[str]:
    out = set()
    for p in list((ROOT / "tests").glob("**/*.jsonl")) + list((ROOT / "tests").glob("**/*.json")):
        try:
            txt = p.read_text(encoding="utf-8")
        except OSError:
            continue
        out |= {norm(m) for m in re.findall(r'"(?:text|sentence)"\s*:\s*"([^"]{3,300})"', txt)}
    return out


def check(path: str) -> list[str]:
    rows = read(path)
    bad: list[str] = []
    if not rows:
        return ["the file has no rows"]
    missing = [c for c in COLUMNS if c not in rows[0]]
    if missing:
        return [f"missing columns: {missing}"]
    seen, known = collections.Counter(r["item_id"] for r in rows), known_sentences()
    for r in rows:
        i = r["item_id"]
        if seen[i] > 1:
            bad.append(f"{i}: duplicate item id")
        if not r["utterance"].strip():
            bad.append(f"{i}: empty utterance")
        if norm(r["utterance"]) in known:
            bad.append(f"{i}: the utterance is already in a repository set (leak)")
        if not acts_of(r) <= set(ACTS):
            bad.append(f"{i}: unknown act in {sorted(acts_of(r) - set(ACTS))}")
        if r["actor"] not in ACTORS:
            bad.append(f"{i}: actor must be one of {sorted(ACTORS)}")
        if r["modality"] not in MODALITIES:
            bad.append(f"{i}: modality must be one of {sorted(MODALITIES)}")
        if r["credit_eligible"] not in ("yes", "no"):
            bad.append(f"{i}: credit_eligible must be yes or no")
        if r["ambiguous"] not in ("yes", "no"):
            bad.append(f"{i}: ambiguous must be yes or no")
        if r["evidence"].strip() and norm(r["evidence"]) not in norm(r["utterance"]):
            bad.append(f"{i}: evidence is not a part of the utterance")
        if acts_of(r) - {"NONE"} and not r["evidence"].strip() and r["modality"] == "actual":
            bad.append(f"{i}: an act performed now needs evidence")
        if r["credit_eligible"] in ("yes", "no") and r["credit_eligible"] != rule_credit(r):
            bad.append(f"{i}: credit_eligible={r['credit_eligible']} but the approved rule gives {rule_credit(r)} (adjudicate)")
    return bad


def kappa(a: list, b: list) -> float | None:
    n = len(a)
    if not n:
        return None
    po = sum(x == y for x, y in zip(a, b)) / n
    ca, cb = collections.Counter(a), collections.Counter(b)
    pe = sum(ca[k] * cb[k] for k in set(ca) | set(cb)) / (n * n)
    return None if pe == 1 else round((po - pe) / (1 - pe), 3)


def agree(fa: str, fb: str) -> dict:
    A, B = {r["item_id"]: r for r in read(fa)}, {r["item_id"]: r for r in read(fb)}
    ids = sorted(set(A) & set(B))
    out = {"items_in_both": len(ids), "only_in_a": len(set(A) - set(B)), "only_in_b": len(set(B) - set(A)), "fields": {}, "differ": []}
    for f in LABEL_FIELDS:
        xs = [A[i][f] if f != "acts_performed" else ";".join(sorted(acts_of(A[i]))) for i in ids]
        ys = [B[i][f] if f != "acts_performed" else ";".join(sorted(acts_of(B[i]))) for i in ids]
        out["fields"][f] = {"raw_agreement": round(sum(x == y for x, y in zip(xs, ys)) / len(ids), 3) if ids else None, "kappa": kappa(xs, ys)}
    out["differ"] = [{"item_id": i, "fields": [f for f in LABEL_FIELDS if (acts_of(A[i]) != acts_of(B[i]) if f == "acts_performed" else A[i][f] != B[i][f])]} for i in ids if any(
        (acts_of(A[i]) != acts_of(B[i]) if f == "acts_performed" else A[i][f] != B[i][f]) for f in LABEL_FIELDS)]
    return out


def merge(fa: str, fb: str, fj: str, out: str) -> dict:
    A, B, J = ({r["item_id"]: r for r in read(f)} for f in (fa, fb, fj))
    final, ambiguous, unresolved = [], [], []
    for i in sorted(set(A) & set(B)):
        same = all((acts_of(A[i]) == acts_of(B[i]) if f == "acts_performed" else A[i][f] == B[i][f]) for f in LABEL_FIELDS)
        row = A[i] if same else J.get(i)
        if row is None:
            unresolved.append(i)
        elif "yes" in (A[i]["ambiguous"], B[i]["ambiguous"], row["ambiguous"]):
            ambiguous.append(i)
        else:
            final.append({**row, "labelled_by": "both annotators agreed" if same else "adjudicated"})
    with open(out, "w", encoding="utf-8", newline="") as fh:
        w = csv.DictWriter(fh, fieldnames=COLUMNS + ["labelled_by"])
        w.writeheader()
        w.writerows({k: r.get(k, "") for k in COLUMNS + ["labelled_by"]} for r in final)
    return {"scored_items": len(final), "ambiguous_excluded": ambiguous, "awaiting_adjudication": unresolved}


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    t = sub.add_parser("template")
    t.add_argument("--out", default=".")
    c = sub.add_parser("check")
    c.add_argument("--file", required=True)
    g = sub.add_parser("agree")
    g.add_argument("--a", required=True)
    g.add_argument("--b", required=True)
    m = sub.add_parser("merge")
    for k in ("a", "b", "adjudicated", "out"):
        m.add_argument("--" + k, required=True)
    a = ap.parse_args(argv)
    if a.cmd == "template":
        p = Path(a.out) / "annotation_template.csv"
        with open(p, "w", encoding="utf-8", newline="") as fh:
            csv.writer(fh).writerow(COLUMNS)
        print("wrote", p)
    elif a.cmd == "check":
        bad = check(a.file)
        print("\n".join(bad) if bad else "OK: the file passes every check")
        return 1 if bad else 0
    elif a.cmd == "agree":
        print(json.dumps(agree(a.a, a.b), indent=1))
    else:
        print(json.dumps(merge(a.a, a.b, a.adjudicated, a.out), indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
