"""Kit for the blind human review of the unsafe-speech sheet (EX-01 part B, p1-ai-language-s17): `tools/.venv/Scripts/python.exe tools/bench/safety_review_kit.py package|check|agree|merge ...`. Protocol: docs/testing/safety_challenge_review.md.

  package --out DIR                    one blind file per reviewer (reviewer_a.csv, reviewer_b.csv) from tests/safety_challenge/review_sheet.csv, each in its own order, and PACKAGE_MANIFEST.json (sha256 of the sheet, counts). A reviewer file holds ONLY:
                                       id, sentence, situation, act_in_question, label, note. It never holds an expected label, a template, a category, a seed family, a split, a prediction or a model decision; `package` refuses to write a file whose header or content shows one.
  check --file F --who a|b             validates a filled file: every id of the sheet once, labels from {credit_allowed, no_credit, unclear}, a note for every `unclear`. Prints what is missing; changes nothing.
  agree --a A --b B [--out DIR]        raw agreement and Cohen's kappa, the rows on which they differ and the rows either marked unclear; writes DIR/adjudication.csv (id, sentence, situation, act_in_question, label_a, label_b, adjudicated_label, note) for the adjudicator, who sees
                                       the two reviewers' labels (not the key, not a system output).
  merge --a A --b B --adj ADJ --out F  final labels: agreement stands; a difference takes the adjudicated label; a row any of the three marks unclear is EXCLUDED from pass or fail counts. Only here is the construction label (tests/safety_challenge/*.jsonl) read: the output is an
                                       OVERRIDE FILE (the generator is never edited) with the label-error rate of the by-construction labels by split and by template. Refuses if a difference has no adjudication, if a reviewer file fails `check`, or if a reviewer is the same file as the other.
It writes nothing into the repository by itself (give --out/--out DIR outside tests/ unless the owner records the review in docs/testing/results/). It invents no result: with no filled files it only packages and checks. Standard library only."""
from __future__ import annotations

import argparse
import csv
import hashlib
import json
import random
import sys
from collections import Counter, defaultdict
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
ROOT = Path(__file__).resolve().parents[2]
SETS = ROOT / "tests" / "safety_challenge"
SHEET = SETS / "review_sheet.csv"
LABELS = ("credit_allowed", "no_credit", "unclear")
BLIND_COLUMNS = ["id", "sentence", "situation", "act_in_question", "label", "note"]
FORBIDDEN = ("expected", "template", "category", "seed", "split", "prediction", "decision", "reason", "credited", "key", "construction", "review_status", "provenance")


def read(path: Path) -> list[dict]:
    with open(path, encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f))


def write(path: Path, header: list[str], rows: list[list]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="") as f:
        w = csv.writer(f, lineterminator="\n")
        w.writerow(header)
        w.writerows(rows)


def assert_blind(header: list[str]) -> None:
    bad = [h for h in header if any(word in h.lower() for word in FORBIDDEN)]
    if bad or header != BLIND_COLUMNS:
        raise SystemExit(f"a reviewer file may hold only {BLIND_COLUMNS}; refused: {header} ({bad})")


def package(out: Path) -> int:
    sheet = read(SHEET)
    manifest = {"sheet": "tests/safety_challenge/review_sheet.csv", "sheet_sha256": hashlib.sha256(SHEET.read_bytes()).hexdigest(), "rows": len(sheet), "files": {}, "note": "blind: no expected label, template, category, seed, split or system output in any reviewer file"}
    for who, seed in (("a", 11), ("b", 29)):
        order = list(sheet)
        random.Random(seed).shuffle(order)                               # each reviewer sees the rows in their own order
        rows = [[r["id"], r["sentence"], r["situation"], r["act_in_question"], "", ""] for r in order]
        assert_blind(BLIND_COLUMNS)
        path = out / f"reviewer_{who}.csv"
        write(path, BLIND_COLUMNS, rows)
        manifest["files"][path.name] = hashlib.sha256(path.read_bytes()).hexdigest()
    (out / "PACKAGE_MANIFEST.json").write_text(json.dumps(manifest, indent=1) + "\n", encoding="utf-8", newline="\n")
    print(f"wrote reviewer_a.csv, reviewer_b.csv and PACKAGE_MANIFEST.json to {out} ({len(sheet)} rows each)")
    return 0


def check_rows(rows: list[dict], sheet_ids: set[str]) -> list[str]:
    problems = []
    seen = Counter(r["id"] for r in rows)
    problems += [f"id {i} appears {n} times" for i, n in seen.items() if n > 1]
    problems += [f"id {i} is not in the sheet" for i in seen if i not in sheet_ids]
    problems += [f"id {i} is missing" for i in sorted(sheet_ids - set(seen))]
    for r in rows:
        if r["label"] not in LABELS:
            problems.append(f"id {r['id']}: label {r['label']!r} is not one of {LABELS}")
        if r["label"] == "unclear" and not r["note"].strip():
            problems.append(f"id {r['id']}: unclear needs a note saying why")
    return problems


def load_reviewer(path: Path) -> list[dict]:
    rows = read(path)
    if rows:
        assert_blind(list(rows[0].keys()))
    return rows


def kappa(a: list[str], b: list[str]) -> float:
    n = len(a)
    if n == 0:
        return float("nan")
    po = sum(x == y for x, y in zip(a, b)) / n
    ca, cb = Counter(a), Counter(b)
    pe = sum(ca[l] * cb[l] for l in LABELS) / (n * n)
    return 1.0 if pe == 1 else (po - pe) / (1 - pe)


