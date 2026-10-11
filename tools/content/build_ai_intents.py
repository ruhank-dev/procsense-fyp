"""Builds content/ai/intents.json: the intents each character may use when an AI voices a line (p1-ai-language s03, guide sections 82 to 84).

    uv run --project tools tools/content/build_ai_intents.py            # write
    uv run --project tools tools/content/build_ai_intents.py --check    # fail if the file is out of date

The staff intents are the guide's table of AI staff and environment interaction (section 84). The patient intents are the project's reading of the behaviour sections (82 and 83): a patient answers, tells a symptom, asks
a question, shows worry or pain, thanks, or refuses. An AI suggests one of these ids; it never sets state (the behaviour model and the other steps do, by their own tables). SR-2871, provisional.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "content" / "ai" / "intents.json"
REF = "SR-2871"

INTENTS = {
    "patient": ["answer_question", "express_symptom", "ask_question", "express_anxiety", "express_pain", "thank", "refuse"],
    "nurse": ["ask_clarification", "confirm_order", "report_observation", "ask_prep_status", "report_patient_request"],
    "anaesthesia": ["ask_fasting_allergy_airway", "raise_approved_concern", "respond_to_escalation", "sign_in_answer"],
    "surgeon": ["request_update", "decision_response", "time_out_answer", "approved_intraop_issue"],
    "porter": ["confirm_transfer", "report_delay", "ask_monitoring"],
    "lab": ["report_approved_result", "report_sample_rejected", "report_delay"],
    "scrub": ["report_missing_item", "report_fault", "confirm_ready", "count_status"],
    "ward": ["confirm_admission_details", "report_bed_status"],
}


def build() -> str:
    return json.dumps({"roles": INTENTS, "refs": [REF]}, indent=1, ensure_ascii=False) + "\n"


def main(argv: list[str]) -> int:
    text = build()
    if "--check" in argv:
        if not OUT.exists() or OUT.read_text(encoding="utf-8") != text:
            print("FAIL  content/ai/intents.json is out of date; run tools/content/build_ai_intents.py")
            return 1
        print("content/ai/intents.json is up to date")
        return 0
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
