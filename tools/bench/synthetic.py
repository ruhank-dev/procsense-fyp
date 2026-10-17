"""The SYNTHETIC diverse communication-act dataset (tests/classifier_synthetic): written and labelled by the language model that built this tooling, NOT by people.

    uv run --project tools tools/bench/synthetic.py sheets          # write the blind sheets (id and text only, no act) for the second labelling pass
    uv run --project tools tools/bench/synthetic.py freeze          # merge authored text, second labels and adjudications; write train/dev/test.jsonl and manifest.json
    uv run --project tools tools/bench/synthetic.py check           # fail if the committed files differ from what the sources give

What it is: eight simulated author profiles write sentences in different styles (terse, chatty, formal, anxious, second-language, text-message, rushed, over-polite) for the 20 acts of
content/assessment/taxonomy.json. Train profiles T1-T4 wrote 16 rows per act. Dev profiles (DA, DB) and test profiles (TC, TD) wrote the cards of docs/research/classifier_benchmark (100 dev, 200 test rows: the same
per-act and per-bucket counts as the planned independent set), so the evaluation checks whether a classifier trained on some styles holds up on styles it has not seen. Every profile is held out of every other split.
Hard cases are in every split: negation, self-correction, incomplete speech, deliberate ambiguity, NONE, and (made later by tools/bench/asr_roundtrip.py) real speech-recogniser errors.

What it is NOT: independent. The same model wrote the generator templates, the cards and these sentences; it also made the second labels (a separate pass over the blind sheet that shows the text and not the intended act,
but by the same model, so not an independent labeller) and adjudicated the disagreements itself. No person wrote, labelled or reviewed any row. Results on it are a generalisation check across writing styles and say nothing
about real trainee speech; the manifest and every result carry `independent: false`. The independent set of docs/research/classifier_benchmark still needs real authors, a second labeller and an adjudicator.
"""
from __future__ import annotations

import hashlib
import json
import random
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "bench"))
import dataset as DS  # noqa: E402

OUT = ROOT / "tests" / "classifier_synthetic"
BUCKET = {"p": "plain", "n": "negation", "c": "correction", "i": "incomplete", "a": "ambiguous"}
PERSONAS = {
    "T1": "terse registrar: clipped, imperative, abbreviations", "T2": "chatty junior nurse: warm, long, fillers", "T3": "formal consultant: complete sentences, passive voice",
    "T4": "anxious student: hedging, apologetic, questions", "DA": "second-language English: simple grammar, missing articles", "DB": "text-message style: lower case, abbreviations, no punctuation",
    "TC": "rushed night-shift clinician: fragments, trailing off", "TD": "over-polite hesitant trainee: apologies, indirect requests"}
SLOT = {"A": "DA", "B": "DB", "C": "TC", "D": "TD"}


def lines(name: str) -> list[list[str]]:
    return [l.split("|") for l in (OUT / name).read_text(encoding="utf-8").splitlines() if l.strip()]


def rows() -> dict[str, list[dict]]:
    tax = {a["id"]: a for a in DS.taxonomy()}
    out: dict[str, list[dict]] = {"train": [], "dev": [], "test": []}
    for n, (persona, act, b, text) in enumerate([l for f in ("authored_train_1.txt", "authored_train_2.txt") for l in lines(f)], 1):
        out["train"].append({"id": f"ST{n:04d}", "text": text.strip(), "intended_act": act, "author": persona, "bucket": BUCKET[b]})
    for split, name in (("dev", "authored_dev.txt"), ("test", "authored_test.txt")):
        cards = {c["card_id"]: c for c in DS.build_cards(split)}
        for cid, text in lines(name):
            c = cards[cid]
            out[split].append({"id": cid, "text": text.strip(), "intended_act": c["act"], "author": SLOT[c["author_slot"]], "bucket": c["bucket"]})
        missing = set(cards) - {r["id"] for r in out[split]}
        if missing:
            raise SystemExit(f"{split}: no text for {sorted(missing)[:5]}")
    for rs in out.values():
        for r in rs:
            if r["intended_act"] not in tax:
                raise SystemExit(f"unknown act {r['intended_act']} in {r['id']}")
    return out


