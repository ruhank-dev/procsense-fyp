"""Is a candidate language model allowed to run, and is it better than the default? `uv run --project tools tools/ai/model_candidate.py status|compare`.

    status                                   per candidate in content/ai/model_candidates.json: gated? terms recorded? installed in Ollama? fits the card? and the one next step
    compare --candidate NAME [--blind F]     the benchmark runs of tools/ai/benchmark.py (artifacts/ai-bench) of the default and of the candidate against the switch rule; prints SWITCH,
                                             NO_SWITCH or INCONCLUSIVE with the reasons. --blind is a JSON file {"samples": N, "candidate_better": M, "baseline_better": K} from people.

Nothing here downloads a model or accepts any terms. A gated model is `terms_not_recorded` until a person with the right to accept them writes name and date into the registry. A decision to switch
needs measured parity or better on valid JSON, grounding (earned and withheld) and speed, AND a blind rating by people; without the rating the answer is at best INCONCLUSIVE, never SWITCH.
"""
from __future__ import annotations

import glob
import json
import statistics
import subprocess
import sys
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
REGISTRY = ROOT / "content" / "ai" / "model_candidates.json"
ART = ROOT / "artifacts" / "ai-bench"


def registry() -> dict:
    return json.loads(REGISTRY.read_text(encoding="utf-8"))


def candidate_state(c: dict, installed: set[str], vram_mb: int | None) -> tuple[str, str]:
    """(state, the one next step). States: terms_not_recorded, too_large, not_installed, ready."""
    t = c.get("terms", {})
    if c.get("gated") and not (t.get("accepted_by") and t.get("accepted_on") and t.get("account_holder_eligibility_confirmed")):
        return "terms_not_recorded", f"the account holder must read and accept '{t.get('name')}' at {c['source']} and record name and date in content/ai/model_candidates.json"
    if vram_mb is not None and c.get("estimated_vram_mb_q4", 0) > vram_mb:
        return "too_large", f"needs about {c['estimated_vram_mb_q4']} MB, the card has {vram_mb} MB: use a smaller quantisation or the processor"
    if c["model"] not in installed:
        return "not_installed", f"pull a quantised build and load it into Ollama as '{c['model']}'"
    return "ready", "run tools/ai/benchmark.py b0, b1 and b2 for the candidate, then compare"


def summarise(model: str, art: Path = ART) -> dict | None:
    """Figures of one model from the newest b0, b1 and b2 files; None when any of the three is missing."""
    out: dict = {"model": model}
    for test in ("b0", "b1", "b2"):
        files = sorted(glob.glob(str(art / f"*-{test}-{model.replace(':', '_').replace('/', '_')}.jsonl")))
        if not files:
            return None
        rows = [json.loads(line) for line in Path(files[-1]).read_text(encoding="utf-8").splitlines() if line.strip()]
        calls = [r for r in rows if r["record"] == "call"]
        if test == "b0":
            res = next((r for r in rows if r["record"] == "residency"), {})
            out["gpu_share"] = res.get("gpu_share", 0.0)
            out["p50_s"] = statistics.median(c["wall_s"] for c in calls if not c["cold"])
        elif test == "b1":
            out["valid_rate"] = sum(c["valid"] for c in calls) / len(calls)
        else:
            for cond in ("earned", "withheld"):
                g = [c for c in calls if c["condition"] == cond]
                out[f"{cond}_pass"] = sum(c["ok"] for c in g) / len(g)
    return out


def decide(base: dict, cand: dict, rule: dict, blind: dict | None) -> tuple[str, list[str]]:
    worse = []
    if rule["valid_json_rate_not_below_baseline"] and cand["valid_rate"] < base["valid_rate"]:
        worse.append(f"valid JSON {cand['valid_rate']:.0%} < {base['valid_rate']:.0%}")
    if rule["grounding_earned_pass_not_below_baseline"] and cand["earned_pass"] < base["earned_pass"]:
        worse.append(f"grounding (earned) {cand['earned_pass']:.0%} < {base['earned_pass']:.0%}")
    if rule["grounding_withheld_pass_not_below_baseline"] and cand["withheld_pass"] < base["withheld_pass"]:
        worse.append(f"grounding (withheld, i.e. leaks) {cand['withheld_pass']:.0%} < {base['withheld_pass']:.0%}")
    if cand["p50_s"] > rule["request_p50_ratio_max"] * base["p50_s"]:
        worse.append(f"request p50 {cand['p50_s']:.2f} s > {rule['request_p50_ratio_max']} x {base['p50_s']:.2f} s")
    if cand["gpu_share"] < rule["gpu_share_min"]:
        worse.append(f"only {cand['gpu_share']:.0%} on the card")
    if worse:
        return "NO_SWITCH", worse
    if rule["blind_rating_required"]:
        if not blind or blind.get("samples", 0) < rule["blind_rating_min_samples"]:
            return "INCONCLUSIVE", [f"measured parity or better, but the blind rating of at least {rule['blind_rating_min_samples']} samples by people is missing"]
        if blind["candidate_better"] <= blind["baseline_better"]:
            return "NO_SWITCH", [f"blind rating: candidate better {blind['candidate_better']}, baseline better {blind['baseline_better']}"]
    return "SWITCH", []


def machine_state(url: str = "http://127.0.0.1:11434") -> tuple[set[str], int | None]:
    try:
        with urllib.request.urlopen(url + "/api/tags", timeout=5) as r:
            installed = {m["name"] for m in json.load(r).get("models", [])}
    except (OSError, ValueError):
        installed = set()
    vram = None
    try:
        out = subprocess.run(["nvidia-smi", "--query-gpu=memory.total", "--format=csv,noheader,nounits"], capture_output=True, text=True, timeout=10).stdout.split()
        vram = int(out[0]) if out else None
    except (OSError, ValueError, subprocess.SubprocessError):
        pass
    return installed, vram


def main(argv: list[str]) -> int:
    reg = registry()
    if not argv or argv[0] == "status":
        installed, vram = machine_state()
        print(f"baseline: {reg['baseline']['model']} ({'installed' if reg['baseline']['model'] in installed else 'not found in Ollama'})")
        for c in reg["candidates"]:
            state, step = candidate_state(c, installed, vram)
            print(f"{c['id']}: {state}\n    next: {step}")
        return 0
    if argv[0] == "compare":
        name = argv[argv.index("--candidate") + 1]
        blind = json.loads(Path(argv[argv.index("--blind") + 1]).read_text(encoding="utf-8")) if "--blind" in argv else None
        base, cand = summarise(reg["baseline"]["model"]), summarise(name)
        if base is None or cand is None:
            print(f"INCONCLUSIVE: no complete b0+b1+b2 run for {'the baseline' if base is None else name} in artifacts/ai-bench (run tools/ai/benchmark.py first)")
            return 2
        verdict, reasons = decide(base, cand, reg["switch_rule"], blind)
        print(verdict + "".join("\n  - " + r for r in reasons))
        return 0 if verdict == "SWITCH" else 1
    print(__doc__)
    return 2


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
