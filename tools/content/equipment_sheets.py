"""Writes docs/research/equipment/NN-<slug>.md for all 53 items of the equipment list (p0-medical-research-data s09) plus an index and the room-objects sheet.

    uv run --project tools tools/content/equipment_sheets.py            # write
    uv run --project tools tools/content/equipment_sheets.py --check    # fail if out of date or a sheet breaks the rules (CI)

Data: tools/content/equipment_data.py. Item names, tiers, kinds and rooms come from docs/plans/p0/p0-blender-equipment/equipment-list.md; the interaction points and asset needs come from
docs/plans/p0/p0-vr-interaction-ui/equipment-interaction-spec.md. Rules checked: all 53 items have a sheet; every dimension row has a source level; every row that is not 'open' names a
source and a URL (or a named standard); every 'open' dimension is repeated under Open points; no dimension row is left without a value.
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(HERE))
import equipment_data as data  # noqa: E402

OUT = ROOT / "docs" / "research" / "equipment"
LIST = ROOT / "docs" / "plans" / "p0" / "p0-blender-equipment" / "equipment-list.md"
SPEC = ROOT / "docs" / "plans" / "p0" / "p0-vr-interaction-ui" / "equipment-interaction-spec.md"
LEVELS = {"listing": "manufacturer specification as printed on a distributor or catalogue page", "standard": "published standard", "datasheet": "manufacturer data sheet opened", "unpublished": "the data sheet, regulatory file, manual and listings named in the source were opened and the figure is not published; the model uses a design size, marked approximate", "design": "a user-interface, layout or content item with no physical dimension; defined by the project", "open": "not found"}
DATE = "10 October 2026"


def cells(line: str) -> list[str]:
    return [c.strip() for c in line.strip().strip("|").split("|")]


def parse_list() -> dict[int, dict]:
    items = {}
    for line in LIST.read_text(encoding="utf-8").split("\n"):
        m = re.match(r"^\| (\d+) \|", line)
        if m:
            c = cells(line)
            items[int(c[0])] = {"name": c[1], "scenarios": c[2], "steps": c[3], "tier": c[4], "kind": c[5], "room": c[6], "detail": c[7]}
    return items


def parse_spec() -> dict[int, list[dict]]:
    out: dict[int, list[dict]] = {}
    for line in SPEC.read_text(encoding="utf-8").split("\n"):
        m = re.match(r"^\| (\d+) \|", line)
        if m:
            c = cells(line)
            if len(c) >= 6:
                out.setdefault(int(c[0]), []).append({"item": c[1], "does": c[2], "system": c[3], "failure": c[4], "needs": c[5]})
    return out


def sheet(n: int, it: dict, li: dict, spec: list[dict]) -> str:
    L = [f"# {n:02d}: {li['name']} (equipment-list #{n})", "",
         f"**Status:** DRAFT, approved for training on the owner's statement · **Written:** {DATE} · **Tier:** {li['tier']} · **Kind:** {li['kind']} · **Room:** {li['room']} · **Scenarios (S1 / S2):** {li['scenarios']} · **Steps:** {li['steps']}", "",
         f"**Representative device:** {it['rep']}", "", "## Dimensions (every number has a source)", "", "| Part | Value | Source (date opened or seen: " + DATE + ") | Level | Exact / approx |", "|---|---|---|---|---|"]
    for dm in it["dims"]:
        src = f"[{dm['source']}]({dm['url']})" if dm["url"] else dm["source"]
        L.append(f"| {dm['part']} | {dm['value']} | {src} | {dm['level']} | {dm['exact']} |")
    L += ["", "Levels: " + "; ".join(f"**{k}** = {v}" for k, v in LEVELS.items()) + ".", "", "## Components", "", it["comps"], "", "## Materials and colours", "", it["mats"], "", "## States", ""]
    L += [f"- {s}" for s in it["states"]]
    L += ["", "## Interaction (from the equipment list and the interaction spec)", "", f"- Equipment list: {li['detail']}"]
    for s in spec:
        L += [f"- Trainee does: {s['does']}", f"- System response: {s['system']}", f"- Failure or scenario: {s['failure']}", f"- Asset needs (named points): {s['needs']}"]
    L += ["", "## Triangle-budget class", "", it["tri"], "", "## Open points (nothing here is guessed)", ""]
    opens = list(it["open_"]) + [f"{dm['part']}: no source found" for dm in it["dims"] if dm["level"] == "open" and not any(dm["part"] in o for o in it["open_"])]
    opens += [f"{dm['part']}: " + ("not published by the manufacturer (a design size is used, approximate)" if dm["level"] == "unpublished" else "design item, no physical dimension") for dm in it["dims"] if dm["level"] in ("unpublished", "design")]
    L += [f"- {o}" for o in opens] if opens else ["- None."]
    return "\n".join(L) + "\n"


def problems() -> list[str]:
    out = []
    li = parse_list()
    if sorted(li) != list(range(1, 54)):
        out.append("equipment-list.md must have items 1 to 53")
    if sorted(data.ITEMS) != list(range(1, 54)):
        out.append("equipment_data.py must have items 1 to 53")
    slugs = [v["slug"] for v in data.ITEMS.values()]
    if len(slugs) != len(set(slugs)):
        out.append("duplicate slugs")
    for n, it in data.ITEMS.items():
        for dm in it["dims"]:
            if dm["level"] not in LEVELS:
                out.append(f"item {n}: unknown level {dm['level']}")
            if dm["level"] in ("datasheet", "listing", "unpublished") and not dm["url"]:
                out.append(f"item {n}: '{dm['part']}' is level {dm['level']} but has no URL")
            if dm["level"] in ("unpublished", "design") and not dm["source"].strip():
                out.append(f"item {n}: '{dm['part']}' is level {dm['level']} but names no source or reason")
            if dm["level"] == "standard" and not dm["source"]:
                out.append(f"item {n}: '{dm['part']}' names no standard")
            if not dm["value"].strip():
                out.append(f"item {n}: '{dm['part']}' has no value")
            if dm["level"] == "open" and dm["exact"] == "exact" and not re.search(r"not found|not in that|set by|none|no figure|returned|name indicates", dm["value"] + dm["source"], re.I):
                out.append(f"item {n}: '{dm['part']}' is open but states a value as exact")
        if not it["dims"]:
            out.append(f"item {n}: no dimension rows")
    return out


def build() -> dict[Path, str]:
    li, sp = parse_list(), parse_spec()
    files = {OUT / f"{n:02d}-{it['slug']}.md": sheet(n, it, li[n], sp.get(n, [])) for n, it in sorted(data.ITEMS.items())}
    idx = ["# Equipment reference sheets", "", f"Generated by `tools/content/equipment_sheets.py` from `tools/content/equipment_data.py` ({DATE}); do not edit. One sheet per item of `docs/plans/p0/p0-blender-equipment/equipment-list.md` (53 items) plus [room objects](room_objects.md). "
           "Each dimension carries the source it came from and how it was found (data sheet opened, manufacturer specification on a catalogue page, a standard, or not found). Anything not found stays 'open': the Blender script uses a placeholder and the sheet says so.", "",
           "| # | Item | Representative device | Dimensions with a source | Open |", "|---|---|---|---|---|"]
    for n, it in sorted(data.ITEMS.items()):
        got = sum(1 for d in it["dims"] if d["level"] != "open")
        idx.append(f"| {n} | [{li[n]['name']}]({n:02d}-{it['slug']}.md) | {it['rep']} | {got} of {len(it['dims'])} | {'yes' if got < len(it['dims']) or it['open_'] else 'no'} |")
    files[OUT / "README.md"] = "\n".join(idx) + "\n"
    room = ["# Room objects the trainee uses (not in Part E)", "", f"Generated; {DATE}. From the list in `equipment-list.md` (interaction-relevant room objects).", "", "| Object | Dimensions | Source | Level |", "|---|---|---|---|"]
    for name, val, src, url, level in data.ROOM:
        room.append(f"| {name} | {val} | {f'[{src}]({url})' if url else src} | {level} |")
    files[OUT / "room_objects.md"] = "\n".join(room) + "\n"
    return files


def main(argv: list[str]) -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    probs = problems()
    files = build()
    if "--check" in argv:
        for p, t in files.items():
            if not p.exists() or p.read_text(encoding="utf-8").replace("\r\n", "\n") != t:
                probs.append(f"{p.relative_to(ROOT).as_posix()} is out of date: run `uv run --project tools tools/content/equipment_sheets.py`")
        stale = {p.name for p in OUT.glob("*.md")} - {p.name for p in files} - {"_template.md"}
        probs += [f"docs/research/equipment/{s} is not generated (old pilot sheet?): delete it" for s in sorted(stale)]
        for p in probs:
            print("  - " + p)
        return 1 if probs else 0
    OUT.mkdir(parents=True, exist_ok=True)
    for p in OUT.glob("*.md"):
        if p.name != "_template.md":
            p.unlink()
    for p, t in files.items():
        p.write_text(t, encoding="utf-8", newline="\n")
    print(f"wrote {len(files)} files ({len(data.ITEMS)} sheets); " + ("no problems" if not probs else f"{len(probs)} problem(s)"))
    for p in probs:
        print("  - " + p)
    return 1 if probs else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
