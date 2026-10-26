"""Splits the misses of a scored spoken-order set by cause (EX-18, C32): `uv run --project tools tools/ai/orders_breakdown.py --set v10 --model gemma4:31b-cloud [--write]`.

For every order row of a set that has already been scored (a set that has not been scored is refused) it runs the read-back once more and puts the row in one class:
  correct            the right item was read back
  guard              the words are a question or a condition ("can I get morphine", "should I hold the metformin"): the safety guard of the grammar (DT-208) refuses to read these back, by design, so the trainee uses the menu
  ambiguous_item     the sentence fits two items of the vocabulary equally well (an antibiotic that treats or prevents, a glucose by mouth or into the vein) and the other one was read back, or a question between them was asked
  wrong              another item was read back although the sentence fits one item
  menu               nothing was read back and the guard did not stop it (the model gave no answer, was slow or was unsure)
and, for the non-order rows, whether the sentence was offered (`false_offer`) and whether it is literally a statement of what the speaker is doing or needs.
The second run of a cloud model is not the scored run (an answer that is late goes to the menu), so the totals here are close to the scored figures but not equal; the scored figures are in the compare file.
"""
from __future__ import annotations

import argparse
import json
import sys
from collections import Counter
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))
sys.path.insert(0, str(ROOT / "tools" / "ai"))
import compare_order_mappers as CM  # noqa: E402
import eval_orders_llm as E  # noqa: E402
from server.llm import client as C  # noqa: E402
from server.orders import llm_fallback as LF  # noqa: E402
from server.orders.grammar import ReadBack, clean, not_an_order, vocabulary  # noqa: E402

AMBIGUOUS = [{"TR-ABX-PROPHYLAXIS", "TR-ABX-S2", "TR-SEPSIS-BUNDLE"}, {"TR-FAST-SUGAR", "TR-IV-GLUCOSE"}, {"TR-IV-FLUIDS", "TR-SEPSIS-BUNDLE"}, {"surgeon", "senior"}, {"theatre", "theatre_reception"}]


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--set", required=True)
    ap.add_argument("--model", default="gemma4:31b-cloud")
    ap.add_argument("--write", action="store_true")
    a = ap.parse_args()
    if not (CM.OUT.with_name(f"orders-mapper-compare-{a.set}_gemma.json")).exists():
        raise SystemExit("REFUSED: this set has not been scored; it is scored once before it is studied")
    client = C.OllamaClient("http://127.0.0.1:11434")
    vocab = vocabulary()
    mapper = LF.OrderMapper(client, model=a.model, timeout=60.0)
    classes, examples = Counter(), {}
    for r in E.load_rows(CM.FILES[a.set]):
        h = ReadBack(vocab, mapper).hear(r["text"])
        offered = h.status in ("confirm", "removed_dose_confirm")
        if r["target"] is None:
            c = "false_offer" if offered else "non_order_ok"
        else:
            heard = h.slots.get("target")
            if offered and h.intent == r["intent"] and heard == r["target"]:
                c = "correct"
            elif not_an_order(clean(r["text"])[0]):
                c = "guard"
            elif any({r["target"], heard} <= grp for grp in AMBIGUOUS) or (h.status == "clarify"):
                c = "ambiguous_item"
            elif offered:
                c = "wrong"
            else:
                c = "menu"
        classes[c] += 1
        examples.setdefault(c, []).append(r["text"])
    orders = sum(v for k, v in classes.items() if k not in ("false_offer", "non_order_ok"))
    not_guard = orders - classes["guard"]
    out = {"set": a.set, "model": a.model, "orders": orders, "classes": dict(classes), "correct_share": round(classes["correct"] / orders, 3),
           "correct_share_among_rows_the_guard_lets_through": round(classes["correct"] / not_guard, 3) if not_guard else None,
           "examples": {k: v[:6] for k, v in examples.items() if k != "correct" and k != "non_order_ok"},
           "note": "second run of a cloud model: close to the scored figure, not equal to it; the guard is the question/condition rule of the grammar (DT-208), kept on purpose"}
    print(json.dumps({k: v for k, v in out.items() if k != "examples"}, indent=1))
    if a.write:
        dest = ROOT / "reports" / "ai" / f"orders-breakdown-{a.set}.json"
        dest.write_text(json.dumps(out, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
        print("wrote", dest.relative_to(ROOT).as_posix())
    return 0


if __name__ == "__main__":
    sys.exit(main())
