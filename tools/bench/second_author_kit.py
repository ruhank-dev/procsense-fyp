"""Everything a second author needs, and the check of what they hand back (EX-18; ADR-0020, ADR-0021): `uv run --project tools tools/bench/second_author_kit.py sheet|check|import ...`.

  sheet   [--out DIR]            writes DIR/INSTRUCTIONS.md, DIR/orders_template.csv and DIR/classifier_template.csv. The instructions list what can be ordered (made from the content files, no example sentence) and describe the nine acts and the five critical
                                 topics in plain words. The author sees NO sentence of any existing set.
  check   --orders F --classifier G   validates the two CSV files the author returns: columns, allowed targets and acts, counts, empty or duplicate sentences, and any sentence (after lower-casing and removing punctuation) that is already in a set of the repository (it would leak a development sentence into a frozen set). Writes nothing.
  import  --orders F --classifier G --author "Name and role"   runs check, then writes tests/orders/paraphrases_v5.json and tests/classifier_challenge_v7/challenge.json in the shapes the scorers read, with `independent: true` ONLY IF --author is given and the
                                 builder did not write the file (the tool records what it is told and cannot verify it; the owner confirms in docs/testing/results/EX-18.md), and adds their SHA-256 to the manifests. It never scores anything: the one-shot scoring is a separate, deliberate step.

Nothing here invents a sentence or a label: every row is the author's. The orders file has columns `text,kind,target` (kind test, medicine, home_medicine, place or staff; empty kind and target for a sentence that is not an order); the classifier file has columns `text,act,expect_credit`
(act: one of the ordinary acts of content/assessment/taxonomy.json, one of the five critical acts, or NONE; expect_credit: yes only for an ordinary act performed now by the speaker, otherwise no).
"""
from __future__ import annotations

import argparse
import csv
import hashlib
import json
import re
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
KINDS = {"test", "medicine", "home_medicine", "place", "staff"}
MIN_ORDERS, MIN_NONORDERS, MIN_CLASSIFIER_PER_BUCKET = 100, 20, 60


def norm(t: str) -> str:
    return re.sub(r"[^a-z0-9 ]+", "", t.lower()).strip()


def existing_sentences() -> set[str]:
    out: set[str] = set()
    for p in (ROOT / "tests" / "orders").glob("paraphrases*.json"):
        out |= {norm(r["text"]) for r in json.loads(p.read_text(encoding="utf-8"))["rows"]}
    for p in list((ROOT / "tests").glob("classifier_*/*.json")) + list((ROOT / "tests" / "classifier_synthetic_v2").glob("*.json")):
        try:
            d = json.loads(p.read_text(encoding="utf-8"))
        except ValueError:
            continue
        rows = d.get("rows") if isinstance(d, dict) else None
        if isinstance(rows, list):
            out |= {norm(r["text"]) for r in rows if isinstance(r, dict) and "text" in r}
    for p in list((ROOT / "tests" / "classifier_synthetic_v2").glob("*.jsonl")) + list((ROOT / "tests" / "classifier_synthetic_v3").glob("*.jsonl")) + list((ROOT / "tests" / "action_bench_v1").glob("*.jsonl")):
        out |= {norm(json.loads(l)["text"]) for l in p.read_text(encoding="utf-8").splitlines() if l.strip()}
    return out


def targets() -> dict[str, list[tuple[str, str]]]:
    from server.orders.grammar import vocabulary
    seen: dict[str, dict[str, str]] = {k: {} for k in KINDS}
    for e in vocabulary():
        seen[e.kind].setdefault(e.target, e.label)
    return {k: sorted(v.items()) for k, v in seen.items()}


def acts() -> tuple[list[dict], list[dict]]:
    tax = json.loads((ROOT / "content" / "assessment" / "taxonomy.json").read_text(encoding="utf-8"))["acts"]
    return [a for a in tax if not a["critical"] and a["id"] != "NONE"], [a for a in tax if a["critical"]]


