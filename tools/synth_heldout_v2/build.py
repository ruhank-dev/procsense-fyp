"""Validates and assembles the FINAL FRESH HELD-OUT set v2 (HO2) (final acceptance of the speech evaluator, ADR-0025): `uv run --project tools tools/synth_heldout_v2/build.py [--write] [--check]`.

Deterministic checks only (no model): every specification has exactly one utterance and nothing else is present; the specifications still equal what the plan produces and match their manifest hash (they were frozen BEFORE the sentences were written); no sentence is a
sentence, or a near copy (word-set Jaccard 0.7 or more), of ANY sentence in tests/synthetic_dev_v1 or any other committed test set; no duplicates inside the set; opener and length spread; no fake ASR spelling. --write assembles tests/synthetic_heldout_v1/dataset.jsonl and the
data manifest (SHA-256 of every file) that the evaluation run refuses to start without. AI-written and AI-reviewed (the same assistant): not independent human validation."""
from __future__ import annotations

import collections
import csv
import hashlib
import json
import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
OUT = ROOT / "tests" / "synthetic_heldout_v2"
sys.path.insert(0, str(HERE))
import importlib.util  # noqa: E402

_spec = importlib.util.spec_from_file_location('heldout_v2_plan', HERE / 'plan.py')
HP = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(HP)


def norm(t: str) -> str:
    return re.sub(r"[^a-z0-9 ]+", " ", t.lower()).split().__str__() if False else " ".join(re.sub(r"[^a-z0-9 ]+", " ", t.lower()).split())


def words(t: str) -> set:
    return set(norm(t).split())


def historical_sentences() -> list[tuple[str, str]]:
    out = []
    tests = ROOT / "tests"
    for p in tests.rglob("*"):
        if not p.is_file() or OUT in p.parents or p.suffix not in (".json", ".jsonl", ".csv", ".txt", ".md"):
            continue
        try:
            text = p.read_text(encoding="utf-8")
        except Exception:
            continue
        rel = p.relative_to(ROOT).as_posix()
        if p.suffix in (".json", ".jsonl"):
            for s in re.findall(r'"(?:utterance|text|sentence)"\s*:\s*"((?:[^"\\]|\\.)*)"', text):
                out.append((rel, json.loads('"' + s + '"')))
        elif p.suffix == ".csv":
            try:
                for row in csv.DictReader(text.splitlines()):
                    for k in ("utterance", "text", "sentence"):
                        if row.get(k):
                            out.append((rel, row[k]))
            except Exception:
                pass
        else:
            out += [(rel, l.strip()) for l in text.splitlines() if 3 <= len(l.split()) <= 40]
    return out


