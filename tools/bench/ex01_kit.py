"""Kit for the independent human-labelled evaluation set of the semantic reader (EX-01, phase two; plan in docs/testing/ex01_semantic_evaluation_plan.md):
`uv run --project tools tools/bench/ex01_kit.py brief|check-authors|package|check-annotator|agree|merge|freeze|verify ...`.

Nothing here writes a sentence or a label, and nothing here sees a model's output, a probability, a guard decision or an existing benchmark label. Every sentence is an author's; every label is an annotator's or the adjudicator's; every role is the owner's record.

  author-packages --authors N --out DIR   writes one folder per author (the brief, personal instructions with the author's own quota per family, and a pre-structured author_template.csv: item id, author code and family only, NO sentence); combine --files F1 F2 ... --out authors.csv joins the returned files.
  summary --dir DIR --roles R.json   the dataset and reviewer summary for the owner BEFORE the freeze (counts, composition, disagreements, adjudication outcomes, definition-insufficient cases, leak check, roles, independence checklist).
  brief   --out DIR            writes AUTHOR_BRIEF.md (the situations to cover, made from tools/bench/ex01_design.json, the approved definitions and the scenario rules; NO example sentence), author_template.csv, annotator_template.csv, roles_record.template.json and specification_issues.template.csv.
  check-authors  --file F      validates the authors' file: columns, family ids, coverage against the design minimums, step numbers, prior turns, the pending-state flags, the real-recording rule for ASR wording, duplicates, and any sentence already in a repository set (leak).
  package --authors F --out DIR [--seed N]   writes one BLIND file per annotator (opaque ids, own shuffled order; the item, the scene and the earlier turns only: no family, no intended act, no label) and id_map.json (kept by the owner, never given to an annotator).
  check-annotator --file F --package A_or_B_file   validates one annotator's returned file against the blind file it came from.
  agree   --a FILE --b FILE    per field raw agreement and Cohen's kappa, the disagreements, and every item either annotator marked `definition_insufficient`.
  merge   --a --b --adjudicated --authors --id-map --out DIR   writes items.csv (agreed or adjudicated rows only), excluded.csv (ambiguous, definition insufficient or awaiting an adjudicator, with BOTH annotators' labels), disagreements.csv (every disagreement is kept, never forced) and specification_issues.csv. A label is never invented.
  freeze  --dir DIR --roles R.json [--out DIR2]   refuses unless the owner's roles record shows enough DIFFERENT people (at least two authors, two annotators, one adjudicator, none of them a builder), coverage minimums are met and nothing is awaiting adjudication; copies the set to tests/ex01_semantic and writes the manifest with SHA-256 of every file.
  verify  [--dir DIR]          recomputes the hashes against the manifest (a set that changed after it was frozen is refused by the evaluator).
"""
from __future__ import annotations

import argparse
import collections
import csv
import hashlib
import json
import random
import re
import shutil
import sys
from datetime import datetime, timezone
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "tools" / "bench"))
import rubric_labelling_kit as K  # noqa: E402

DESIGN = json.loads((ROOT / "tools" / "bench" / "ex01_design.json").read_text(encoding="utf-8"))
MAPPING = json.loads((ROOT / "content" / "assessment" / "mapping.json").read_text(encoding="utf-8"))
FAMILIES = {f["id"]: f for f in DESIGN["families"]}
DEFAULT_SET = ROOT / "tests" / "ex01_semantic"
AUTHOR_COLUMNS = ["item_id", "author_code", "family", "scenario_step", "scenario_context", "prior_turns", "speaker_role", "utterance", "patient_question_pending", "readback_pending", "asr_source", "author_intended_acts"]
VIEW = ["item_id", "scenario_step", "scenario_context", "prior_turns", "speaker_role", "utterance"]
LABELS = ["acts_performed", "actor", "modality", "evidence", "credit_eligible", "ambiguous", "definition_insufficient", "note"]
ANNOT_COLUMNS = VIEW + LABELS
COMPARE = ["acts_performed", "actor", "modality", "credit_eligible", "ambiguous", "definition_insufficient"]
SPEAKERS = {"trainee", "patient", "colleague", "tutor"}
FILES = ["items.csv", "excluded.csv", "disagreements.csv", "specification_issues.csv", "agreement.json"]


def sha(p: Path) -> str:
    return hashlib.sha256(p.read_bytes()).hexdigest()


def read(p) -> list[dict]:
    with open(p, encoding="utf-8", newline="") as fh:
        return list(csv.DictReader(fh))


def write(p, cols, rows) -> None:
    with open(p, "w", encoding="utf-8", newline="") as fh:
        w = csv.DictWriter(fh, fieldnames=cols)
        w.writeheader()
        w.writerows({c: r.get(c, "") for c in cols} for r in rows)


def window_of(act: str) -> set[int]:
    return {s for b in MAPPING["behaviours"] if act in b["ai_acts"] for s in b["steps"]}


def acts_field(v: str) -> set[str]:
    return {x.strip() for x in (v or "").split(";") if x.strip()}


