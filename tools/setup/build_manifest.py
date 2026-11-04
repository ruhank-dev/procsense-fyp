"""Builds tools/setup/asset_manifest.json (machine-readable) and the generated tables of docs/new_unity_environment.md and docs/research/asset_sources_and_licenses.md.

  python -I tools/setup/build_manifest.py            # write the manifest and refresh the generated blocks in the two documents
  python -I tools/setup/build_manifest.py --check    # fail when the manifest or the generated blocks are out of date (used by CI and before every commit that touches assets)

Inputs: the curated source records below (creator, page, licence, redistribution, recipe), tools/setup/remote_metadata.json (checksums of the downloads, from collect_sources.py) and the files tracked by Git.
Every tracked file under unity/Assets, unity/Packages, unity/ProjectSettings and blender/ is listed with its SHA-256 (text files are hashed with LF line endings), so restore_environment.ps1 can prove a clone is complete.
"""
from __future__ import annotations

import hashlib
import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SETUP = ROOT / "tools" / "setup"
MANIFEST = SETUP / "asset_manifest.json"
REMOTE = json.loads((SETUP / "remote_metadata.json").read_text(encoding="utf-8"))
TRACKED_ROOTS = ["unity/Assets", "unity/Packages", "unity/ProjectSettings", "blender"]
TEXT_EXT = {".cs", ".meta", ".unity", ".prefab", ".mat", ".asset", ".shader", ".cginc", ".hlsl", ".json", ".md", ".txt", ".py", ".ps1", ".sh", ".yaml", ".yml", ".asmdef", ".asmref", ".shadergraph",
            ".shadersubgraph", ".controller", ".anim", ".overrideController", ".mask", ".physicMaterial", ".renderTexture", ".flare", ".cubemap", ".playable", ".signal", ".inputactions", ".uss", ".uxml",
            ".xml", ".csv", ".obj", ".mtl", ".lighting", ".preset", ".mixer", ".guiskin", ".fontsettings", ".spriteatlas", ".terrainlayer", ".brush", ".compute", ".raytrace", ".vfx", ".tpsd", ".txt"}

LICENSES = {
    "CC0": "https://creativecommons.org/publicdomain/zero/1.0/",
    "MIT-Rocketbox": "https://github.com/microsoft/Microsoft-Rocketbox/blob/master/LICENSE.md",
    "PolyHaven": "https://polyhaven.com/license",
    "ambientCG": "https://docs.ambientcg.com/license/",
    "UnityCompanion": "https://unity.com/legal/licenses/unity-companion-license",
    "Blender-GPL": "https://www.blender.org/about/license/",
    "MPFB": "https://github.com/makehumancommunity/mpfb2",
}

PBR_SETS = {   # local prefix in unity/Assets/Textures/Hospital/PBR -> (source kind, source id, note)
    "hosp_wall": ("polyhaven", "white_plaster_02", "recoloured"), "hosp_floor": ("polyhaven", "rubber_tiles", "recoloured"), "oak_veneer": ("polyhaven", "oak_veneer_01", ""),
    "sidewalk": ("polyhaven", "concrete_pavement_02", ""), "acoustic_ceiling": ("ambientcg", "OfficeCeiling001", ""), "satin_steel": ("ambientcg", "Metal011", ""),
    "brushed_steel": ("ambientcg", "Metal009", ""), "curtain_weave": ("ambientcg", "Fabric066", ""), "twill_cotton": ("ambientcg", "Fabric061", ""), "cotton_white": ("ambientcg", "Fabric086", ""),
    "white_plastic": ("ambientcg", "Plastic010", ""), "terrazzo": ("ambientcg", "Terrazzo001", ""), "dark_plastic": ("ambientcg", "Plastic011", ""), "rubber_black": ("ambientcg", "Rubber004", ""),
    "asphalt": ("ambientcg", "Asphalt031", ""), "road_marked": ("ambientcg", "Road007", ""), "facade_dark_glass": ("ambientcg", "Facade019A", ""), "facade_brick_glass": ("ambientcg", "Facade018A", ""),
    "facade_curtainwall": ("ambientcg", "Facade001", ""),
}
PH_MODEL_USE = {
    "potted_plant_01": "Floor plants, corridor and patient bay", "potted_plant_02": "Floor plants (syngonium), corridor and patient bay", "potted_plant_04": "Tabletop succulent (not yet placed)",
    "wheelchair_01": "Parked wheelchairs in the corridor", "fire_alarm": "Wall call points", "korean_fire_extinguisher_01": "Floor extinguishers", "hanging_picture_frame_02": "Framed photograph",
    "wall_clock": "Wall clocks", "fancy_picture_frame_01": "Imported, gilded frame, not placed", "fancy_picture_frame_02": "Imported, gilded frame, not placed",
    "hanging_picture_frame_01": "Framed pictures", "hanging_picture_frame_03": "Framed pictures",
}


