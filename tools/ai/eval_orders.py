"""Measures the spoken-order grammar (p1-voice-input-s03, EXP-VOX-4): `uv run --project tools tools/ai/eval_orders.py [--write]`.

For every test, medicine, place and staff role in the content, several spoken phrasings are made from the intent templates and corrupted the way a recogniser corrupts words (a letter dropped, doubled or swapped in the
named thing, a filler word before it). Each is heard by the read-back (server/orders/grammar.py). Reported: the share parsed to the right intent and target at the first hearing, the share that ended in a
clarifying question or the menu (not wrong, but not an order), the share parsed to the WRONG target (the number that matters: the read-back must catch it), and `released_without_confirmation`, which must be 0:
no action may leave the read-back before a yes. The corruption is synthetic and mild; real recogniser errors on accented speech are not modelled (DT-205). `--write` saves reports/ai/orders-eval.json.
"""
import argparse
import json
import random
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT))

PHRASES = {
    "test": ["order {x}", "could I get {x}", "please send {x}", "I would like {x}"],
    "medicine": ["give {x}", "request {x}", "start {x}", "can I have {x}"],
    "place": ["transport the patient to {x}", "take the patient to {x}", "porter please for {x}"],
    "staff": ["call {x}", "bleep {x}", "I need {x}", "ask the {x}"],
}
INTENT = {"test": "ORDER_TEST", "medicine": "REQUEST_MEDICATION", "place": "REQUEST_TRANSPORT", "staff": "CALL_STAFF"}


def corrupt(text: str, rng: random.Random) -> str:
    words = text.split()
    i = rng.randrange(len(words))
    w = words[i]
    if len(w) > 4:
        j = rng.randrange(1, len(w) - 1)
        w = rng.choice([w[:j] + w[j + 1:], w[:j] + w[j] + w[j:], w[:j] + w[j + 1] + w[j] + w[j + 2:]])
    words[i] = w
    return rng.choice(["", "um ", "so "]) + " ".join(words)


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--write", action="store_true")
    a = ap.parse_args()
    from server.orders.grammar import ReadBack, vocabulary
    vocab = vocabulary()
    rng = random.Random(11)
    n = right = wrong = other = released = 0
    wrong_examples = []
    for e in vocab:
        if e.kind not in PHRASES or len(e.phrase) < 3:
            continue
        for template in PHRASES[e.kind]:
            for noisy in (False, True):
                said = template.format(x=e.phrase)
                said = corrupt(said, rng) if noisy else said
                rb = ReadBack(vocab)
                h = rb.hear(said)
                n += 1
                if h.status in ("confirm", "removed_dose_confirm") and h.intent == INTENT[e.kind] and h.slots.get("target") == e.target:
                    right += 1
                elif h.status in ("confirm", "removed_dose_confirm"):
                    # a different target with the same words (two phrases of one target, a synonym) is not wrong; a different target is
                    same = [x for x in vocab if x.phrase == e.phrase and x.target == h.slots.get("target")]
                    if same and h.intent == INTENT[e.kind]:
                        right += 1
                    else:
                        wrong += 1
                        if len(wrong_examples) < 8:
                            wrong_examples.append({"said": said, "wanted": e.target, "heard": h.slots.get("target"), "intent": h.intent})
                else:
                    other += 1
                released += len(rb.released)                                   # nothing was confirmed, so nothing may be released
    out = {"phrases": n, "parsed_right": round(right / n, 3), "wrong_target": round(wrong / n, 3), "clarify_or_menu": round(other / n, 3), "released_without_confirmation": released, "wrong_examples": wrong_examples,
           "limit": "synthetic mild corruption of one-word errors; real recogniser errors on accented speech are not modelled"}
    print(json.dumps(out, indent=1))
    if a.write:
        p = ROOT / "reports" / "ai" / "orders-eval.json"
        p.write_text(json.dumps(out, indent=1) + "\n", encoding="utf-8", newline="\n")
        print("wrote", p.relative_to(ROOT).as_posix())
