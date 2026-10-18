"""Size, memory, load time and speed of the two local models, measured through Ollama (`uv run --project tools tools/ai/model_resources.py`; writes reports/ai/model-resources.json).

For each of qwen3.5:4b-q4_K_M and qwen3.5:9b-q4_K_M: disk size (/api/tags), the model unloaded, then a cold call whose `load_duration` is the load time, the memory and the share on the GPU while loaded (/api/ps), the generation speed
(`eval_count` over `eval_duration` of a 200-token answer) and the median of five short structured calls (the shape of the gateway's calls). Run it when nothing else uses the GPU; the numbers are for THIS desktop only
(GTX 1660 SUPER, 6 GB). It changes nothing in the repository except the result file.
"""
from __future__ import annotations

import json
import statistics
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

import httpx

ROOT = Path(__file__).resolve().parents[2]
URL = "http://127.0.0.1:11434"
MODELS = {"4b": "qwen3.5:4b-q4_K_M", "9b": "qwen3.5:9b-q4_K_M"}
SCHEMA = {"type": "object", "required": ["act"], "additionalProperties": False, "properties": {"act": {"enum": ["ASK_TOPIC", "REASSURE", "NONE"]}}}


def main() -> int:
    http = httpx.Client(base_url=URL, timeout=600)
    tags = {m["name"]: m for m in http.get("/api/tags").json()["models"]}
    version = http.get("/api/version").json().get("version", "")
    out = {"measured_on": datetime.now(timezone.utc).strftime("%d %B %Y, %H:%M UTC"), "ollama": version, "machine": "desktop, GTX 1660 SUPER 6 GB"}
    for key, name in MODELS.items():
        http.post("/api/generate", json={"model": name, "keep_alive": 0, "prompt": ""})                      # unload
        time.sleep(3)
        r = http.post("/api/generate", json={"model": name, "prompt": "Write two sentences about the weather.", "stream": False, "think": False, "options": {"num_predict": 200, "temperature": 0.7}, "keep_alive": "5m"}).json()
        ps = next((m for m in http.get("/api/ps").json()["models"] if m["name"] == name), {})
        calls = []
        for _ in range(5):
            t = time.perf_counter()
            http.post("/api/chat", json={"model": name, "stream": False, "think": False, "format": SCHEMA, "messages": [{"role": "user", "content": "Classify: Could you tell me about your last meal?"}], "options": {"num_predict": 40, "temperature": 0}})
            calls.append(time.perf_counter() - t)
        size, vram = ps.get("size", 0), ps.get("size_vram", 0)
        out[key] = {"model": name, "disk_gb": round(tags[name]["size"] / 1e9, 2), "loaded_gb": round(size / 1e9, 2), "gpu_share": f"{round(100 * vram / size)} percent" if size else "-", "load_s": round(r.get("load_duration", 0) / 1e9, 1),
                    "tokens_per_s": round(r["eval_count"] / (r["eval_duration"] / 1e9), 1), "call_s": round(statistics.median(calls), 2)}
        print(key, out[key])
    (ROOT / "reports" / "ai" / "model-resources.json").write_text(json.dumps(out, indent=1) + "\n", encoding="utf-8", newline="\n")
    return 0


if __name__ == "__main__":
    sys.exit(main())
