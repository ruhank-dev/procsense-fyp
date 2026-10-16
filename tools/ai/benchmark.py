"""Benchmark of the local language models on ProcSense work (p1-ai-infrastructure-s02, p1-ai-language-s02, EXP-AI-1).

    uv run --project tools tools/ai/benchmark.py inventory
    uv run --project tools tools/ai/benchmark.py b0 --model qwen3.5:4b-q4_K_M --think off --runs 5
    uv run --project tools tools/ai/benchmark.py b1 --model qwen3.5:4b-q4_K_M --think off --n 100
    uv run --project tools tools/ai/benchmark.py b2 --model qwen3.5:4b-q4_K_M --think off --limit 60
    uv run --project tools tools/ai/benchmark.py blind artifacts/ai-bench/<run>.jsonl ...      (a blind rating sheet for BT3)

What each test measures, against a running Ollama (default http://127.0.0.1:11434). The plan names the tests BT0 to BT6; BT3 is the blind human rating:
  inventory  the installed models, what is loaded, how much sits on the GPU, free RAM and VRAM
  b0         speed and memory: load time, prompt and generation speed, p50 and p95 of a dialogue-shaped request, share of the model on the GPU
  b1         structured output: the reply must be valid JSON with an intent from the real role enum (content/ai/intents.json), text and fact ids
  b2         grounding: the patient is asked about one topic of a real P0 case twice, once with that fact earned (in the context) and once withheld;
             every reply goes through the real grounding validator, and the texts are saved for a person to read (the validator matches text only)

The context here is a Python stand-in for the patient case of src/ProcSense.Core/AIContext/ContextBuilder.cs: the model sees only the facts the patient
has earned (volunteered, or asked about this turn), the validator also knows the hidden ones. The C# builder stays the source of truth; the stand-in
exists so the models can be compared on the real case files without a Unity or .NET session. Prompts are the first version (v0) and are expected to change.
Results go to artifacts/ai-bench/ (git-ignored) as one JSON line per call, with the model digest, the options and a UTC time. Standard library only,
plus the repository's own validator. This tool changes nothing in the repository and sends nothing outside the machine running Ollama.
"""
from __future__ import annotations

import argparse
import csv
import json
import random
import shutil
import subprocess
import sys
import time
import urllib.error
import urllib.request
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
from validation.validator import has_term, norm, numbers, validate  # noqa: E402

OUT_DIR = ROOT / "artifacts" / "ai-bench"
DEFAULT_URL = "http://127.0.0.1:11434"    # not "localhost": from Python on Windows that name costs a flat 2 s per request (measured 3.0 s against 1.0 s)
SAMPLING = {"temperature": 0.7, "top_p": 0.8, "top_k": 20}      # the model card's non-thinking settings
INSTRUCTION = (
    "You are the patient in a hospital pre-operative check, speaking to a nurse. Reply in one or two short sentences, first person, plain English. "
    "Use only the facts listed below. If the nurse asks about something that is not listed, use the intent cannot_recall and say you are not sure or do not remember; "
    "never invent a medicine, number, test or condition. Answer as JSON with: intent (one of the allowed intents), text (what you say), "
    "fact_ids (the ids of the listed facts you used, or an empty list)."
)


def utc_now() -> str:
    return datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def load_json(path: str) -> dict:
    return json.loads((ROOT / path).read_text(encoding="utf-8"))


def percentile(values: list[float], q: float) -> float:
    """The q-th percentile (0 to 100) by linear interpolation; 0.0 for an empty list."""
    if not values:
        return 0.0
    s = sorted(values)
    k = (len(s) - 1) * q / 100
    lo, hi = int(k), min(int(k) + 1, len(s) - 1)
    return s[lo] + (s[hi] - s[lo]) * (k - lo)


# --- talking to Ollama ---------------------------------------------------------------------------------------------------------------------

def post(url: str, path: str, body: dict, timeout: int = 900) -> dict:
    req = urllib.request.Request(url + path, data=json.dumps(body).encode(), headers={"Content-Type": "application/json"})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return json.loads(r.read())


