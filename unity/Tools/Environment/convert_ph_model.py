"""Converts a fetched Poly Haven model for Unity: textures repacked to the project's PBR layout, geometry decimated and exported as FBX.

  python -I convert_ph_model.py <id> <target_triangles>        (runs Blender itself)
Outputs: unity/Assets/Textures/PolyHaven/<material>_{albedo,normal,mask,ao}.png and unity/Logs/PhModels/<id>.fbx plus <id>.json (material names, triangle count, bounds).
albedo (RGBA when a leaf alpha exists), normal = OpenGL map, mask = RGBA (R metallic, A smoothness), ao.
"""
import json
import subprocess
import sys
from pathlib import Path

import cv2
import numpy as np

ROOT = Path(__file__).resolve().parents[3]
BLENDER = ROOT / "build" / "blender" / "blender-4.5.14-windows-x64" / "blender.exe"
mid, target = sys.argv[1], int(sys.argv[2]) if len(sys.argv) > 2 else 20000
src = ROOT / "build" / "phmodels" / mid
gltf = json.loads((src / f"{mid}.gltf").read_text(encoding="utf8"))
tex = ROOT / "unity" / "Assets" / "Textures" / "PolyHaven"; tex.mkdir(parents=True, exist_ok=True)


def rd(name, gray=False):
    p = src / "textures" / name
    return cv2.imread(str(p), cv2.IMREAD_GRAYSCALE if gray else cv2.IMREAD_COLOR) if p.exists() else None


for m in gltf["materials"]:
    n = m["name"]
    if not (src / "textures" / f"{n}_diff_2k.jpg").exists(): n = mid            # single-material models name the glTF material "lambert1" but the maps after the model
    diff = rd(f"{n}_diff_2k.jpg"); nor = rd(f"{n}_nor_gl_2k.jpg")
    arm = rd(f"{n}_arm_2k.jpg"); rough = rd(f"{n}_rough_2k.jpg", True); alpha = rd(f"{n}_alpha_2k.jpg", True)
    h, w = diff.shape[:2]
    bgra = np.dstack([diff, alpha if alpha is not None else np.full((h, w), 255, np.uint8)]) if alpha is not None else diff
    cv2.imwrite(str(tex / f"{n}_albedo.png"), bgra)
    cv2.imwrite(str(tex / f"{n}_normal.png"), nor)
    mask = np.zeros((h, w, 4), np.uint8); ao = np.full((h, w), 255, np.uint8)
    if arm is not None:
        ao = arm[..., 2]; mask[..., 2] = arm[..., 0]; mask[..., 3] = 255 - arm[..., 1]          # arm jpg is BGR: R=ao, G=roughness, B=metal
    elif rough is not None:
        mask[..., 3] = 255 - rough
    cv2.imwrite(str(tex / f"{n}_mask.png"), mask); cv2.imwrite(str(tex / f"{n}_ao.png"), ao)
    print("packed", n, "alpha" if alpha is not None else "", "arm" if arm is not None else "rough")

script = ROOT / "unity" / "Logs" / "PhModels" / f"_convert_{mid}.py"; script.parent.mkdir(parents=True, exist_ok=True)
script.write_text(f'''
import bpy, json, bmesh
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=r"{src / (mid + '.gltf')}")
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
bpy.ops.object.select_all(action="DESELECT")
for o in meshes: o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
tris = sum(len(p.vertices) - 2 for o in meshes for p in o.data.polygons)
for o in meshes:
    t = sum(len(p.vertices) - 2 for p in o.data.polygons); share = t / max(1, tris)
    want = max(500, int({target} * share))
    if t > want:
        m = o.modifiers.new("Decimate", "DECIMATE"); m.ratio = want / t; m.use_collapse_triangulate = True
        bpy.context.view_layer.objects.active = o; bpy.ops.object.modifier_apply(modifier=m.name)
out_tris = sum(len(p.vertices) - 2 for o in meshes for p in o.data.polygons)
from mathutils import Vector
co = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
b = [[min(c[i] for c in co) for i in range(3)], [max(c[i] for c in co) for i in range(3)]]
bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(filepath=r"{ROOT / 'unity' / 'Logs' / 'PhModels' / (mid + '.fbx')}", object_types={{"MESH"}}, axis_forward="-Z", axis_up="Y", apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS", bake_space_transform=True, add_leaf_bones=False, path_mode="STRIP")
open(r"{ROOT / 'unity' / 'Logs' / 'PhModels' / (mid + '.json')}", "w").write(json.dumps({{"id": "{mid}", "tris_in": tris, "tris_out": out_tris, "bounds": b, "materials": [m.name for o in meshes for m in o.data.materials]}}))
print("CONVERTED", "{mid}", tris, "->", out_tris, b)
''', encoding="utf8")
r = subprocess.run([str(BLENDER), "-b", "--factory-startup", "--python-exit-code", "1", "-P", str(script)], capture_output=True, text=True)
print("\n".join(l for l in r.stdout.splitlines() if "CONVERTED" in l or "Error" in l)); print(r.stderr[-500:] if r.returncode else "")
