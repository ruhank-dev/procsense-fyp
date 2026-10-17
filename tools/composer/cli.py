"""Compose cases (p1-scenario-composer-difficulty-s02, guide B-M1 step 5).

    uv run --project tools tools/composer/cli.py compose --scenario S1 --difficulty 2 --n 20 --seed 7 [--targets handover,communication] [--out build/composed]
    uv run --project tools tools/composer/cli.py check build/composed/GEN-S1-0007-001.json        # one card through Stage B

Each case is the same for the same seed. Every card is checked by Stage B (schema, required fields, constraints and the contradiction detector) before it is written; a card that fails is reported and not written.
A composed case is practice-only material, status DRAFT here: the composer never approves. `--targets` weights the choice of spine toward stories whose text mentions the target words.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))
sys.path.insert(0, str(HERE.parents[1] / "tools" / "content"))

from composer import blocks as B  # noqa: E402
from composer import solve, stage_b  # noqa: E402

ROOT = HERE.parents[1]


def main(argv: list[str]) -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    if not argv or argv[0] not in ("compose", "check"):
        print(__doc__)
        return 2
    opt = lambda k, d=None: argv[argv.index(k) + 1] if k in argv else d   # noqa: E731
    cases = B.load_cases()
    ctx = stage_b.load_ctx(False, cases)
    if argv[0] == "check":
        card = json.loads(Path(argv[1]).read_text(encoding="utf-8"))
        r = stage_b.validate(card, ctx)
        print(("PASS " if r.passed else "FAIL ") + card["id"], *[f"\n  {d.code}: {d.detail}" for d in r.defects])
        return 0 if r.passed else 1
    scenario, difficulty, n, seed = opt("--scenario", "S1"), int(opt("--difficulty", "2")), int(opt("--n", "10")), int(opt("--seed", "7"))
    targets = [t.strip() for t in opt("--targets", "").split(",") if t.strip()]
    out = Path(opt("--out", str(ROOT / "build" / "composed")))
    try:
        cards = solve.compose(scenario, difficulty, n, seed, targets, cases)
    except solve.Unsatisfiable as e:
        print("cannot compose: " + str(e))
        return 1
    out.mkdir(parents=True, exist_ok=True)
    bad = 0
    for card in cards:
        ctx["others"] = cases + [c for c in cards if c is not card]
        r = stage_b.validate(card, ctx)
        c = card["composition"]
        print(f"{'PASS' if r.passed else 'FAIL'}  {card['id']}  spine {c['spine']}  {card['identity']['title']} {card['identity']['full_name']}, {card['identity']['age']}  blocks {len(c['blocks'])}  steps {c['solver_steps']}")
        if r.passed:
            (out / f"{card['id']}.json").write_text(json.dumps(card, indent=1, ensure_ascii=False) + "\n", encoding="utf-8")
        else:
            bad += 1
            for d in r.defects[:3]:
                print(f"      {d.code}: {d.detail[:160]}")
    print(f"{len(cards) - bad}/{len(cards)} composed cases passed Stage B -> {out}")
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
