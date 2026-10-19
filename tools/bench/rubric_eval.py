"""Context-conditioned semantic rubric evaluator (EXPERIMENT; nothing in the application imports it): `tools/.venv/Scripts/python.exe tools/bench/rubric_eval.py run --split dev|test|test2 [--tag T] [--limit N]`.

One call of the installed local model (qwen3.5:4b-q4_K_M through the local Ollama, thinking OFF, no other model) judges ONE utterance against ALL the objectives that are eligible at that moment and returns, per reported objective,
verdict (SUPPORTED / NOT_SUPPORTED / UNCERTAIN), actor, modality, evidence copied from the utterance and a reason code. The objectives are the communication acts of content/assessment/taxonomy.json (their definitions and
positive/negative examples are the approved wording); the qualifying conditions (the speaker performs it, now, in this utterance) and the tie-breaks come from the annotation rubric tests/classifier_synthetic_v2/rubric.md and the
non-assertive categories of server/classify/assertion.py. Nothing clinical is invented here.

Semantic interpretation (this file: `evaluate`) is kept apart from credit eligibility (`credit`): the model proposes, deterministic code decides, and the production gate is not modified. A critical objective can be SUPPORTED
as evidence but is never credited from speech (ADR-0005). Valid JSON, present evidence, a reason code or a stated certainty are never treated as proof the reading is right: only the benchmark's labels (by construction) are.

Eligibility. `eligible(step)` reads content/assessment/mapping.json: the objectives of every behaviour whose step window contains the current step come first (`in_window`). CORRECTION (19 Oct 2026, ADR-0022): the engine DOES reject an act outside its behaviour window (`EvidenceStore.Submit`, CONTEXT_MISMATCH); this experiment kept out-of-window objectives eligible (flagged `in_window: false`), which is not the approved rule. The benchmark rows carry no scenario state, so every frozen result here used ALL objectives and is unaffected; the planned path (p1-ai-language-s10) applies the engine's rule.
(the engine scores windows, not a fixed sequence), so objectives outside the window stay eligible and are flagged `in_window: false` rather than dropped; the benchmark rows carry no scenario state, so the benchmark uses ALL objectives (the largest, worst-case set).
"""
from __future__ import annotations

import argparse
import json
import re
import statistics
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "server"))
sys.path.insert(0, str(ROOT / "tools" / "bench"))
OUT = ROOT / "artifacts" / "ai-bench" / "rubric"
MODEL = "qwen3.5:4b-q4_K_M"
URL = "http://127.0.0.1:11434"
VERDICTS = ["SUPPORTED", "NOT_SUPPORTED", "UNCERTAIN"]
ACTORS = ["trainee", "other", "unclear"]
MODALITIES = ["actual", "negated", "intended", "hypothetical", "reported", "incomplete"]
REASONS = ["MEETS_RUBRIC", "DENIED", "FUTURE_INTENT", "HYPOTHETICAL", "REPORTED", "OTHER_SPEAKER", "BROKEN_OFF", "WRONG_CONTENT", "DAMAGED_SPEECH", "AMBIGUOUS"]
MAX_FINDINGS = 2
NUM_PREDICT = 160
VARIANT = "clartb"                   # prompt variant: full (definitions, one yes/no example each, tie-breaks), lean (no examples, tie-breaks), min (definitions only)
NUM_CTX = 2048

TIE_BREAKS = (
    "Tie-breaks (from the annotation rubric): asking the patient for name, date of birth or wristband is CONFIRM_IDENTITY; asking about or stating an allergy to the patient is STATE_ALLERGY, never ASK_TOPIC; asking for agreement to the operation or a signature is CONSENT_SIGN. "
    "To a colleague, stating an allergy, a not-fasted state, an identity mismatch or a worsening sign is HANDOVER_CRITICAL_ELEMENT. Asking a colleague to do a task (test, medicine, transport, call) is VOICE_ORDER; asking a senior to come, advise or decide is ESCALATE_SPEECH. "
    "Repeating an instruction or result back to the colleague who gave it is CLOSED_LOOP; noting back what the patient said is ACKNOWLEDGE_FACT. Explaining the operation is EXPLAIN_PROCEDURE, a test or monitor EXPLAIN_TEST, what you are about to do to the patient now EXPLAIN_ACTION. "
    "Giving the patient information that fits nothing else is ANSWER_QUESTION. Asking to repeat or explain something unclear is CLARIFY; asking the patient to say back what they understood is CHECK_UNDERSTANDING.")


