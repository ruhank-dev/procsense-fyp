"""Model identity, thinking switch, cold start, warm latency, contention with a patient reply and the latency budget of the voice workflow, for the rubric evaluator: `tools/.venv/Scripts/python.exe tools/bench/rubric_latency.py [--variant lean] [--write]`.

Run it with nothing else using the GPU. It calls only the installed qwen3.5:4b-q4_K_M through the local Ollama (rubric_eval.call), on a fixed list of PROBE sentences written for this probe (not benchmark rows), and reads the already-measured speech-recognition and speech-output figures from reports/ai (it does not re-measure them).
Writes reports/ai/rubric-latency.json with --write. Nothing here is a benchmark of correctness.
"""
from __future__ import annotations

import argparse
import json
import statistics
import subprocess
import sys
import threading
import time
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "bench"))
import httpx  # noqa: E402
import rubric_eval as R  # noqa: E402

PROBES = ["Could you tell me what medicines you take?", "I will put the cuff on your arm now.", "I haven't explained the operation yet.", "Is she allergic to anything, can you check the chart?",
          "So you took your tablets this morning, thank you.", "The suction at bed two is not working.", "Hmm, lovely weather today.", "If I were to reassure you, I would say it is routine."]
PATIENT_SYSTEM = "You are a pre-operative patient. Reply in one or two short sentences."
PATIENT_USER = "The anaesthetist says: Do you have any questions before we start?"


def stats(xs: list[float]) -> dict:
    xs = sorted(xs)
    return {"n": len(xs), "median": round(statistics.median(xs)), "p95": round(xs[max(0, int(len(xs) * 0.95) - 1)]), "max": round(xs[-1])} if xs else {}


def sh(cmd: list[str]) -> str:
    return subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace").stdout.strip()


def gpu() -> dict:
    o = sh(["nvidia-smi", "--query-gpu=name,memory.used,memory.total,driver_version", "--format=csv,noheader,nounits"]).split(",")
    return {"gpu": o[0].strip(), "memory_used_mb": int(o[1]), "memory_total_mb": int(o[2]), "driver": o[3].strip()} if len(o) == 4 else {}


def runner_rss_mb() -> list[dict]:
    out = sh(["powershell", "-NoProfile", "-Command", "Get-Process | Where-Object { $_.ProcessName -like '*ollama*' -or $_.ProcessName -like '*llama*' } | ForEach-Object { '{0},{1}' -f $_.ProcessName, [math]::Round($_.WorkingSet64/1MB) }"])
    return [{"process": x.split(",")[0], "rss_mb": int(x.split(",")[1])} for x in out.splitlines() if "," in x]