def source_records() -> list[dict]:
    rb = REMOTE["rocketbox"]; recs: list[dict] = []
    recs.append(dict(id="rocketbox", name="Microsoft Rocketbox avatars (17 clothed, rigged characters)", category="Character", creator="Microsoft / Rocketbox Studios GmbH", source_page=rb["repo"],
                     download_url=rb["raw_base"] + "Assets/Avatars/<group>/<Name>/Export/<Name>_facial.fbx", version=f"git commit {rb['commit']}", format="FBX (Biped, 176 facial blend shapes) + TGA textures converted to JPG/PNG",
                     license="MIT", license_url=LICENSES["MIT-Rocketbox"], attribution="Keep the MIT licence text (copied to unity/Assets/Characters/Rocketbox/LICENSE.md)", redistribution="Yes (MIT)",
                     account_required="No", paths=["unity/Assets/Characters/Rocketbox"], used_in="Corridor (seated and standing people), OT (two staff), PatientBay (patient: Medical_Male_04 recoloured)",
                     restore={"method": "script", "command": "python -I unity/Tools/Characters/fetch_rocketbox.py [Name ...]", "note": "downloads at the pinned commit, converts textures; files are also in Git"}, downloads=[
                         {"path": f["path"], "git_blob_sha1": f["git_blob_sha1"], "size": f["size"], "url": rb["raw_base"] + f["path"]} for fl in rb["avatars"].values() for f in fl]))
    recs.append(dict(id="quaternius_ual", name="Universal Animation Library [Standard] (43 clips)", category="Animation", creator="Quaternius (Quaternius.com)", source_page="https://quaternius.com",
                     download_url="manual: Universal Animation Library [Standard] pack, file Unity/UAL1_Standard.fbx", version="Standard pack as downloaded in Nov 2026 (file hash in the manifest)", format="FBX (humanoid clips)",
                     license="CC0 1.0", license_url=LICENSES["CC0"], attribution="Not required; credited in docs", redistribution="Yes (CC0; copy of the licence text at unity/Assets/Characters/Animations/LICENSE_Quaternius_UAL_CC0.txt)",
                     account_required="Pack page of the author (no purchase for the Standard pack as used)", paths=["unity/Assets/Characters/Animations"], used_in="All Rocketbox avatars through Unity Humanoid retargeting (Corridor, OT, PatientBay)",
                     restore={"method": "git-or-manual", "note": "The FBX is in Git. If it is missing, download the pack from quaternius.com, drop the zip in build/external/manual/ and rerun restore_environment.ps1"}))
    for mid, info in REMOTE["polyhaven_models"].items():
        recs.append(dict(id=f"ph_model_{mid}", name=f"Poly Haven model {mid}", category="3D model", creator="Poly Haven contributors", source_page=info["page"], download_url=info["api"] + "  (glTF 2k files listed in downloads)",
                         version="2K glTF as served by the Poly Haven API on 3 Nov 2026 (md5 per file)", format="glTF + JPG, decimated and converted to FBX by convert_ph_model.py", license="CC0 1.0", license_url=LICENSES["PolyHaven"], attribution="Not required",
                         redistribution="Yes (CC0)", account_required="No", paths=[f"unity/Assets/Models/Environment/PolyHaven/{mid}", f"unity/Assets/Textures/PolyHaven/{mid}"], used_in=PH_MODEL_USE.get(mid, ""),
                         restore={"method": "script", "command": f"python -I unity/Tools/Environment/fetch_ph_model.py {mid}  then  python -I unity/Tools/Environment/convert_ph_model.py {mid}  (needs Blender 4.5.14)", "note": "files are also in Git"},
                         downloads=[{"path": f["rel"], "md5": f["md5"], "size": f["size"], "url": f["url"]} for f in info["files"]]))
    for hid, info in REMOTE["polyhaven_hdri"].items():
        local = "city_skyline_8k.hdr" if hid.startswith("homecoming") else "sky_partly_cloudy_4k.hdr"
        recs.append(dict(id=f"ph_hdri_{hid}", name=f"Poly Haven HDRI {hid} ({info['resolution']})", category="HDRI / sky", creator="Dario Barresi" if hid.startswith("homecoming") else "Greg Zaal, Jarod Guest", source_page=info["page"],
                         download_url=info["url"], version=f"{info['resolution']} .hdr (md5 {info['md5']})", format="HDR", license="CC0 1.0", license_url=LICENSES["PolyHaven"], attribution="Not required", redistribution="Yes (CC0)", account_required="No",
                         paths=[f"unity/Assets/Textures/Environment/Sky/{local}"], used_in="Exterior sky behind the windows (PatientBay, Corridor, OT); processed by CityExterior.cs",
                         restore={"method": "download-copy", "note": "the downloaded file is used as is (renamed to the local name)", "local_name": local}, downloads=[{"path": local, "md5": info["md5"], "size": info["size"], "url": info["url"]}]))
    for tid, info in REMOTE["polyhaven_textures"].items():
        sets = [k for k, v in PBR_SETS.items() if v[1] == tid]
        recs.append(dict(id=f"ph_texture_{tid}", name=f"Poly Haven texture {tid}", category="PBR texture", creator="Poly Haven contributors", source_page=info["page"], download_url=info["api"], version="2K JPG maps (md5 per file)",
                         format="JPG (Diffuse, nor_gl, Rough, AO)", license="CC0 1.0", license_url=LICENSES["PolyHaven"], attribution="Not required", redistribution="Yes (CC0)", account_required="No",
                         paths=[f"unity/Assets/Textures/Hospital/PBR/{s}_*" for s in sets], used_in="Hospital materials (see docs/research/asset_sources_and_licenses.md table)",
                         restore={"method": "script", "command": f"python -I unity/Tools/Environment/pack_pbr.py polyhaven <scan_dir> {tid} unity/Assets/Textures/Hospital/PBR <prefix>", "note": "files are also in Git"},
                         downloads=[{"path": f"{tid}_{k}.jpg", "md5": v["md5"], "size": v["size"], "url": v["url"]} for k, v in info["files"].items()]))
    for aid, info in REMOTE["ambientcg"].items():
        sets = [k for k, v in PBR_SETS.items() if v[1] == aid]
        loose = {"PaintedPlaster017": "unity/Assets/Textures/Hospital/PaintedPlaster017_1K-JPG_*", "Wood050": "unity/Assets/Textures/Hospital/Wood050_1K-JPG_*"}.get(aid)
        recs.append(dict(id=f"acg_{aid}", name=f"ambientCG {aid}", category="PBR texture", creator="ambientCG (Lennart Demes)", source_page=info["page"], download_url=info["url"], version=info["file"],
                         format="ZIP of JPG maps (Color, NormalGL, Roughness, AO, Metalness, Displacement)", license="CC0 1.0", license_url=LICENSES["ambientCG"], attribution="Not required", redistribution="Yes (CC0)", account_required="No",
                         paths=([f"unity/Assets/Textures/Hospital/PBR/{s}_*" for s in sets] + ([loose] if loose else [])), used_in="Hospital materials (walls, ceilings, steel, cloth, plastics, city and facades)",
                         restore={"method": "script", "command": f"python -I unity/Tools/Environment/pack_pbr.py ambientcg <unzipped_dir> {aid} unity/Assets/Textures/Hospital/PBR <prefix>", "note": "files are also in Git"},
                         downloads=[{"path": info["file"], "sha256": info["sha256"], "size": info["size"], "url": info["url"]}]))
    deps = json.loads((ROOT / "unity" / "Tools" / "MPFB" / "dependencies.json").read_text(encoding="utf-8"))
    recs.append(dict(id="makehuman_mpfb", name="MakeHuman system assets + MPFB 2.0.18 + face units + visemes (generated civilians, staff, MPFB patient)", category="Character source", creator="MakeHuman Community (MPFB add-on authors and asset artists)",
                     source_page="https://static.makehumancommunity.org/mpfb.html", download_url="; ".join(d["url"] for d in deps["downloads"]), version="MPFB v2.0.18 (commit d0a32e5); system assets CC0 pack", format="ZIP (Blender add-on and assets)",
                     license="Assets CC0; MPFB add-on: see its repository licence", license_url=LICENSES["MPFB"], attribution="Credit MakeHuman Community", redistribution="Models generated from CC0 assets are redistributed; the add-on itself is a download",
                     account_required="No", paths=["unity/Assets/Models/Characters"], used_in="PatientBay (hidden rig under the visible Rocketbox patient: sensors, idle hooks), older corridor/OT staff before the Rocketbox replacement",
                     restore={"method": "script", "command": "see unity/Tools/MPFB/README.md (Blender 4.5.14 + the four archives of dependencies.json)", "note": "files are also in Git"},
                     downloads=[{"path": d["file"], "sha256": d["sha256"].lower(), "url": d["url"]} for d in deps["downloads"]]))
    recs.append(dict(id="blender_tool", name="Blender 4.5.14 (build tool for the project's Blender scripts)", category="Tool", creator="Blender Foundation", source_page="https://www.blender.org/download/releases/4-5/",
                     download_url=REMOTE["blender"]["windows_zip"], version="4.5.14 (LTS)", format="ZIP (portable) or installer", license="GPL-2.0-or-later (Blender)", license_url=LICENSES["Blender-GPL"], attribution="None",
                     redistribution="Not redistributed (download)", account_required="No", paths=[], used_in="All unity/Tools/*/build_*.py generators and conversions", restore={"method": "download-extract", "dest": "build/blender"},
                     downloads=[{"path": "blender-4.5.14-windows-x64.zip", "sha256": REMOTE["blender"]["windows_zip_sha256"], "url": REMOTE["blender"]["windows_zip"]}]))
    recs.append(dict(id="unity_packages", name="Unity packages and package content kept in the project (TextMesh Pro, XR Interaction Toolkit samples, XRI settings)", category="Plugin / package", creator="Unity Technologies", source_page="https://docs.unity3d.com/Manual/upm-ui.html",
                     download_url="Unity Package Manager (versions pinned in unity/Packages/manifest.json and packages-lock.json)", version="see manifest.json", format="UPM", license="Unity Companion License / package licences", license_url=LICENSES["UnityCompanion"],
                     attribution="None", redistribution="Package manager restores packages; the sample and TextMesh Pro copies in Assets are part of the project", account_required="No (Unity Hub needs a free Unity ID to activate the Editor)",
                     paths=["unity/Assets/TextMesh Pro", "unity/Assets/Samples", "unity/Assets/XRI", "unity/Assets/XR"], used_in="All scenes (UI text, XR rig)", restore={"method": "unity-package-manager"}))
    recs.append(dict(id="project_owned", name="Project-owned models, textures, scripts, scenes and baked lighting", category="Project", creator="ProcSense team", source_page="https://github.com/ruhank-dev/procsense-fyp", download_url="this repository",
                     version="this commit", format="FBX, PNG, JPG, EXR, Unity YAML, C#, Blender .blend and Python", license="Project (academic FYP)", license_url="", attribution="n/a", redistribution="Yes (our own work; generated from CC0 sources where noted)",
                     account_required="No", paths=["unity/Assets", "blender/sources"], used_in="All scenes", restore={"method": "git"}))
    return recs


