"""Component selection and the scenario skeleton (p1-live-scenario-generation-s03: LG-04 baseline and LG-05).

    from generation.skeleton import build_skeleton
    skeleton = build_skeleton(objectives, index, cases, tables)

The deterministic heart: a fresh case exists before any language model runs, and this is also what is served when a model is unavailable. For an accepted request's objectives it
  1. ranks the approved spines of the scenario family by how well their story matches the objectives (BM25 over the case documents, retrieve.py) and how near their tier rating is to the difficulty target,
     leaving out spines played lately;
  2. takes the best few and picks one by the request's seed (variety with the same answer for the same request);
  3. varies the patient with the composer's solver (tools/composer, used here as a library), giving up optional slots in a fixed order if a spine will not fit, and trying other blocks if the story's
     fingerprint is one the trainee has met;
  4. draws the free numbers again from the packs (numeric.py: normal laboratory results and the starting vital signs; the numbers the story is built on stay);
  5. picks the picture, ECG strip and report of every imaging slot by the finding tags (media.py), and lets the starting pulse agree with an ECG strip;
  6. returns the skeleton: the composed case file, its typed slots (which blocks fill what) and the discovery rules the story needs, copied from the spine, because the engine finds a problem by those rules.
The composer lives in tools/composer; it is found next to this package (a service image copies it in, p1-live-scenario-generation-s10).
"""
from __future__ import annotations

import json
import random
import sys
from pathlib import Path

from . import contracts, media, numeric, packs, retrieve

ROOT = Path(__file__).resolve().parents[2]
TOOLS = ROOT / "tools"
TOP_CHOICES = 5


def _composer():
    if str(TOOLS) not in sys.path:
        sys.path.insert(0, str(TOOLS))
    try:
        from composer import blocks as B, solve  # type: ignore
    except ImportError as e:        # pragma: no cover
        raise RuntimeError("the composer (tools/composer) is not next to the service: copy it in, or use the pre-generated pool") from e
    return B, solve


def discovery_rules(spine_id: str, content_dir: Path = ROOT / "content") -> list[dict]:
    """The rules by which the engine finds the spine's problems (content/problems/discovery.json), as they will apply to a case composed from it."""
    doc = json.loads((content_dir / "problems" / "discovery.json").read_text(encoding="utf-8"))["discovery"]
    return [r for r in doc if r["case"] == spine_id]


def target_difficulty(objectives: dict) -> float:
    """One number from the difficulty vector: the mean of the dimensions the objectives name, else of all eight."""
    vec = objectives["difficulty_vector"]
    names = objectives["dimensions"] or list(vec)
    return sum(vec[n] for n in names) / len(names)


def rank_spines(objectives: dict, index: retrieve.Index, cases: list[dict], tables, query: str) -> list[tuple[float, dict]]:
    """Approved spines of the scenario, best match first. Score: relevance to the objectives (BM25, scaled to 0..1) minus how far the tier's rating is from the target, in hundreds of rating points."""
    B, solve = _composer()
    template = solve.load_template(objectives["scenario"])
    allowed_tiers = set(template["difficulty_limits"]["3" if objectives["level"] == "advanced" else "2"]["tiers"])
    hits = {h.doc.id.removeprefix("case:"): h.score for h in retrieve.retrieve(index, query, 200, kinds={"case"})} if query.strip() else {}
    top = max(hits.values(), default=1.0) or 1.0
    target = target_difficulty(objectives)
    out = []
    for c in cases:
        if c["scenario"] != objectives["scenario"] or c["tier"] not in allowed_tiers or c["status"] not in retrieve.DEFAULT_ALLOWED or c.get("created_by") == "composer":
            continue
        if c["id"] in objectives["exclusions"]["spines"]:
            continue
        score = hits.get(c["id"], 0.0) / top - abs(tables.case_rating(c["tier"]) - target) / 100.0
        out.append((score, c))
    out.sort(key=lambda s: (-s[0], s[1]["id"]))
    return out


def _news2():
    content = str(TOOLS / "content")
    if content not in sys.path:
        sys.path.insert(0, content)
    import news2  # type: ignore
    return news2.score


def build_skeleton(objectives: dict, index: retrieve.Index, cases: list[dict], tables, behaviour_labels: dict[str, str] | None = None, library: list[dict] | None = None, serial: int = 1,
                   vary_numbers: bool = True, force_spine: str | None = None, numeric_seed: int | None = None, media_seed: int | None = None) -> dict:
    """`force_spine`, `numeric_seed` and `media_seed` are for repair (repair.py): keep the story and redraw only the numbers, or only the pictures, or the blocks."""
    B, solve = _composer()
    labels = behaviour_labels or {}
    query = retrieve.query_from_objectives(objectives, labels, {d.id: d.label for d in tables.dimensions})
    ranked = rank_spines(objectives, index, cases, tables, query)
    if force_spine:
        ranked = [r for r in ranked if r[1]["id"] == force_spine]
    if not ranked:
        raise solve.Unsatisfiable(f"no approved spine for scenario {objectives['scenario']} outside the recent ones")
    rng = random.Random(objectives["seed"])
    template = solve.load_template(objectives["scenario"])
    library = library if library is not None else solve.load_library()
    names = {c["identity"]["full_name"] for c in cases}
    mrns = {c["identity"]["mrn"] for c in cases}
    avoid = set(objectives["exclusions"]["fingerprints"])
    pool = ranked[:TOP_CHOICES]
    order = list(pool)
    rng.shuffle(order)
    order += [r for r in ranked[TOP_CHOICES:]]                     # if none of the best few can be varied, the rest follow in rank order
    case_id = f"GEN-{objectives['scenario']}-{objectives['seed'] % 10000:04d}-{serial:03d}"
    last_error = None
    for score, spine in order:
        try:
            card, choice = solve.build_card(spine, objectives["scenario"], objectives["seed"], case_id, library, template, names, mrns, avoid)
        except solve.Unsatisfiable as e:
            last_error = e
            continue
        numeric_log = numeric.vary_numbers(card, objectives["seed"] if numeric_seed is None else numeric_seed, packs.LabPack(), packs.load("vitals"), _news2()) if vary_numbers else []
        picked = media.select_media(card, spine["id"], objectives["seed"] if media_seed is None else media_seed)
        picked["defects"] = media.check_media(card, picked)
        slots: dict[str, list[str]] = {}
        for b in choice.blocks:
            slots.setdefault(b["type"], []).append(b["id"])
        return {"version": 1, "request_id": objectives["request_id"], "case": card, "slots": slots, "discovery": discovery_rules(spine["id"]), "numeric_log": numeric_log, "media": picked,
                "selection": {"spine": spine["id"], "score": round(score, 4), "query": query, "candidates": [c["id"] for _, c in pool], "target_difficulty": round(target_difficulty(objectives), 2)},
                "fingerprint": solve.fingerprint(spine["id"], choice.blocks)}
    raise solve.Unsatisfiable(f"no spine could be varied into a story the trainee has not met: {last_error}")
