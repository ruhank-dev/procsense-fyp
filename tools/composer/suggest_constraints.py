"""Compatibility suggestions for the block library (feature A12, capability A12.c2): `uv run --project tools tools/composer/suggest_constraints.py [--check]`.

Mines the tags of the blocks of each source case (content/blocks, one tag set per case) for pairs of tags of different block types that go together more or less often than chance, and writes them as SUGGESTIONS
(tests/composition/block_suggestions.json), never as constraints: a person reads each one and, if it is right, adds it to the block's `requires` or `excludes` through tools/composer/blocks.py. Two kinds:

  excludes  tag A and tag B never occur in the same case although independence says they should in at least MIN_EXPECTED cases (A and B each appear in at least MIN_SUPPORT cases)
  requires  every case that has tag A also has tag B, A appears in at least MIN_SUPPORT cases, and B is not in nearly every case (at most 90 percent) and A brings it at least 3 cases more often than independence predicts

Each suggestion carries its evidence (the counts and the cases) and is `status: SUGGESTED`, `accepted: null`. Pairs the library already forbids or requires are left out. No model writes notes: the capability's optional
model note is not built, and nothing here changes a block. The acceptance of the plan (at least 50 percent of suggestions accepted) needs a person reading them (EX-13). `--check` fails when the file is out of date.
"""
from __future__ import annotations

import itertools
import json
import sys
from collections import Counter, defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
BLOCKS = ROOT / "content" / "blocks"
OUT = ROOT / "tests" / "composition" / "block_suggestions.json"
MIN_SUPPORT, MIN_EXPECTED = 6, 4.0


def case_tags() -> tuple[dict[str, dict[str, set[str]]], dict[str, dict]]:
    """case -> block type -> tags, and the blocks by id."""
    per: dict[str, dict[str, set[str]]] = defaultdict(lambda: defaultdict(set))
    blocks = {}
    for f in sorted(BLOCKS.glob("*/*.json")):
        b = json.loads(f.read_text(encoding="utf-8"))
        blocks[b["id"]] = b
        for c in b["derived_from"]:
            per[c][b["type"]] |= set(b["tags"])
    return per, blocks


def suggestions() -> dict:
    per, blocks = case_tags()
    cases = sorted(per)
    n = len(cases)
    known_excl = {frozenset((t, x)) for b in blocks.values() for t in b["tags"] for x in b["excludes"]} | {frozenset((b2, x)) for b in blocks.values() for b2 in [t for t in b["excludes"]] for x in b["tags"]}
    known_req = {(t, r) for b in blocks.values() for t in b["tags"] for r in b["requires"]}
    owner: dict[str, set[str]] = defaultdict(set)                                    # tag -> block types that carry it
    has: dict[str, set[str]] = defaultdict(set)                                       # tag -> cases
    for c in cases:
        for typ, tags in per[c].items():
            for t in tags:
                owner[t].add(typ)
                has[t].add(c)
    out = []
    tags = sorted(t for t in has if len(has[t]) >= MIN_SUPPORT)
    for a, b in itertools.combinations(tags, 2):
        if owner[a] & owner[b] or frozenset((a, b)) in known_excl:
            continue                                                               # tags of one block type are alternatives by construction
        both = len(has[a] & has[b])
        expected = len(has[a]) * len(has[b]) / n
        if both == 0 and expected >= MIN_EXPECTED:
            out.append({"kind": "excludes", "a": a, "b": b, "cases_with_a": len(has[a]), "cases_with_b": len(has[b]), "together": 0, "expected_together": round(expected, 1)})
    for a, b in itertools.permutations(tags, 2):
        if owner[a] & owner[b] or (a, b) in known_req:
            continue
        if has[a] <= has[b] and len(has[a]) < len(has[b]) and len(has[b]) <= 0.9 * n and len(has[a]) - len(has[a]) * len(has[b]) / n >= 3:       # B is not nearly everywhere (then it says nothing) and A brings it more often than chance
            out.append({"kind": "requires", "a": a, "b": b, "cases_with_a": len(has[a]), "cases_with_b": len(has[b]), "together": len(has[a]), "expected_together": round(len(has[a]) * len(has[b]) / n, 1)})
    for s in out:
        s.update({"status": "SUGGESTED", "accepted": None, "note": ""})
    return {"synthetic": False, "kind": "suggestions for a person to review; nothing here is a constraint", "cases": n, "min_support": MIN_SUPPORT, "min_expected": MIN_EXPECTED, "count": len(out), "suggestions": sorted(out, key=lambda s: (s["kind"], s["a"], s["b"]))}


def main(argv: list[str]) -> int:
    text = json.dumps(suggestions(), indent=1) + "\n"
    if "--check" in argv:
        ok = OUT.exists() and OUT.read_text(encoding="utf-8") == text
        print("block suggestions " + ("current" if ok else "OUT OF DATE: run tools/composer/suggest_constraints.py"))
        return 0 if ok else 1
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print("wrote", OUT.relative_to(ROOT).as_posix(), json.loads(text)["count"], "suggestions")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
