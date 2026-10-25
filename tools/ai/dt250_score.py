"""Unblinds and scores the AI-simulated blind comparison of DT-250 (ADR-0026): `uv run --project tools tools/ai/dt250_score.py`.

Reads tests/dt250/ratings_pass1.json and ratings_pass2.json and the key (tests/dt250/key.json), maps A and B back to the local model (qwen3.5:4b-q4_K_M) and the cloud model (gemma4:31b-cloud), and writes
reports/ai/dt250-dialogue-evaluation.json: per-dimension means, preference counts, unsupported-statement counts, the agreement of the two passes, the cases where they disagreed (with the resolution rule), and the
limitations. The raters are an AI assistant reading twice: this is an AI-SIMULATED human-style assessment, not ratings by people.
"""
import json
import statistics
import sys
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
D = ROOT / "tests" / "dt250"
PATIENT_DIMS = ["realism", "flow", "persona", "history_consistency", "grounding", "uncertainty", "disclosure_control", "clarity"]
STAFF_DIMS = ["appropriateness", "role_fit", "restraint", "clarity"]


def main():
    key = json.loads((D / "key.json").read_text(encoding="utf-8"))
    cases = {c["id"]: c for c in json.loads((D / "cases_v1.json").read_text(encoding="utf-8"))["cases"]}
    passes = {n: json.loads((D / f"ratings_pass{n}.json").read_text(encoding="utf-8"))["ratings"] for n in (1, 2)}
    sheets = {n: key["sheets"][f"pass{n}"] for n in (1, 2)}
    dims = {"patient": PATIENT_DIMS, "staff": STAFF_DIMS}
    scores = {k: {s: defaultdict(list) for s in ("local", "cloud")} for k in dims}
    pref = {k: {"local": 0, "cloud": 0, "tie": 0} for k in dims}
    unsupported = {"local": 0, "cloud": 0}
    per_case = {}
    for cid, case in cases.items():
        kind = case["kind"]
        votes = []
        for n in (1, 2):
            r, pos = passes[n][cid], sheets[n][cid]
            side = {"A": pos["A"], "B": pos["B"]}
            for letter in ("A", "B"):
                for name, value in zip(dims[kind], r[letter]):
                    scores[kind][side[letter]][name].append(value)
                unsupported[side[letter]] += r["u" + letter]
            votes.append("tie" if r["pref"] == "tie" else side[r["pref"]])
        per_case[cid] = {"kind": kind, "category": case["cat"], "votes": votes, "agree": votes[0] == votes[1]}
        # resolution: agreeing passes stand; a split between a model and a tie is a tie; a split between the two models is a tie and is listed
        final = votes[0] if votes[0] == votes[1] else "tie"
        per_case[cid]["resolved"] = final
        pref[kind][final] += 1
    report = {"label": "AI-SIMULATED human-style blind assessment (DT-250). Two readings by the same AI assistant, model identities hidden. Not ratings by people.", "date": "2026-10-25",
              "models": key["models"], "cases": len(cases), "patient_cases": sum(c["kind"] == "patient" for c in cases.values()), "staff_cases": sum(c["kind"] == "staff" for c in cases.values()),
              "cloud_answers_rerun_after_a_fallback": key["cloud_reruns_after_a_fallback"], "means": {}, "preference_resolved": pref, "unsupported_statements": unsupported}
    for kind in dims:
        report["means"][kind] = {s: {name: round(statistics.mean(v), 2) for name, v in scores[kind][s].items()} for s in ("local", "cloud")}
        for s in ("local", "cloud"):
            report["means"][kind][s]["overall"] = round(statistics.mean(x for v in scores[kind][s].values() for x in v), 2)
    agree = sum(v["agree"] for v in per_case.values())
    report["pass_agreement"] = {"same_preference": agree, "of": len(per_case), "share": round(agree / len(per_case), 3)}
    report["disagreements"] = {cid: v["votes"] for cid, v in per_case.items() if not v["agree"]}
    by_cat = defaultdict(lambda: {"local": 0, "cloud": 0, "tie": 0})
    for cid, v in per_case.items():
        by_cat[v["category"]][v["resolved"]] += 1
    report["by_category"] = dict(sorted(by_cat.items()))
    report["per_case"] = per_case
    report["rubric"] = {"patient": {d: "1 to 5" for d in PATIENT_DIMS}, "staff": {d: "1 to 5" for d in STAFF_DIMS},
                        "notes": "disclosure_control: 5 = nothing withheld was revealed or contradicted; grounding: 5 = every statement is supported by the released facts; uncertainty: 5 = says it does not know exactly when it should; unsupported statement: any claim or denial the released facts do not support."}
    report["limitations"] = ["Both readings were made by the same AI assistant, one after the other, so they are not independent in the way two people would be; the second reading used a new order and swapped positions.",
                             "The cases are 10 invented patients and 60 AI-written conversations, none used to develop the prompts; they are not real trainee dialogue.",
                             "Raters could see the withheld facts and the intended behaviour, as a clinician reviewer would; they could not see which model wrote a response, but short answers and styles can hint at the model.",
                             "The local model is shown with the prompt it keeps (version 1) and the cloud model with the shipped version 2; staff choices for the local model were forced through the staff-pick prompt although production does not ask it.",
                             "Real human participants (a person reading the replies, 50 blind human ratings) are still required by DT-250(a)(b) and are kept as a separate external item."]
    (ROOT / "reports" / "ai" / "dt250-dialogue-evaluation.json").write_text(json.dumps(report, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    print(json.dumps({k: report[k] for k in ("cases", "means", "preference_resolved", "unsupported_statements", "pass_agreement", "disagreements")}, indent=1))
    return 0


if __name__ == "__main__":
    sys.exit(main())
