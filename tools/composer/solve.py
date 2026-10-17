"""The scenario composer's solver (p1-scenario-composer-difficulty-s02, guide B-M1 steps 3 to 5).

    from composer.solve import Solver, compose

A composed case is an approved **spine** (an authored case file) varied by blocks that do not change its clinical story (see tools/composer/blocks.py). The solver is deterministic and uses no model:
slots are ordered by how few blocks can fill them (tightest first); for each slot the compatible blocks are tried in a seeded order; `requires` and `excludes` are checked on every partial choice against the tags of the
spine and of the blocks already chosen; a dead end backtracks; dead ends are remembered (keyed by the set of chosen blocks) and the search stops after 10,000 steps with a clear error.
The card it returns is a complete case file (same schema, `created_by: composer`, status DRAFT, a `composition` record with the seed, template, spine and blocks, so any card can be made again).
It never approves anything: a composed case goes through Stage B (tools/composer/stage_b.py) and is practice-only.
"""
from __future__ import annotations

import copy
import datetime as dt
import hashlib
import json
import random
import re
import sys
from dataclasses import dataclass, field
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from composer import blocks as B  # noqa: E402

ROOT = B.ROOT
MAX_STEPS = 10_000
MONTHS = ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"]
ORDINALS = ["", "first", "second", "third", "fourth", "fifth", "sixth", "seventh", "eighth", "ninth", "tenth", "eleventh", "twelfth", "thirteenth", "fourteenth", "fifteenth", "sixteenth",
            "seventeenth", "eighteenth", "nineteenth", "twentieth", "twenty-first", "twenty-second", "twenty-third", "twenty-fourth", "twenty-fifth", "twenty-sixth", "twenty-seventh",
            "twenty-eighth", "twenty-ninth", "thirtieth", "thirty-first"]


class Unsatisfiable(Exception):
    """No set of blocks fits the spine and the template (or the search ran out of steps)."""


@dataclass
class Choice:
    blocks: list[dict] = field(default_factory=list)
    steps: int = 0


def load_library() -> list[dict]:
    return [json.loads(p.read_text(encoding="utf-8")) for p in sorted((ROOT / "content" / "blocks").glob("*/*.json")) if p.name != "types.json"]


def load_template(scenario: str) -> dict:
    return json.loads((ROOT / "content" / "templates" / f"SCN-{scenario}.json").read_text(encoding="utf-8"))