def sheet(out: Path) -> int:
    out.mkdir(parents=True, exist_ok=True)
    t = targets()
    ordinary, critical = acts()
    L = ["# Instructions for the second author", "",
         "You are helping test a training simulator for hospital staff. Please write sentences in your OWN words, in the way a trainee might actually say them aloud to a patient or to a colleague. You do not need medical training for part A. Do not look at, copy from or ask for example sentences from the project: "
         "the point is that your wording is yours. About 90 minutes in all. Your name and role will be recorded; nothing you write is used to judge you.", "",
         "## Part A: orders (file orders_template.csv, at least 100 sentences)", "",
         f"Write at least {MIN_ORDERS} different sentences that each ask for ONE of the things below, WITHOUT using the exact name where you can (say what it is for, what it shows, or describe it), plus at least {MIN_NONORDERS} sentences that are NOT orders but mention a test, a medicine, a place or a person (small talk, a remark, a question about someone). "
         "For each order write the kind and the target id exactly as listed; leave kind and target empty for a sentence that is not an order. Use every target at least once if you can.", ""]
    for kind in ("test", "medicine", "home_medicine", "place", "staff"):
        L += [f"* **{kind}**: " + "; ".join(f"`{tid}` ({label})" for tid, label in t[kind])]
    L += ["", "## Part B: what a trainee says (file classifier_template.csv, at least 60 sentences in each of the three groups)", "",
          "Columns: `text,act,expect_credit`. Write the sentence, choose the act it performs, and put `yes` in expect_credit only when the speaker is doing that act now, in that sentence (not denying it, not imagining it, not planning it for later, not saying someone else did or will, not breaking off).", "",
          "**Group 1, ordinary acts, said now (expect_credit yes):** at least 60 sentences across these acts:", ""]
    L += [f"* `{a['id']}`: {a['definition']}" for a in ordinary]
    L += ["", "**Group 2, the same acts but NOT performed now (expect_credit no):** at least 60 sentences where the speaker denies, imagines, plans for later, reports someone else doing it, says someone else will do it, or breaks off. Use the act the sentence is about.", "",
          "**Group 3, five sensitive topics worded WITHOUT the obvious words (expect_credit no, act = the topic):** at least 60 sentences. Write each so that it does not contain the plain word (for example, ask about an allergy without the words allergy or allergic). The topics:", ""]
    L += [f"* `{a['id']}`: {a['definition']}" for a in critical]
    L += ["", "Return both CSV files to the person who arranged this. Thank you.", ""]
    (out / "INSTRUCTIONS.md").write_text("\n".join(L), encoding="utf-8", newline="\n")
    (out / "orders_template.csv").write_text("text,kind,target\n", encoding="utf-8", newline="\n")
    (out / "classifier_template.csv").write_text("text,act,expect_credit\n", encoding="utf-8", newline="\n")
    print(f"wrote {out}/INSTRUCTIONS.md, orders_template.csv and classifier_template.csv")
    return 0


def read(path: str, cols: list[str]) -> list[dict]:
    with open(path, encoding="utf-8-sig", newline="") as f:
        r = csv.DictReader(f)
        if [c.strip() for c in (r.fieldnames or [])] != cols:
            raise SystemExit(f"{path}: the columns must be exactly {','.join(cols)}")
        return [{k.strip(): (v or "").strip() for k, v in row.items()} for row in r]


