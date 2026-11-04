"""Opens every committed Blender source and checks it (run inside Blender 4.5.14):

  build\\blender\\blender-4.5.14-windows-x64\\blender.exe --background --python tools/setup/validate_blender.py -- <out.json>

For each blender/sources/**/*.blend: it opens, no linked library is used, every image is packed, generated or present on disk, objects/materials/armatures/actions are counted, and the scene exports to FBX
with the project's Unity settings (-Z forward, Y up) into a temporary folder, proving the export script path works and giving the exported size.
"""
import json
import sys
import tempfile
from pathlib import Path

import bpy

ROOT = Path(bpy.path.abspath("//")).resolve() if False else Path(__file__).resolve().parents[2]
out = Path(sys.argv[sys.argv.index("--") + 1]) if "--" in sys.argv else ROOT / "build" / "blender_validation.json"
results, bad = [], 0
tmp = Path(tempfile.mkdtemp(prefix="procsense_fbx_"))
for blend in sorted((ROOT / "blender" / "sources").rglob("*.blend")):
    r = {"file": str(blend.relative_to(ROOT)).replace("\\", "/")}
    try:
        bpy.ops.wm.open_mainfile(filepath=str(blend))
        r["libraries"] = [l.filepath for l in bpy.data.libraries]
        missing = []
        for im in bpy.data.images:
            if im.source == "FILE" and not im.packed_file and im.filepath:
                p = Path(bpy.path.abspath(im.filepath))
                if not p.exists():
                    missing.append(im.filepath)
        r["missing_images"] = missing
        r["objects"] = len(bpy.data.objects); r["meshes"] = len(bpy.data.meshes); r["materials"] = len(bpy.data.materials)
        r["armatures"] = len(bpy.data.armatures); r["actions"] = len(bpy.data.actions)
        meshes = [o for o in bpy.data.objects if o.type == "MESH"]
        for o in bpy.context.scene.objects:
            o.select_set(o.type in {"MESH", "ARMATURE"})
        dst = tmp / (blend.stem + ".fbx")
        if meshes:
            bpy.ops.export_scene.fbx(filepath=str(dst), use_selection=True, axis_forward="-Z", axis_up="Y", apply_scale_options="FBX_SCALE_ALL", object_types={"MESH", "ARMATURE"})
            r["fbx_bytes"] = dst.stat().st_size if dst.exists() else 0
        r["ok"] = not r["libraries"] and not missing and (r.get("fbx_bytes", 1) > 0)
    except Exception as e:  # noqa: BLE001
        r["error"] = str(e); r["ok"] = False
    bad += 0 if r["ok"] else 1
    results.append(r)
    print(("OK   " if r["ok"] else "FAIL ") + r["file"], {k: r.get(k) for k in ("objects", "materials", "armatures", "actions", "fbx_bytes")})
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps({"blender": bpy.app.version_string, "files": len(results), "failed": bad, "results": results}, indent=1), encoding="utf-8")
print(f"validated {len(results)} blend files with Blender {bpy.app.version_string}: {bad} failed")
sys.exit(1 if bad else 0)