# ---------------------------------------------------------------------------------------------------- authors
def brief_text() -> str:
    L = ["# Author brief: independent evaluation set for the semantic reader (EX-01)\n",
         "Write NEW fictional dialogue of a simulated pre-operative consultation or handover. You will not be shown any existing sentence, any prompt, any model answer or any label, and you must not look for them. No real patient or staff, no clinical rule, dose or threshold of your own: use only the approved action definitions and the scenario steps below.\n",
         "**Scope of this evaluation:** it measures how well a reader understands what a trainee meant from clean written text, independently of speech-recognition quality. Real speech-recognition wording (family F17) is NOT part of it; it will be evaluated later from real recordings. Write clean, natural text: no deliberate spelling damage of any kind.\n",
         "**Do not look for, copy, or rewrite any existing benchmark sentence.** You are not shown any; write from the situations below in your own words. A sentence that matches or nearly matches a repository sentence is refused by the owner's check and has to be replaced.\n",
         "You write sentences; you do **not** label them. Two other people label every item without seeing your family tag or your intended act. Write each sentence the way a person says it, in your own words. **Do not invent typos, swapped letters or glued words**: spoken-language wording is welcome, deliberate spelling damage is not, and `asr_source` stays empty.\n",
         "## The approved actions (content/assessment/taxonomy.json)\n"]
    tax = json.loads((ROOT / "content" / "assessment" / "taxonomy.json").read_text(encoding="utf-8"))["acts"]
    for a in tax:
        w = sorted(window_of(a["id"]))
        L.append(f"* `{a['id']}`{' (critical)' if a['critical'] else ''}: {a['definition']}" + (f" Steps where it counts: {', '.join(map(str, w))}." if w else ""))
    L.append("\n## The situations you must cover (items per family)\n")
    L.append(f"The set is {DESIGN['target_items_total']} items in all, written by at least {DESIGN['min_authors']} authors, with the number of items per family shown below (your own share is in your personal instructions). Give every item exactly ONE family (the one that describes why the sentence is interesting).\n")
    L.append("| family | name | items | what to write |\n|---|---|---|---|")
    for f in DESIGN["families"]:
        L.append(f"| {f['id']} | {f['name']} | {f['min']} | {f['what']} |")
    L.append("\nThe quotas weight the safety-sensitive and historically ambiguous families more heavily; they are not built around any particular failure.")
    L.append("\n## The columns of your file (`author_template.csv`)\n")
    for k, v in DESIGN["item_rules"].items():
        L.append(f"* `{k}`: {v}")
    L.append("* `item_id`: your own running number; `family`: F01 to F17; `scenario_context`: one or two lines saying where the scene is and who is present; `utterance`: the one sentence to be judged.\n")
    L.append("## What happens next\nThe owner removes your name, shuffles the items and gives each annotator a blind file with only the scene, the earlier turns and the sentence. Where the two annotators differ, an adjudicator decides, and a sentence that the approved definitions cannot settle is recorded as a specification issue, not as a failure of any system. Nobody changes a sentence or a label after the set is frozen.")
    return "\n".join(L) + "\n"