def load_json(rel: str) -> dict:
    return json.loads((ROOT / rel).read_text(encoding="utf-8"))


def objectives() -> list[dict]:
    return [a for a in load_json("content/assessment/taxonomy.json")["acts"] if a["id"] != "NONE"]


def critical_ids() -> set[str]:
    return {a["id"] for a in objectives() if a["critical"]}


CLARIFICATIONS = (
    "Clarifications (written after reading DEV failures only): intended means the SPEAKER says they will do the act themselves later (\"I'll ask about that in a moment\"). Telling the patient what will happen, how long it takes or what the operation involves is the act of explaining or answering, performed now: it is SUPPORTED, "
    "not intended and not hypothetical. A statement the speaker makes to the patient about what is true is not reported speech. If words are cut off or mis-heard but the act can still be recognised, judge the act the words most likely carry; use UNCERTAIN only when it cannot be recovered. "
    "Report an objective that is SUPPORTED once only.")


def system_prompt(variant: str | None = None) -> str:
    variant = variant or VARIANT
    if variant == "full":
        lines = [f"{a['id']}: {a['definition']} Yes: \"{a['positive'][0]}\"" + (f" No: \"{a['negative'][0]}\"" if a["negative"] else "") for a in objectives()]
    else:
        lines = [f"{a['id']}: {a['definition']}" for a in objectives()]
    return (
        "You judge what a trainee clinician (the speaker) does with ONE spoken utterance in a simulated pre-operative consultation or handover. The text comes from speech recognition and may contain mis-heard words; read it for the meaning it most likely carries.\n"
        "For each objective decide: SUPPORTED = the speaker performs it themself, now, in this utterance; NOT_SUPPORTED = they do not; UNCERTAIN = the words are too damaged or ambiguous to tell.\n"
        "An objective is NOT_SUPPORTED when the utterance only denies it (modality negated), only plans it for later (intended), only imagines it (hypothetical), says someone else will do or did it (reported), is spoken by someone else (actor other), or breaks off before the act is done (incomplete). "
        "Fill modality (actual, negated, intended, hypothetical, reported, incomplete) and actor (trainee, other, unclear) for every finding.\n"
        f"Report at most {MAX_FINDINGS} objectives: those the utterance performs or that are UNCERTAIN, then (if room) an objective it names but does not perform. Leave out every other objective (they count as NOT_SUPPORTED). Small talk, thinking aloud or noise: report nothing. Answer as compact one-line JSON.\n"
        "evidence = words copied exactly from the utterance (empty for NOT_SUPPORTED if nothing fits). reason is one of: " + ", ".join(REASONS) + ".\n"
        "Objectives:\n" + "\n".join(lines) + ("\n" + TIE_BREAKS if variant in ("full", "lean", "clartb") else "") + ("\n" + CLARIFICATIONS if variant in ("clar", "clartb") else ""))


def schema(eligible_ids: list[str]) -> dict:
    return {"type": "object", "required": ["findings"], "additionalProperties": False, "properties": {"findings": {"type": "array", "maxItems": MAX_FINDINGS, "items": {
        "type": "object", "required": ["objective", "verdict", "actor", "modality", "evidence", "reason"], "additionalProperties": False, "properties": {
            "objective": {"enum": eligible_ids}, "verdict": {"enum": VERDICTS}, "actor": {"enum": ACTORS}, "modality": {"enum": MODALITIES},
            "evidence": {"type": "string", "maxLength": 120}, "reason": {"enum": REASONS}}}}}}


def eligible(step: int | None = None) -> list[dict]:
    """The eligible objectives at a moment, each with `in_window`. step=None (no scenario state) means every objective, all flagged in_window=None."""
    mapping = load_json("content/assessment/mapping.json")["behaviours"]
    window = {act for b in mapping if step is not None and step in b["steps"] for act in b["ai_acts"]}
    out = [{"id": a["id"], "in_window": (a["id"] in window) if step is not None else None} for a in objectives()]
    out.sort(key=lambda o: (o["in_window"] is False,))                       # stable: in-window objectives first
    return out