def get(url: str, path: str, timeout: int = 30) -> dict:
    with urllib.request.urlopen(url + path, timeout=timeout) as r:
        return json.loads(r.read())


def generate(url: str, model: str, prompt: str, *, think: bool, ctx: int, schema: dict | None, seed: int, num_predict: int) -> dict:
    """One call; returns the reply text, the thinking text and the timings (seconds) Ollama reports, plus the wall-clock time."""
    body = {"model": model, "prompt": prompt, "stream": False, "think": think, "keep_alive": "10m",
            "options": {**SAMPLING, "num_ctx": ctx, "num_predict": num_predict, "seed": seed}}
    if schema is not None:
        body["format"] = schema
    started = time.perf_counter()
    r = post(url, "/api/generate", body)
    wall = time.perf_counter() - started
    ev, pe = r.get("eval_duration", 0) / 1e9, r.get("prompt_eval_duration", 0) / 1e9
    return {"response": r.get("response", ""), "thinking": r.get("thinking", "") or "", "wall_s": wall, "load_s": r.get("load_duration", 0) / 1e9,
            "prompt_tokens": r.get("prompt_eval_count", 0), "gen_tokens": r.get("eval_count", 0),
            "prompt_tps": (r.get("prompt_eval_count", 0) / pe) if pe else 0.0, "gen_tps": (r.get("eval_count", 0) / ev) if ev else 0.0,
            "done_reason": r.get("done_reason", "")}


def residency(url: str, model: str) -> dict:
    """How the loaded model is split between GPU and CPU (from /api/ps); empty when it is not loaded."""
    try:
        for m in get(url, "/api/ps").get("models", []):
            if m.get("name") == model or m.get("model") == model:
                size, vram = m.get("size", 0), m.get("size_vram", 0)
                return {"size_gb": round(size / 1e9, 2), "gpu_share": round(vram / size, 3) if size else 0.0, "context": m.get("context_length")}
    except (urllib.error.URLError, OSError, ValueError):
        pass
    return {}


def machine() -> dict:
    """Free RAM and VRAM in use, best effort (nvidia-smi and the OS may be missing)."""
    info: dict = {}
    smi = shutil.which("nvidia-smi")
    if smi:
        try:
            out = subprocess.run([smi, "--query-gpu=name,memory.used,memory.total", "--format=csv,noheader,nounits"], capture_output=True, text=True, timeout=10).stdout.strip()
            name, used, total = [x.strip() for x in out.split(",")]
            info.update(gpu=name, vram_used_mb=int(used), vram_total_mb=int(total))
        except (subprocess.SubprocessError, ValueError, OSError):
            pass
    try:
        import ctypes

        class Mem(ctypes.Structure):
            _fields_ = [("l", ctypes.c_ulong), ("load", ctypes.c_ulong), ("total", ctypes.c_ulonglong), ("avail", ctypes.c_ulonglong), ("pt", ctypes.c_ulonglong),
                        ("pa", ctypes.c_ulonglong), ("vt", ctypes.c_ulonglong), ("va", ctypes.c_ulonglong), ("ve", ctypes.c_ulonglong)]
        m = Mem(); m.l = ctypes.sizeof(Mem)
        if ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(m)):                                          # type: ignore[attr-defined]
            info["ram_free_gb"] = round(m.avail / 2**30, 1)
    except (AttributeError, OSError):
        pass
    return info


# --- ProcSense material --------------------------------------------------------------------------------------------------------------------

def schema_for(intents: list[str], fact_ids: list[str] | None = None) -> dict:
    """The reply schema. With fact_ids, the ids the model may cite are limited to the facts it was shown (none shown: an empty list only)."""
    ids = {"type": "array", "items": {"type": "string"}} if fact_ids is None else (
        {"type": "array", "items": {"type": "string", "enum": fact_ids}, "maxItems": len(fact_ids)} if fact_ids else {"type": "array", "maxItems": 0})
    return {"type": "object", "additionalProperties": False, "required": ["intent", "text", "fact_ids"],
            "properties": {"intent": {"type": "string", "enum": intents}, "text": {"type": "string"}, "fact_ids": ids}}


