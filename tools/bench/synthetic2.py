"""The SECOND synthetic communication-act dataset, AI-judged (tests/classifier_synthetic_v2): written by the language model that built this tooling and labelled by AI judges, NOT by people.

    uv run --project tools tools/bench/synthetic2.py sources                 # structure, scenario-group and leakage checks on the authored files (no model needed except --cosine)
    uv run --project tools tools/bench/synthetic2.py judge J1 [--split S]    # run one judge configuration over the sentences (blind: text only, never the intended act); resumable
    uv run --project tools tools/bench/synthetic2.py agreement               # agreement between judges and with the author's intent; writes contested.txt for adjudication
    uv run --project tools tools/bench/synthetic2.py freeze                  # merge intent, judgements and adjudications; write train/dev/test.jsonl and manifest.json
    uv run --project tools tools/bench/synthetic2.py check                   # fail if the committed files differ from what the sources give

What it is: 1,860 sentences from 12 simulated author profiles (6 train, 3 dev, 3 test, no profile in two splits) for the 20 labels of content/assessment/taxonomy.json, in seven buckets (plain,
negation, correction, incomplete, ambiguous, recogniser-style noise). Sentences are about different scenario groups in each split (train G1-G4: ward, abdominal pain, day-case, medicines; dev G5-G6: recovery,
theatre equipment and handover; test G7-G8: airway and induction, pregnancy, transfusion, diabetes), so the test split also shifts topic, names and drugs.

Labels: each sentence has the label its simulated author meant (the intent). Two AI judge configurations then label every sentence blind, from a written rubric (rubric.md, which carries the tie-break order of the
annotation guide): J1 and J2 differ in prompt layout, label order, examples and sampling. A row is CERTAIN when both judges agree with the intent, UNCERTAIN when the judges split, and CONTESTED when both
judges agree with each other against the intent; every contested row is read and settled in adjudication.txt by the language model that built this tooling (blind to the intent when it reads it). The final
label is the adjudication if there is one, else the intent.

What it is NOT: independent human annotation. The author, the judges' rubric and the adjudicator are all machine-made; agreement between judges is a consistency check and not proof that a label is right (two
models can share a mistake). No person wrote, labelled or reviewed any row. Every result file and the manifest carry `independent: false` and `valid_for_claims: false`.
"""
from __future__ import annotations

import hashlib
import json
import random
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "bench"))
sys.path.insert(0, str(ROOT / "server"))
import dataset as DS  # noqa: E402

OUT = ROOT / "tests" / "classifier_synthetic_v2"
BUCKET = {"p": "plain", "n": "negation", "c": "correction", "i": "incomplete", "a": "ambiguous", "s": "asr_style"}
PROFILES = {
    "R1": ("train", "junior doctor on call: clipped, abbreviations, imperatives"), "R2": ("train", "nurse practitioner: warm, long, explanatory sentences"),
    "R3": ("train", "anaesthetic technician: practical, equipment-focused, informal"), "R4": ("train", "final-year medical student: textbook, formal phrasing"),
    "R5": ("train", "senior registrar: calm, concise, complete"), "R6": ("train", "second-language clinician: simple grammar, dropped articles"),
    "D1": ("dev", "exhausted night-shift clinician: fragments, trailing off"), "D2": ("dev", "formal senior consultant: long, passive, courteous"),
    "D3": ("dev", "nervous first-week trainee: apologetic, hedging"),
    "E1": ("test", "brusque surgeon: curt, imperative, minimal"), "E2": ("test", "chatty ward nurse: warm, digressive, endearments"),
    "E3": ("test", "second-language clinician (other mother tongue): hesitant, unusual word order"),
}
GROUPS = {"train": {"G1", "G2", "G3", "G4"}, "dev": {"G5", "G6"}, "test": {"G7", "G8"}}
PER_CELL = {"train": 7, "dev": 7, "test": 10}
PREFIX = {"train": "V2T", "dev": "V2D", "test": "V2E"}
CONFIGS = {"J1": "rubric, zero-shot, temperature 0, labels in taxonomy order", "J2": "rubric reworded with one example per label, labels in a fixed shuffled order, temperature 0.3, seed 7"}
JUDGE_MODEL = "qwen3.5:4b-q4_K_M"


