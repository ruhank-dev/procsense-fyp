"""Measures the text consistency critic against the rule-based baseline (p1-live-scenario-generation-s15): `uv run --project tools tools/ai/eval_critic.py --model qwen3.5:4b-q4_K_M [--limit 214] [--timeout 120]`.

Over the seeded set (tests/generation/text_inconsistencies.json) each changed case is shown to the critic. A hit is a flag on the line that was changed. The baseline (tools/generation/inconsistency_set.py
`baseline_flags`: cross-modal rules, Stage B, lexicon, age, pronoun) is run on the same cases. The pipeline uses both (the critic only adds defects), so the number that matters is the recall of the union
against the recall of the baseline alone; the plan asks for at least 20 points more. False alarms are measured on the clean authored cases (any flag counts).

Two critics are measured on the same model calls: RAW (the first pass lists contradictions) and VERIFIED (each listed flag is then put to the model alone as a yes-or-no question, which is what the pipeline
runs; server/generation/critic.py). The first run of the raw critic flagged 92 percent of the clean cases, which is why the verification pass exists. Model answers are cached per model in
artifacts/ai-bench/critic-cache-<model>.json, so a change to the verification can be measured again without repeating the slow first pass. Results go to artifacts/ai-bench/ (git-ignored). Needs a running Ollama.
"""
import argparse
import json
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools"))
sys.path.insert(0, str(ROOT / "tools" / "generation"))


def rates(per: dict, key_n: str = "n") -> dict:
    n = sum(v[key_n] for v in per.values())
    return {k: round(sum(v[k] for v in per.values()) / n, 3) for k in ("baseline", "baseline_before_rules", "critic", "union", "union_before_rules")}


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", default="qwen3.5:9b-q4_K_M")
    ap.add_argument("--limit", type=int, default=0)
    ap.add_argument("--timeout", type=float, default=300.0)
    ap.add_argument("--set", choices=["v1", "v2"], default="v1", help="v1: the first seeded set; v2: the held-out set worded differently (DT-217), scored once per critic version")
    a = ap.parse_args()
    import inconsistency_set as I
    import inconsistency_set_v2 as I2
    from composer import blocks as B
    from generation import critic
    from llm import client as C
    client = C.OllamaClient()
    if a.model not in client.models():
        raise SystemExit(f"{a.model} is not installed")
    cases = {c["id"]: c for c in B.load_cases()}
    data = json.loads((ROOT / "tests" / "generation" / ("text_inconsistencies.json" if a.set == "v1" else "text_inconsistencies_v2.json")).read_text(encoding="utf-8"))
    apply_edit = I.apply if a.set == "v1" else I2.apply
    tag = "" if a.set == "v1" else "v2|"                                   # cache keys of the held-out set never meet those of the first set (the same case, line and kind can carry different text)
    members = data["members"][: a.limit] if a.limit else data["members"]
    crit = critic.Critic(client, model=a.model, timeout=a.timeout)
    cache_path = ROOT / "artifacts" / "ai-bench" / f"critic-cache-{a.model.replace(':', '_')}.json"
    cache = json.loads(cache_path.read_text(encoding="utf-8")) if cache_path.exists() else {}

    def ask(key: str, case: dict) -> tuple[list, list, bool]:
        """(raw flags, verified flags, could the model answer) for one case, from the cache or the model."""
        if key not in cache:
            raw = crit.review_raw(case)
            cache[key] = {"ok": raw is not None, "raw": [f.__dict__ for f in raw or []], "verified": [f.__dict__ for f in crit.verified(case, raw)] if raw else []}
            cache_path.parent.mkdir(parents=True, exist_ok=True)
            cache_path.write_text(json.dumps(cache), encoding="utf-8")
        e = cache[key]
        return e["raw"], e["verified"], e["ok"]

    started = time.monotonic()
    per = {"raw": {}, "verified": {}}
    failed = 0
    for n, m in enumerate(members, 1):
        case = apply_edit(cases[m["case"]], m["line"], m["text"])
        base = m["line"] in I.baseline_flags(case)
        base_old = m["line"] in I.baseline_flags(case, rules=False)
        raw, ver, ok = ask(f"{tag}member|{m['case']}|{m['line']}|{m['kind']}", case)
        failed += not ok
        for arm, flags in (("raw", raw), ("verified", ver)):
            hit = any(f["line"] == m["line"] for f in flags)
            k = per[arm].setdefault(m["kind"], {"n": 0, "baseline": 0, "baseline_before_rules": 0, "critic": 0, "union": 0, "union_before_rules": 0})
            k["n"] += 1
            k["baseline"] += base
            k["baseline_before_rules"] += base_old
            k["critic"] += hit
            k["union"] += base or hit
            k["union_before_rules"] += base_old or hit
        if n % 10 == 0:
            print(f"  {n}/{len(members)} members, {round(time.monotonic() - started)} s", flush=True)
    clean = data["clean"][: max(10, len(data["clean"]) // 2)] if a.limit else data["clean"]
    alarms = {"raw": 0, "verified": 0}
    baseline_alarms = 0
    for cid in clean:
        raw, ver, ok = ask(f"clean|{cid}", cases[cid])
        alarms["raw"] += bool(raw)
        alarms["verified"] += bool(ver)
        baseline_alarms += bool(I.baseline_flags(cases[cid]))
    out = {"set": a.set, "model": a.model, "digest": client.models().get(a.model, ""), "members": len(members), "critic_could_not_answer": failed, "recall": rates(per["verified"]), "by_kind": per["verified"],
           "clean_cases": len(clean), "critic_false_alarm_rate": round(alarms["verified"] / len(clean), 3), "baseline_false_alarm_rate": round(baseline_alarms / len(clean), 3),
           "raw": {"recall": rates(per["raw"]), "by_kind": per["raw"], "critic_false_alarm_rate": round(alarms["raw"] / len(clean), 3)},
           "note_rules": "baseline is the rule-based checks INCLUDING the side-of-body and denied-pain rules added in server/generation/consistency.py (they were written after the first critic run showed the model missed those two kinds); baseline_before_rules is the baseline the critic was first measured against. The plan's +20 points was set against the earlier baseline.",
           "note": "recall and critic_false_alarm_rate are those of the verified critic, the one the pipeline runs; raw is the first pass alone",
           "seconds": round(time.monotonic() - started)}
    path = ROOT / "artifacts" / "ai-bench" / ("critic-eval.json" if a.set == "v1" else "critic-eval-v2.json")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(out, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({k: v for k, v in out.items() if k not in ("by_kind",)}, indent=1))
