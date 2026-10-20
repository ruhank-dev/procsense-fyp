"""Reconciles the Ollama Cloud rubric experiment from its RAW artifacts into one committed summary (`reports/ai/cloud-gemma-rubric.json`): `tools/.venv/Scripts/python.exe tools/bench/cloud_summary.py [--write]`.

Reads artifacts/ai-bench/rubric/cloud-*.jsonl (git-ignored raw replies, written by tools/bench/cloud_eval.py) and scores them with tools/bench/rubric_compare.py, the same scorer and ground truth as the Qwen experiment. Every figure on docs/research/cloud_gemma_rubric.md comes from the JSON this writes.
STRICT is the primary view: the unmodified reply must be the JSON object the contract asks for. FENCED is a secondary POST-HOC view in which a reply wrapped in a markdown code fence is unwrapped before scoring (files `cloud-gemma31b-fullfenced-*.jsonl`); it never replaces the strict view. The sha256 of each raw file is recorded so a reader can tell the files were not changed.
Needs the raw files, so it cannot run in CI; the committed JSON is the evidence. No network, no model call.
"""
from __future__ import annotations

import hashlib
import json
import statistics
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "bench"))
sys.path.insert(0, str(ROOT / "server"))
import rubric_compare as RC  # noqa: E402

RAW = ROOT / "artifacts" / "ai-bench" / "rubric"
OUT = ROOT / "reports" / "ai" / "cloud-gemma-rubric.json"
RUNS = [("dev", "cloud-gemma31b-full", "cloud-gemma31b-fullfenced", 1), ("test", "cloud-gemma31b-full", "cloud-gemma31b-fullfenced", 1), ("test2", "cloud-gemma31b-full", "cloud-gemma31b-fullfenced", 1)]
SUBSET = [("dev", "cloud-gptoss20b-hint", None, 5), ("dev", "cloud-gemma31b-hint", "cloud-gemma31b-hintfenced", 5)]


def raws(tag: str, split: str) -> list[dict]:
    return [json.loads(l)["raw"] for l in (RAW / f"{tag}-{split}.jsonl").read_text(encoding="utf-8").splitlines() if l.strip()]


def pct(xs, q):
    xs = sorted(xs)
    return xs[min(len(xs) - 1, int(len(xs) * q))]


def view(tag: str, split: str, stride: int) -> dict:
    a = RC.analyse(tag, split, stride)
    c = a["credit"]
    return {"rows": a["rows"], "call_health": a["call_health"], "rows_without_usable_decision": a["abstention_coverage"]["rows_without_usable_decision"],
            "objective_level": {k: a["objective_level"][k] for k in ("tp", "fp", "fn", "precision", "recall", "f1")},
            "credit": {k: {"legitimate_recall": v["legitimate_credit_recall"], "credited_precision": v["credited_precision"], "critical_false_credit": v["critical_false_credit"], "adversarial_false_credit": v["adversarial_false_credit"]} for k, v in c.items()}}


def stats(tag: str, split: str, stride: int) -> dict:
    r = raws(tag, split)
    if stride > 1:
        import nli_zero_shot as N
        keep = {x["id"] for x in N.load_rows(split)[::stride]}
        r = [json.loads(l)["raw"] for l in (RAW / f"{tag}-{split}.jsonl").read_text(encoding="utf-8").splitlines() if l.strip() and json.loads(l)["id"] in keep]
    wall = [x["wall_ms"] for x in r][1:]                                   # the first call is excluded, as in rubric_compare
    return {"calls": len(r), "answered_by": sorted({x.get("answered_by") for x in r if x.get("answered_by")}), "transport_errors": sum(1 for x in r if "error" in x), "done_reasons": sorted({str(x.get("done_reason")) for x in r if "error" not in x}),
            "prompt_tokens": sum(x.get("prompt_tokens", 0) for x in r), "output_tokens": sum(x.get("output_tokens", 0) for x in r),
            "wall_ms": {"median": round(statistics.median(wall)), "p95": pct(wall, 0.95), "max": max(wall), "over_5s": sum(1 for w in wall if w > 5000)},
            "reasoning_trace_returned": sum(1 for x in r if x.get("thinking"))}


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()[:16]


def build() -> dict:
    out = {"note": "Synthetic tests/action_bench_v1 rows, labels by construction (builder-written), NOT independently reviewed; the baseline gate was tuned on parts of these splits. STRICT is primary; FENCED is a secondary post-hoc view. Development-only experiment, nothing here is production evidence.",
           "model": "gemma4:31b (requested as gemma4:31b-cloud through a signed-in local Ollama 0.40.5)", "settings": {"format_hint": True, "temperature": 0, "seed": 7, "think": "low", "num_predict": 1024, "prompt_variant": "clartb", "timeout_s": 120}, "splits": {}, "subset_dev_stride5": {}, "raw_sha256_16": {}}
    for split, strict, fenced, stride in RUNS:
        out["splits"][split] = {"strict": view(strict, split, stride), "fenced_post_hoc": view(fenced, split, stride), "calls": stats(strict, split, stride)}
        out["raw_sha256_16"][f"{strict}-{split}.jsonl"] = sha(RAW / f"{strict}-{split}.jsonl")
        out["raw_sha256_16"][f"{fenced}-{split}.jsonl"] = sha(RAW / f"{fenced}-{split}.jsonl")
    for split, tag, fenced, stride in SUBSET:
        out["subset_dev_stride5"][tag] = {"strict": view(tag, split, stride), "calls": stats(tag, split, stride)}
        out["raw_sha256_16"][f"{tag}-{split}.jsonl"] = sha(RAW / f"{tag}-{split}.jsonl")
    return out


def main(argv):
    doc = build()
    text = json.dumps(doc, indent=1, ensure_ascii=False) + "\n"
    if "--write" in argv:
        OUT.write_text(text, encoding="utf-8", newline="\n")
        print("wrote", OUT.relative_to(ROOT).as_posix())
    else:
        print(text)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