def p0_cases() -> list[dict]:
    cases = [json.loads(p.read_text(encoding="utf-8")) for p in sorted((ROOT / "content" / "case_files").glob("*.json"))]
    return [c for c in cases if c.get("tier") == "P0"]


def question_for(topic: str) -> str:
    return "Can you tell me about your " + topic.replace("_", " ") + "?"


def patient_context(case: dict, asked_topic: str, lexicon: dict, intents: list[str], asked: str = "") -> dict:
    """The validator's context for the patient (every fact; the earned ones marked) and, separately, the facts the model may be shown.
    The nurse's question counts as allowed text, as the kept turns do in ContextBuilder."""
    facts, shown, text = [], [], []
    for h in case["history"]:
        if h.get("variant") is not None:
            continue
        earned = bool(h["volunteered"]) or h["topic"] == asked_topic
        fact = {"id": "h." + h["topic"], "value": h["answer"], "disclosed": False, "disclosable_now": earned}
        facts.append(fact)
        if earned:
            shown.append(fact)
            text.append(h["answer"])
    for a in case.get("allergies", []):
        text.append(a["substance"])
    text.append(asked)
    joined = norm(" ".join(text))
    entities = [t for cat in ("drug", "test", "condition", "allergen", "equipment") for t in lexicon["categories"][cat] if has_term(joined, t)]
    return {"allowed_intents": intents, "facts": facts, "allowed_entities": entities, "numbers": numbers(" ".join(text)), "dose_text": [], "shown": shown}


def build_prompt(ctx: dict, topic: str) -> str:
    lines = [INSTRUCTION, "", "Allowed intents: " + ", ".join(ctx["allowed_intents"]), "Facts you may use:"]
    lines += [f"- {f['id']}: {f['value']}" for f in ctx["shown"]] or ["- (none)"]
    lines += ["", "The nurse asks: " + question_for(topic)]
    return "\n".join(lines)


def score_reply(reply: str, ctx: dict, lexicon: dict) -> dict:
    """Parse and validate one reply; the validator never sees the 'shown' helper list."""
    try:
        out = json.loads(reply)
    except ValueError:
        return {"parsed": False, "codes": ["PARSE"], "ok": False}
    if not isinstance(out, dict):
        return {"parsed": False, "codes": ["PARSE"], "ok": False}
    out = {"kind": "utterance", "role": "patient", **out}
    vctx = {k: v for k, v in ctx.items() if k != "shown"}
    res = validate(out, vctx, lexicon)
    return {"parsed": True, "codes": list(res.codes), "ok": res.ok, "intent": out.get("intent"), "text": out.get("text"), "fact_ids": out.get("fact_ids")}


# --- records -------------------------------------------------------------------------------------------------------------------------------

def run_file(test: str, model: str) -> Path:
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    return OUT_DIR / f"{stamp}-{test}-{model.replace(':', '_').replace('/', '_')}.jsonl"


def digest(url: str, model: str) -> str:
    try:
        for m in get(url, "/api/tags").get("models", []):
            if m.get("name") == model or m.get("model") == model:
                return m.get("digest", "")[:12]
    except (urllib.error.URLError, OSError, ValueError):
        pass
    return ""


def header(args, test: str) -> dict:
    return {"record": "run", "test": test, "model": args.model, "digest": digest(args.url, args.model), "think": args.think == "on", "ctx": args.ctx,
            "options": SAMPLING, "utc": utc_now(), "machine": machine()}


def shown(path: Path) -> str:
    """The path as printed: relative to the repository when it is inside it, absolute otherwise."""
    try:
        return str(path.relative_to(ROOT))
    except ValueError:
        return str(path)


def write(path: Path, rows: list[dict]) -> None:
    path.write_text("".join(json.dumps(r, ensure_ascii=False) + "\n" for r in rows), encoding="utf-8")


# --- tests ---------------------------------------------------------------------------------------------------------------------------------