def agree(a_path: Path, b_path: Path, out: Path | None) -> int:
    sheet = {r["id"]: r for r in read(SHEET)}
    a, b = load_reviewer(a_path), load_reviewer(b_path)
    for name, rows in (("A", a), ("B", b)):
        problems = check_rows(rows, set(sheet))
        if problems:
            print(f"reviewer {name} file fails check ({len(problems)} problems), first: {problems[:3]}")
            return 1
    la, lb = {r["id"]: r for r in a}, {r["id"]: r for r in b}
    ids = sorted(sheet)
    xa, xb = [la[i]["label"] for i in ids], [lb[i]["label"] for i in ids]
    diff = [i for i in ids if la[i]["label"] != lb[i]["label"]]
    unclear = [i for i in ids if "unclear" in (la[i]["label"], lb[i]["label"]) and i not in diff]
    print(json.dumps({"rows": len(ids), "raw_agreement": round(1 - len(diff) / len(ids), 4), "cohens_kappa": round(kappa(xa, xb), 4), "differ": len(diff), "agree_but_one_unclear": len(unclear),
                      "labels_a": dict(Counter(xa)), "labels_b": dict(Counter(xb))}, indent=1))
    if out is not None:
        todo = diff + unclear
        write(out / "adjudication.csv", ["id", "sentence", "situation", "act_in_question", "label_a", "label_b", "adjudicated_label", "note"],
              [[i, sheet[i]["sentence"], sheet[i]["situation"], sheet[i]["act_in_question"], la[i]["label"], lb[i]["label"], "", ""] for i in todo])
        print(f"wrote {out / 'adjudication.csv'} ({len(todo)} rows for the adjudicator)")
    return 0


def merge(a_path: Path, b_path: Path, adj_path: Path, out: Path) -> int:
    if a_path.resolve() == b_path.resolve():
        print("reviewer A and B are the same file")
        return 1
    sheet = {r["id"]: r for r in read(SHEET)}
    a, b = load_reviewer(a_path), load_reviewer(b_path)
    for name, rows in (("A", a), ("B", b)):
        problems = check_rows(rows, set(sheet))
        if problems:
            print(f"reviewer {name} file fails check: {problems[:3]}")
            return 1
    la, lb = {r["id"]: r["label"] for r in a}, {r["id"]: r["label"] for r in b}
    adj = {r["id"]: r["adjudicated_label"] for r in read(adj_path)}
    final, excluded, unresolved = {}, [], []
    for i in sorted(sheet):
        if la[i] == lb[i] and la[i] != "unclear":
            final[i] = la[i]
            continue
        verdict = adj.get(i, "")
        if verdict not in LABELS:
            unresolved.append(i)
        elif verdict == "unclear":
            excluded.append(i)
        else:
            final[i] = verdict
    if unresolved:
        print(f"{len(unresolved)} rows differ or are unclear and have no adjudicated label (first: {unresolved[:5]}); refusing to merge")
        return 1
    key = {}
    for sp in ("dev", "challenge", "final"):
        for line in (SETS / f"{sp}.jsonl").read_text(encoding="utf-8").splitlines():
            if line.strip():
                r = json.loads(line)
                key[r["id"]] = r
    by_split, by_template = defaultdict(lambda: [0, 0]), defaultdict(lambda: [0, 0])
    overrides = {}
    for i, label in final.items():
        r = key[i]
        construction = "credit_allowed" if r["expected"] == "credit_allowed" else "no_credit" if r["expected"] == "no_credit" else None
        if construction is None:
            continue                                             # ambiguous_review rows have no construction label to be wrong
        wrong = label != construction
        by_split[r["split"]][0] += wrong
        by_split[r["split"]][1] += 1
        by_template[(r["split"], r["template"])][0] += wrong
        by_template[(r["split"], r["template"])][1] += 1
        if wrong:
            overrides[i] = {"construction": construction, "reviewed": label}
    result = {"note": "OVERRIDES from independent review; the generator and the .jsonl sets are not edited. Excluded rows leave all pass or fail counts.", "excluded_unclear": excluded, "overrides": overrides,
              "label_error_rate_by_split": {k: {"wrong": v[0], "of": v[1]} for k, v in sorted(by_split.items())},
              "label_error_rate_by_template": {f"{k[0]}/{k[1]}": {"wrong": v[0], "of": v[1]} for k, v in sorted(by_template.items()) if v[0]},
              "reviewed_rows": len(final), "sheet_sha256": hashlib.sha256(SHEET.read_bytes()).hexdigest()}
    out.write_text(json.dumps(result, indent=1) + "\n", encoding="utf-8", newline="\n")
    print(f"wrote {out}: {len(final)} reviewed, {len(excluded)} excluded, {len(overrides)} construction labels overridden")
    return 0


def main(argv):
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("package"); p.add_argument("--out", required=True)
    c = sub.add_parser("check"); c.add_argument("--file", required=True); c.add_argument("--who", choices=["a", "b"], required=True)
    g = sub.add_parser("agree"); g.add_argument("--a", required=True); g.add_argument("--b", required=True); g.add_argument("--out")
    m = sub.add_parser("merge"); m.add_argument("--a", required=True); m.add_argument("--b", required=True); m.add_argument("--adj", required=True); m.add_argument("--out", required=True)
    a = ap.parse_args(argv)
    if a.cmd == "package":
        return package(Path(a.out))
    if a.cmd == "check":
        rows = load_reviewer(Path(a.file))
        problems = check_rows(rows, {r["id"] for r in read(SHEET)})
        print("OK" if not problems else f"{len(problems)} problems:\n  " + "\n  ".join(problems[:20]))
        return 1 if problems else 0
    if a.cmd == "agree":
        return agree(Path(a.a), Path(a.b), Path(a.out) if a.out else None)
    return merge(Path(a.a), Path(a.b), Path(a.adj), Path(a.out))


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