def source_files(split: str) -> list[Path]:
    return sorted(OUT.glob(f"authored_{split}_*.txt"))


def rows() -> dict[str, list[dict]]:
    tax = {a["id"] for a in DS.taxonomy()}
    out: dict[str, list[dict]] = {}
    for split in ("train", "dev", "test"):
        rs = []
        for f in source_files(split):
            profile = f.stem.rsplit("_", 1)[1]
            if PROFILES[profile][0] != split:
                raise SystemExit(f"{f.name}: profile {profile} belongs to {PROFILES[profile][0]}, not {split}")
            for line in f.read_text(encoding="utf-8").splitlines():
                if not line.strip():
                    continue
                label, bucket, group, text = line.split("|", 3)
                if label not in tax or bucket not in BUCKET or group not in GROUPS[split]:
                    raise SystemExit(f"{f.name}: bad label, bucket or group in: {line[:60]}")
                rs.append({"text": text.strip(), "intended_act": label, "author": profile, "bucket": BUCKET[bucket], "scenario_group": group})
        for n, r in enumerate(rs, 1):
            r["id"] = f"{PREFIX[split]}{n:04d}"
        out[split] = rs
    return out


def structure_problems(data: dict[str, list[dict]]) -> list[str]:
    tax = [a["id"] for a in DS.taxonomy()]
    problems = []
    for split, rs in data.items():
        authors = {r["author"] for r in rs}
        if authors != {p for p, (s, _) in PROFILES.items() if s == split}:
            problems.append(f"{split}: authors {sorted(authors)} are not the profiles of the split")
        for a in authors:
            for lab in tax:
                n = sum(1 for r in rs if r["author"] == a and r["intended_act"] == lab)
                if n != PER_CELL[split]:
                    problems.append(f"{split}/{a}/{lab}: {n} rows, expected {PER_CELL[split]}")
        if len({r["text"].lower() for r in rs}) != len(rs):
            problems.append(f"{split}: duplicate sentences")
    for a in data:
        for b in data:
            if a < b and ({r["author"] for r in data[a]} & {r["author"] for r in data[b]} or {r["scenario_group"] for r in data[a]} & {r["scenario_group"] for r in data[b]}):
                problems.append(f"{a} and {b} share an author profile or a scenario group")
    return problems


def leakage(data: dict[str, list[dict]], cosine: bool) -> dict:
    """Near-duplicates (token Jaccard >= 0.8) between every pair of splits, and, with --cosine, embedding paraphrases (cosine >= 0.9 listed, >= 0.95 refused)."""
    out = {"jaccard_pairs": {}, "cosine_review": {}, "cosine_refused": {}, "first3_overlap": {}}
    names = list(data)
    for i, a in enumerate(names):
        for b in names[i + 1:]:
            hits = DS.near_duplicates([r["text"] for r in data[a]], [r["text"] for r in data[b]])
            out["jaccard_pairs"][f"{a}~{b}"] = [(data[a][x]["id"], data[b][y]["id"], round(s, 3)) for x, y, s in hits]
    for a, b in (("train", "dev"), ("train", "test"), ("dev", "test")):
        first3 = lambda t: " ".join(t.lower().split()[:3])                   # noqa: E731
        left = {first3(r["text"]) for r in data[a] if len(r["text"].split()) >= 4}
        out["first3_overlap"][f"{a}~{b}"] = round(sum(first3(r["text"]) in left for r in data[b] if len(r["text"].split()) >= 4) / len(data[b]), 3)
    if cosine:
        import bench_classifier as BC
        emb = {s: BC.embed([r["text"] for r in rs], "all-minilm:22m", "http://127.0.0.1:11434") for s, rs in data.items()}
        for a, b in (("train", "dev"), ("train", "test"), ("dev", "test")):
            review, refused = [], []
            for j, vb in enumerate(emb[b]):
                best = max(((sum(x * y for x, y in zip(va, vb)), i) for i, va in enumerate(emb[a])))
                if best[0] >= 0.9:
                    (refused if best[0] >= 0.95 else review).append((data[b][j]["id"], data[a][best[1]]["id"], round(best[0], 3)))
            out["cosine_review"][f"{a}~{b}"], out["cosine_refused"][f"{a}~{b}"] = review, refused
    return out