def cmd_inventory(args) -> int:
    for m in get(args.url, "/api/tags").get("models", []):
        print(f"{m['name']:28} {m['digest'][:12]}  {m['size'] / 1e9:5.1f} GB")
    for m in get(args.url, "/api/ps").get("models", []):
        print(f"loaded: {m['name']}  {m['size'] / 1e9:.1f} GB, {m.get('size_vram', 0) / max(m['size'], 1):.0%} on the GPU")
    print(machine())
    return 0


def cmd_b0(args) -> int:
    lex, intents = load_json("content/ai/lexicon.json"), load_json("content/ai/intents.json")["roles"]["patient"]
    case = p0_cases()[0]
    topic = next(h["topic"] for h in case["history"] if not h["volunteered"])
    ctx = patient_context(case, topic, lex, intents, question_for(topic))
    prompt, schema = build_prompt(ctx, topic), schema_for(intents, [f["id"] for f in ctx["shown"]])
    rows = [header(args, "b0")]
    for i in range(args.runs):
        r = generate(args.url, args.model, prompt, think=args.think == "on", ctx=args.ctx, schema=schema, seed=i, num_predict=args.num_predict)
        rows.append({"record": "call", "run": i, "cold": i == 0, **{k: v for k, v in r.items() if k not in ("response", "thinking")}, "thinking_chars": len(r["thinking"])})
    rows.append({"record": "residency", **residency(args.url, args.model), "machine": machine()})
    warm = [r["wall_s"] for r in rows if r["record"] == "call" and not r["cold"]]
    gen = [r["gen_tps"] for r in rows if r["record"] == "call" and not r["cold"]]
    path = run_file("b0", args.model)
    write(path, rows)
    res = rows[-1]
    print(f"B0 {args.model} think={args.think}: warm p50 {percentile(warm, 50):.2f} s, p95 {percentile(warm, 95):.2f} s, generation {percentile(gen, 50):.1f} tok/s, "
          f"{res.get('gpu_share', 0):.0%} on the GPU ({res.get('size_gb', 0)} GB)  -> {shown(path)}")
    return 0


def cmd_b1(args) -> int:
    intents_all = load_json("content/ai/intents.json")["roles"]
    lex, cases = load_json("content/ai/lexicon.json"), p0_cases()
    rng = random.Random(args.seed)
    rows, bad = [header(args, "b1")], 0
    for i in range(args.n):
        case = rng.choice(cases)
        topic = rng.choice([h["topic"] for h in case["history"] if h.get("variant") is None])
        intents = intents_all["patient"]
        ctx = patient_context(case, topic, lex, intents, question_for(topic))
        r = generate(args.url, args.model, build_prompt(ctx, topic), think=args.think == "on", ctx=args.ctx, schema=schema_for(intents, [f["id"] for f in ctx["shown"]]), seed=i, num_predict=args.num_predict)
        s = score_reply(r["response"], ctx, lex)
        valid = s["parsed"] and "SCHEMA" not in s["codes"] and "UNKNOWN_INTENT" not in s["codes"]
        bad += 0 if valid else 1
        rows.append({"record": "call", "i": i, "case": case["id"], "topic": topic, "valid": valid, "empty": not r["response"].strip(), "thinking_chars": len(r["thinking"]),
                     "wall_s": r["wall_s"], "gen_tokens": r["gen_tokens"], "done_reason": r["done_reason"], **s})
    path = run_file("b1", args.model)
    write(path, rows)
    calls = [r for r in rows if r["record"] == "call"]
    print(f"B1 {args.model} think={args.think}: {len(calls) - bad}/{len(calls)} valid, {sum(r['empty'] for r in calls)} empty, "
          f"{sum(1 for r in calls if r['thinking_chars'])} with thinking text, {sum(1 for r in calls if r['done_reason'] == 'length')} cut off  -> {shown(path)}")
    return 0


