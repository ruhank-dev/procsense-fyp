"""Downloads a Poly Haven (CC0) model as glTF plus its textures into build/phmodels/<id>/ (2K textures).

  python -I fetch_ph_model.py <id> [<id> ...]
Source: https://api.polyhaven.com/files/<id>. Licence: CC0 1.0, record every id in docs/research/asset_sources_and_licenses.md.
"""
import json
import subprocess
import sys
from pathlib import Path
from urllib.request import Request, urlopen

ROOT = Path(__file__).resolve().parents[3]
for mid in sys.argv[1:]:
    req = Request(f"https://api.polyhaven.com/files/{mid}", headers={"User-Agent": "ProcSense-FYP"})
    g = json.load(urlopen(req))["gltf"]["2k"]["gltf"]
    out = ROOT / "build" / "phmodels" / mid
    (out / "textures").mkdir(parents=True, exist_ok=True)
    subprocess.run(["curl", "-s", "-L", "-o", str(out / f"{mid}.gltf"), g["url"]], check=True)
    for rel, info in g["include"].items():
        subprocess.run(["curl", "-s", "-L", "-o", str(out / rel), info["url"]], check=True)
    print("fetched", mid, len(g["include"]), "files")

# leaf cut-out masks are not part of the glTF includes: fetch any *_alpha map separately (2K jpg)
for mid in sys.argv[1:]:
    req = Request(f"https://api.polyhaven.com/files/{mid}", headers={"User-Agent": "ProcSense-FYP"})
    for key, val in json.load(urlopen(req)).items():
        if key.endswith("_alpha") and "2k" in val:
            out = ROOT / "build" / "phmodels" / mid / "textures" / f"{mid}_{key}_2k.jpg"
            subprocess.run(["curl", "-s", "-L", "-o", str(out), val["2k"]["jpg"]["url"]], check=True)
            print("alpha", out.name)