def check(orders_f: str, clf_f: str) -> tuple[list[dict], list[dict], list[str]]:
    problems: list[str] = []
    orders, clf = read(orders_f, ["text", "kind", "target"]), read(clf_f, ["text", "act", "expect_credit"])
    t = targets()
    known = existing_sentences()
    ordinary, critical = acts()
    ordinary_ids, critical_ids = {a["id"] for a in ordinary}, {a["id"] for a in critical}
    seen: set[str] = set()
    for i, r in enumerate(orders, 2):
        if not r["text"]:
            problems.append(f"orders row {i}: empty sentence")
        if bool(r["kind"]) != bool(r["target"]):
            problems.append(f"orders row {i}: kind and target must both be given or both be empty")
        elif r["kind"] and (r["kind"] not in KINDS or r["target"] not in dict(t[r["kind"]])):
            problems.append(f"orders row {i}: {r['kind']}/{r['target']} is not something that can be ordered")
        n = norm(r["text"])
        if n in seen:
            problems.append(f"orders row {i}: a duplicate of an earlier row")
        if n in known:
            problems.append(f"orders row {i}: this sentence is already in a set of the repository: write your own")
        seen.add(n)
    for i, r in enumerate(clf, 2):
        if not r["text"]:
            problems.append(f"classifier row {i}: empty sentence")
        if r["act"] not in ordinary_ids | critical_ids | {"NONE"}:
            problems.append(f"classifier row {i}: {r['act']} is not an act")
        if r["expect_credit"].lower() not in ("yes", "no"):
            problems.append(f"classifier row {i}: expect_credit must be yes or no")
        if r["expect_credit"].lower() == "yes" and r["act"] not in ordinary_ids:
            problems.append(f"classifier row {i}: only an ordinary act can be credited")
        n = norm(r["text"])
        if n in seen:
            problems.append(f"classifier row {i}: a duplicate of an earlier row")
        if n in known:
            problems.append(f"classifier row {i}: this sentence is already in a set of the repository: write your own")
        seen.add(n)
    n_orders = sum(1 for r in orders if r["kind"])
    n_non = sum(1 for r in orders if not r["kind"])
    g1 = sum(1 for r in clf if r["expect_credit"].lower() == "yes")
    g2 = sum(1 for r in clf if r["expect_credit"].lower() == "no" and r["act"] in ordinary_ids)
    g3 = sum(1 for r in clf if r["act"] in critical_ids)
    for label, have, need in (("orders", n_orders, MIN_ORDERS), ("sentences that are not orders", n_non, MIN_NONORDERS), ("group 1 (ordinary, now)", g1, MIN_CLASSIFIER_PER_BUCKET), ("group 2 (not now)", g2, MIN_CLASSIFIER_PER_BUCKET), ("group 3 (sensitive topics)", g3, MIN_CLASSIFIER_PER_BUCKET)):
        if have < need:
            problems.append(f"too few {label}: {have}, at least {need} are needed")
    return orders, clf, problems


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    s = sub.add_parser("sheet")
    s.add_argument("--out", default="artifacts/second-author")
    for name in ("check", "import"):
        c = sub.add_parser(name)
        c.add_argument("--orders", required=True)
        c.add_argument("--classifier", required=True)
        if name == "import":
            c.add_argument("--author", required=True, help="the author's name and role, as they gave it")
    a = ap.parse_args(argv)
    if a.cmd == "sheet":
        return sheet(ROOT / a.out)
    orders, clf, problems = check(a.orders, a.classifier)
    if problems:
        print("\n".join(problems[:60]) + (f"\n... {len(problems) - 60} more" if len(problems) > 60 else ""))
        return 1
    print(f"ok: {sum(1 for r in orders if r['kind'])} orders, {sum(1 for r in orders if not r['kind'])} non-orders, {len(clf)} classifier sentences")
    if a.cmd == "check":
        return 0
    note = f"Written by {a.author} (recorded as told; the owner confirms the author did not work on the mapper or the classifier in docs/testing/results/EX-18.md). Unscored until the one-shot commands are run."
    o_rows = [{"id": f"OS{i + 1:03d}", "text": r["text"], "kind": r["kind"] or None, "target": r["target"] or None,
               "intent": ({"test": "ORDER_TEST", "medicine": "REQUEST_MEDICATION", "home_medicine": "HOLD_MED", "place": "REQUEST_TRANSPORT", "staff": "CALL_STAFF"}[r["kind"]] if r["kind"] else None)} for i, r in enumerate(orders)]
    ordinary, critical = acts()
    crit_ids = {x["id"] for x in critical}
    c_rows = []
    for i, r in enumerate(clf):
        yes = r["expect_credit"].lower() == "yes"
        bucket = "critical_evasive" if r["act"] in crit_ids else ("ordinary_hard" if yes else "non_assertive")
        c_rows.append({"id": f"C7-{i + 1:03d}", "bucket": bucket, "act": r["act"] if bucket != "non_assertive" else "NONE", "text": r["text"], "expect_credit": yes})
    for path, doc in ((ROOT / "tests" / "orders" / "paraphrases_v5.json", {"synthetic": False, "independent": True, "note": note, "rows": o_rows}),
                      (ROOT / "tests" / "classifier_challenge_v7" / "challenge.json", {"synthetic": False, "independent": True, "note": note, "rows": c_rows})):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(doc, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    m = ROOT / "tests" / "orders" / "manifest.json"
    man = json.loads(m.read_text(encoding="utf-8"))
    man["files"]["paraphrases_v5.json"] = hashlib.sha256((ROOT / "tests" / "orders" / "paraphrases_v5.json").read_bytes()).hexdigest()
    m.write_text(json.dumps(man, indent=1) + "\n", encoding="utf-8", newline="\n")
    (ROOT / "tests" / "classifier_challenge_v7" / "manifest.json").write_text(json.dumps({"files": {"challenge.json": hashlib.sha256((ROOT / "tests" / "classifier_challenge_v7" / "challenge.json").read_bytes()).hexdigest()}}, indent=1) + "\n", encoding="utf-8", newline="\n")
    print("wrote tests/orders/paraphrases_v5.json and tests/classifier_challenge_v7/challenge.json (not scored)")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
