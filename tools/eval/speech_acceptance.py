"""The final acceptance record of the Gemma speech evaluator (ADR-0025, p1-ai-language-s22): `uv run --project tools tools/eval/speech_acceptance.py [--write | --check]`.

Collects, from the stored reports of the two frozen acceptance runs, the flat numbers the `speech_evaluation` gate reads (content/ai/thresholds.json): the final held-out set v2 (reports/ai/speech-evaluation-heldout-v2.json: the semantic evidence, accepted by the owner)
and the fresh partial-credit set v3 (reports/ai/speech-evaluation-partial-v3.json: the follow-up for the one partial-credit criterion that held-out v2 missed by one item and for the evidence-length limit of DT-247). It adds nothing to either run and recomputes nothing from a model.
The evidence is AI-written, AI-reviewed synthetic data scored once per set against criteria fixed beforehand; it is acceptance evidence for the software and not independent human validation. The real Meta Quest and ASR end-to-end validation (DC-10, Sannan) remains a separate, later check.
`--check` fails when the written file is not what the two reports give."""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
V2 = ROOT / "reports" / "ai" / "speech-evaluation-heldout-v2.json"
V3 = ROOT / "reports" / "ai" / "speech-evaluation-partial-v3.json"
OUT = ROOT / "reports" / "ai" / "speech-evaluation-acceptance.json"


def build() -> dict:
    v2, v3 = json.loads(V2.read_text(encoding="utf-8")), json.loads(V3.read_text(encoding="utf-8"))
    obs = {c["metric"]: c["observed"] for c in v2["criteria"]}
    p3 = {c["metric"]: c["observed"] for c in v3["criteria"]}
    missed = [c["id"] for c in v2["criteria"] if not c["passed"]]
    final = "APPROVED" if (v3["verdict"] == "APPROVED" and missed == ["C5"] and v2["prompt_hash"] == v3["prompt_hash"]) else "NOT APPROVED"
    inputs = {
        "critical_physical_completions": obs["critical_physical_completions"], "window_state_bypass_items": obs["window_state_bypass_items"], "legitimate_recall": obs["legitimate_recall"], "credited_precision": obs["credited_precision"],
        "performed_vs_not_accuracy": obs["performed_vs_not_accuracy"], "valid_reply_rate": obs["valid_reply_rate"], "median_latency_ms": obs["median_latency_ms"], "non_performed_false_credit_rate": obs["non_performed_false_credit_rate"],
        "unjustified_ordinary_credit_rate": obs["unjustified_ordinary_credit_rate"], "mixed_ordinary_recall": obs["mixed_ordinary_recall"], "multi_intent_act_recall": obs["multi_intent_act_recall"], "multi_intent_extra_credit_items": obs["multi_intent_extra_credit_items"],
        "contrast_set_rate": obs["contrast_set_rate"], "partial_in_band_rate": p3["partial_in_band_rate"], "evidence_length_rejections": p3["evidence_length_rejections"]}
    return {"adr": "ADR-0025", "note": "Flat inputs of the speech_evaluation gate, collected from two stored acceptance reports. Synthetic, AI-written and AI-reviewed, each set scored once against criteria fixed beforehand; not independent human validation, not real audio, not a headset result (DC-10, Sannan).",
            "sources": {"semantic_heldout_v2": {"file": V2.relative_to(ROOT).as_posix(), "prompt_hash": v2["prompt_hash"], "verdict": v2["verdict"], "criteria_missed": missed},
                        "partial_credit_v3": {"file": V3.relative_to(ROOT).as_posix(), "prompt_hash": v3["prompt_hash"], "verdict": v3["verdict"]}},
            "partial_credit_basis": "The partial-credit input is the fresh set v3 (84.4 percent, 27 of 32). Held-out v2 missed that single criterion by one item (25 of 32, three misses being the 160-character evidence limit, DT-247); the owner chose a contract fix and a fresh set (22 October 2026).",
            "same_prompt_in_both_runs": v2["prompt_hash"] == v3["prompt_hash"], "final_verdict": final,
            "owner_decision_recorded": True, "gate_inputs": inputs}


def main(argv):
    rec = build()
    text = json.dumps(rec, indent=1, ensure_ascii=False) + "\n"
    if "--write" in argv:
        OUT.write_text(text, encoding="utf-8", newline="\n")
        print("wrote", OUT.relative_to(ROOT).as_posix())
        return 0
    ok = OUT.exists() and OUT.read_text(encoding="utf-8").replace("\r\n", "\n") == text
    print("speech acceptance record is current" if ok else "FAIL speech acceptance record differs from the two reports: run tools/eval/speech_acceptance.py --write")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
