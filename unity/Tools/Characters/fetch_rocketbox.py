"""Downloads (at a pinned commit, see tools/setup/remote_metadata.json) the chosen Microsoft Rocketbox avatars (MIT licence) from GitHub and converts their textures to project-sized maps.

  python -I fetch_rocketbox.py            ->  unity/Assets/Characters/Rocketbox/<Name>/<Name>.fbx + <map>.jpg/png
Source: https://github.com/microsoft/Microsoft-Rocketbox  (Assets/Avatars/<group>/<Name>/Export/<Name>_facial.fbx and Textures/*.tga). The raw maps are 2048 px TGA (12 MB each); they are stored as
color JPG q90 at 2048, normal PNG at 1024 and specular as a 1024 JPG, opacity PNG at 1024. Licence text is copied next to the avatars (docs/research/asset_sources_and_licenses.md).
"""
import json
import subprocess
import sys
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / "unity" / "Assets" / "Characters" / "Rocketbox"
RAW = ROOT / "build" / "rb"
META = json.loads((ROOT / "tools" / "setup" / "remote_metadata.json").read_text(encoding="utf8"))["rocketbox"]
COMMIT = META["commit"]                                   # pinned commit of github.com/microsoft/Microsoft-Rocketbox: the files below are exactly the ones the project was built from
BASE = META["raw_base"]
TREE = [{"path": f["path"], "type": "blob"} for fl in META["avatars"].values() for f in fl]

CAST = ["Medical_Male_03", "Medical_Female_02", "Medical_Female_01", "Medical_Male_01", "Medical_Male_04", "Medical_Male_05", "Business_Female_03",
        "Male_Child_01", "Female_Child_01", "Male_Adult_03", "Female_Adult_14", "Female_Adult_05", "Female_Adult_08", "Male_Adult_08", "Male_Adult_16", "Female_Adult_15", "Male_Adult_13"]


def get(path, dest):
    dest.parent.mkdir(parents=True, exist_ok=True)
    if not dest.exists():
        subprocess.run(["curl", "-s", "-L", "-A", "ProcSense", "-o", str(dest), BASE + path], check=True)


names = sys.argv[1:] or CAST
(OUT).mkdir(parents=True, exist_ok=True)
get("LICENSE.md", OUT / "LICENSE.md")
for name in names:
    files = [t["path"] for t in TREE if t["type"] == "blob" and f"/{name}/" in t["path"] and t["path"].startswith("Assets/Avatars")]
    fbx = next(p for p in files if p.endswith(f"{name}_facial.fbx"))
    dst = OUT / name; dst.mkdir(exist_ok=True)
    get(fbx, dst / f"{name}.fbx")
    for p in files:
        if "/Textures/" not in p or not p.lower().endswith(".tga"):
            continue
        base = Path(p).stem; raw = RAW / name / (base + ".tga"); get(p, raw)
        im = Image.open(raw)
        if base.endswith("_color"):
            im.convert("RGB").save(dst / f"{base}.jpg", quality=90) if im.mode != "RGBA" else im.save(dst / f"{base}.png")
        elif base.endswith("_normal"):
            im.convert("RGB").resize((1024, 1024), Image.LANCZOS).save(dst / f"{base}.png")
        elif base.endswith("_specular"):
            im.convert("RGB").resize((1024, 1024), Image.LANCZOS).save(dst / f"{base}.jpg", quality=88)
        else:
            im.convert("RGBA" if im.mode == "RGBA" else "L").resize((1024, 1024), Image.LANCZOS).save(dst / f"{base}.png")
        raw.unlink()
    print("fetched", name, sorted(x.name for x in dst.iterdir()))
