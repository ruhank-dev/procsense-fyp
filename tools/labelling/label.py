"""Labelling toolkit for the human-labelled sets (p1-ai-infrastructure-s04): sheets, agreement, adjudication and frozen splits.

    uv run --project tools tools/labelling/label.py sheet --kind utterance --items items.jsonl --out sheet.csv
    uv run --project tools tools/labelling/label.py agreement --a labeller_a.csv --b labeller_b.csv --kind utterance [--out disagreements.csv]
    uv run --project tools tools/labelling/label.py adjudicate --a a.csv --b b.csv --adjudication third.csv --kind utterance --out final.csv
    uv run --project tools tools/labelling/label.py freeze --final final.csv --items items.jsonl --kind utterance --out-dir tests/labelled/utterances --kappa 0.71 [--min-rows 300] [--min-kappa 0.6]

What it covers (the plan's kinds): utterance (a communication act of content/assessment/taxonomy.json), topic (a history topic), scenario (a quality rating 1 to 5, scored with quadratic-weighted kappa), relation (a
knowledge-base relation judged correct, wrong or unclear) and persona (consistent, inconsistent, unclear). Two labellers fill separate sheets without seeing each other's; a third person settles every disagreement
(never one of the two); a set can be frozen only with at least the planned number of rows, kappa of at least 0.6 and split by group so nobody's items sit in two splits. The tool writes nothing it was not given: it
labels nothing itself and invents no labeller. Recordings of role-plays follow docs/research/labelling/consent_and_storage.md.
"""
from __future__ import annotations

import argparse
import csv
import hashlib
import json
import random
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "bench"))
import dataset as DS  # noqa: E402  (kappa)

KINDS = {"utterance": None, "topic": None, "scenario": ["1", "2", "3", "4", "5"], "relation": ["correct", "wrong", "unclear"], "persona": ["consistent", "inconsistent", "unclear"]}
MIN_ROWS = {"utterance": 300, "topic": 100, "scenario": 100, "relation": 100, "persona": 100}


def labels_for(kind: str, topics: list[str] | None = None) -> list[str]:
    if KINDS[kind] is not None:
        return KINDS[kind]
    if kind == "utterance":
        return [a["id"] for a in DS.taxonomy()]
    return topics or []


def read_csv(path: Path) -> list[dict]:
    with path.open(encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f))