# ---------------------------------------------------------------- judges
def rubric_text() -> str:
    return (OUT / "rubric.md").read_text(encoding="utf-8")


def judge_prompt(config: str) -> tuple[str, dict, dict]:
    """(system prompt, JSON schema, sampling options) of one judge configuration."""
    tax = DS.taxonomy()
    ids = [a["id"] for a in tax]
    if config == "J2":
        ids = random.Random(11).sample(ids, len(ids))
    by = {a["id"]: a for a in tax}
    lines = []
    for i in ids:
        a = by[i]
        if config == "J1":
            lines.append(f"- {i}: {a['definition']}")
        else:
            ex = a["positive"][0] if a["positive"] else "(small talk, a noise or thinking aloud)"
            lines.append(f"{i} = {a['definition']} Example: \"{ex}\"")
    rub = rubric_text()
    system = ("You label one sentence said by a clinician in a hospital pre-operative setting with the single communication act it performs. Use ONLY the label list and the rules below. "
              "Judge the sentence on its own words. Answer with JSON: the act id and your confidence between 0 and 1.\n\nLABELS\n" + "\n".join(lines) + "\n\nRULES\n" + rub)
    schema = {"type": "object", "required": ["act", "confidence"], "additionalProperties": False, "properties": {"act": {"enum": ids}, "confidence": {"type": "number", "minimum": 0, "maximum": 1}}}
    options = {"temperature": 0, "top_k": 1, "seed": 7} if config == "J1" else {"temperature": 0.3, "seed": 7, "top_k": 20}
    return system, schema, options


def run_judge(config: str, split: str, limit: int = 0) -> None:
    from llm import client as L
    data = rows()[split]
    path = OUT / "judgements" / f"{config}_{split}.jsonl"
    path.parent.mkdir(exist_ok=True)
    done = {json.loads(line)["id"] for line in path.read_text(encoding="utf-8").splitlines() if line.strip()} if path.exists() else set()
    client = L.OllamaClient()
    system, schema, options = judge_prompt(config)
    todo = [r for r in data if r["id"] not in done][: limit or None]
    started = time.monotonic()
    with path.open("a", encoding="utf-8", newline="\n") as f:
        for n, r in enumerate(todo, 1):
            rep = client.chat(JUDGE_MODEL, system, f"Sentence: {r['text']}", schema=schema, think=False, timeout=90, num_predict=60, options=options)
            act, conf = None, None
            if rep.ok:
                try:
                    v = json.loads(rep.text)
                    act, conf = v["act"], float(v["confidence"])
                except (ValueError, KeyError):
                    pass
            f.write(json.dumps({"id": r["id"], "judge": config, "act": act, "confidence": conf, "ms": rep.latency_ms}, sort_keys=True) + "\n")
            f.flush()
            if n % 50 == 0:
                print(f"  {config} {split}: {n}/{len(todo)} in {round(time.monotonic() - started)} s", flush=True)


def judgements() -> dict[str, dict[str, dict]]:
    out: dict[str, dict[str, dict]] = {}
    for p in sorted((OUT / "judgements").glob("*.jsonl")) if (OUT / "judgements").exists() else []:
        cfg = p.stem.split("_")[0]
        for line in p.read_text(encoding="utf-8").splitlines():
            if line.strip():
                r = json.loads(line)
                out.setdefault(cfg, {})[r["id"]] = r
    return out