def grp(path: str) -> str:
    rules = [("unity/Assets/Characters/Rocketbox/", "rocketbox"), ("unity/Assets/Characters/Animations/", "quaternius_ual"), ("unity/Assets/Models/Environment/PolyHaven/", "polyhaven"), ("unity/Assets/Textures/PolyHaven/", "polyhaven"),
             ("unity/Assets/Prefabs/Environment/PolyHaven/", "polyhaven"), ("unity/Assets/Textures/Environment/Sky/", "polyhaven_hdri"), ("unity/Assets/Textures/Hospital/PBR/", "cc0_pbr"),
             ("unity/Assets/Textures/Hospital/PaintedPlaster017", "cc0_pbr"), ("unity/Assets/Textures/Hospital/Wood050", "cc0_pbr"), ("unity/Assets/Models/Characters/", "mpfb_generated"),
             ("unity/Assets/TextMesh Pro/", "unity_packages"), ("unity/Assets/Samples/", "unity_packages"), ("unity/Assets/XRI/", "unity_packages"), ("unity/Assets/XR/", "unity_packages"),
             ("unity/Packages/", "unity_packages"), ("unity/ProjectSettings/", "unity_settings"), ("blender/", "blender_sources")]
    for pre, g in rules:
        if path.startswith(pre):
            return g
    if path.startswith("unity/Assets/Scenes/") and re.search(r"(Lightmap-|ReflectionProbe-|LightingData|\.exr$)", path):
        return "baked_lighting"
    return "project_owned"