def problems(items: list[dict], utt: dict) -> tuple[list[str], dict]:
    probs = []
    ids = [i["id"] for i in items]
    if sorted(utt) != sorted(ids):
        probs.append("utterances and specifications do not cover the same ids: %s" % sorted(set(utt) ^ set(ids))[:10])
    seen = collections.defaultdict(list)
    for i, t in utt.items():
        if not isinstance(t, str) or not t.strip():
            probs.append(f"{i}: empty utterance")
            continue
        seen[norm(t)].append(i)
    probs += [f"duplicate inside the set: {v}" for v in seen.values() if len(v) > 1]
    hist = historical_sentences()
    exact = {norm(s): r for r, s in hist}
    stats = {"historical_sentences_compared": len(hist), "near_copies": 0, "exact_copies": 0}
    hw = [(r, s, words(s)) for r, s in hist if len(words(s)) >= 4]
    for i, t in utt.items():
        n = norm(t)
        if len(n.split()) >= 3 and n in exact:
            probs.append(f"{i}: exact copy of a sentence in {exact[n]}")
            stats["exact_copies"] += 1
            continue
        w = words(t)
        if len(w) < 5:
            continue
        for r, s, sw in hw:
            if len(sw & w) / len(sw | w) >= 0.7:
                probs.append(f"{i}: near copy (Jaccard >= 0.7) of {r}: {s[:70]!r}")
                stats["near_copies"] += 1
                break
    # within the set: members of a contrast set may share core words (that is the design); other pairs may not be near copies
    by_set = {i["id"]: i["set_id"] for i in items}
    ids_sorted = sorted(utt)
    for a in range(len(ids_sorted)):
        for b in range(a + 1, len(ids_sorted)):
            x, y = ids_sorted[a], ids_sorted[b]
            if by_set[x] and by_set[x] == by_set[y]:
                continue
            wx, wy = words(utt[x]), words(utt[y])
            if len(wx) >= 5 and len(wy) >= 5 and len(wx & wy) / len(wx | wy) >= 0.7:
                probs.append(f"near copy inside the set: {x} and {y}")
    free = {i["id"] for i in items if not (i["set_id"] and i["set_role"] != "performed")}      # the marked members of a contrast set carry their marker ("I haven't", "I'll", "if I") by design
    opener = collections.Counter(norm(utt[i]).split()[0] for i in free if norm(utt[i]))
    top, n = opener.most_common(1)[0]
    stats["most_common_opener_outside_marked_contrast_members"] = [top, n, round(n / len(free), 3)]
    if n / len(free) > 0.12:
        probs.append(f"opener {top!r} starts {n} of {len(free)} unmarked sentences ({n / len(free):.0%}; cap 12%)")
    lens = sorted(len(t.split()) for t in utt.values())
    stats["words_per_sentence"] = {"min": lens[0], "median": lens[len(lens) // 2], "max": lens[-1]}
    for i, t in utt.items():
        if re.search(r"\b(teh|recieve|adn|wiht)\b", t.lower()):
            probs.append(f"{i}: fake spelling error")
    return probs, stats


def main(argv):
    items = HP.build()
    manifest = json.loads((OUT / "manifest.json").read_text(encoding="utf-8"))
    if hashlib.sha256((OUT / "specs.json").read_bytes()).hexdigest() != manifest["specs_sha256"] or json.loads((OUT / "specs.json").read_text(encoding="utf-8")) != items:
        print("FAIL the specifications changed after they were frozen")
        return 1
    utt = json.loads((OUT / "utterances.json").read_text(encoding="utf-8"))
    probs, stats = problems(items, utt)
    for p in probs:
        print("FAIL", p)
    print(json.dumps(stats))
    if probs:
        return 1
    rows = []
    for it in items:
        rows.append({"id": it["id"], "utterance": utt[it["id"]], "prior_turn": it["prior_turn"], "prior_speaker": it["prior_speaker"], "spec": {k: v for k, v in it.items() if k != "gold"}, "gold": it["gold"],
                     "provenance": {"generation": "AI-assisted (an AI assistant), no cloud call", "review": "AI-assisted (same assistant), not independent human validation", "kind": "synthetic FINAL FRESH HELD-OUT (v2) acceptance data"}})
    data = "\n".join(json.dumps(r, ensure_ascii=False) for r in rows) + "\n"
    if "--write" in argv:
        (OUT / "dataset.jsonl").write_text(data, encoding="utf-8", newline="\n")
        h = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
        manifest.update({"utterances_sha256": h(OUT / "utterances.json"), "dataset_sha256": h(OUT / "dataset.jsonl"), "validation": stats, "frozen_before_scoring": True,
                         "note": "The sentences were written after the specifications were frozen and reviewed once by the author before any evaluator run (rewrites: 3 sentences that did not carry both acts of a multi-intent specification clearly). Nothing was changed after seeing a result."})
        (OUT / "manifest.json").write_text(json.dumps(manifest, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
        print("wrote dataset.jsonl", len(rows))
    elif "--check" in argv:
        ok = (OUT / "dataset.jsonl").read_text(encoding="utf-8") == data and manifest.get("dataset_sha256") == hashlib.sha256((OUT / "dataset.jsonl").read_bytes()).hexdigest()
        print("OK dataset matches" if ok else "FAIL dataset differs from the specifications and utterances")
        return 0 if ok else 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