def classify_rows(data: dict[str, list[dict]], judged: dict[str, dict[str, dict]]) -> dict[str, str]:
    """certain | uncertain | contested | unjudged for each row id."""
    status = {}
    for split, rs in data.items():
        for r in rs:
            j1, j2 = judged.get("J1", {}).get(r["id"], {}).get("act"), judged.get("J2", {}).get(r["id"], {}).get("act")
            if j1 is None and j2 is None and split == "train":
                status[r["id"]] = "intent_only"                                  # training rows are not judged (GPU time): their label is the author's intent, and the manifest says so
            elif j1 is None or j2 is None:
                status[r["id"]] = "unjudged"
            elif j1 == j2 == r["intended_act"]:
                status[r["id"]] = "certain"
            elif j1 == j2:
                status[r["id"]] = "contested"
            else:
                status[r["id"]] = "uncertain"
    return status


def agreement() -> dict:
    data, judged = rows(), judgements()
    status = classify_rows(data, judged)
    out = {"synthetic": True, "independent": False, "valid_for_claims": False, "judge_model": JUDGE_MODEL, "configs": CONFIGS, "rubric_sha256": hashlib.sha256(rubric_text().encode("utf-8")).hexdigest(), "splits": {}}
    contested = []
    for split, rs in data.items():
        by = {r["id"]: r for r in rs}
        both = [r["id"] for r in rs if status[r["id"]] not in ("unjudged", "intent_only")]
        j1 = [judged["J1"][i]["act"] for i in both]
        j2 = [judged["J2"][i]["act"] for i in both]
        intent = [by[i]["intended_act"] for i in both]
        counts = {k: sum(1 for i in both if status[i] == k) for k in ("certain", "uncertain", "contested")}
        by_bucket = {}
        for i in both:
            b = by[i]["bucket"]
            by_bucket.setdefault(b, {"rows": 0, "certain": 0})
            by_bucket[b]["rows"] += 1
            by_bucket[b]["certain"] += status[i] == "certain"
        if not both:
            out["splits"][split] = {"rows": len(rs), "judged": 0, "note": "not judged: the label is the author's intent"}
            continue
        out["splits"][split] = {"rows": len(rs), "judged": len(both), "kappa_J1_J2": DS.kappa(j1, j2) if both else None, "kappa_J1_intent": DS.kappa(j1, intent) if both else None,
                                "kappa_J2_intent": DS.kappa(j2, intent) if both else None, "raw_agreement_J1_J2": round(sum(a == b for a, b in zip(j1, j2)) / len(both), 3) if both else None,
                                "raw_agreement_judges_with_intent": round(sum(a == b == c for a, b, c in zip(j1, j2, intent)) / len(both), 3) if both else None, **counts, "by_bucket": by_bucket}
        contested += [(i, by[i]["text"], judged["J1"][i]["act"], judged["J2"][i]["act"]) for i in both if status[i] == "contested"]
    out["note"] = "Agreement between two configurations of the same judge model is a consistency check, not evidence that a label is right."
    (OUT / "agreement.json").write_text(json.dumps(out, indent=1) + "\n", encoding="utf-8", newline="\n")
    (OUT / "contested.txt").write_text("".join(f"{i}|{t}|{a}|{b}\n" for i, t, a, b in contested), encoding="utf-8", newline="\n")
    return out


def adjudications() -> dict[str, tuple[str, str]]:
    p = OUT / "adjudication.txt"
    out = {}
    for line in p.read_text(encoding="utf-8").splitlines() if p.exists() else []:
        if line.strip():
            i, lab, why = line.split("|", 2)
            out[i] = (lab, why)
    return out


