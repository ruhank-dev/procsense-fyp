"""Acceptance run of the FRESH 32-item partial-credit set (DT-247 follow-up, ADR-0025): `server/.venv/Scripts/python.exe tools/synth_partial_v3/accept.py freeze | run | report`.

freeze  records, before any model call, the SHA-256 of the set, its manifest, the acceptance criteria, the prompt (version and hash), the reply contract (server/evaluate/contract.py) and the engine's span cap, and the model name in tests/synthetic_partial_v3/eval_config.json.
run     refuses unless the frozen configuration is intact and no scored run exists; asks the model (free plan, hard ceilings, stops on 401/402/403/429) once for the 32 items; stores every raw reply; writes RUN_LOCK.json. A second run is refused.
report  recomputes everything from the stored replies (credits come from the engine itself through the CLI), checks the criteria and writes reports/ai/speech-evaluation-partial-v3.json.
AI-written and AI-reviewed synthetic data: acceptance evidence for the software, not independent human validation."""
from __future__ import annotations

import hashlib
import json
import math
import os
import sys
from datetime import datetime, timezone
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
SET = ROOT / "tests" / "synthetic_partial_v3"
os.environ["SYNTH_SET_DIR"] = str(SET)
sys.path.insert(0, str(ROOT))
sys.path.insert(0, str(ROOT / "tools" / "synth_dev"))
CONFIG, LOCK, RAW = SET / "eval_config.json", SET / "RUN_LOCK.json", SET / "run_raw.json"
REPORT = ROOT / "reports" / "ai" / "speech-evaluation-partial-v3.json"
CODE = ["server/evaluate/prompt.py", "server/evaluate/examples.py", "server/evaluate/wording.json", "server/evaluate/contract.py", "server/evaluate/reader.py", "server/evaluate/rubric.py", "server/evaluate/provider.py", "server/evaluate/service.py",
        "content/assessment/mapping.json", "content/scoring/rubric.json", "src/ProcSense.Core/Scoring/Evidence.cs", "src/ProcSense.Core/Scoring/CommunicationScorer.cs", "src/ProcSense.Core/Speech/SpeechEvidenceSubsystem.cs",
        "tools/synth_dev/evaluator_run.py", "tools/synth_dev/engine_score.py"]
DATA = ["partial_credit.jsonl", "partial_credit.manifest.json", "acceptance.json"]


def sha(p: Path) -> str:
    return hashlib.sha256(p.read_bytes().replace(b"\r\n", b"\n")).hexdigest()


def wilson(k: int, n: int):
    if not n:
        return None
    z, p = 1.96, k / n
    d = 1 + z * z / n
    c = (p + z * z / (2 * n)) / d
    h = z * math.sqrt(p * (1 - p) / n + z * z / (4 * n * n)) / d
    return [round(max(0.0, c - h), 4), round(min(1.0, c + h), 4)]


def current(model: str) -> dict:
    from server.evaluate import prompt as P
    return {"prompt_version": P.VERSION, "prompt_hash": P.prompt_hash(), "model": model, "data_sha256": {f: sha(SET / f) for f in DATA}, "code_sha256": {f: sha(ROOT / f) for f in CODE}}


def acceptance() -> dict:
    return json.loads((SET / "acceptance.json").read_text(encoding="utf-8"))