def user_prompt(utterance: str, context: list[str] | None = None, step: int | None = None, elig: list[dict] | None = None) -> str:
    parts = []
    if step is not None:
        parts.append(f"Scenario step {step}.")
    if elig and any(o["in_window"] is not None for o in elig):
        parts.append("Objectives expected at this step: " + ", ".join(o["id"] for o in elig if o["in_window"]) + ". Others are still valid if the utterance clearly performs them.")
    if context:
        parts.append("Earlier in the dialogue:\n" + "\n".join(f"- {c}" for c in context[-4:]))
    parts.append(f"Utterance: \"{utterance}\"")
    return "\n".join(parts)


def norm(t: str) -> str:
    return " ".join(re.sub(r"[^a-z0-9' ]+", " ", t.lower().replace("’", "'")).split())


def call(utterance: str, context: list[str] | None = None, step: int | None = None, elig: list[dict] | None = None, think: bool | None = False, timeout: float = 30.0,
         model: str = MODEL, keep_alive: str = "30m", system: str | None = None) -> dict:
    """One model call. Returns the raw answer and the host's own timings; never raises for an ordinary failure."""
    import httpx
    elig = elig or eligible(step)
    body = {"model": model, "stream": False, "keep_alive": keep_alive, "format": schema([o["id"] for o in elig]),
            "messages": [{"role": "system", "content": system or system_prompt()}, {"role": "user", "content": user_prompt(utterance, context, step, elig)}],
            "options": {"temperature": 0, "seed": 7, "num_predict": NUM_PREDICT, "num_ctx": NUM_CTX}}
    if think is not None:
        body["think"] = think
    t = time.perf_counter()
    try:
        r = httpx.post(URL + "/api/chat", json=body, timeout=timeout)
    except httpx.TimeoutException:
        return {"error": "TIMEOUT", "wall_ms": round((time.perf_counter() - t) * 1000)}
    except httpx.HTTPError as e:
        return {"error": "UNREACHABLE:" + type(e).__name__, "wall_ms": round((time.perf_counter() - t) * 1000)}
    wall = round((time.perf_counter() - t) * 1000)
    if r.status_code >= 400:
        return {"error": f"HTTP_{r.status_code}", "detail": r.text[:200], "wall_ms": wall}
    d = r.json()
    m = d.get("message") or {}
    return {"content": m.get("content", ""), "thinking": m.get("thinking", "") or "", "wall_ms": wall, "done_reason": d.get("done_reason"),
            "total_ms": round(d.get("total_duration", 0) / 1e6), "load_ms": round(d.get("load_duration", 0) / 1e6),
            "prompt_tokens": d.get("prompt_eval_count", 0), "prompt_ms": round(d.get("prompt_eval_duration", 0) / 1e6),
            "output_tokens": d.get("eval_count", 0), "gen_ms": round(d.get("eval_duration", 0) / 1e6)}


def evaluate(utterance: str, raw: dict, elig: list[dict]) -> dict:
    """Parse and validate one raw answer into {objective: finding} for ALL eligible objectives (omitted ones are NOT_SUPPORTED by the stated rule). `problems` lists every defect found; a defective finding is dropped, never repaired."""
    problems: list[str] = []
    ids = {o["id"] for o in elig}
    res = {i: {"verdict": "NOT_SUPPORTED", "reported": False} for i in ids}
    if raw.get("error"):
        return {"ok": False, "problems": [raw["error"]], "results": res}
    if raw.get("thinking"):
        problems.append("THINKING_PRESENT")
    try:
        doc = json.loads(raw["content"])
        findings = doc["findings"]
        assert isinstance(findings, list)
    except (ValueError, KeyError, AssertionError, TypeError):
        return {"ok": False, "problems": problems + ["NOT_JSON_OR_SCHEMA"], "results": res}
    seen = set()
    nu = norm(utterance)
    for f in findings[:MAX_FINDINGS]:
        try:
            o, v, a, mo, ev, rs = f["objective"], f["verdict"], f["actor"], f["modality"], f["evidence"], f["reason"]
        except (KeyError, TypeError):
            problems.append("FINDING_SCHEMA")
            continue
        if o not in ids or v not in VERDICTS or a not in ACTORS or mo not in MODALITIES or rs not in REASONS:
            problems.append("FINDING_ENUM")
            continue
        if o in seen:
            problems.append("DUPLICATE_OBJECTIVE")
            continue
        seen.add(o)
        ev_ok = len(norm(ev)) >= 2 and norm(ev) in nu
        if v == "SUPPORTED" and not ev_ok:
            problems.append("EVIDENCE_NOT_IN_UTTERANCE")
        if v == "SUPPORTED" and (a != "trainee" or mo != "actual"):
            problems.append("SUPPORTED_BUT_NOT_ACTUAL_TRAINEE")
        res[o] = {"verdict": v, "actor": a, "modality": mo, "evidence": ev, "evidence_in_utterance": ev_ok, "reason": rs, "reported": True}
    return {"ok": True, "problems": problems, "results": res}