class Solver:
    """Chooses the blocks that vary one spine. `avoid_names` holds full names already in use (authored cases and earlier composed ones)."""

    def __init__(self, library: list[dict], template: dict, spine: dict, rng: random.Random, avoid_names: set[str] | None = None, max_steps: int = MAX_STEPS):
        self.template, self.spine, self.rng, self.max_steps = template, spine, rng, max_steps
        self.avoid = {n.lower() for n in (avoid_names or set())}
        self.spine_tags = B.spine_tags(spine)
        floor = B.STATUS_ORDER.index(template["spine_status_at_least"])
        self.library = [b for b in library if B.STATUS_ORDER.index(b["status"]) >= floor]
        self.slots = [s for s in template["slots"] if s["source"] == "block"]
        self.steps = 0
        self._dead: set[frozenset[str]] = set()

    # ---------------------------------------------------------------- rules

    def candidates(self, slot: dict) -> list[dict]:
        out = [b for b in self.library if b["type"] == slot["type"]]
        if slot["type"] == "DEM":
            keep = {t for t in self.spine_tags if t.startswith(("sex:", "age_class:"))}
            out = [b for b in out if {t for t in b["tags"] if t.startswith(("sex:", "age_class:"))} == keep]
        return out

    def consistent(self, chosen: list[dict]) -> bool:
        """requires and excludes hold for the spine's tags together with every chosen block's tags; names are free; at most one name part of each kind."""
        tags = set(self.spine_tags)
        for b in chosen:
            tags |= set(b["tags"])
        for b in chosen:
            if not set(b["requires"]) <= tags:
                return False
            if set(b["excludes"]) & (tags - set(b["tags"])) or set(b["excludes"]) & set(self.spine_tags):
                return False
        parts = [b["attributes"]["part"] for b in chosen if b["type"] == "NAME"]
        if len(parts) != len(set(parts)):
            return False
        sexes = {t for b in chosen if b["type"] == "NAME" and b["attributes"]["part"] == "first" for t in b["tags"] if t.startswith("sex:")}
        if sexes and sexes != {f"sex:{self.spine['identity']['sex']}"}:
            return False
        if len(parts) == 2:
            full = " ".join(b["attributes"]["text"] for part in ("first", "last") for b in chosen if b["type"] == "NAME" and b["attributes"]["part"] == part)
            if full.lower() in self.avoid:
                return False
        # a story that depends on smoking, alcohol or an earlier anaesthetic keeps its own answers
        for b in chosen:
            if b["type"] == "SOC" and self.spine_tags & {"pathway:smoking", "pathway:alcohol"}:
                return False
            if b["type"] == "PSH" and "pathway:anaesthetic_history" in self.spine_tags:
                return False
            if b["type"] == "ALG" and spine_has_own_allergy(self.spine):
                return False
        return True

    # ---------------------------------------------------------------- search

    def solve(self) -> Choice:
        wanted: list[tuple[dict, int]] = []
        for s in self.slots:
            usable = sum(1 for b in self.candidates(s) if self.consistent([b]))          # blocks that fit the spine on their own
            top = min(s["max"], usable)
            if top < s["min"]:
                raise Unsatisfiable(f"no blocks fit the spine {self.spine['id']} with template {self.template['scenario']}: the {s['type']} slot needs {s['min']} and {usable} fit")
            wanted += [(s, 1)] * self.rng.randint(s["min"], top)
        slots = sorted(wanted, key=lambda sl: len(self.candidates(sl[0])))     # tightest slot first
        chosen = self._search(slots, 0, [])
        if chosen is None:
            raise Unsatisfiable(f"no blocks fit the spine {self.spine['id']} with template {self.template['scenario']} (tried {self.steps} steps)")
        return Choice(sorted(chosen, key=lambda b: (b["type"], b["id"])), self.steps)

    def _search(self, slots: list[tuple[dict, int]], i: int, chosen: list[dict]) -> list[dict] | None:
        if i == len(slots):
            return chosen
        key = frozenset(b["id"] for b in chosen) | {f"@{i}"}
        if key in self._dead:
            return None
        pool = [b for b in self.candidates(slots[i][0]) if b not in chosen]
        self.rng.shuffle(pool)
        for b in pool:
            self.steps += 1
            if self.steps > self.max_steps:
                raise Unsatisfiable(f"gave up after {self.max_steps} steps on spine {self.spine['id']}")
            trial = chosen + [b]
            if self.consistent(trial):
                got = self._search(slots, i + 1, trial)
                if got is not None:
                    return got
        self._dead.add(key)
        return None


def spine_has_own_allergy(spine: dict) -> bool:
    """A spine that lists an allergy, or whose story touches allergies, keeps its own."""
    return bool(spine["allergies"]) or "pathway:allergy" in B.spine_tags(spine)


# ---------------------------------------------------------------- applying the blocks

def ordinal_date(d: dt.date) -> str:
    return f"{ORDINALS[d.day]} of {MONTHS[d.month - 1]} {d.year}"


def birth_date(age: int, reference: dt.date, rng: random.Random) -> dt.date:
    """A birth date that makes the patient exactly `age` on the reference date."""
    year = reference.year - age
    for _ in range(400):
        month, day = rng.randint(1, 12), rng.randint(1, 28)
        if (month, day) <= (reference.month, reference.day):
            return dt.date(year, month, day)
    return dt.date(year, 1, 1)


def replace_words(text: str, mapping: list[tuple[str, str]]) -> str:
    for old, new in mapping:
        if old:
            text = re.sub(r"(?<![A-Za-z])" + re.escape(old) + r"(?![A-Za-z])", new, text)
    return text


def walk_strings(x, fn):
    if isinstance(x, str):
        return fn(x)
    if isinstance(x, list):
        return [walk_strings(v, fn) for v in x]
    if isinstance(x, dict):
        return {k: walk_strings(v, fn) for k, v in x.items()}
    return x


def with_article(occupation: str) -> str:
    return ("an " if occupation[0].lower() in "aeiou" else "a ") + occupation