def freeze(argv) -> int:
    if CONFIG.exists() or LOCK.exists():
        raise SystemExit("REFUSED: the configuration is already frozen")
    man = json.loads((SET / "partial_credit.manifest.json").read_text(encoding="utf-8"))
    if man["sha256"] != sha(SET / "partial_credit.jsonl") or man["n"] != 32:
        raise SystemExit("REFUSED: the set is not whole")
    rec = {**current(acceptance()["run"]["model"]), "frozen_utc": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"), "note": "frozen before any model call on this set"}
    CONFIG.write_text(json.dumps(rec, indent=1) + "\n", encoding="utf-8", newline="\n")
    print("froze", CONFIG.relative_to(ROOT).as_posix(), rec["prompt_version"], rec["prompt_hash"][:12])
    return 0


def run(argv) -> int:
    if LOCK.exists():
        raise SystemExit("REFUSED: the set was already scored once (RUN_LOCK.json)")
    if not CONFIG.exists():
        raise SystemExit("REFUSED: freeze the configuration first")
    frozen, cfg = json.loads(CONFIG.read_text(encoding="utf-8")), acceptance()["run"]
    now = current(cfg["model"])
    drift = [k for k in ("prompt_version", "prompt_hash", "model", "data_sha256", "code_sha256") if now[k] != frozen[k]]
    if drift:
        raise SystemExit("REFUSED: changed after it was frozen: " + ", ".join(drift))
    import evaluator_run as ER
    rows = [json.loads(l) for l in (SET / "partial_credit.jsonl").read_text(encoding="utf-8").splitlines() if l.strip()]
    for it in rows:
        it["context"] = ER.context_of(it["spec"], it.get("verified"))
    d = ER.run(rows, cfg["max_calls"], cfg["max_tokens"], workers=cfg["workers"], model=cfg["model"])
    RAW.write_text(json.dumps(d, ensure_ascii=False), encoding="utf-8")
    LOCK.write_text(json.dumps({"scored_utc": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"), "prompt_hash": d["prompt_hash"], "calls": d["calls"], "tokens": d["tokens"], "stopped": d["stopped"]}, indent=1) + "\n", encoding="utf-8", newline="\n")
    st = {}
    for v in d["results"].values():
        st[v["status"]] = st.get(v["status"], 0) + 1
    print(json.dumps({"calls": d["calls"], "tokens": d["tokens"], "stopped": d["stopped"], "status": st, "wall_s": d["wall_s"]}))
    return 0


def report(argv) -> int:
    if not LOCK.exists():
        raise SystemExit("REFUSED: there is no scored run")
    import engine_score as ES
    from evaluator_run import context_of
    dp = json.loads(RAW.read_text(encoding="utf-8"))
    rows = [json.loads(l) for l in (SET / "partial_credit.jsonl").read_text(encoding="utf-8").splitlines() if l.strip()]
    items = []
    for r in rows:
        ev = dp["results"][r["id"]]["evaluation"]
        c = context_of(r["spec"])
        acts = [] if not ev else [{"act": a["objective"], "assertion": a["assertion"], "actor": a["actor"], "form": a["form"], "relevance": a["relevance"], "correctness": a["correctness"], "completeness": a["completeness"],
                                    "span": a["evidence"] or a["objective"], "claims_physical": ev["claims_physical_action"]} for a in ev["acts"]]
        items.append({"id": r["id"], "steps": c["steps"], "patient_question_pending": c["patient_question_pending"], "readback_pending": c["readback_pending"], "acts": acts})
    eng = {x["id"]: x for x in ES.score(items)}
    out, per_band = [], {}
    for r in rows:
        res = dp["results"][r["id"]]
        total = eng[r["id"]]["communication_total"]
        lo, hi = r["expected_score"]
        inside = total >= lo if r["band"] == "full" else lo <= total <= hi
        per_band.setdefault(r["band"], []).append(total)
        ev = res["evaluation"]
        out.append({"id": r["id"], "objective": r["objective"], "band": r["band"], "long": r["long"], "chars": len(r["utterance"]), "score": total, "in_band": inside, "status": res["status"], "codes": res["codes"],
                    "own_grades": next(([a["relevance"], a["correctness"], a["completeness"]] for a in (ev["acts"] if ev else []) if a["objective"] == r["objective"]), None),
                    "other_objectives": [a["objective"] for a in (ev["acts"] if ev else []) if a["objective"] != r["objective"]], "wall_ms": res["wall_ms"], "utterance": r["utterance"]})
    n_in = sum(x["in_band"] for x in out)
    length_rej = sum(1 for x in out if any("text fields" in c for c in x["codes"]) and x["status"] != "VALID")
    values = {"partial_in_band_rate": n_in / len(out), "evidence_length_rejections": length_rej}
    ops = {"==": lambda a, b: a == b, ">=": lambda a, b: a >= b, "<=": lambda a, b: a <= b}
    acc = acceptance()
    results = [{**c, "observed": round(values[c["metric"]], 4), "passed": ops[c["op"]](values[c["metric"]], c["value"])} for c in acc["criteria"]]
    verdict = "APPROVED" if all(r["passed"] for r in results) else "NOT APPROVED (owner decision)"
    walls = sorted(x["wall_ms"] for x in out)
    rep = {"set": "synthetic_partial_v3", "kind": acc["evidence_class"], "run_utc": json.loads(LOCK.read_text(encoding="utf-8"))["scored_utc"], "prompt_version": dp["prompt_version"], "prompt_hash": dp["prompt_hash"], "model": dp["model"],
           "verdict": verdict, "criteria": results, "wilson95_in_band": wilson(n_in, len(out)), "in_band": n_in, "n": len(out),
           "in_band_by_band": {b: [sum(x["in_band"] for x in out if x["band"] == b), len([x for x in out if x["band"] == b])] for b in per_band}, "long_items": {"n": sum(x["long"] for x in out), "in_band": sum(x["in_band"] for x in out if x["long"]), "valid": sum(1 for x in out if x["long"] and x["status"] == "VALID")},
           "valid_replies": [sum(1 for x in out if x["status"] == "VALID"), len(out)], "mean_score_by_band": {b: round(sum(v) / len(v), 3) for b, v in per_band.items()}, "items": out,
           "latency_ms": {"median": walls[len(walls) // 2], "max": walls[-1]}, "free_plan": {"calls": dp["calls"], "tokens": dp["tokens"], "stopped": dp["stopped"], "paid_api": False},
           "caveat": "AI-written and AI-reviewed synthetic data; acceptance evidence for the software. The real Meta Quest and ASR end-to-end validation (DC-10, Sannan) is still to be done."}
    REPORT.parent.mkdir(parents=True, exist_ok=True)
    REPORT.write_text(json.dumps(rep, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    print(json.dumps({"verdict": verdict, "criteria": [(r["id"], r["observed"], r["passed"]) for r in results], "in_band_by_band": rep["in_band_by_band"], "valid": rep["valid_replies"], "long": rep["long_items"]}))
    return 0


def main(argv):
    cmd = argv[0] if argv else ""
    if cmd not in ("freeze", "run", "report"):
        print(__doc__)
        return 2
    return {"freeze": freeze, "run": run, "report": report}[cmd](argv[1:])


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