def credit(utterance: str, ev: dict, objective: str, guards: bool) -> tuple[bool, str]:
    """Credit eligibility, kept apart from interpretation. Eligible only if the model's finding is SUPPORTED, by the trainee, as an actual (not negated, intended, hypothetical, reported or broken-off) act, with evidence that is a verbatim part of the utterance, for a NON-critical objective.
    With guards=True the production deterministic checks are also applied unchanged (critical-content cue; non-assertive sentence), which can only remove credit."""
    r = ev["results"].get(objective, {})
    if r.get("verdict") != "SUPPORTED":
        return False, "NOT_SUPPORTED_OR_UNCERTAIN"
    if r.get("actor") != "trainee" or r.get("modality") != "actual":
        return False, "NOT_ACTUAL_TRAINEE"
    if not r.get("evidence_in_utterance"):
        return False, "EVIDENCE_NOT_IN_UTTERANCE"
    if objective in critical_ids():
        return False, "CRITICAL_NEVER_FROM_SPEECH"
    if guards:
        from classify.assertion import non_assertive
        from classify.guard import critical_cue
        if critical_cue(utterance):
            return False, "CRITICAL_CUE"
        if (k := non_assertive(utterance)):
            return False, "NON_ASSERTIVE:" + k
    return True, ""


def run(split: str, tag: str, limit: int, resume: bool = True, timeout: float = 30.0, variant: str = "full", stride: int = 1) -> Path:
    import nli_zero_shot as N
    rows = N.load_rows(split)[::stride][: limit or None]
    OUT.mkdir(parents=True, exist_ok=True)
    p = OUT / f"{tag}-{split}.jsonl"
    done = {}
    if resume and p.exists():
        done = {json.loads(l)["id"]: 1 for l in p.read_text(encoding="utf-8").splitlines() if l.strip()}
    elig = eligible(None)
    system = system_prompt(variant)
    with p.open("a", encoding="utf-8", newline="\n") as fh:
        for n, r in enumerate(rows):
            if r["id"] in done:
                continue
            raw = call(r["text"], elig=elig, timeout=timeout, system=system)
            fh.write(json.dumps({"id": r["id"], "raw": raw}) + "\n")
            fh.flush()
            if n % 50 == 0:
                print(split, n, raw.get("wall_ms"), raw.get("error", ""), flush=True)
    return p


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    r = sub.add_parser("run")
    r.add_argument("--split", required=True, choices=["dev", "test", "test2"])
    r.add_argument("--tag", default="v1")
    r.add_argument("--limit", type=int, default=0)
    r.add_argument("--timeout", type=float, default=30.0)
    r.add_argument("--variant", default="full", choices=["full", "lean", "min", "clar", "clartb"])
    r.add_argument("--stride", type=int, default=1, help="every n-th row (variant selection on a dev subset)")
    sp = sub.add_parser("show-prompt")
    sp.add_argument("--variant", default="full", choices=["full", "lean", "min", "clar", "clartb"])
    a = ap.parse_args(argv)
    if a.cmd == "show-prompt":
        print(system_prompt(a.variant))
        return 0
    print(run(a.split, a.tag, a.limit, timeout=a.timeout, variant=a.variant, stride=a.stride))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