def apply(spine: dict, chosen: list[dict], case_id: str, seed: int, template: dict, rng: random.Random, used_mrns: set[str], jitter: int = 3) -> dict:
    card = copy.deepcopy(spine)
    old = spine["identity"]
    by = {t: [b for b in chosen if b["type"] == t] for t in B.TYPES}
    dem = by["DEM"][0]["attributes"]
    first = next(b["attributes"]["text"] for b in by["NAME"] if b["attributes"]["part"] == "first")
    last = next(b["attributes"]["text"] for b in by["NAME"] if b["attributes"]["part"] == "last")
    # the age moves a few years inside the same age class (the DEM block was chosen from it)
    cls = next(t for t in B.spine_tags(spine) if t.startswith("age_class:"))
    lo, hi = (int(x) for x in cls.removeprefix("age_class:").split("-"))
    age = max(lo, min(hi - 1, dem["age"] + rng.randint(-jitter, jitter)))
    ref = dt.date.fromisoformat(spine["reference_date"])
    dob = birth_date(age, ref, rng)
    mrn = next(m for m in (str(rng.randint(100000, 999999)) for _ in range(1000)) if m not in used_mrns)
    used_mrns.add(mrn)
    full = f"{first} {last}"
    card["identity"] = {"title": dem["title"], "full_name": full, "sex": old["sex"], "age": age, "occupation": dem["occupation"], "dob": dob.isoformat(), "mrn": mrn}
    card["wristband"] = {"name_text": full.upper(), "dob_text": dob.strftime("%d/%m/%Y"), "mrn_text": "MR " + mrn}
    card["spoken_identity_answer"] = f"{full}, {ordinal_date(dob)}."
    # the old name, title and age are spoken of in the story's text: say the new ones (whole words only)
    o_first, o_last = old["full_name"].split()[0], " ".join(old["full_name"].split()[1:])
    mapping = [(f"{old['title']} {o_first} {o_last}", f"{dem['title']} {full}"), (old["full_name"], full), (f"{old['title']} {o_first}", f"{dem['title']} {first}"), (f"{old['title']} {o_last}", f"{dem['title']} {last}"),
               (o_first, first), (o_last, last), (with_article(old["occupation"]), with_article(dem["occupation"])), (old["occupation"], dem["occupation"])]
    age_pats = [(rf"(?<!\d){old['age']}(?=-year-old|-yr|, | years old| year old)", str(age))]

    def fix(s: str) -> str:
        s = replace_words(s, mapping)
        for pat, rep in age_pats:
            s = re.sub(pat, rep, s)
        return s

    keep = {k: card[k] for k in ("identity", "wristband", "spoken_identity_answer")}
    card = walk_strings(card, fix)
    card.update(keep)
    # social, anaesthetic and allergy answers
    for b in by["SOC"]:
        _set_history(card, "smoking", b["attributes"]["smoking"])
        _set_history(card, "alcohol", b["attributes"]["alcohol"])
    for b in by["PSH"]:
        _set_history(card, "previous_anaesthetic", b["attributes"]["previous_anaesthetic"])
        _set_history(card, "family_anaesthetic", b["attributes"]["family_anaesthetic"])
    for b in by["ALG"]:
        card["allergies"] = copy.deepcopy(b["attributes"]["allergies"])
        card["allergy_answer"] = b["attributes"]["allergy_answer"]
    card["id"] = case_id
    card["walkthrough_label"] = f"{spine['walkthrough_label']} (composed {case_id})"
    card["created_by"] = "composer"
    card["status"] = "DRAFT"
    card.pop("approval", None)
    card["composition"] = {"template": f"SCN-{template['scenario']}", "seed": seed, "spine": spine["id"], "blocks": sorted(b["id"] for b in chosen), "solver_steps": None}
    return card


def _set_history(card: dict, topic: str, answer: str) -> None:
    for h in card["history"]:
        if h["topic"] == topic and h.get("variant") is None:
            h["answer"] = answer
            h["origin"] = "authored"       # chosen by the composer from authored cases
            return
    raise KeyError(topic)


# ---------------------------------------------------------------- spines and the whole run

