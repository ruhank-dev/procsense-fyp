"""Pre-generate a pool of practice scenarios: `python tools/generation/pool.py --out build/pool [--per-family 6] [--seed 0] [--headless]`.

Each package is generated from empty history (the pool is for no one in particular), sealed, and with --headless also played through the engine (idle run and the spine's correct path, needs the CLI built:
`dotnet build src/ProcSense.Cli -c Release`). The pool folder is git-ignored; `index.json` lists what it holds. See server/generation/pool.py.
"""
import argparse
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools"))

if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=str(ROOT / "build" / "pool"))
    ap.add_argument("--per-family", type=int, default=6)
    ap.add_argument("--seed", type=int, default=0)
    ap.add_argument("--headless", action="store_true")
    a = ap.parse_args()
    from composer import blocks as B
    from generation import objectives as ob, pipeline, pool, retrieve
    ctx = pipeline.Context(retrieve.build_index(), B.load_cases(), ob.Tables.load())
    index = pool.build(ctx, Path(a.out), a.per_family, seed0=a.seed, headless=a.headless)
    print(f"{len(index['packages'])} packages in {a.out} ({len(index['skipped'])} seeds skipped), content {index['content_hash'][:12]}")
