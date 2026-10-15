"""Writes the attributions (credits and sources) from the project's registers (p0-evaluation-performance-s07).

    uv run --project tools tools/eval/attributions.py            # write docs/ATTRIBUTIONS.md and content/attributions/attributions.json (the in-app Credits & sources page)
    uv run --project tools tools/eval/attributions.py --check    # fail if either is out of date or a register row lacks what an attribution needs

Only the registers are read, nothing is typed here: the source register (guidelines and other published sources a clinical value cites), the dataset register (datasets in use),
the picture indexes content/{images,ecg,cxr,xsec}/index.json (every picture: synthetic or from a dataset, with its licence and attribution text), the provenance notes of the 3D character and the textures, the package
register (software libraries and their licences) and the pinned tool versions. A row without a source name, a used dataset without a licence, a picture without a licence or a package
without a licence fails the check, so a credit is never missing silently.
"""
from __future__ import annotations

import csv
import json
import re
import sys
from collections import OrderedDict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DOC = ROOT / "docs" / "ATTRIBUTIONS.md"
APP = ROOT / "content" / "attributions" / "attributions.json"
PICTURE_INDEXES = ["images", "ecg", "cxr", "xsec"]       # ultrasound, ECG strips, chest films, CT and MRI slices
PROVENANCE = [
    ("3D character", "Patient (MPFB, MakeHuman assets)", "unity/Assets/Models/Characters/MPFBPatient/README.md"),
    ("Textures", "Hospital wall and floor surfaces (ambientCG)", "unity/Assets/Textures/Hospital/README.md"),
]


def rows(rel: str) -> list[dict]:
    with open(ROOT / rel, encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f))


def collect() -> tuple[dict, list[str]]:
    problems: list[str] = []

    sources: "OrderedDict[str, dict]" = OrderedDict()
    for r in rows("docs/research/source_register.csv"):
        name = (r.get("source_name") or "").strip()
        if not name:
            problems.append(f"source register {r['id']}: no source name")
            continue
        if name.startswith("ProcSense"):
            continue                                   # the project's own documents are not third-party sources
        s = sources.setdefault(name, {"name": name, "url": "", "rows": 0})
        s["rows"] += 1
        if not s["url"] and r.get("source_url", "").startswith("http"):
            s["url"] = r["source_url"].split()[0]

    datasets = []
    for r in rows("docs/research/dataset_register.csv"):
        if not r["status"].startswith("USED"):
            continue
        if not r["licence"].strip():
            problems.append(f"dataset register {r['id']} ({r['name']}): used but no licence")
        datasets.append({"id": r["id"], "name": r["name"], "licence": r["licence"], "url": r["url"].split()[0] if r["url"] else "", "use": r["purpose"]})

    images = [im for folder in PICTURE_INDEXES for im in json.loads((ROOT / "content" / folder / "index.json").read_text(encoding="utf-8"))["images"]]
    groups: "OrderedDict[tuple, dict]" = OrderedDict()
    for im in images:
        if not im.get("licence"):
            problems.append(f"picture {im['id']}: no licence")
            continue
        src = im.get("source") or {}
        key = (bool(im.get("synthetic")), src.get("dataset", "") if isinstance(src, dict) else str(src), im["licence"], im.get("attribution") or "")
        g = groups.setdefault(key, {"synthetic": key[0], "source": key[1] or ("ProcSense (generated)" if key[0] else ""), "licence": key[2], "attribution": key[3], "count": 0})
        g["count"] += 1

    assets = []
    for kind, what, rel in PROVENANCE:
        p = ROOT / rel
        if not p.exists():
            problems.append(f"{rel}: the provenance note is missing")
            continue
        text = p.read_text(encoding="utf-8")
        lic = "CC0 1.0" if "CC0" in text else ""
        if not lic:
            problems.append(f"{rel}: the provenance note states no licence")
        assets.append({"kind": kind, "what": what, "licence": lic, "note": rel})

    packages = []
    for r in rows("docs/research/package_register.csv"):
        if not r["licence"].strip():
            problems.append(f"package register {r['id']} ({r['name']}): no licence")
        packages.append({"name": r["name"], "version": r["version"], "ecosystem": r["ecosystem"], "licence": r["licence"], "use": r["used_for"]})

    unity = re.search(r"m_EditorVersion: (\S+)", (ROOT / "unity" / "ProjectSettings" / "ProjectVersion.txt").read_text(encoding="utf-8")).group(1)
    blender = (ROOT / "tools" / "blender" / "VERSION").read_text(encoding="utf-8").strip()
    dotnet = json.loads((ROOT / "global.json").read_text(encoding="utf-8"))["sdk"]["version"]
    tools = [{"name": "Unity", "version": unity}, {"name": "Blender", "version": blender}, {"name": ".NET SDK", "version": dotnet}]

    data = {
        "sources": sorted(sources.values(), key=lambda s: s["name"].lower()),
        "datasets": datasets,
        "pictures": list(groups.values()),
        "assets": assets,
        "voices_and_sounds": [],
        "packages": packages,
        "tools": tools,
    }
    return data, problems