def cmd_brief(out: Path) -> None:
    out.mkdir(parents=True, exist_ok=True)
    (out / "AUTHOR_BRIEF.md").write_text(brief_text(), encoding="utf-8", newline="\n")
    for name, cols in (("author_template.csv", AUTHOR_COLUMNS), ("annotator_template.csv", ANNOT_COLUMNS), ("specification_issues.template.csv", ["item_id", "flagged_by", "why_the_definitions_do_not_settle_it", "suggested_clarification"])):
        write(out / name, cols, [])
    (out / "roles_record.template.json").write_text(json.dumps({
        "set": "ex01-semantic", "attested_by": "<the owner's name>", "attested_on": "<YYYY-MM-DD>",
        "authors": ["<person>", "<person>"], "annotators": ["<person A>", "<person B>"], "adjudicator": "<person>", "builder_names": ["<every person who built or tuned the evaluator, the prompts or the verifier>"],
        "statement": "I confirm that these are different people, that none of the authors or annotators saw an existing benchmark sentence, a prompt, a model answer, a probability, a guard decision or another person's labels, and that no builder wrote or labelled an item. Names are real; none is invented.",
        "notes": ""}, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    print("wrote", out)


# ---------------------------------------------------------------------------------------------------- authors check
_KNOWN: list = []
_KNOWN_SET: set = set()


def known_cached() -> set:
    if not _KNOWN_SET:
        _KNOWN_SET.update(K.known_sentences())
    return _KNOWN_SET


def known_sets_cached() -> list:
    if not _KNOWN:
        _KNOWN.extend((frozenset(k.split()), k) for k in known_cached() if len(k.split()) >= 4)
    return _KNOWN


def near_duplicate(text: str, known_sets: list, threshold: float = 0.7) -> bool:
    w = frozenset(K.norm(text).split())
    if len(w) < 4:
        return False
    for kw, _ in known_sets:
        inter = len(w & kw)
        if inter and inter / len(w | kw) >= threshold:
            return True
    return False


def check_authors(rows: list[dict]) -> tuple[list[str], dict]:
    bad: list[str] = []
    if not rows:
        return ["the file has no rows"], {}
    missing = [c for c in AUTHOR_COLUMNS if c not in rows[0]]
    if missing:
        return [f"missing columns: {missing}"], {}
    known, seen = known_cached(), collections.Counter(r["item_id"] for r in rows)
    known_sets = known_sets_cached()
    texts = collections.Counter(K.norm(r["utterance"]) for r in rows)
    for r in rows:
        i = r["item_id"]
        if seen[i] > 1:
            bad.append(f"{i}: duplicate item id")
        if texts[K.norm(r["utterance"])] > 1:
            bad.append(f"{i}: the same sentence appears twice")
        if not r["utterance"].strip():
            bad.append(f"{i}: empty utterance")
        if K.norm(r["utterance"]) in known:
            bad.append(f"{i}: the sentence is already in a repository set (leak)")
        elif near_duplicate(r["utterance"], known_sets):
            bad.append(f"{i}: the sentence is a near-copy of a repository sentence (leak; the repository sentence is not shown)")
        if not re.fullmatch(r"[A-Za-z][A-Za-z0-9]{0,5}", r["author_code"] or ""):
            bad.append(f"{i}: author_code must be a short code such as A1 (never a name)")
        if r["family"] not in FAMILIES:
            bad.append(f"{i}: unknown family {r['family']!r}")
        if not (r["scenario_step"].isdigit() and 1 <= int(r["scenario_step"]) <= 15):
            bad.append(f"{i}: scenario_step must be a number 1 to 15")
        if r["speaker_role"] not in SPEAKERS:
            bad.append(f"{i}: speaker_role must be one of {sorted(SPEAKERS)}")
        for f in ("patient_question_pending", "readback_pending"):
            if r[f] not in ("yes", "no"):
                bad.append(f"{i}: {f} must be yes or no")
        turns = [t for t in r["prior_turns"].split(" | ") if t.strip()]
        if len(turns) > 4 or any(":" not in t or t.split(":")[0].strip() not in SPEAKERS for t in turns):
            bad.append(f"{i}: prior_turns must be at most four 'speaker: text' turns separated by ' | '")
        if not acts_field(r["author_intended_acts"]) <= set(K.ACTS) or not r["author_intended_acts"].strip():
            bad.append(f"{i}: author_intended_acts must be acts of the taxonomy (or NONE)")
        fam = r["family"]
        if fam in FAMILIES and not FAMILIES[fam].get("allowed_in_phase_two", True):
            bad.append(f"{i}: family {fam} is out of scope in this phase (real speech-recognition wording is evaluated later from real recordings)")
        if r["asr_source"].strip():
            bad.append(f"{i}: asr_source must be empty in this phase (no real-ASR family; invented typos are never allowed)")
        if fam == "F12" and (r["patient_question_pending"] != "yes" or not turns):
            bad.append(f"{i}: F12 needs prior_turns and patient_question_pending=yes")
        if fam == "F13" and (r["readback_pending"] != "yes" or not turns):
            bad.append(f"{i}: F13 needs prior_turns and readback_pending=yes")
        if fam == "F16" and r["scenario_step"].isdigit():
            acts = acts_field(r["author_intended_acts"]) - {"NONE"}
            if not acts or any(int(r["scenario_step"]) in window_of(a) or not window_of(a) for a in acts):
                bad.append(f"{i}: F16 needs an ordinary act whose behaviour window does not contain step {r['scenario_step']}")
    cov = collections.Counter(r["family"] for r in rows)
    for fid, f in FAMILIES.items():
        if cov[fid] < f["min"]:
            bad.append(f"coverage: family {fid} {f['name']} has {cov[fid]} items, the minimum is {f['min']}")
    for fid, n in cov.items():
        if fid in FAMILIES and n > FAMILIES[fid]["min"] * 1.1 + 1 and FAMILIES[fid].get("allowed_in_phase_two", True):
            bad.append(f"coverage: family {fid} has {n} items, far above its {FAMILIES[fid]['min']} (the set is balanced on purpose)")
    if len(rows) < DESIGN["min_items_total"]:
        bad.append(f"coverage: {len(rows)} items, the minimum is {DESIGN['min_items_total']}")
    return bad, dict(cov)


# ---------------------------------------------------------------------------------------------------- author packages
def split_quota(n_authors: int) -> list[dict]:
    """each family's quota shared as evenly as possible between the authors; the remainder goes round-robin so that no author holds all the odd items."""
    out = [dict() for _ in range(n_authors)]
    k = 0
    for fid, f in FAMILIES.items():
        q, r = divmod(f["min"], n_authors)
        for a in range(n_authors):
            out[a][fid] = q
        for _ in range(r):
            out[k % n_authors][fid] += 1
            k += 1
    return out


def author_packages(n_authors: int, out: Path) -> dict:
    if n_authors < DESIGN["min_authors"]:
        raise SystemExit(f"refused: at least {DESIGN['min_authors']} authors are needed")
    quotas = split_quota(n_authors)
    out.mkdir(parents=True, exist_ok=True)
    brief = brief_text()
    for a, qt in enumerate(quotas, 1):
        code = f"A{a}"
        d = out / f"author_{code}"
        d.mkdir(parents=True, exist_ok=True)
        (d / "AUTHOR_BRIEF.md").write_text(brief, encoding="utf-8", newline="\n")
        total = sum(qt.values())
        L = [f"# Personal instructions for author {code}\n",
             f"You write **{total} sentences** in total, shared with the other author(s) so that the whole set is {DESIGN['target_items_total']} items. Your file `author_template.csv` is already structured: one row per sentence you must write, with `item_id`, `author_code` and `family` filled. Fill every other column of every row. Do not add, delete or re-order rows, do not change a family, and do not put your name anywhere.\n",
             "Rules that matter most: write on your own, from the brief only; do not look for, copy or rewrite any existing benchmark or example sentence, and do not use a search engine or a model to produce sentences; clean natural text only (no deliberate typos); no real patient or staff; no clinical rule of your own; leave `asr_source` empty. Return the file to the owner; do not send it to the other author.\n",
             "## Your quota\n", "| family | name | your items |\n|---|---|---|"]
        L += [f"| {fid} | {FAMILIES[fid]['name']} | {n} |" for fid, n in qt.items() if n]
        (d / "AUTHOR_INSTRUCTIONS.md").write_text("\n".join(L) + "\n", encoding="utf-8", newline="\n")
        rows, n = [], 0
        for fid, c in qt.items():
            for _ in range(c):
                n += 1
                rows.append({"item_id": f"{code}-{n:03d}", "author_code": code, "family": fid})
        write(d / "author_template.csv", AUTHOR_COLUMNS, rows)
    return {"authors": n_authors, "per_author_totals": [sum(q.values()) for q in quotas], "family_totals": {fid: sum(q[fid] for q in quotas) for fid in FAMILIES}}


def combine(files: list[Path], out: Path) -> dict:
    rows, problems = [], []
    for f in files:
        rs = read(f)
        codes = {r["author_code"] for r in rs}
        if len(codes) != 1:
            problems.append(f"{f.name}: one file must hold exactly one author code, found {sorted(codes)}")
        for r in rs:
            for c in ("utterance", "scenario_context", "speaker_role", "scenario_step", "patient_question_pending", "readback_pending", "author_intended_acts"):
                if not (r.get(c) or "").strip():
                    problems.append(f"{r['item_id']}: {c} is empty")
        rows += rs
    if len({r["item_id"] for r in rows}) != len(rows):
        problems.append("duplicate item ids across the files")
    if problems:
        raise SystemExit("refused:\n" + "\n".join(problems[:40]))
    write(out, AUTHOR_COLUMNS, rows)
    return {"items": len(rows), "authors": len({r["author_code"] for r in rows})}


# ---------------------------------------------------------------------------------------------------- package
def package(authors: Path, out: Path, seed: int) -> dict:
    rows = read(authors)
    bad, _ = check_authors(rows)
    if bad:
        raise SystemExit("refused: the authors' file does not pass check-authors:\n" + "\n".join(bad[:40]))
    rng = random.Random(seed)
    order = list(range(len(rows)))
    rng.shuffle(order)
    ids = {rows[i]["item_id"]: f"X{n + 1:04d}" for n, i in enumerate(order)}
    out.mkdir(parents=True, exist_ok=True)
    views = [{**{c: r[c] for c in VIEW if c != "item_id"}, "item_id": ids[r["item_id"]]} for r in rows]
    for name in ("A", "B"):
        v = list(views)
        random.Random(seed + (1 if name == "A" else 2)).shuffle(v)
        write(out / f"annotator_{name}.csv", ANNOT_COLUMNS, v)
    (out / "id_map.json").write_text(json.dumps({"seed": seed, "note": "KEPT BY THE OWNER; never given to an annotator", "map": ids}, indent=1) + "\n", encoding="utf-8", newline="\n")
    return {"items": len(rows), "files": ["annotator_A.csv", "annotator_B.csv", "id_map.json"]}


# ---------------------------------------------------------------------------------------------------- annotators
def check_annotator(rows: list[dict], blind: list[dict]) -> list[str]:
    bad: list[str] = []
    if not rows:
        return ["the file has no rows"]
    missing = [c for c in ANNOT_COLUMNS if c not in rows[0]]
    if missing:
        return [f"missing columns: {missing}"]
    src = {r["item_id"]: r for r in blind}
    got = collections.Counter(r["item_id"] for r in rows)
    for i in src:
        if got[i] == 0:
            bad.append(f"{i}: not labelled")
    for r in rows:
        i = r["item_id"]
        if i not in src:
            bad.append(f"{i}: not an item of the file you were given")
            continue
        if got[i] > 1:
            bad.append(f"{i}: duplicate item id")
        if any(r[c] != src[i][c] for c in VIEW):
            bad.append(f"{i}: the item text or scene was changed")
        if not K.acts_of(r) <= set(K.ACTS):
            bad.append(f"{i}: unknown act in {sorted(K.acts_of(r) - set(K.ACTS))}")
        if not K.acts_of(r):
            bad.append(f"{i}: acts_performed is empty (write NONE if no act is performed)")
        if r["actor"] not in K.ACTORS:
            bad.append(f"{i}: actor must be one of {sorted(K.ACTORS)}")
        if r["modality"] not in K.MODALITIES:
            bad.append(f"{i}: modality must be one of {sorted(K.MODALITIES)}")
        for f in ("credit_eligible", "ambiguous", "definition_insufficient"):
            if r[f] not in ("yes", "no"):
                bad.append(f"{i}: {f} must be yes or no")
        if r["evidence"].strip() and K.norm(r["evidence"]) not in K.norm(r["utterance"]):
            bad.append(f"{i}: evidence is not a part of the utterance")
        if K.acts_of(r) - {"NONE"} and r["modality"] == "actual" and not r["evidence"].strip():
            bad.append(f"{i}: an act performed now needs evidence")
        if r["credit_eligible"] in ("yes", "no") and r["credit_eligible"] != K.rule_credit(r):
            bad.append(f"{i}: credit_eligible={r['credit_eligible']} but the approved rule gives {K.rule_credit(r)} (adjudicate)")
        if r["definition_insufficient"] == "yes" and not r["note"].strip():
            bad.append(f"{i}: definition_insufficient needs a note saying why")
    return bad


def same(a: dict, b: dict) -> bool:
    return all((K.acts_of(a) == K.acts_of(b)) if f == "acts_performed" else a[f] == b[f] for f in COMPARE)


def agreement(fa: Path, fb: Path) -> dict:
    A, B = {r["item_id"]: r for r in read(fa)}, {r["item_id"]: r for r in read(fb)}
    ids = sorted(set(A) & set(B))
    out = {"items_in_both": len(ids), "only_in_a": len(set(A) - set(B)), "only_in_b": len(set(B) - set(A)), "fields": {}, "differ": [], "definition_insufficient": []}
    for f in COMPARE:
        xs = [";".join(sorted(K.acts_of(A[i]))) if f == "acts_performed" else A[i][f] for i in ids]
        ys = [";".join(sorted(K.acts_of(B[i]))) if f == "acts_performed" else B[i][f] for i in ids]
        out["fields"][f] = {"raw_agreement": round(sum(x == y for x, y in zip(xs, ys)) / len(ids), 3) if ids else None, "kappa": K.kappa(xs, ys)}
    out["differ"] = [{"item_id": i, "fields": [f for f in COMPARE if (K.acts_of(A[i]) != K.acts_of(B[i]) if f == "acts_performed" else A[i][f] != B[i][f])]} for i in ids if not same(A[i], B[i])]
    out["definition_insufficient"] = [i for i in ids if "yes" in (A[i]["definition_insufficient"], B[i]["definition_insufficient"])]
    return out


def merge(fa: Path, fb: Path, fj: Path, authors: Path, idmap: Path, out: Path) -> dict:
    A, B = ({r["item_id"]: r for r in read(f)} for f in (fa, fb))
    J = {r["item_id"]: r for r in read(fj)} if fj.exists() else {}
    auth = {r["item_id"]: r for r in read(authors)}
    inv = {v: k for k, v in json.loads(idmap.read_text(encoding="utf-8"))["map"].items()}
    scored, excluded, disagreements, spec = [], [], [], []
    for i in sorted(set(A) & set(B)):
        a, b, j = A[i], B[i], J.get(i)
        ar = auth[inv[i]]
        base = {"item_id": i, **{c: ar[c] for c in ("family", "scenario_step", "scenario_context", "prior_turns", "speaker_role", "utterance", "patient_question_pending", "readback_pending", "asr_source")}}
        both = {"a_acts": ";".join(sorted(K.acts_of(a))), "b_acts": ";".join(sorted(K.acts_of(b))), "a_credit": a["credit_eligible"], "b_credit": b["credit_eligible"]}
        agreed = same(a, b)
        if not agreed:
            disagreements.append({**base, **both, "adjudicated_acts": ";".join(sorted(K.acts_of(j))) if j else "", "adjudicated_credit": j["credit_eligible"] if j else "", "fields": ";".join(f for f in COMPARE if (K.acts_of(a) != K.acts_of(b) if f == "acts_performed" else a[f] != b[f]))})
        final = a if agreed else j
        flagged = [n for n, r in (("A", a), ("B", b), ("adjudicator", j)) if r and r["definition_insufficient"] == "yes"]
        if flagged:
            spec.append({"item_id": i, "flagged_by": ";".join(flagged), "why_the_definitions_do_not_settle_it": " / ".join(r["note"] for r in (a, b, j) if r and r["definition_insufficient"] == "yes"), "suggested_clarification": ""})
        if final is None:
            excluded.append({**base, **both, "reason": "awaiting adjudication"})
        elif "yes" in [r["ambiguous"] for r in (a, b, j) if r] or flagged:
            excluded.append({**base, **both, "reason": "definition insufficient" if flagged else "ambiguous"})
        else:
            scored.append({**base, **both, "gold_acts": ";".join(sorted(K.acts_of(final))), "gold_actor": final["actor"], "gold_modality": final["modality"], "gold_credit_eligible": final["credit_eligible"], "gold_status": "agreed" if agreed else "adjudicated"})
    out.mkdir(parents=True, exist_ok=True)
    cols_b = ["item_id", "family", "scenario_step", "scenario_context", "prior_turns", "speaker_role", "utterance", "patient_question_pending", "readback_pending", "asr_source"]
    write(out / "items.csv", cols_b + ["gold_acts", "gold_actor", "gold_modality", "gold_credit_eligible", "gold_status", "a_acts", "b_acts", "a_credit", "b_credit"], scored)
    write(out / "excluded.csv", cols_b + ["reason", "a_acts", "b_acts", "a_credit", "b_credit"], excluded)
    write(out / "disagreements.csv", cols_b + ["a_acts", "b_acts", "a_credit", "b_credit", "adjudicated_acts", "adjudicated_credit", "fields"], disagreements)
    write(out / "specification_issues.csv", ["item_id", "flagged_by", "why_the_definitions_do_not_settle_it", "suggested_clarification"], spec)
    ag = agreement(fa, fb)
    (out / "agreement.json").write_text(json.dumps(ag, indent=1) + "\n", encoding="utf-8", newline="\n")
    return {"scored": len(scored), "excluded": len(excluded), "awaiting_adjudication": sum(1 for e in excluded if e["reason"] == "awaiting adjudication"), "disagreements": len(disagreements), "specification_issues": len(spec)}


# ---------------------------------------------------------------------------------------------------- summary for the owner (before the freeze)
def gold_kind(r: dict) -> str:
    acts = {a for a in r["gold_acts"].split(";") if a} - {"NONE"}
    if acts & set(K.CRITICAL):
        return "critical"
    if r["gold_credit_eligible"] == "yes":
        ordinary = acts - set(K.CRITICAL)
        return "legitimate in window" if ordinary and all(int(r["scenario_step"]) in window_of(a) for a in ordinary) else "legitimate out of window"
    return "adversarial / not performed"


def summary(d: Path, roles: Path | None) -> dict:
    items, excl = read(d / "items.csv"), read(d / "excluded.csv")
    dis = read(d / "disagreements.csv") if (d / "disagreements.csv").exists() else []
    spec = read(d / "specification_issues.csv") if (d / "specification_issues.csv").exists() else []
    ag = json.loads((d / "agreement.json").read_text(encoding="utf-8"))
    allrows = items + excl
    authored = collections.Counter(r["family"] for r in allrows)
    scored = collections.Counter(r["family"] for r in items)
    obj = collections.Counter(a for r in items for a in (r["gold_acts"].split(";") if r["gold_acts"] else []) if a)
    comp = collections.Counter(gold_kind(r) for r in items)
    n_both = ag["items_in_both"]
    outcome = collections.Counter()
    for r in dis:
        adj = {a for a in r["adjudicated_acts"].split(";") if a}
        if not r["adjudicated_acts"] and not r["adjudicated_credit"]:
            outcome["no adjudication yet"] += 1
        elif adj == {a for a in r["a_acts"].split(";") if a} and r["adjudicated_credit"] == r["a_credit"]:
            outcome["adjudicator agreed with annotator A"] += 1
        elif adj == {a for a in r["b_acts"].split(";") if a} and r["adjudicated_credit"] == r["b_credit"]:
            outcome["adjudicator agreed with annotator B"] += 1
        else:
            outcome["adjudicator chose a third label"] += 1
    known = known_cached()
    known_sets = known_sets_cached()
    exact = sum(1 for r in allrows if K.norm(r["utterance"]) in known)
    near = sum(1 for r in allrows if K.norm(r["utterance"]) not in known and near_duplicate(r["utterance"], known_sets))
    rr = json.loads(roles.read_text(encoding="utf-8")) if roles and roles.exists() else None
    rp = roles_problems(rr) if rr else ["no roles record supplied"]
    min_ok = {fid: authored[fid] >= f["min"] for fid, f in FAMILIES.items()}
    checklist = [
        ("at least 2 independent authors", bool(rr) and len(rr.get("authors", [])) >= DESIGN["min_authors"], "owner record"),
        ("2 independent annotators who labelled every item", bool(rr) and len(rr.get("annotators", [])) == 2 and n_both == len(allrows), "owner record + tool (every item has both labels)"),
        ("1 adjudicator", bool(rr) and bool(rr.get("adjudicator")), "owner record"),
        ("all roles are different people and none is a builder", not rp, "tool check of the owner record"),
        ("no item is awaiting adjudication", not any(e["reason"] == "awaiting adjudication" for e in excl), "tool"),
        ("every family reached its quota (authored items)", all(min_ok.values()), "tool"),
        (f"{DESIGN['target_items_total']} items authored", len(allrows) >= DESIGN["target_items_total"], "tool"),
        ("real-ASR family F17 has 0 items", authored.get("F17", 0) == 0, "tool"),
        ("no repository sentence or near-copy among the items", exact == 0 and near == 0, "tool (counts only)"),
        ("no author, annotator or adjudicator saw a model prediction, probability, guard decision or intended output", None, "OWNER ATTESTATION ONLY: the tools cannot verify this"),
    ]
    out = {"items_authored": len(allrows), "items_scored": len(items), "items_excluded": len(excl), "excluded_by_reason": dict(collections.Counter(e["reason"] for e in excl)),
           "by_family": {fid: {"quota": f["min"], "authored": authored.get(fid, 0), "scored": scored.get(fid, 0), "quota_met": min_ok[fid]} for fid, f in FAMILIES.items()},
           "by_objective_gold_acts_scored": dict(sorted(obj.items())), "composition_scored": dict(comp),
           "annotator_disagreement": {"items_labelled_by_both": n_both, "items_with_any_difference": len(ag["differ"]), "rate": round(len(ag["differ"]) / n_both, 4) if n_both else None, "fields": ag["fields"]},
           "adjudication_outcomes": dict(outcome), "definition_insufficient": {"count": len(spec), "items": [{"item_id": x["item_id"], "flagged_by": x["flagged_by"], "why": x["why_the_definitions_do_not_settle_it"]} for x in spec]},
           "leak_check": {"exact_repository_matches": exact, "near_copies": near, "items_checked": len(allrows)},
           "roles": ({k: rr[k] for k in ("authors", "annotators", "adjudicator", "builder_names", "attested_by", "attested_on")} if rr else None), "roles_problems": rp,
           "independence_checklist": [{"requirement": a, "satisfied": b, "verified_by": c} for a, b, c in checklist],
           "every_independence_requirement_satisfied": all(b for _, b, _ in checklist[:-1]) and "pending the owner's attestation of the last requirement"}
    NL = chr(10)
    L = ["# EX-01 phase two: dataset and reviewer summary (before the freeze)", "",
         f"Items authored {out['items_authored']}; scored {out['items_scored']}; excluded {out['items_excluded']} ({out['excluded_by_reason']}).", "", "## By coverage family", "",
         "| family | name | quota | authored | scored |", "|---|---|---|---|---|"]
    L += [f"| {fid} | {FAMILIES[fid]['name']} | {v['quota']} | {v['authored']} | {v['scored']} |" for fid, v in out["by_family"].items()]
    L += ["", "## Composition of the scored set", ""] + [f"* {k}: {v}" for k, v in comp.items()] + ["", "## By objective (gold acts, scored items)", ""] + [f"* {k}: {v}" for k, v in out["by_objective_gold_acts_scored"].items()]
    L += ["", "## Annotators", "", f"Items labelled by both: {n_both}; items with any difference: {len(ag['differ'])} ({out['annotator_disagreement']['rate']}).", "",
          "| field | raw agreement | kappa |", "|---|---|---|"]
    L += [f"| {f} | {v['raw_agreement']} | {v['kappa']} |" for f, v in ag["fields"].items()]
    L += ["", "Adjudication outcomes: " + (", ".join(f"{k}: {v}" for k, v in outcome.items()) or "none") + ".", "", f"Definition-insufficient items: {len(spec)}.", "",
          f"Leak check: {exact} exact matches and {near} near-copies of repository sentences among {len(allrows)} items.", "", "## Roles and independence", ""]
    L += [f"* {x['requirement']}: {'YES' if x['satisfied'] else ('NOT VERIFIABLE BY THE TOOL' if x['satisfied'] is None else 'NO')} ({x['verified_by']})" for x in out["independence_checklist"]]
    L += ["", f"Roles record problems: {rp or 'none'}"]
    (d / "summary.md").write_text(NL.join(L) + NL, encoding="utf-8", newline=NL)
    return out


# ---------------------------------------------------------------------------------------------------- freeze / verify
def roles_problems(r: dict) -> list[str]:
    bad = []
    for k in ("attested_by", "attested_on", "authors", "annotators", "adjudicator", "builder_names", "statement"):
        if not r.get(k):
            bad.append(f"roles record: {k} is missing")
    if bad:
        return bad
    people = list(r["authors"]) + list(r["annotators"]) + [r["adjudicator"]]
    if any("<" in p for p in people + list(r["builder_names"])) or "<" in r["attested_by"] or "<" in r["attested_on"]:
        bad.append("roles record still holds template placeholders")
    if len(r["authors"]) < DESIGN["min_authors"]:
        bad.append(f"at least {DESIGN['min_authors']} authors are needed")
    if len(r["annotators"]) != 2:
        bad.append("exactly two annotators are needed")
    low = [p.strip().lower() for p in people]
    if len(set(low)) != len(low):
        bad.append("an author, an annotator and the adjudicator must be different people (a name repeats)")
    if set(low) & {b.strip().lower() for b in r["builder_names"]}:
        bad.append("a builder of the evaluator, the prompts or the verifier cannot be an author, annotator or adjudicator")
    return bad


def freeze(src: Path, roles: Path, out: Path, authors_file: Path | None = None) -> dict:
    rr = json.loads(roles.read_text(encoding="utf-8"))
    bad = roles_problems(rr)
    for f in FILES:
        if not (src / f).exists():
            bad.append(f"{f} is missing from {src}")
    if bad:
        raise SystemExit("refused:\n" + "\n".join(bad))
    items, excl = read(src / "items.csv"), read(src / "excluded.csv")
    if any(e["reason"] == "awaiting adjudication" for e in excl):
        raise SystemExit("refused: items are still awaiting adjudication")
    authored = collections.Counter(r["family"] for r in items + excl)
    short = [f"family {fid} has {authored[fid]} authored items, the minimum is {f['min']}" for fid, f in FAMILIES.items() if authored[fid] < f["min"]]
    if short or len(items) + len(excl) < DESIGN["min_items_total"]:
        raise SystemExit("refused: coverage minimums not met:\n" + "\n".join(short + ([f"{len(items) + len(excl)} items, the minimum is {DESIGN['min_items_total']}"] if len(items) + len(excl) < DESIGN["min_items_total"] else [])))
    out.mkdir(parents=True, exist_ok=True)
    for f in FILES:
        shutil.copyfile(src / f, out / f)
    man = {"set": "ex01-semantic", "version": "ex01-semantic-1", "design_version": DESIGN["version"], "frozen_utc": datetime.now(timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ"), "independent": True,
           "items_scored": len(items), "items_excluded": len(excl), "authored_per_family": dict(authored), "scored_per_family": dict(collections.Counter(r["family"] for r in items)), "excluded_reasons": dict(collections.Counter(e["reason"] for e in excl)),
           "agreement": json.loads((src / "agreement.json").read_text(encoding="utf-8"))["fields"], "roles": {k: rr[k] for k in ("authors", "annotators", "adjudicator", "builder_names", "attested_by", "attested_on", "statement")},
           "files": {f: sha(out / f) for f in FILES}, "scored_runs": 0,
           "note": "Frozen BEFORE any model or system saw it. The first scored run is the independent measurement; a second look makes the set development data. Names are the owner's record, never invented."}
    (out / "manifest.json").write_text(json.dumps(man, indent=1, ensure_ascii=False) + "\n", encoding="utf-8", newline="\n")
    return man


def verify(d: Path) -> list[str]:
    m = d / "manifest.json"
    if not m.exists():
        return ["no manifest: the set is not frozen"]
    man = json.loads(m.read_text(encoding="utf-8"))
    bad = []
    if not man.get("independent"):
        bad.append("the manifest does not record an independent set")
    for f, h in man.get("files", {}).items():
        if not (d / f).exists() or sha(d / f) != h:
            bad.append(f"{f} changed after the set was frozen")
    return bad


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    sub.add_parser("brief").add_argument("--out", required=True)
    sub.add_parser("check-authors").add_argument("--file", required=True)
    p = sub.add_parser("package")
    p.add_argument("--authors", required=True)
    p.add_argument("--out", required=True)
    p.add_argument("--seed", type=int, default=20261021)
    p = sub.add_parser("check-annotator")
    p.add_argument("--file", required=True)
    p.add_argument("--package", required=True)
    p = sub.add_parser("agree")
    p.add_argument("--a", required=True)
    p.add_argument("--b", required=True)
    p = sub.add_parser("merge")
    for k in ("a", "b", "adjudicated", "authors", "id-map", "out"):
        p.add_argument("--" + k, required=True)
    p = sub.add_parser("freeze")
    p.add_argument("--dir", required=True)
    p.add_argument("--roles", required=True)
    p.add_argument("--out", default=str(DEFAULT_SET))
    p = sub.add_parser("verify")
    p.add_argument("--dir", default=str(DEFAULT_SET))
    p = sub.add_parser("author-packages")
    p.add_argument("--authors", type=int, required=True)
    p.add_argument("--out", required=True)
    p = sub.add_parser("combine")
    p.add_argument("--files", nargs="+", required=True)
    p.add_argument("--out", required=True)
    p = sub.add_parser("summary")
    p.add_argument("--dir", required=True)
    p.add_argument("--roles", default="")
    a = ap.parse_args(argv)
    if a.cmd == "brief":
        cmd_brief(Path(a.out))
    elif a.cmd == "check-authors":
        bad, cov = check_authors(read(a.file))
        print("\n".join(bad) if bad else "OK: the file passes every check", "\ncoverage:", json.dumps(cov))
        return 1 if bad else 0
    elif a.cmd == "package":
        print(json.dumps(package(Path(a.authors), Path(a.out), a.seed), indent=1))
    elif a.cmd == "check-annotator":
        bad = check_annotator(read(a.file), read(a.package))
        print("\n".join(bad) if bad else "OK: the file passes every check")
        return 1 if bad else 0
    elif a.cmd == "agree":
        print(json.dumps(agreement(Path(a.a), Path(a.b)), indent=1))
    elif a.cmd == "merge":
        print(json.dumps(merge(Path(a.a), Path(a.b), Path(a.adjudicated), Path(a.authors), Path(a.id_map), Path(a.out)), indent=1))
    elif a.cmd == "freeze":
        m = freeze(Path(a.dir), Path(a.roles), Path(a.out))
        print(json.dumps({k: m[k] for k in ("version", "frozen_utc", "items_scored", "items_excluded", "files")}, indent=1))
    elif a.cmd == "author-packages":
        print(json.dumps(author_packages(a.authors, Path(a.out)), indent=1))
    elif a.cmd == "combine":
        print(json.dumps(combine([Path(f) for f in a.files], Path(a.out)), indent=1))
    elif a.cmd == "summary":
        print(json.dumps(summary(Path(a.dir), Path(a.roles) if a.roles else None), indent=1, ensure_ascii=False))
    else:
        bad = verify(Path(a.dir))
        print("\n".join(bad) if bad else "OK: the frozen set is unchanged")
        return 1 if bad else 0
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
