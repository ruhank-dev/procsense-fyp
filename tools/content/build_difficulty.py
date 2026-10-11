"""Builds content/difficulty/dimensions.json: the adaptive difficulty settings (p0-case-library s06, guide B-M7 and section 86).

    uv run --project tools tools/content/build_difficulty.py            # write
    uv run --project tools tools/content/build_difficulty.py --check    # fail if the file is out of date

This is NOT a trained model. It is a rating (Elo style, K factor 16) of the trainee in each of the 22 assessed behaviours, a table that says which of 8 difficulty dimensions trains which behaviours, and a selector that picks
an approved case tagged with the dimension of the weakest behaviour. The numbers are simulator design values (provisional, SR-2866); the guide fixes only K = 16 and "at least 3 sessions before adapting".
The engine chooses inside the approved case pool; it never invents a condition, and it is off in assessment mode.
"""
from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "content" / "difficulty" / "dimensions.json"
REF = "SR-2866"

# the 8 dimensions of the guide (section 86); weak_behaviours says which behaviours, when weakest, point at the dimension; tag is the case tag that realises it (see CaseTags in Core)
DIMENSIONS = [
    ("more_complex_history", "More complex history", "More comorbidity and medication blocks (approved combinations)", ["b01", "b02", "b03", "b09"], "history_complex"),
    ("communication_challenges", "More communication challenges", "Personas with low literacy, high anxiety or a terse style (no case carries a persona yet)", ["b04", "b05", "b06", "b07", "b08"], "communication_challenge"),
    ("competing_priorities", "More competing priorities", "Parallel tasks and several problems at once", ["b17", "b21", "b22"], "multiple_problems"),
    ("environmental_interruptions", "More environmental interruptions", "Phone calls, theatre calls, faults (no case carries them yet)", ["b18"], "environment"),
    ("information_to_clarify", "More information requiring clarification", "More hidden facts, a chart that disagrees with the patient", ["b16", "b19"], "hidden_facts"),
    ("unexpected_events", "Approved unexpected events", "A higher event count or tier", ["b15", "b20"], "unexpected_events"),
    ("complex_handover", "More complex handover", "More required elements, abnormal results, uncertainty", ["b10", "b11"], "complex_handover"),
    ("cognitive_workload", "Higher cognitive workload", "Emergency pace", ["b12", "b13", "b14"], "emergency_pace"),
]


def build() -> str:
    covered = [b for d in DIMENSIONS for b in d[3]]
    if sorted(covered) != [f"b{i:02d}" for i in range(1, 23)]:
        raise SystemExit("every one of the 22 behaviours must point at exactly one dimension")
    doc = {
        "k_factor": 16, "min_sessions": 3, "start_rating": 1000, "tier_offsets": {"P0": 0, "P1": 100, "P2": 200},
        "curriculum_first": ["VAR-S1-A"], "curriculum_tier_order": ["P0", "P1", "P2"],
        "dimensions": [{"id": i, "label": l, "realised_by": r, "weak_behaviours": w, "tag": t} for i, l, r, w, t in DIMENSIONS],
        "explanations": {
            "default": "Next case: {case}. It is the next one in the standard order; adapting starts after {min_sessions} sessions.",
            "adapted": "Next case: {case}. Your weakest behaviour so far is '{behaviour}' (rating {rating}), so this case practises '{dimension}'.",
            "assessment": "Next case: {case}. Assessment mode follows the fixed order and never adapts.",
        },
        "refs": [REF],
    }
    return json.dumps(doc, indent=1, ensure_ascii=False) + "\n"


def main(argv: list[str]) -> int:
    text = build()
    if "--check" in argv:
        if not OUT.exists() or OUT.read_text(encoding="utf-8") != text:
            print("FAIL  content/difficulty/dimensions.json is out of date; run tools/content/build_difficulty.py")
            return 1
        print("content/difficulty/dimensions.json is up to date")
        return 0
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(text, encoding="utf-8", newline="\n")
    print(f"wrote {OUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