def markdown(d: dict) -> str:
    L = ["# Attributions", "",
         "_Generated by `tools/eval/attributions.py` from the project's registers; do not edit by hand (`task attributions`). The same list is shown in the app's Credits & sources page (`content/attributions/attributions.json`)._", "",
         "ProcSense is an educational simulation. All patients are invented. It is not a medical device.", "",
         "## Guidelines and published sources", "", "Sources that clinical values in the content cite (from `docs/research/source_register.csv`).", "",
         "| Source | Values citing it | Link |", "|---|---|---|"]
    L += [f"| {s['name']} | {s['rows']} | {s['url'] or '-'} |" for s in d["sources"]]
    L += ["", "## Datasets", "", "Datasets in use (from `docs/research/dataset_register.csv`; licences and conditions in `docs/research/licence_provenance_register.md`).", "", "| Dataset | Licence | Used for | Link |", "|---|---|---|---|"]
    L += [f"| {x['name']} | {x['licence']} | {x['use']} | {x['url'] or '-'} |" for x in d["datasets"]]
    L += ["", "## Pictures", "", "Every picture in the app (from `content/images`, `content/ecg`, `content/cxr` and `content/xsec`, each `index.json`).", "", "| Pictures | Source | Licence | Attribution |", "|---|---|---|---|"]
    L += [f"| {g['count']} {'synthetic (labelled as such)' if g['synthetic'] else 'real'} | {g['source'] or '-'} | {g['licence']} | {g['attribution'] or '-'} |" for g in d["pictures"]]
    L += ["", "## 3D character and textures", "", "| What | Licence | Provenance |", "|---|---|---|"]
    L += [f"| {a['what']} | {a['licence']} | `{a['note']}` |" for a in d["assets"]]
    L += ["", "## Voices and sounds", "", "None yet: voices and sounds are added with p0-voice-audio, and each will be listed here from its register.", "",
          "## Software", "", "Libraries (from `docs/research/package_register.csv`).", "", "| Package | Version | Licence | Used for |", "|---|---|---|---|"]
    L += [f"| {p['name']} ({p['ecosystem']}) | {p['version']} | {p['licence']} | {p['use']} |" for p in d["packages"]]
    L += ["", "Tools: " + ", ".join(f"{t['name']} {t['version']}" for t in d["tools"]) + "."]
    return "\n".join(L) + "\n"


def main(argv: list[str]) -> int:
    sys.stdout.reconfigure(encoding="utf-8")
    data, problems = collect()
    md, js = markdown(data), json.dumps(data, indent=1, ensure_ascii=False) + "\n"
    if "--check" in argv:
        for p, text in ((DOC, md), (APP, js)):
            if not p.exists() or p.read_text(encoding="utf-8") != text:
                problems.append(f"{p.relative_to(ROOT).as_posix()} is out of date: run tools/eval/attributions.py")
        for p in problems:
            print("  - " + p)
        print("attributions: " + ("up to date, every register row has what an attribution needs" if not problems else f"{len(problems)} problems"))
        return 1 if problems else 0
    if problems:
        for p in problems:
            print("  - " + p)
        return 1
    APP.parent.mkdir(parents=True, exist_ok=True)
    DOC.write_text(md, encoding="utf-8", newline="\n")
    APP.write_text(js, encoding="utf-8", newline="\n")
    print(f"wrote {DOC.relative_to(ROOT)} ({len(data['sources'])} sources, {len(data['datasets'])} datasets, {sum(g['count'] for g in data['pictures'])} pictures, {len(data['packages'])} packages) and {APP.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