def cmd_b2(args) -> int:
    intents = load_json("content/ai/intents.json")["roles"]["patient"]
    lex, cases = load_json("content/ai/lexicon.json"), p0_cases()
    rng = random.Random(args.seed)
    pairs = [(c, h["topic"]) for c in cases for h in c["history"] if h.get("variant") is None and not h["volunteered"]]
    rng.shuffle(pairs)
    rows = [header(args, "b2")]
    for i, (case, topic) in enumerate(pairs[: args.limit]):
        for condition in ("earned", "withheld"):
            ctx = patient_context(case, topic if condition == "earned" else "", lex, intents, question_for(topic))
            r = generate(args.url, args.model, build_prompt(ctx, topic), think=args.think == "on", ctx=args.ctx, schema=schema_for(intents, [f["id"] for f in ctx["shown"]]), seed=i, num_predict=args.num_predict)
            s = score_reply(r["response"], ctx, lex)
            rows.append({"record": "call", "i": i, "case": case["id"], "topic": topic, "condition": condition, "wall_s": r["wall_s"], "thinking_chars": len(r["thinking"]),
                         "asked": question_for(topic), **s})
    path = run_file("b2", args.model)
    write(path, rows)
    calls = [r for r in rows if r["record"] == "call"]
    for condition in ("earned", "withheld"):
        group = [r for r in calls if r["condition"] == condition]
        codes: dict[str, int] = {}
        for r in group:
            for c in r["codes"]:
                codes[c] = codes.get(c, 0) + 1
        print(f"B2 {args.model} think={args.think} {condition:9}: {sum(r['ok'] for r in group)}/{len(group)} pass the validator; codes {codes or 'none'}")
    print(f"   texts for a person to read (the validator checks text only)  -> {shown(path)}")
    return 0


def cmd_blind(args) -> int:
    """A rating sheet with the model hidden and the order shuffled (B3); the key is written next to it."""
    items = []
    for f in args.files:
        lines = [json.loads(x) for x in Path(f).read_text(encoding="utf-8").splitlines() if x.strip()]
        model = next(r["model"] for r in lines if r["record"] == "run")
        items += [{**r, "model": model} for r in lines if r["record"] == "call" and r.get("text")]
    random.Random(args.seed).shuffle(items)
    sheet, key = OUT_DIR / "blind-sheet.csv", OUT_DIR / "blind-key.json"
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    with sheet.open("w", newline="", encoding="utf-8") as fh:
        w = csv.writer(fh)
        w.writerow(["id", "question", "condition", "reply", "natural_1to5", "grounded_1to5", "safe_yes_no", "notes"])
        for n, it in enumerate(items, 1):
            w.writerow([n, it["asked"], it["condition"], it["text"], "", "", "", ""])
    key.write_text(json.dumps({str(n): {"model": it["model"], "case": it["case"], "topic": it["topic"]} for n, it in enumerate(items, 1)}, indent=1), encoding="utf-8")
    print(f"{len(items)} replies -> {shown(sheet)} (key: {shown(key)}; do not open the key before rating)")
    return 0


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    ap.add_argument("--url", default=DEFAULT_URL)
    sub = ap.add_subparsers(dest="cmd", required=True)

    def common(p, n_default=None):
        p.add_argument("--model", required=True)
        p.add_argument("--think", choices=["on", "off"], default="off")
        p.add_argument("--ctx", type=int, default=4096)
        p.add_argument("--num-predict", type=int, default=300)
        p.add_argument("--seed", type=int, default=1)
        return p

    sub.add_parser("inventory")
    common(sub.add_parser("b0")).add_argument("--runs", type=int, default=5)
    common(sub.add_parser("b1")).add_argument("--n", type=int, default=100)
    common(sub.add_parser("b2")).add_argument("--limit", type=int, default=60)
    b = sub.add_parser("blind")
    b.add_argument("files", nargs="+")
    b.add_argument("--seed", type=int, default=1)
    args = ap.parse_args(argv)
    try:
        return {"inventory": cmd_inventory, "b0": cmd_b0, "b1": cmd_b1, "b2": cmd_b2, "blind": cmd_blind}[args.cmd](args)
    except (urllib.error.URLError, ConnectionError, OSError) as e:
        print(f"cannot reach Ollama at {args.url}: {e}. Start it with: ollama serve")
        return 2


if __name__ == "__main__":
    sys.exit(main())
