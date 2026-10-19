"""Latency of the speech interpretation path, measured in parts and composed (p1-ai-language-s14, ADR-0022): `tools/.venv/Scripts/python.exe tools/bench/interpretation_latency.py [--write]`.

Measured here, on this desktop (GTX 1660 SUPER, Ryzen 5 2600): the embedding model's cold start (unloaded, then one call); the warm time of one interpretation (embedding call, hybrid and TF-IDF heads, rules) over real benchmark sentences, split into the embedding call and
everything else; the same while the 4B model is resident and generating a patient-reply-sized answer (the single model host is shared: ADR-0011); and the 4B reply's own time alone and while interpretations are fired at the same time (does the interpretation slow the
patient's reply?). Read from committed reports and NOT re-measured: speech recognition (reports/ai/asr-primock57.json), the grounded patient reply (reports/ai/dialogue-eval.json) and the first spoken sentence (reports/ai/tts-latency.json).
The interpretation runs BESIDE the reply (its result is evidence for the debrief and a clarification, not an input of the reply), so the reply path is recognition, reply, speech; the sum of their committed 95th percentiles is an upper bound, not a 95th percentile of the sum.
Nothing here measures the headset, the microphone chain or Unity. Writes reports/ai/interpretation-latency.json with --write.
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
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools" / "bench"))
import httpx  # noqa: E402
from classify.embedding import OllamaEmbedder  # noqa: E402
from classify.ensemble import EnsembleActClassifier  # noqa: E402
from interpret.decide import Interpreter  # noqa: E402
import interpretation_bench as B  # noqa: E402
import nli_zero_shot as N  # noqa: E402

URL = "http://127.0.0.1:11434"
EMBED_MODEL = "all-minilm:22m"
QWEN = "qwen3.5:4b-q4_K_M"


def stats(xs: list[float]) -> dict:
    xs = sorted(xs)
    return {"n": len(xs), "median": round(statistics.median(xs), 1), "p95": round(xs[max(0, int(len(xs) * 0.95) - 1)], 1), "max": round(xs[-1], 1)} if xs else {}


def patient_call() -> float:
    t = time.perf_counter()
    httpx.post(URL + "/api/chat", json={"model": QWEN, "stream": False, "think": False, "keep_alive": "30m", "messages": [{"role": "system", "content": "You are a pre-operative patient. Reply in one or two short sentences."},
               {"role": "user", "content": "The anaesthetist says: Do you have any questions before we start?"}], "options": {"temperature": 0, "num_predict": 40, "num_ctx": 2048}}, timeout=60)
    return (time.perf_counter() - t) * 1000


class Timed:
    def __init__(self):
        self.inner, self.ms = OllamaEmbedder(URL), []

    def __call__(self, text):
        t = time.perf_counter()
        v = self.inner(text)
        self.ms.append((time.perf_counter() - t) * 1000)
        return v


def interpret_times(sentences: list[tuple[str, str]], contexts) -> tuple[list[float], list[float]]:
    emb = Timed()
    interp = Interpreter(EnsembleActClassifier(emb))
    total = []
    for (rid, text), ctx in zip(sentences, contexts):
        before = len(emb.ms)
        t = time.perf_counter()
        interp.interpret(ctx, text, rid, 0)
        total.append((time.perf_counter() - t) * 1000)
        assert len(emb.ms) > before
    return total, emb.ms


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--n", type=int, default=150)
    ap.add_argument("--write", action="store_true")
    a = ap.parse_args(argv)
    rows = N.load_rows("dev")[::3][: a.n]
    sents = [(r["id"], r["text"]) for r in rows]
    ctxs = [B.conditions(r)["in_window"] for r in rows]
    out: dict = {"embedding_model": EMBED_MODEL, "sentences": len(sents), "source": "tests/action_bench_v1 dev, every third row (synthetic text; the time does not depend on the label)"}
    # cold start of the embedding model
    subprocess.run(["ollama", "stop", EMBED_MODEL], capture_output=True)
    time.sleep(2)
    t = time.perf_counter()
    Interpreter(EnsembleActClassifier(OllamaEmbedder(URL))).interpret(ctxs[0], sents[0][1], "cold", 0)
    out["embedder_cold_start_ms"] = round((time.perf_counter() - t) * 1000)
    # warm, nothing else running
    total, emb = interpret_times(sents, ctxs)
    out["warm_alone_ms"] = {"whole": stats(total[1:]), "embedding_call": stats(emb[1:]), "heads_and_rules": stats([x - e for x, e in zip(total[1:], emb[1:])])}
    # with the 4B resident and generating
    patient_call()
    stop = threading.Event()
    loops = []

    def load():
        while not stop.is_set():
            loops.append(patient_call())
    th = threading.Thread(target=load)
    th.start()
    time.sleep(1)
    total2, emb2 = interpret_times(sents[:80], ctxs[:80])
    stop.set()
    th.join()
    out["warm_while_4b_generates_ms"] = {"whole": stats(total2), "embedding_call": stats(emb2), "patient_replies_during": stats(loops)}
    # the patient's reply, alone and with interpretations firing
    alone = [patient_call() for _ in range(12)]
    stop2 = threading.Event()
    done = []

    def fire():
        e = Timed()
        i = Interpreter(EnsembleActClassifier(e))
        k = 0
        while not stop2.is_set():
            i.interpret(ctxs[k % len(ctxs)], sents[k % len(sents)][1], "x", 0)
            k += 1
        done.append(k)
    th2 = threading.Thread(target=fire)
    th2.start()
    time.sleep(0.5)
    with_interp = [patient_call() for _ in range(12)]
    stop2.set()
    th2.join()
    out["patient_reply_ms"] = {"alone": stats(alone), "with_interpretations_firing_continuously": stats(with_interp), "interpretations_completed_meanwhile": done[0] if done else 0}
    # the rest of the voice workflow, from committed reports (not re-measured)
    asr = json.loads((ROOT / "reports" / "ai" / "asr-primock57.json").read_text(encoding="utf-8"))["small_en-cuda-int8_float16"]
    dlg = json.loads((ROOT / "reports" / "ai" / "dialogue-eval.json").read_text(encoding="utf-8"))
    tts = json.loads((ROOT / "reports" / "ai" / "tts-latency.json").read_text(encoding="utf-8"))
    p95 = out["warm_while_4b_generates_ms"]["whole"]["p95"]
    out["voice_workflow_from_reports"] = {"asr_p95_s": asr["seconds_per_clip_p95"], "patient_reply_p95_s": round(dlg["ms_p95"] / 1000, 3), "first_spoken_sentence_p95_s": tts["first_sentence_seconds"],
                                          "reply_path_sum_of_p95s_s": round(asr["seconds_per_clip_p95"] + dlg["ms_p95"] / 1000 + tts["first_sentence_seconds"], 2), "round_trip_target_p95_s": 4.0, "device_check_speech_to_first_word_p95_s": 2.5,
                                          "interpretation_p95_ms_while_4b_generates": p95, "interpretation_in_reply_path": False,
                                          "reading": "Interpretation runs beside the reply and adds nothing to it. The sum of the three committed 95th percentiles exceeds the 4 s target, but a sum of percentiles is an upper bound; the real figure is device check DC-08 (Sannan). Not changed by this work."}
    out["interpretation_p95_within_300_ms_alone"] = out["warm_alone_ms"]["whole"]["p95"] <= 300
    out["interpretation_p95_within_300_ms_while_4b_generates"] = p95 <= 300
    print(json.dumps(out, indent=1))
    if a.write:
        (ROOT / "reports" / "ai" / "interpretation-latency.json").write_text(json.dumps(out, indent=1) + "\n", encoding="utf-8", newline="\n")
        print("wrote reports/ai/interpretation-latency.json")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