def patient_call(timeout: float = 30.0) -> float:
    t = time.perf_counter()
    httpx.post(R.URL + "/api/chat", json={"model": R.MODEL, "stream": False, "think": False, "keep_alive": "30m", "messages": [{"role": "system", "content": PATIENT_SYSTEM}, {"role": "user", "content": PATIENT_USER}],
                                           "options": {"temperature": 0, "num_predict": 40, "num_ctx": 2048}}, timeout=timeout)
    return (time.perf_counter() - t) * 1000


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--variant", default="lean")
    ap.add_argument("--warm", type=int, default=24)
    ap.add_argument("--write", action="store_true")
    a = ap.parse_args(argv)
    system = R.system_prompt(a.variant)
    elig = R.eligible(None)
    out: dict = {"variant": a.variant}
    # ---- identity and runtime
    show = httpx.post(R.URL + "/api/show", json={"model": R.MODEL}, timeout=10).json()
    tags = {m["name"]: m for m in httpx.get(R.URL + "/api/tags", timeout=10).json()["models"]}
    th = json.loads((ROOT / "content" / "assessment" / "thresholds.json").read_text(encoding="utf-8"))
    out["model"] = {"name": R.MODEL, "digest": tags[R.MODEL]["digest"], "digest_matches_content_assessment_thresholds_json": tags[R.MODEL]["digest"] == th["digest"], "details": show["details"], "capabilities": show.get("capabilities"),
                    "ollama_version": httpx.get(R.URL + "/api/version", timeout=10).json()["version"], "ollama_ps": sh(["ollama", "ps"]), "system_prompt_chars": len(system), "gpu_before_load": gpu()}
    # ---- thinking switch (behaviour, not just hidden output)
    th_rows = {}
    for label, think in (("think_false", False), ("think_omitted", None), ("think_true", True)):
        rs = [R.call(u, elig=elig, think=think, timeout=90, system=system) for u in PROBES[:3]]
        th_rows[label] = {"calls": len(rs), "errors": [r.get("error") for r in rs if r.get("error")], "thinking_chars": [len(r.get("thinking", "")) for r in rs], "output_tokens": [r.get("output_tokens") for r in rs], "content_empty": sum(1 for r in rs if not r.get("content", "").strip()),
                          "done_reason": [r.get("done_reason") for r in rs], "wall_ms": [r.get("wall_ms") for r in rs]}
    out["thinking"] = {**th_rows, "reading": "think=false is accepted by the API; thinking text is empty and the output tokens are only the JSON. With the option omitted this model thinks by default (capabilities list thinking, default true)."}
    # ---- cold start: unload, then the first call, then warm calls
    sh(["ollama", "stop", R.MODEL])
    time.sleep(2)
    idle = gpu()
    cold = [R.call(PROBES[0], elig=elig, timeout=120, system=system)]
    out["gpu_after_unload"] = idle
    out["cold_start"] = {"first_call": {k: cold[0].get(k) for k in ("wall_ms", "load_ms", "prompt_ms", "gen_ms", "total_ms", "error")}}
    warm = [R.call(PROBES[i % len(PROBES)], elig=elig, timeout=60, system=system) for i in range(a.warm)]
    out["gpu_loaded"] = gpu()
    out["runner_rss"] = runner_rss_mb()
    ok = [w for w in warm if not w.get("error")]
    out["warm"] = {"calls": len(warm), "errors": [w.get("error") for w in warm if w.get("error")], "wall_ms": stats([w["wall_ms"] for w in ok]), "prompt_eval_ms": stats([w["prompt_ms"] for w in ok]), "generation_ms": stats([w["gen_ms"] for w in ok]),
                   "prompt_tokens": ok[0]["prompt_tokens"] if ok else None, "output_tokens": stats([w["output_tokens"] for w in ok]), "generation_tokens_per_s": round(statistics.median([w["output_tokens"] / (w["gen_ms"] / 1000) for w in ok if w["gen_ms"]]), 1) if ok else None,
                   "prompt_tokens_per_s": round(statistics.median([w["prompt_tokens"] / (w["prompt_ms"] / 1000) for w in ok if w["prompt_ms"]])) if ok else None,
                   "evaluator_vs_a_second_identical_call": "no prefix reuse: prompt_eval_count equals the whole prompt on every call" if ok and min(w["prompt_tokens"] for w in ok) > 0.9 * max(w["prompt_tokens"] for w in ok) and stats([w["prompt_ms"] for w in ok])["p95"] > 0.9 * stats([w["prompt_ms"] for w in ok])["median"] else "prompt_tokens or prompt time vary: check"}
    # ---- application-side processing that can be measured: parse, validate, credit decision with the production guards
    t = time.perf_counter()
    n = 200
    for _ in range(n):
        ev = R.evaluate(PROBES[0], ok[0], elig)
        for o in R.critical_ids() | {"ASK_TOPIC"}:
            R.credit(PROBES[0], ev, o, True)
    out["application_side_ms_per_utterance"] = round((time.perf_counter() - t) / n * 1000, 3)
    # ---- contention: a patient reply issued while an evaluator call is running (one model slot, ADR-0011)
    alone = [patient_call() for _ in range(6)]
    during = []
    for i in range(6):
        th_ = threading.Thread(target=lambda: R.call(PROBES[i % len(PROBES)], elig=elig, timeout=60, system=system))
        th_.start()
        time.sleep(0.3)
        during.append(patient_call(60))
        th_.join()
    out["contention"] = {"patient_reply_alone_ms": stats(alone), "patient_reply_issued_300ms_after_an_evaluator_call_ms": stats(during), "note": "a 40-token proxy reply, not the dialogue prompt; shows queueing behind one evaluator call"}
    # ---- the latency budget from the existing documents and reports
    asr = json.loads((ROOT / "reports" / "ai" / "asr-primock57.json").read_text(encoding="utf-8"))["small_en-cuda-int8_float16"]
    tts = json.loads((ROOT / "reports" / "ai" / "tts-latency.json").read_text(encoding="utf-8"))
    base = json.loads((ROOT / "artifacts" / "ai-bench" / "nli" / "baseline-test.json").read_text(encoding="utf-8"))["meta"]["latency_ms_per_utterance"]
    w = out["warm"]["wall_ms"]
    out["budget"] = {"round_trip_target_p95_s": 4.0, "source_round_trip": "docs/architecture/feature_registry.md C31: p95 round trip under 4 s; unity_ai_hooks.md hook 1: live AI call timeout 3 s", "asr_seconds_per_clip": {"p50": asr["seconds_per_clip_p50"], "p95": asr["seconds_per_clip_p95"], "source": "reports/ai/asr-primock57.json (not re-measured here)"},
                     "speech_output_first_sentence_s": {"p95_worst": tts["first_sentence_seconds"], "p50_all": tts["p50_seconds"], "source": "reports/ai/tts-latency.json (not re-measured here)"},
                     "left_for_everything_between_s": round(4.0 - asr["seconds_per_clip_p95"] - tts["first_sentence_seconds"], 2), "baseline_gate_ms": base,
                     "evaluator_added_if_serial_s": {"median": round(w["median"] / 1000, 2), "p95": round(w["p95"] / 1000, 2)}}
    out["budget"]["serial_in_the_reply_path_fits"] = (asr["seconds_per_clip_p95"] + w["p95"] / 1000 + tts["first_sentence_seconds"]) <= 4.0
    print(json.dumps(out, indent=1))
    if a.write:
        (ROOT / "reports" / "ai" / "rubric-latency.json").write_text(json.dumps(out, indent=1) + "\n", encoding="utf-8", newline="\n")
        print("wrote reports/ai/rubric-latency.json")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
