"""Builds the blind comparison sheets for DT-250 (ADR-0026): `uv run --project tools tools/ai/dt250_build.py`.

Runs the production dialogue code on tests/dt250/cases_v1.json twice, once with the local model (qwen3.5:4b-q4_K_M, the prompt it keeps) and once with the cloud model (gemma4:31b-cloud, the shipped prompt), through the
same gateway. Patient cases use `Dialogue.reply`; staff cases use `Dialogue.pick_many` for the nurse and the anaesthetist with the approved lines, because that is how each model chooses who speaks. A cloud answer that
fell back to the local model is run again (up to three times) so that the two sides really are the two models; the count is recorded. The outputs are shuffled into two blind sheets (different order, positions swapped)
with no model names, and the key that unblinds them is written to a separate file that the raters do not open. Synthetic data only.
"""
import json
import os
import random
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
D_DIR = ROOT / "tests" / "dt250"
MODELS = {"local": "qwen3.5:4b-q4_K_M", "cloud": "gemma4:31b-cloud"}


def main():
    os.environ["PROCSENSE_RELAY_MODELS"] = MODELS["cloud"]
    from dialogue import dialogue as D
    from llm import client as L
    client = L.OllamaClient()
    doc = json.loads((D_DIR / "cases_v1.json").read_text(encoding="utf-8"))
    intents = json.loads((ROOT / "content" / "ai" / "intents.json").read_text(encoding="utf-8"))["roles"]
    lines = json.loads((ROOT / "content" / "ai" / "staff_intent_lines.json").read_text(encoding="utf-8"))["roles"]
    out = {}
    reruns = 0
    for case in doc["cases"]:
        p = doc["patients"][case["patient"]]
        turns = [{"who": w, "text": t} for w, t in case["turns"]]
        entry = {}
        for side, model in MODELS.items():
            d = D.Dialogue(client, model=model, timeout=60.0)
            for attempt in range(4):
                if case["kind"] == "patient":
                    facts = [{"id": f["id"], "value": f["value"], "disclosed": False, "disclosable_now": bool(f["volunteered"]) or f["topic"] in case["released"]} for f in p["facts"]]
                    ctx = {"role": "patient", "case": case["patient"], "allowed_intents": intents["patient"], "facts": facts, "last_turns": turns, "results": []}
                    authored = {f["id"]: f["value"] for f in facts if f["disclosable_now"]}
                    asked = "h." + case["released"][0] if case["released"] else None
                    r = d.reply(ctx, case["q"], "patient", p["style"], p["age"], p["sex"], authored, None, asked)
                    audit = r.audit or {}
                    res = {"text": r.text, "intent": r.intent, "source": r.source, "reasons": r.reasons, "model": audit.get("model", ""), "fell_back_from": audit.get("fell_back_from", "")}
                else:
                    ctxs = {role: {"role": role, "case": case["patient"], "allowed_intents": intents[role], "facts": [], "last_turns": turns + [{"who": "trainee", "text": case["q"]}], "results": []} for role in ("nurse", "anaesthesia")}
                    got = d.pick_many(ctxs, case["q"], lines)
                    audit = next(iter(got.values())).audit or {}
                    spoken = {role: (got[role].text if got[role].intent != "stay_silent" else None) for role in got}
                    res = {"spoken": spoken, "intents": {role: got[role].intent for role in got}, "source": next(iter(got.values())).source, "reasons": next(iter(got.values())).reasons, "model": audit.get("model", ""), "fell_back_from": audit.get("fell_back_from", "")}
                if side == "cloud" and res["fell_back_from"]:
                    reruns += 1
                    continue
                break
            entry[side] = res
        out[case["id"]] = entry
        print(case["id"], {s: (e.get("text") or e.get("spoken")) for s, e in entry.items()} if False else "ok", end=" ", flush=True)
    rng = random.Random(25102026)
    key = {"models": MODELS, "cloud_reruns_after_a_fallback": reruns, "sheets": {}, "responses": out}
    for sheet, order_seed in (("pass1", 1), ("pass2", 2)):
        ids = [c["id"] for c in doc["cases"]]
        random.Random(order_seed * 977).shuffle(ids)
        items = []
        for cid in ids:
            case = next(c for c in doc["cases"] if c["id"] == cid)
            if sheet == "pass1":
                first = rng.choice(["local", "cloud"])
                key["sheets"].setdefault("positions", {})[cid] = first
            else:
                first = "cloud" if key["sheets"]["positions"][cid] == "local" else "local"       # the second pass presents the opposite order
            sides = [first, "cloud" if first == "local" else "local"]
            p = doc["patients"][case["patient"]]
            shown_facts = [{"fact": f["value"], "status": "the patient may say this now" if (f["volunteered"] or f["topic"] in case.get("released", [])) else "WITHHELD: the trainee has not earned it, the patient must not reveal it"} for f in p["facts"]]
            items.append({"id": cid, "kind": case["kind"], "category": case["cat"], "patient": f"{p['name']}, {p['age']}, speaking style: {p['style']}", "known_facts": shown_facts, "conversation_so_far": case["turns"], "trainee_says": case["q"],
                          "expectation": case.get("expect", ""), "A": out[cid][sides[0]].get("text") or out[cid][sides[0]]["spoken"], "B": out[cid][sides[1]].get("text") or out[cid][sides[1]]["spoken"]})
            key["sheets"].setdefault(sheet, {})[cid] = {"A": sides[0], "B": sides[1]}
        (D_DIR / f"sheet_{sheet}.json").write_text(json.dumps({"label": "Blind sheet. Model names are hidden. Rate A and B independently.", "items": items}, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    (D_DIR / "key.json").write_text(json.dumps(key, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    print("\nsheets written; cloud reruns:", reruns)


if __name__ == "__main__":
    main()
