"""Builds the UI icon atlas from the Lucide SVGs (P3-UIX-06).

    uv run --project tools tools/ui/build_icon_atlas.py            # render unity/Assets/UI/Icons/Resources/UiIcons.png and UiIcons.json
    uv run --project tools tools/ui/build_icon_atlas.py --check    # the atlas lists exactly the SVGs in tools/ui/icons/lucide and every icon the code names exists

The sources are unmodified Lucide icons (ISC licence, tools/ui/icons/lucide/LICENSE, lucide-static 0.460.0). Each is drawn white on a transparent
cell of 128 px (96 px icon, 16 px margin so mipmaps never bleed into a neighbour), 8 per row, by headless Microsoft Edge: no image library and no
download. The colour is set in Unity (Image.color), so one atlas serves every state. UiIcons.json lists the names in cell order; the Unity editor
step (Assets/Editor/UI/UiIconImporter.cs) slices the atlas into one sprite per name, and UiIcons.Get(name) returns it at run time.
"""
from __future__ import annotations

import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCES = ROOT / "tools" / "ui" / "icons" / "lucide"
OUT = ROOT / "unity" / "Assets" / "UI" / "Icons" / "Resources"
PNG, INDEX = OUT / "UiIcons.png", OUT / "UiIcons.json"
CODE = ROOT / "unity" / "Assets" / "Scripts" / "UI" / "Theme" / "UiIcons.cs"
CELL, ICON, COLS = 128, 96, 8


def names() -> list[str]:
    return sorted(p.stem for p in SOURCES.glob("*.svg"))


def edge() -> str:
    for c in (os.environ.get("EDGE_EXE", ""), shutil.which("msedge") or "", r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
              r"C:\Program Files\Microsoft\Edge\Application\msedge.exe"):
        if c and Path(c).exists():
            return c
    raise SystemExit("Microsoft Edge not found: set EDGE_EXE")


def page(icons: list[str]) -> str:
    cells = []
    for i, n in enumerate(icons):
        svg = (SOURCES / f"{n}.svg").read_text(encoding="utf-8")
        svg = re.sub(r"<!--.*?-->", "", svg, flags=re.S)
        svg = re.sub(r'width="24"\s+height="24"', f'width="{ICON}" height="{ICON}"', svg, count=1).replace("currentColor", "#ffffff")
        x, y = (i % COLS) * CELL + (CELL - ICON) // 2, (i // COLS) * CELL + (CELL - ICON) // 2
        cells.append(f'<div style="position:absolute;left:{x}px;top:{y}px;width:{ICON}px;height:{ICON}px">{svg}</div>')
    return "<!doctype html><html><head><style>html,body{margin:0;background:transparent}</style></head><body>" + "".join(cells) + "</body></html>"


def size(count: int) -> tuple[int, int]:
    rows = (count + COLS - 1) // COLS
    h = 1
    while h < rows * CELL:
        h *= 2
    return COLS * CELL, h      # 1024 wide, height the next power of two (ASTC and mipmaps)


def build() -> int:
    icons = names()
    w, h = size(len(icons))
    OUT.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory() as tmp:
        html = Path(tmp) / "atlas.html"
        html.write_text(page(icons), encoding="utf-8")
        shot = Path(tmp) / "atlas.png"
        subprocess.run([edge(), "--headless=new", "--disable-gpu", "--hide-scrollbars", "--default-background-color=00000000", "--force-device-scale-factor=1",
                        f"--window-size={w},{h}", f"--screenshot={shot}", f"--user-data-dir={Path(tmp) / 'profile'}", html.as_uri()], capture_output=True, timeout=120, check=False)
        if not shot.exists():
            raise SystemExit("Edge did not write the screenshot")
        shutil.copyfile(shot, PNG)
    INDEX.write_text(json.dumps({"source": "Lucide (lucide-static 0.460.0), ISC licence", "cell": CELL, "columns": COLS, "width": w, "height": h, "names": icons}, indent=1) + "\n",
                     encoding="utf-8", newline="\n")
    print(f"wrote {PNG.relative_to(ROOT)} ({w}x{h}, {len(icons)} icons)")
    return 0


def problems() -> list[str]:
    p = []
    if not PNG.exists() or not INDEX.exists():
        return ["the atlas is not built (run task ui-icons)"]
    index = json.loads(INDEX.read_text(encoding="utf-8"))
    if index["names"] != names():
        p.append("UiIcons.json does not list exactly the SVGs in tools/ui/icons/lucide (run task ui-icons)")
    head = PNG.read_bytes()[:24]
    w, h = int.from_bytes(head[16:20], "big"), int.from_bytes(head[20:24], "big")
    if (w, h) != (index["width"], index["height"]):
        p.append(f"UiIcons.png is {w}x{h}, the index says {index['width']}x{index['height']}")
    if CODE.exists():
        for n in re.findall(r'public const string \w+ = "([a-z0-9-]+)"', CODE.read_text(encoding="utf-8")):
            if n not in index["names"]:
                p.append(f"UiIcons.cs names {n}, which is not in the atlas")
    if not (SOURCES / "LICENSE").exists():
        p.append("tools/ui/icons/lucide/LICENSE is missing")
    return p


def main(argv: list[str]) -> int:
    if "--check" in argv:
        p = problems()
        for x in p:
            print("FAIL  " + x)
        if not p:
            print(f"PASS  icon atlas current ({len(names())} icons)")
        return 1 if p else 0
    return build()


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