def spines(scenario: str, difficulty: int, template: dict, cases: list[dict], targets: list[str] | None = None) -> list[tuple[dict, float]]:
    lim = template["difficulty_limits"][str(difficulty)]
    floor = B.STATUS_ORDER.index(template["spine_status_at_least"])
    out = []
    for c in cases:
        if c["scenario"] != scenario or c["tier"] not in lim["tiers"] or B.STATUS_ORDER.index(c["status"]) < floor or c.get("created_by") == "composer":
            continue
        if len(c["problems"]) > lim["max_problems"]:
            continue
        weight = 1.0
        if targets:
            text = B.case_text(c).lower()
            weight += 3.0 * sum(1 for t in targets if t.lower() in text)
        out.append((c, weight))
    return out


def fingerprint(spine_id: str, blocks: list[dict] | list[str]) -> str:
    """What makes a composed case the same story again: its spine and the blocks that are not names (a new name on the same story is not a new story)."""
    ids = sorted(b["id"] if isinstance(b, dict) else b for b in blocks)
    ids = [i for i in ids if not i.startswith("NAME-") and not i.startswith("DEM-")]
    return hashlib.sha256(json.dumps([spine_id, ids]).encode("utf-8")).hexdigest()[:16]


RELAX_ORDER = ("ALG", "PSH", "SOC")        # optional slots given up first when a spine cannot be varied, in this order


def build_card(spine: dict, scenario: str, case_seed: int, case_id: str, library: list[dict], template: dict, names: set[str], used_mrns: set[str], avoid_fingerprints: set[str] | None = None) -> tuple[dict, Choice]:
    """One composed card from one spine. If the story's fingerprint is one to avoid, other blocks are tried (up to 20 seeds), then optional slots are given up in RELAX_ORDER; Unsatisfiable if nothing is left."""
    avoid = avoid_fingerprints or set()
    for relaxed in range(len(RELAX_ORDER) + 1):
        tpl = copy.deepcopy(template)
        for s in tpl["slots"]:
            if s["type"] in RELAX_ORDER[:relaxed]:
                s["min"], s["max"] = 0, 0
        for attempt in range(20):
            rng = random.Random(case_seed * 31 + attempt)
            try:
                choice = Solver(library, tpl, spine, rng, names).solve()
            except Unsatisfiable:
                break
            if fingerprint(spine["id"], choice.blocks) in avoid:
                continue
            card = apply(spine, choice.blocks, case_id, case_seed, tpl, rng, used_mrns)
            card["composition"]["solver_steps"] = choice.steps
            return card, choice
    raise Unsatisfiable(f"spine {spine['id']} cannot be varied into a story that is not one to avoid")


def compose(scenario: str, difficulty: int, n: int, seed: int, targets: list[str] | None = None, cases: list[dict] | None = None, library: list[dict] | None = None) -> list[dict]:
    """n composed cards. Case i uses seed*1000+i; the card says so, so it can be made again."""
    cases = cases if cases is not None else B.load_cases()
    library = library if library is not None else load_library()
    template = load_template(scenario)
    pool = spines(scenario, difficulty, template, cases, targets)
    if not pool:
        raise Unsatisfiable(f"no spine for scenario {scenario} at difficulty {difficulty}")
    used_mrns = {c["identity"]["mrn"] for c in cases}
    names = {c["identity"]["full_name"] for c in cases}
    out = []
    for i in range(1, n + 1):
        case_seed = seed * 1000 + i
        rng = random.Random(case_seed)
        ordered = sorted(pool, key=lambda p: p[0]["id"])
        weights = [p[1] for p in ordered]
        tried, card = [], None
        while len(tried) < len(ordered):
            spine = rng.choices([p[0] for p in ordered], weights=[w if p[0]["id"] not in tried else 0.0 for p, w in zip(ordered, weights)])[0]
            try:
                card, _ = build_card(spine, scenario, case_seed, f"GEN-{scenario}-{seed:04d}-{i:03d}", library, template, names, used_mrns)
                break
            except Unsatisfiable:
                tried.append(spine["id"])          # a spine no block fits (for example no DEM block of its age class) is set aside for this case
        if card is None:
            raise Unsatisfiable(f"no spine of scenario {scenario} at difficulty {difficulty} can be varied with the library")
        names.add(card["identity"]["full_name"])
        out.append(card)
    return out
