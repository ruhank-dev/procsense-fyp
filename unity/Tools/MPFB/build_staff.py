"""Hospital staff characters (receptionist, two nurses) built with MPFB 2.0.18 exactly like the patient: GameEngine rig, CC0 system assets, Faceunits + Visemes shape keys.

Run from the repository root with BLENDER_USER_RESOURCES pointing at build/mpfb/blender-user (see README.md in this folder):
  BLENDER_USER_RESOURCES=<abs>/build/mpfb/blender-user build/blender/blender-4.5.14-windows-x64/blender.exe -b --factory-startup --python-exit-code 1 -P unity/Tools/MPFB/build_staff.py -- <key> [<key> ...]
Keys: receptionist, nurse_f, nurse_m. Output: build/mpfb/output/staff/<key>.blend (rig + body + eyes + brows + lashes + teeth + tongue + hair + clothes), a validation JSON, and preview renders.
All people are fictional: generic MPFB presets, no real individual is depicted. Garment recipes (scrubs split from the casual suit, lab coat lengthened from a jacket) are in this file.
"""
import json
import math
import sys
from pathlib import Path

import bpy
import bmesh
from mathutils import Vector

root = Path(__file__).resolve().parents[3]
src = next((root / "build/mpfb/source").glob("*/src"))
bpy.context.preferences.extensions.repos.new(name="ProcSense MPFB", module="procsense", custom_directory=str(src))
import addon_utils  # noqa: E402
addon_utils.enable("bl_ext.procsense.mpfb", default_set=True)
from bl_ext.procsense.mpfb.services.locationservice import LocationService  # noqa: E402
prefs = bpy.context.preferences.addons["bl_ext.procsense.mpfb"].preferences
prefs.mpfb_second_root = str(root / "build/mpfb/asset-library"); LocationService.update_second_root()
from bl_ext.procsense.mpfb.services.humanservice import HumanService  # noqa: E402
from bl_ext.procsense.mpfb.services.exportservice import ExportService  # noqa: E402
from bl_ext.procsense.mpfb.services.targetservice import TargetService  # noqa: E402
from bl_ext.procsense.mpfb.services.faceservice import FaceService  # noqa: E402

sys.path.insert(0, str(Path(__file__).resolve().parent))
from staff_outfit import outfit  # noqa: E402
OUT = root / "build/mpfb/output/staff"
OUT.mkdir(parents=True, exist_ok=True)

BASE = {
    "phenotype": {"age": .5, "gender": 1.0, "height": .5, "muscle": .4, "weight": .5, "proportions": .5, "cupsize": .5, "firmness": .5,
                  "race": {"asian": .33, "caucasian": .34, "african": .33}},
    "targets": [], "rig": "game_engine", "skin_material_type": "GAMEENGINE", "skin_material_settings": {}, "eyes_material_type": "GAMEENGINE", "eyes_material_settings": {},
    "eyes": "high-poly/high-poly.mhclo", "teeth": "teeth_base/teeth_base.mhclo", "tongue": "tongue01/tongue01.mhclo", "proxy": "", "color_adjustments": {},
    "material_instances": "NEVER", "alternative_materials": {},
}

PEOPLE = {
    "receptionist": dict(name="Receptionist", gender=0.0, age=.40, height=.52, weight=.44, muscle=.35, cupsize=.40, race={"asian": .25, "caucasian": .5, "african": .25},
                         skin="young_caucasian_female2/young_caucasian_female2.mhmat", eyebrows="eyebrow005/eyebrow005.mhclo", eyelashes="eyelashes02/eyelashes02.mhclo",
                         hair="short03/short03.mhclo", hair_rgb=(.09, .055, .035), clothes=["female_elegantsuit01/female_elegantsuit01.mhclo", "shoes04/shoes04.mhclo"]),
    "nurse_f": dict(name="NurseFemale", gender=0.0, age=.42, height=.50, weight=.46, muscle=.38, cupsize=.40, race={"asian": .45, "caucasian": .3, "african": .25},
                    skin="young_asian_female/young_asian_female.mhmat", eyebrows="eyebrow001/eyebrow001.mhclo", eyelashes="eyelashes01/eyelashes01.mhclo",
                    hair="ponytail01/ponytail01.mhclo", hair_rgb=(.04, .03, .026), scrubs=(.04, .13, .62), coat_out=.016, clothes=["female_casualsuit01/female_casualsuit01.mhclo", "female_elegantsuit01/female_elegantsuit01.mhclo", "shoes05/shoes05.mhclo"]),
    "nurse_m": dict(name="NurseMale", gender=1.0, age=.45, height=.55, weight=.50, muscle=.45, cupsize=.5, race={"asian": .15, "caucasian": .25, "african": .60},
                    skin="young_african_male/young_african_male.mhmat", eyebrows="eyebrow004/eyebrow004.mhclo", eyelashes="eyelashes01/eyelashes01.mhclo",
                    hair="short04/short04.mhclo", hair_rgb=(.025, .02, .018), scrubs=(.56, .70, .90), trousers=(.07, .07, .09), coat_out=.018, clothes=["male_casualsuit06/male_casualsuit06.mhclo", "female_elegantsuit01/female_elegantsuit01.mhclo", "shoes05/shoes05.mhclo"]),
}


def build(key):
    spec = PEOPLE[key]
    bpy.ops.object.select_all(action="SELECT"); bpy.ops.object.delete(use_global=False)
    preset = json.loads(json.dumps(BASE))
    ph = preset["phenotype"]
    for k in ("age", "gender", "height", "weight", "muscle", "cupsize"):
        ph[k] = spec[k]
    ph["race"] = spec["race"]
    preset.update({"name": spec["name"], "skin_mhmat": spec["skin"], "eyebrows": spec["eyebrows"], "eyelashes": spec["eyelashes"], "hair": spec["hair"], "clothes": spec["clothes"]})
    settings = HumanService.get_default_deserialization_settings(); settings["subdiv_levels"] = 0
    settings["override_clothes_model"] = "GAMEENGINE"; settings["override_eyes_model"] = "GAMEENGINE"
    human = HumanService.deserialize_from_dict(preset, settings)
    rig = human.parent
    TargetService.bake_targets(human)
    FaceService.load_targets(human, load_microsoft_visemes=False, load_meta_visemes=True, load_arkit_faceunits=True)
    FaceService.interpolate_targets(human)
    for o in list(rig.children):
        if o.type == "MESH":
            if o == human:
                for mod in list(o.modifiers):
                    if mod.type == "MASK" and mod.vertex_group != "body":
                        o.modifiers.remove(mod)
            ExportService.bake_modifiers_remove_helpers(o, bake_masks=True, bake_subdiv=False, remove_helpers=True, also_proxy=False)
            for m in list(o.modifiers):
                if m.type == "SUBSURF":
                    o.modifiers.remove(m)
            o.data.calc_loop_triangles()
            print("PART", key, o.name, len(o.data.vertices), len(o.data.loop_triangles))
    return rig, human


def main():
    keys = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else list(PEOPLE)
    report = {}
    for key in keys:
        rig, human = build(key)
        outfit(PEOPLE[key], rig, human)
        bpy.ops.wm.save_as_mainfile(filepath=str(OUT / f"{key}.blend"))
        report[key] = {"bones": len(rig.data.bones), "meshes": [{"name": o.name, "verts": len(o.data.vertices)} for o in rig.children if o.type == "MESH"]}
        print("STAFF_BUILT", key, json.dumps(report[key]))
    (OUT / "build_report.json").write_text(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
