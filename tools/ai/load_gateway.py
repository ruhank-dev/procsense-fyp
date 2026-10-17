"""EXP-BE-2 (part): concurrent callers against one model host through the gateway's single slot (p1-backend-s06): `uv run --project tools tools/ai/load_gateway.py --callers 4 --calls 5 [--model qwen3.5:4b-q4_K_M]`.

Each caller sends registered-prompt calls in a row. A call takes the slot (one model call at a time, as in server/routers/ai_gateway.py) and waits at most --wait seconds for it; a call that cannot get it in
time is counted as the fallback signal. Reported: calls served, calls that got the fallback signal, queue wait and service time (p50, p95) and calls per minute. This measures the host and the slot rule, not the
HTTP layer (the route tests cover that), and not speech recognition (no speech recognition is installed yet: the ASR half of EXP-BE-2 is open). Writes artifacts/ai-bench/ (git-ignored).
"""
import argparse
import json
import statistics
import sys
import threading
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))


def pct(xs, q):
    xs = sorted(xs)
    return round(xs[min(len(xs) - 1, int(q * len(xs)))], 2) if xs else 0.0


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("--callers", type=int, default=4)
    ap.add_argument("--calls", type=int, default=5)
    ap.add_argument("--model", default="qwen3.5:4b-q4_K_M")
    ap.add_argument("--wait", type=float, default=5.0)
    ap.add_argument("--timeout", type=float, default=60.0)
    a = ap.parse_args()
    from llm import client as C, gateway
    client = C.OllamaClient()
    if a.model not in client.models():
        raise SystemExit(f"{a.model} is not installed")
    slot = threading.Lock()
    waits, services, served, fallbacks = [], [], [], []
    lock = threading.Lock()
    variables = {"style": "brief; calm", "age": "60", "sex_word": "man", "topic": "medicines", "original": "I take 5 mg of amlodipine every day for my blood pressure."}

    def caller():
        for _ in range(a.calls):
            t0 = time.monotonic()
            if not slot.acquire(timeout=a.wait):
                with lock:
                    fallbacks.append(1)
                continue
            waited = time.monotonic() - t0
            try:
                t1 = time.monotonic()
                r = gateway.run(client, "compose.narrative", variables, model=a.model, timeout=a.timeout, retries=0)
                took = time.monotonic() - t1
            finally:
                slot.release()
            with lock:
                waits.append(waited)
                services.append(took)
                (served if r.ok else fallbacks).append(1)

    started = time.monotonic()
    threads = [threading.Thread(target=caller) for _ in range(a.callers)]
    for t in threads:
        t.start()
    for t in threads:
        t.join()
    total = time.monotonic() - started
    out = {"model": a.model, "callers": a.callers, "calls_each": a.calls, "served": len(served), "fallback_signals": len(fallbacks), "queue_wait_s_p50": pct(waits, 0.5), "queue_wait_s_p95": pct(waits, 0.95),
           "service_s_p50": pct(services, 0.5), "service_s_p95": pct(services, 0.95), "calls_per_minute": round(60 * (len(served) + len(fallbacks)) / total, 1), "seconds": round(total, 1)}
    path = ROOT / "artifacts" / "ai-bench" / f"load-gateway-{a.callers}x{a.calls}.json"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(out, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(out))