def sha256_norm(path: Path) -> tuple[str, int]:
    data = path.read_bytes()
    if path.suffix.lower() in TEXT_EXT:
        data = data.replace(b"\r\n", b"\n")
    return hashlib.sha256(data).hexdigest(), len(data)


def git(*args: str) -> bytes:
    return subprocess.run(["git", *args], cwd=ROOT, check=True, capture_output=True).stdout


def file_table() -> list[dict]:
    names = [n for n in git("ls-files", "-z", *TRACKED_ROOTS).decode("utf-8").split("\0") if n]
    lfs = set(l for l in git("lfs", "ls-files", "-n").decode("utf-8").splitlines())
    rows = []
    for n in sorted(names):
        p = ROOT / n
        if not p.exists():
            continue
        h, size = sha256_norm(p); rows.append({"path": n, "sha256": h, "size": size, "group": grp(n), "lfs": n in lfs})
    return rows


def rocketbox_check(rows: list[dict]) -> list[str]:
    """The FBX files are verbatim downloads: their git blob SHA-1 must equal the one in the pinned Rocketbox tree."""
    problems = []
    for name, files in REMOTE["rocketbox"]["avatars"].items():
        want = next(f["git_blob_sha1"] for f in files if f["path"].endswith("_facial.fbx"))
        p = ROOT / "unity" / "Assets" / "Characters" / "Rocketbox" / name / f"{name}.fbx"
        if not p.exists():
            problems.append(f"{name}: FBX missing"); continue
        b = p.read_bytes(); got = hashlib.sha1(b"blob %d\0" % len(b) + b).hexdigest()
        if got != want:
            problems.append(f"{name}: FBX differs from the pinned Rocketbox blob ({got[:10]} vs {want[:10]})")
    return problems


