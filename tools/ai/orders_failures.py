"""Lists the rows of a spoken-order set that the order mapper did not read back correctly, with what it did instead: `uv run --project tools tools/ai/orders_failures.py --set paraphrases --model gemma4:31b-cloud`.

For development sets only (a frozen set is scored once by compare_order_mappers.py and its rows are not studied). Needs a running Ollama; a cloud model needs PROCSENSE_RELAY_MODELS=<name>.
"""
from __future__ import annotations

import argparse
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
sys.path.insert(0, str(ROOT / "tools" / "ai"))
import compare_order_mappers as CM  # noqa: E402
import eval_orders_llm as E  # noqa: E402
from server.llm import client as C  # noqa: E402
from server.orders import llm_fallback as LF  # noqa: E402
from server.orders.grammar import ReadBack, vocabulary  # noqa: E402


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--set", default="paraphrases")
    ap.add_argument("--model", default="qwen3.5:4b-q4_K_M")
    a = ap.parse_args()
    if a.set in CM.FROZEN:
        raise SystemExit("REFUSED: a frozen set is scored once, not studied")
    client = C.OllamaClient("http://127.0.0.1:11434")
    vocab = vocabulary()
    mapper = LF.OrderMapper(client, model=a.model, timeout=60.0)
    bad = ok = 0
    for r in E.load_rows(CM.FILES[a.set]):
        h = ReadBack(vocab, mapper).hear(r["text"])
        offered = h.status in ("confirm", "removed_dose_confirm")
        if r["target"] is None:
            if offered:
                bad += 1
                print(f"FALSE OFFER  {r['text']!r} -> {h.slots.get('target')}")
            continue
        if offered and h.intent == r["intent"] and h.slots.get("target") == r["target"]:
            ok += 1
            continue
        bad += 1
        print(f"{'WRONG' if offered else h.status.upper():11s}  {r['text']!r}  wanted {r['target']} ({r['kind']})  got {h.slots.get('target')}  options {getattr(h, 'options', '')}")
    print(f"correct {ok}, not correct {bad}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