def sample_ids(split: str, rs: list[dict]) -> list[str]:
    """Rows shown to the second labelling pass: every row whose intended act is critical plus a quarter of the others, in a seeded random order so the critical rows cannot be spotted."""
    crit = {a["id"] for a in DS.taxonomy() if a["critical"]}
    others = [r for r in rs if r["intended_act"] not in crit]
    chosen = {r["id"] for r in random.Random(f"synthetic-second-pass-v1:{split}").sample(others, len(others) // 4)}
    shown = [r["id"] for r in rs if r["intended_act"] in crit or r["id"] in chosen]
    random.Random(f"synthetic-order-v1:{split}").shuffle(shown)
    return shown


def pairs(name: str) -> dict[str, list[str]]:
    p = OUT / name
    return {l.split("|")[0]: l.split("|")[1:] for l in p.read_text(encoding="utf-8").splitlines() if l.strip()} if p.exists() else {}


def build() -> dict[str, str]:
    data = rows()
    files: dict[str, str] = {}
    for split in ("dev", "test"):
        by = {r["id"]: r for r in data[split]}
        files[f"blind_sheet_{split}.txt"] = "".join(f"{i}|{by[i]['text']}\n" for i in sample_ids(split, data[split]))
    second, adjud = {**pairs("second_labels_dev.txt"), **pairs("second_labels_test.txt")}, pairs("adjudication.txt")
    disagreements = []
    manifest = {"synthetic": True, "independent": False, "note": "Written, second-labelled and adjudicated by the language model that built the tooling; no person wrote, labelled or reviewed any row. A generalisation check across simulated writing styles, not an independent benchmark.",
                "personas": PERSONAS, "files": {}, "rows": {}, "authors": {}, "second_label_rows": {}, "disagreements": {}, "agreement_with_intended": {}}
    for split, rs in data.items():
        for r in rs:
            lab2 = second.get(r["id"], [""])[0]
            final = r["intended_act"]
            if lab2 and lab2 != r["intended_act"]:
                if r["id"] not in adjud:
                    raise SystemExit(f"{r['id']}: second label {lab2} differs from {r['intended_act']} and has no adjudication")
                final = adjud[r["id"]][0]
                disagreements.append(r["id"])
            r.update({"act": final, "second_label": lab2, "adjudicated": r["id"] in adjud, "critical": final in {a["id"] for a in DS.taxonomy() if a["critical"]}, "synthetic": True, "independent": False,
                      "persona": PERSONAS[r["author"]]})
        text = "".join(json.dumps(r, ensure_ascii=False, sort_keys=True) + "\n" for r in rs)
        files[f"{split}.jsonl"] = text
        manifest["files"][f"{split}.jsonl"] = hashlib.sha256(text.encode("utf-8")).hexdigest()
        manifest["rows"][split] = len(rs)
        manifest["authors"][split] = sorted({r["author"] for r in rs})
        both = [(r["intended_act"], r["second_label"]) for r in rs if r["second_label"]]
        manifest["second_label_rows"][split] = len(both)
        manifest["disagreements"][split] = sum(a != b for a, b in both)
        manifest["agreement_with_intended"][split] = {"kappa": DS.kappa([a for a, _ in both], [b for _, b in both]) if both else None}
    files["manifest.json"] = json.dumps(manifest, indent=1) + "\n"
    return files


def main(argv: list[str]) -> int:
    cmd = argv[0] if argv else "check"
    files = build()
    if cmd == "sheets":
        for n in ("blind_sheet_dev.txt", "blind_sheet_test.txt"):
            (OUT / n).write_text(files[n], encoding="utf-8", newline="\n")
        print("wrote the blind sheets")
        return 0
    if cmd == "freeze":
        if any(not (OUT / f"second_labels_{s}.txt").exists() for s in ("dev", "test")):
            raise SystemExit("run `sheets`, do the second labelling pass, and write second_labels_dev.txt and second_labels_test.txt first")
        for n, t in files.items():
            (OUT / n).write_text(t, encoding="utf-8", newline="\n")
        print(json.loads(files["manifest.json"])["disagreements"], "disagreements;", {s: len(json.loads(files["manifest.json"])["files"]) for s in ("files",)})
        return 0
    stale = [n for n, t in files.items() if not (OUT / n).exists() or (OUT / n).read_text(encoding="utf-8") != t]
    print("FAIL out of date: " + ", ".join(stale) if stale else "synthetic diverse dataset is current (model-written, model-labelled, NOT independent)")
    return 1 if stale else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
