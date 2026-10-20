"""Side-by-side summary of the live semantic-reader runs on the dev split (p1-ai-language-s18): `python tools/bench/semantic_compare.py [--write]` reads reports/ai/semantic-live-*-head-dev.json (written by tools/bench/semantic_eval.py --live) and writes reports/ai/semantic-live-comparison-dev.json.
Nothing is recomputed that the reports do not hold. Same rows, same prompt, same strict validation, same deterministic checks and the same scoring for every model; one run each; synthetic builder-written labels; development evidence only."""
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
R = ROOT / "reports" / "ai"
ORDER = ["gemma4_31b-cloud", "gpt-oss_120b-cloud", "nemotron-3-super_cloud"]


def main(argv):
    rows = []
    for m in ORDER:
        d = json.loads((R / f"semantic-live-{m}-head-dev.json").read_text(encoding="utf-8"))
        r, ip = d["reader"], d["interpretation_in_window"]
        rows.append({"model_requested": r["model_requested"], "answered_by": r["answered_by"], "rows_sent_to_model": r["rows_read"], "provider_calls": r["provider_calls"], "first_attempt": r["first_attempt"], "first_attempt_valid_rate": r["first_attempt_valid_rate"], "retried_rows": r["retried_rows"],
                     "final": r["final"], "final_valid_rate": r["final_valid_rate"], "provider_timeouts": r["provider_errors"].get("TIMEOUT", 0), "tokens": {"prompt": r["prompt_tokens"], "output": r["output_tokens"], "total": r["tokens_total"]}, "wall_ms": r["wall_ms"],
                     "offers": ip["offers"], "credited_precision": ip["credited_precision"], "legitimate_recall": ip["legitimate_recall"], "critical_false_credit": ip["critical_false_credit"], "adversarial_false_credit": ip["non_assertive_false_credit"],
                     "out_of_window_offers": d["out_of_window"]["interpretation_offered_any_act"], "decisions_on_legitimate_rows": ip["legitimate_decisions"]})
    out = {"note": "Live runs, dev split of tests/action_bench_v1 (488 rows; 140 legitimate, 300 adversarial, 40 critical), the production reader and pipeline, confidence mode head, 8 s per attempt, hard caps 220 calls and 450,000 tokens per model. Gemma ran before the per-attempt timeout fix (its 4 timeouts used the whole 12 s budget); the two others ran after it. Synthetic builder-written labels; development evidence only.",
           "shipped_path_dev": {"legitimate_recall": "95/140", "credited_precision": "95/96"}, "models": rows}
    text = json.dumps(out, indent=1, ensure_ascii=False) + "\n"
    if "--write" in argv:
        (R / "semantic-live-comparison-dev.json").write_text(text, encoding="utf-8", newline="\n")
        print("wrote reports/ai/semantic-live-comparison-dev.json")
    else:
        print(text)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