def render_tables(manifest: dict) -> str:
    out = []
    recs = manifest["sources"]
    out.append("| Asset | Category | Creator | Source page | Version | Format | Licence (terms) | Attribution | Redistribution | Account | In Git | Local path | Used in | Restore |")
    out.append("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|")
    for r in recs:
        paths = "<br>".join(f"`{p}`" for p in r["paths"][:6]) + ("<br>…" if len(r["paths"]) > 6 else "") if r["paths"] else "n/a (tool)"
        lic = f"[{r['license']}]({r['license_url']})" if r["license_url"] else r["license"]
        restore = r["restore"]["method"] + (f": `{r['restore']['command']}`" if r["restore"].get("command") else "")
        ingit = "Yes" if r["paths"] else "No (download)"
        out.append(f"| {r['name']} | {r['category']} | {r['creator']} | {r['source_page']} | {r['version']} | {r['format']} | {lic} | {r['attribution']} | {r['redistribution']} | {r['account_required']} | {ingit} | {paths} | {r['used_in']} | {restore} |")
    return "\n".join(out)


def render_packages() -> str:
    man = json.loads((ROOT / "unity" / "Packages" / "manifest.json").read_text(encoding="utf-8"))["dependencies"]
    lock = json.loads((ROOT / "unity" / "Packages" / "packages-lock.json").read_text(encoding="utf-8"))["dependencies"]
    lines = ["| Package id | Version (locked) | Source | Direct (manifest) | Depends on | Restored by |", "|---|---|---|---|---|---|"]
    for pid in sorted(lock):
        if pid.startswith("com.unity.modules."):
            continue
        e = lock[pid]; deps = ", ".join(f"`{d}`" for d in e.get("dependencies", {}) if not d.startswith("com.unity.modules.")) or "-"
        how = "this repository (file package `src/ProcSense.Core`)" if e["source"] == "local" else ("built into the Unity Editor" if e["source"] == "builtin" else "Unity Package Manager (Unity registry), automatic on first open")
        lines.append(f"| `{pid}` | {e['version']} | {e['source']} | {'yes' if pid in man else 'no (transitive)'} | {deps} | {how} |")
    mods = sorted(p for p in lock if p.startswith("com.unity.modules."))
    lines.append("")
    lines.append(f"Plus {len(mods)} built-in Unity modules (`com.unity.modules.*`, all version 1.0.0, shipped inside the Editor, nothing to install).")
    return "\n".join(lines)