def write_csv(path: Path, rows: list[dict], fields: list[str]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as f:
        w = csv.DictWriter(f, fieldnames=fields, lineterminator="\n")
        w.writeheader()
        w.writerows(rows)


def weighted_kappa(a: list[str], b: list[str], levels: list[str]) -> float | None:
    """Quadratic-weighted kappa for ordered ratings."""
    k, n = len(levels), len(a)
    if n == 0:
        return None
    idx = {l: i for i, l in enumerate(levels)}
    w = [[((i - j) / (k - 1)) ** 2 for j in range(k)] for i in range(k)]
    obs = [[0.0] * k for _ in range(k)]
    for x, y in zip(a, b):
        obs[idx[x]][idx[y]] += 1 / n
    ra = [sum(row) for row in obs]
    cb = [sum(obs[i][j] for i in range(k)) for j in range(k)]
    num = sum(w[i][j] * obs[i][j] for i in range(k) for j in range(k))
    den = sum(w[i][j] * ra[i] * cb[j] for i in range(k) for j in range(k))
    return None if den == 0 else round(1 - num / den, 3)


def agreement(a_rows: list[dict], b_rows: list[dict], kind: str, labels: list[str]) -> dict:
    a, b = {r["id"]: r for r in a_rows}, {r["id"]: r for r in b_rows}
    common = sorted(set(a) & set(b))
    problems = []
    if set(a) != set(b):
        problems.append(f"the sheets hold different items: {len(set(a) ^ set(b))} are on one sheet only")
    for name, rows in (("a", a), ("b", b)):
        bad = sorted(i for i in common if rows[i]["label"] not in labels)
        if bad:
            problems.append(f"sheet {name}: {len(bad)} labels are not in the label set (first: {bad[0]})")
    names = {a_rows[0].get("labeller", "") if a_rows else "", b_rows[0].get("labeller", "") if b_rows else ""}
    if len(names) < 2 or "" in names:
        problems.append("each sheet needs the labeller's name, and the two must be different people")
    la, lb = [a[i]["label"] for i in common], [b[i]["label"] for i in common]
    kappa = weighted_kappa(la, lb, labels) if kind == "scenario" else DS.kappa(la, lb)
    return {"rows": len(common), "agreement": round(sum(x == y for x, y in zip(la, lb)) / len(common), 3) if common else None, "kappa": kappa, "weighted": kind == "scenario",
            "disagreements": [i for i in common if a[i]["label"] != b[i]["label"]], "problems": problems}


def adjudicate(a_rows, b_rows, third_rows, labels) -> tuple[list[dict], list[str]]:
    """Final labels: the agreed label, or the third person's. Returns (rows, problems)."""
    a, b, t = ({r["id"]: r for r in rows} for rows in (a_rows, b_rows, third_rows))
    la = a_rows[0].get("labeller", "").strip().lower() if a_rows else ""
    lb = b_rows[0].get("labeller", "").strip().lower() if b_rows else ""
    out, problems = [], []
    for i in sorted(a):
        if i not in b:
            problems.append(f"{i}: missing from sheet b")
            continue
        if a[i]["label"] == b[i]["label"]:
            out.append({"id": i, "label": a[i]["label"], "adjudicated": "false", "adjudicator": ""})
            continue
        r = t.get(i)
        if r is None or r["label"] not in labels:
            problems.append(f"{i}: the labellers disagree and nobody adjudicated with a valid label")
        elif r.get("adjudicator", "").strip().lower() in ("", la, lb):
            problems.append(f"{i}: the adjudicator must be a third person")
        else:
            out.append({"id": i, "label": r["label"], "adjudicated": "true", "adjudicator": r["adjudicator"]})
    return out, problems


def freeze(final: list[dict], items: list[dict], kind: str, out_dir: Path, kappa: float | None, min_rows: int, min_kappa: float, seed: str = "labelled-v1") -> dict:
    """Frozen train/dev/test files split by group (the item's `group`, e.g. the speaker or author), written with their hashes. Refuses a set that is too small or labelled without enough agreement."""
    if len(final) < min_rows:
        raise SystemExit(f"NOT FROZEN: {len(final)} rows, the plan needs at least {min_rows}")
    if kappa is None or kappa < min_kappa:
        raise SystemExit(f"NOT FROZEN: kappa {kappa} is below {min_kappa}")
    by = {i["id"]: i for i in items}
    rows = [{**by[f["id"]], "label": f["label"], "adjudicated": f["adjudicated"] == "true"} for f in final]
    groups = sorted({r.get("group", r["id"]) for r in rows})
    random.Random(f"{seed}:{kind}").shuffle(groups)
    cut = {g: ("test" if k < max(1, len(groups) // 5) else "dev" if k < max(2, len(groups) * 2 // 5) else "train") for k, g in enumerate(groups)}
    manifest = {"kind": kind, "rows": {}, "sha256": {}, "kappa": kappa, "min_kappa": min_kappa, "note": "Frozen: a result is valid only for these hashes. Split by group; no group is in two splits."}
    out_dir.mkdir(parents=True, exist_ok=True)
    for split in ("train", "dev", "test"):
        part = [r for r in rows if cut[r.get("group", r["id"])] == split]
        text = "".join(json.dumps(r, ensure_ascii=False, sort_keys=True) + "\n" for r in part)
        (out_dir / f"{split}.jsonl").write_text(text, encoding="utf-8", newline="\n")
        manifest["rows"][split] = len(part)
        manifest["sha256"][split] = hashlib.sha256(text.encode("utf-8")).hexdigest()
    (out_dir / "manifest.json").write_text(json.dumps(manifest, indent=1) + "\n", encoding="utf-8", newline="\n")
    return manifest


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd", choices=["sheet", "agreement", "adjudicate", "freeze"])
    ap.add_argument("--kind", choices=sorted(KINDS), default="utterance")
    ap.add_argument("--items", type=Path)
    ap.add_argument("--out", type=Path)
    ap.add_argument("--a", type=Path)
    ap.add_argument("--b", type=Path)
    ap.add_argument("--adjudication", type=Path)
    ap.add_argument("--final", type=Path)
    ap.add_argument("--out-dir", type=Path)
    ap.add_argument("--min-rows", type=int)
    ap.add_argument("--min-kappa", type=float, default=0.6)
    ap.add_argument("--kappa", type=float, help="the kappa printed by the agreement command for this set")
    a = ap.parse_args(argv)
    labels = labels_for(a.kind)
    if a.cmd == "sheet":
        items = [json.loads(l) for l in a.items.read_text(encoding="utf-8").splitlines() if l.strip()]
        write_csv(a.out, [{"id": i["id"], "text": i["text"], "label": "", "labeller": ""} for i in items], ["id", "text", "label", "labeller"])
        print(f"wrote {a.out} ({len(items)} items); the label set is {labels or 'free text'}")
        return 0
    if a.cmd == "agreement":
        r = agreement(read_csv(a.a), read_csv(a.b), a.kind, labels)
        print(json.dumps({k: v for k, v in r.items() if k != "disagreements"} | {"disagreements": len(r["disagreements"])}, indent=1))
        if a.out:
            write_csv(a.out, [{"id": i, "label": "", "adjudicator": ""} for i in r["disagreements"]], ["id", "label", "adjudicator"])
        return 1 if r["problems"] else 0
    if a.cmd == "adjudicate":
        rows, problems = adjudicate(read_csv(a.a), read_csv(a.b), read_csv(a.adjudication), labels)
        for p in problems[:30]:
            print("PROBLEM", p)
        if problems:
            return 1
        write_csv(a.out, rows, ["id", "label", "adjudicated", "adjudicator"])
        print(f"wrote {a.out} ({len(rows)} final labels)")
        return 0
    final = read_csv(a.final)
    items = [json.loads(l) for l in a.items.read_text(encoding="utf-8").splitlines() if l.strip()]
    print(json.dumps(freeze(final, items, a.kind, a.out_dir, a.kappa, a.min_rows or MIN_ROWS[a.kind], a.min_kappa)))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
