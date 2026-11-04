"""Collects the integrity data of every externally downloaded source the project uses and writes tools/setup/remote_metadata.json.

  python -I tools/setup/collect_sources.py            # network: Poly Haven API (md5), ambientCG zips (downloaded once into build/external/cache to compute sha256), Rocketbox tree (git blob sha1), Blender sha256 list

The file is read by build_manifest.py. Run it again only when a source is added or a version is changed. Nothing here is a secret; all URLs are public.
"""
from __future__ import annotations

import hashlib
import json
import subprocess
import sys
import urllib.request
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "tools" / "setup" / "remote_metadata.json"
CACHE = ROOT / "build" / "external" / "cache"
UA = {"User-Agent": "ProcSense-FYP-setup"}

AMBIENTCG = {  # id -> resolution actually used (see docs/research/asset_sources_and_licenses.md)
    "OfficeCeiling001": "2K", "Metal011": "2K", "Metal009": "2K", "Fabric066": "2K", "Fabric061": "2K", "Fabric086": "2K", "Plastic010": "2K", "Terrazzo001": "2K",
    "Plastic011": "2K", "Rubber004": "2K", "Asphalt031": "2K", "Road007": "2K", "Facade019A": "2K", "Facade018A": "2K", "Facade001": "2K", "PaintedPlaster017": "1K", "Wood050": "1K",
}
PH_TEXTURES = ["white_plaster_02", "rubber_tiles", "oak_veneer_01", "concrete_pavement_02"]
PH_MODELS = ["potted_plant_01", "potted_plant_02", "potted_plant_04", "wheelchair_01", "fire_alarm", "korean_fire_extinguisher_01", "hanging_picture_frame_02", "wall_clock",
             "fancy_picture_frame_01", "fancy_picture_frame_02", "hanging_picture_frame_01", "hanging_picture_frame_03"]
PH_HDRI = {"homecoming_center_rooftop": "8k", "kloofendal_48d_partly_cloudy_puresky": "4k"}
ROCKETBOX = ["Medical_Male_03", "Medical_Female_02", "Medical_Female_01", "Medical_Male_01", "Medical_Male_04", "Medical_Male_05", "Business_Female_03", "Male_Child_01", "Female_Child_01",
             "Male_Adult_03", "Female_Adult_14", "Female_Adult_05", "Female_Adult_08", "Male_Adult_08", "Male_Adult_16", "Female_Adult_15", "Male_Adult_13"]


def get_json(url: str):
    with urllib.request.urlopen(urllib.request.Request(url, headers=UA)) as r:
        return json.load(r)


def sha256_file(p: Path) -> str:
    h = hashlib.sha256()
    with open(p, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def download(url: str, dest: Path) -> None:
    dest.parent.mkdir(parents=True, exist_ok=True)
    if dest.exists() and dest.stat().st_size > 0:
        return
    subprocess.run(["curl", "-s", "-S", "-L", "-A", UA["User-Agent"], "--fail", "-o", str(dest), url], check=True)


def main() -> int:
    meta: dict = {"generated_utc": __import__("datetime").datetime.now(__import__("datetime").timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")}

    acg = {}
    for aid, res in AMBIENTCG.items():
        fn = f"{aid}_{res}-JPG.zip"; url = f"https://ambientcg.com/get?file={fn}"; dest = CACHE / "ambientcg" / fn
        download(url, dest)
        acg[aid] = {"file": fn, "url": url, "page": f"https://ambientcg.com/view?id={aid}", "sha256": sha256_file(dest), "size": dest.stat().st_size}
        print("ambientCG", aid, acg[aid]["size"])
    meta["ambientcg"] = acg

    ph_tex = {}
    for tid in PH_TEXTURES:
        files = get_json(f"https://api.polyhaven.com/files/{tid}")
        sel = {}
        for key in ("Diffuse", "nor_gl", "Rough", "AO", "Metal", "arm"):
            v = files.get(key, {}).get("2k", {}).get("jpg")
            if v:
                sel[key] = {"url": v["url"], "md5": v["md5"], "size": v["size"]}
        ph_tex[tid] = {"page": f"https://polyhaven.com/a/{tid}", "api": f"https://api.polyhaven.com/files/{tid}", "files": sel}
        print("PH texture", tid, list(sel))
    meta["polyhaven_textures"] = ph_tex

    ph_models = {}
    for mid in PH_MODELS:
        files = get_json(f"https://api.polyhaven.com/files/{mid}"); g = files["gltf"]["2k"]["gltf"]
        items = [{"rel": f"{mid}.gltf", "url": g["url"], "md5": g["md5"], "size": g["size"]}] + [{"rel": rel, "url": i["url"], "md5": i["md5"], "size": i["size"]} for rel, i in g["include"].items()]
        for key, val in files.items():
            if key.endswith("_alpha") and "2k" in val:
                a = val["2k"]["jpg"]; items.append({"rel": f"textures/{mid}_{key}_2k.jpg", "url": a["url"], "md5": a["md5"], "size": a["size"]})
        ph_models[mid] = {"page": f"https://polyhaven.com/a/{mid}", "api": f"https://api.polyhaven.com/files/{mid}", "files": items}
        print("PH model", mid, len(items))
    meta["polyhaven_models"] = ph_models

    hdri = {}
    for hid, res in PH_HDRI.items():
        v = get_json(f"https://api.polyhaven.com/files/{hid}")["hdri"][res]["hdr"]
        hdri[hid] = {"page": f"https://polyhaven.com/a/{hid}", "resolution": res, "url": v["url"], "md5": v["md5"], "size": v["size"]}
        print("PH hdri", hid, v["size"])
    meta["polyhaven_hdri"] = hdri

    commit = get_json("https://api.github.com/repos/microsoft/Microsoft-Rocketbox/commits/master")["sha"]
    tree = get_json(f"https://api.github.com/repos/microsoft/Microsoft-Rocketbox/git/trees/{commit}?recursive=1")
    if tree.get("truncated"):
        print("WARNING: Rocketbox tree truncated", file=sys.stderr)
    rb = {}
    for name in ROCKETBOX:
        files = [t for t in tree["tree"] if t["type"] == "blob" and f"/{name}/" in t["path"] and t["path"].startswith("Assets/Avatars")]
        keep = [t for t in files if t["path"].endswith(f"{name}_facial.fbx") or (("/Textures/" in t["path"]) and t["path"].lower().endswith(".tga"))]
        rb[name] = [{"path": t["path"], "git_blob_sha1": t["sha"], "size": t["size"]} for t in keep]
        print("Rocketbox", name, len(keep))
    meta["rocketbox"] = {"commit": commit, "repo": "https://github.com/microsoft/Microsoft-Rocketbox", "raw_base": f"https://raw.githubusercontent.com/microsoft/Microsoft-Rocketbox/{commit}/", "avatars": rb}

    bl = urllib.request.urlopen(urllib.request.Request("https://download.blender.org/release/Blender4.5/blender-4.5.14.sha256", headers=UA)).read().decode()
    meta["blender"] = {"version": "4.5.14", "sha256_list": "https://download.blender.org/release/Blender4.5/blender-4.5.14.sha256",
                       "windows_zip": "https://download.blender.org/release/Blender4.5/blender-4.5.14-windows-x64.zip",
                       "windows_zip_sha256": next((l.split()[0] for l in bl.splitlines() if l.strip().endswith("windows-x64.zip")), None)}
    OUT.write_text(json.dumps(meta, indent=1), encoding="utf-8")
    print("wrote", OUT)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