def replace_block(path: Path, name: str, body: str) -> bool:
    start, end = f"<!-- BEGIN GENERATED:{name} -->", f"<!-- END GENERATED:{name} -->"
    text = path.read_text(encoding="utf-8")
    if start not in text:
        raise SystemExit(f"{path} has no generated block {name}")
    new = re.sub(re.escape(start) + r".*?" + re.escape(end), lambda m: f"{start}\n{body}\n{end}", text, flags=re.S)
    if new != text:
        path.write_text(new, encoding="utf-8", newline="\n")
    return new != text


def build() -> dict:
    rows = file_table(); groups: dict = {}
    for r in rows:
        g = groups.setdefault(r["group"], {"files": 0, "bytes": 0}); g["files"] += 1; g["bytes"] += r["size"]
    manifest = {"schema": 1, "project": "ProcSense", "repository": "https://github.com/ruhank-dev/procsense-fyp", "unity_editor": "6000.5.10f1", "unity_editor_revision": "3bd4f66ad299",
                "hash": "sha256 of the file content; text files hashed with LF line endings", "groups": groups, "sources": source_records(), "files": rows, "problems": rocketbox_check(rows)}
    return manifest


def main() -> int:
    manifest = build()
    text = json.dumps(manifest, indent=1)
    if "--check" in sys.argv:
        old = MANIFEST.read_text(encoding="utf-8") if MANIFEST.exists() else ""
        if json.loads(old) != json.loads(text):
            print("asset_manifest.json is out of date: run python -I tools/setup/build_manifest.py"); return 1
        for doc, name, body in ((ROOT / "docs" / "research" / "asset_sources_and_licenses.md", "inventory", render_tables(manifest)), (ROOT / "docs" / "new_unity_environment.md", "inventory", render_tables(manifest)), (ROOT / "docs" / "new_unity_environment.md", "packages", render_packages())):
            if doc.exists():
                m = re.search(r"<!-- BEGIN GENERATED:" + name + r" -->" + chr(10) + r"(.*?)" + chr(10) + r"<!-- END GENERATED:" + name + r" -->", doc.read_text(encoding="utf-8"), re.S)
                if not m or m.group(1) != body:
                    print(f"{doc.name}: generated block '{name}' is out of date: run python -I tools/setup/build_manifest.py"); return 1
        print(f"manifest ok: {len(manifest['files'])} files, {len(manifest['sources'])} sources, problems: {manifest['problems']}"); return 1 if manifest["problems"] else 0
    MANIFEST.write_text(text, encoding="utf-8", newline="\n")
    tables = render_tables(manifest)
    changed = [replace_block(ROOT / "docs" / "research" / "asset_sources_and_licenses.md", "inventory", tables)]
    envdoc = ROOT / "docs" / "new_unity_environment.md"
    if envdoc.exists():
        changed.append(replace_block(envdoc, "inventory", tables))
        changed.append(replace_block(envdoc, "packages", render_packages()))
    print(f"wrote {MANIFEST.name}: {len(manifest['files'])} files, {len(manifest['sources'])} sources; docs updated: {changed}; problems: {manifest['problems']}")
    return 1 if manifest["problems"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