def build() -> dict[str, str]:
    data, judged, adj = rows(), judgements(), adjudications()
    status = classify_rows(data, judged)
    if any(s == "unjudged" for s in status.values()):
        raise SystemExit("not every row has both judgements: run `judge J1` and `judge J2` for every split")
    tax = {a["id"]: a for a in DS.taxonomy()}
    missing = [i for i, s in status.items() if s == "contested" and i not in adj]
    if missing:
        raise SystemExit(f"{len(missing)} contested rows have no adjudication (see contested.txt): {missing[:5]}")
    manifest = {"synthetic": True, "independent": False, "valid_for_claims": False, "ai_judged": True,
                "note": "Written by the language model that built the tooling and labelled by two AI judge configurations of one local model; contested rows settled by that same builder. No person wrote, labelled or reviewed any row.",
                "personas": {p: d for p, (_, d) in PROFILES.items()}, "scenario_groups": {k: sorted(v) for k, v in GROUPS.items()}, "judge_model": JUDGE_MODEL, "judge_configs": CONFIGS,
                "rubric_sha256": hashlib.sha256(rubric_text().encode("utf-8")).hexdigest(), "files": {}, "rows": {}, "authors": {}, "status": {}, "adjudicated": {}, "changed_by_adjudication": {}}
    files = {}
    for split, rs in data.items():
        for r in rs:
            s = status[r["id"]]
            final = adj[r["id"]][0] if r["id"] in adj else r["intended_act"]
            both = s != "intent_only"
            r.update({"act": final, "judge_1": judged["J1"][r["id"]]["act"] if both else None, "judge_2": judged["J2"][r["id"]]["act"] if both else None, "judge_status": "adjudicated" if r["id"] in adj else s, "critical": tax[final]["critical"],
                      "synthetic": True, "independent": False, "persona": PROFILES[r["author"]][1]})
        text = "".join(json.dumps(r, ensure_ascii=False, sort_keys=True) + "\n" for r in rs)
        files[f"{split}.jsonl"] = text
        manifest["files"][f"{split}.jsonl"] = hashlib.sha256(text.encode("utf-8")).hexdigest()
        manifest["rows"][split] = len(rs)
        manifest["authors"][split] = sorted({r["author"] for r in rs})
        manifest["status"][split] = {k: sum(1 for r in rs if r["judge_status"] == k) for k in ("certain", "uncertain", "adjudicated", "intent_only")}
        manifest["changed_by_adjudication"][split] = sum(1 for r in rs if r["act"] != r["intended_act"])
    manifest["leakage"] = leakage(data, False)
    if any(v for v in manifest["leakage"]["jaccard_pairs"].values()):
        raise SystemExit("REFUSED: near-duplicate sentences across splits: " + json.dumps(manifest["leakage"]["jaccard_pairs"])[:400])
    manifest["agreement_sha256"] = hashlib.sha256((OUT / "agreement.json").read_bytes()).hexdigest() if (OUT / "agreement.json").exists() else None
    files["manifest.json"] = json.dumps(manifest, indent=1, ensure_ascii=False) + "\n"
    return files


def main(argv: list[str]) -> int:
    cmd = argv[0] if argv else "check"
    if cmd == "sources":
        data = rows()
        problems = structure_problems(data)
        lk = leakage(data, "--cosine" in argv)
        for k, v in lk["jaccard_pairs"].items():
            if v:
                problems.append(f"near-duplicates {k}: {v[:6]}")
        for k, v in lk["cosine_refused"].items():
            if v:
                problems.append(f"embedding paraphrases >= 0.95 {k}: {v[:6]}")
        print(json.dumps({k: {a: (len(b) if isinstance(b, list) else b) for a, b in v.items()} for k, v in lk.items()}, indent=1))
        print("\n".join(["PROBLEMS:"] + problems) if problems else f"sources ok: {({s: len(r) for s, r in data.items()})}")
        return 1 if problems else 0
    if cmd == "judge":
        cfg = argv[1]
        splits = [argv[argv.index("--split") + 1]] if "--split" in argv else ["dev", "test", "train"]
        for s in splits:
            run_judge(cfg, s, int(argv[argv.index("--limit") + 1]) if "--limit" in argv else 0)
        return 0
    if cmd == "agreement":
        print(json.dumps(agreement(), indent=1))
        return 0
    files = build()
    if cmd == "freeze":
        for n, t in files.items():
            (OUT / n).write_text(t, encoding="utf-8", newline="\n")
        print("frozen:", json.loads(files["manifest.json"])["status"])
        return 0
    stale = [n for n, t in files.items() if not (OUT / n).exists() or (OUT / n).read_text(encoding="utf-8") != t]
    print("FAIL out of date: " + ", ".join(stale) if stale else "synthetic v2 dataset is current (model-written, AI-judged, NOT independent)")
    return 1 if stale else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
