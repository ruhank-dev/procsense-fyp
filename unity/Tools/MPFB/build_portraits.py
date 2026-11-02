"""Portrait photographs of fictional doctors for the corridor's wall displays: seven MPFB people (varied age, background, sex, hair) in professional clothing, lit like a studio portrait.

  BLENDER_USER_RESOURCES=<abs>/build/mpfb/blender-user build/blender/blender-4.5.14-windows-x64/blender.exe -b --factory-startup --python-exit-code 1 -P unity/Tools/MPFB/build_portraits.py -- [index ...]
Output: build/mpfb/output/portraits/doctor_<n>.png (700 x 900) and portraits.json (fictional names and departments). Nobody depicted is a real person; names and departments are invented.
"""
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
import build_staff as bs  # noqa: E402  (enables MPFB, defines build())
from staff_outfit import outfit, tint_texture, clear_of  # noqa: E402

root = Path(__file__).resolve().parents[3]
OUT = root / "build/mpfb/output/portraits"; OUT.mkdir(parents=True, exist_ok=True)

M, F = 1.0, 0.0
DOCTORS = [
    dict(name="Dr. Amara Osei", dept="Cardiology", g=F, age=.55, h=.52, w=.46, race={"asian": .1, "caucasian": .15, "african": .75}, skin="middleage_african_female/middleage_african_female.mhmat",
         hair="ponytail01/ponytail01.mhclo", hair_rgb=(.008, .006, .006), brow="eyebrow002/eyebrow002.mhclo", lash="eyelashes02/eyelashes02.mhclo", coat=True, top=(.05, .12, .16)),
    dict(name="Dr. Marcus Lindqvist", dept="General Surgery", g=M, age=.62, h=.56, w=.52, race={"asian": .05, "caucasian": .85, "african": .10}, skin="middleage_caucasian_male/middleage_caucasian_male.mhmat",
         hair="short02/short02.mhclo", hair_rgb=(.30, .27, .24), brow="eyebrow001/eyebrow001.mhclo", lash="eyelashes01/eyelashes01.mhclo", suit="male_elegantsuit01/male_elegantsuit01.mhclo"),
    dict(name="Dr. Mei-Ling Zhao", dept="Radiology", g=F, age=.42, h=.50, w=.42, race={"asian": .85, "caucasian": .1, "african": .05}, skin="young_asian_female/young_asian_female.mhmat",
         hair="long01/long01.mhclo", hair_rgb=(.0015, .0012, .0012), brow="eyebrow001/eyebrow001.mhclo", lash="eyelashes01/eyelashes01.mhclo", coat=True, top=(.62, .66, .70)),
    dict(name="Dr. Rafael Moreno", dept="Emergency Medicine", g=M, age=.48, h=.55, w=.50, race={"asian": .15, "caucasian": .6, "african": .25}, skin="young_caucasian_male/young_caucasian_male.mhmat",
         hair="short04/short04.mhclo", hair_rgb=(.02, .014, .011), brow="eyebrow004/eyebrow004.mhclo", lash="eyelashes01/eyelashes01.mhclo", coat=True, top=(.18, .24, .36)),
    dict(name="Dr. Fatima Al-Rashid", dept="Haematology", g=F, age=.5, h=.5, w=.48, race={"asian": .3, "caucasian": .5, "african": .2}, skin="middleage_caucasian_female/middleage_caucasian_female.mhmat",
         hair="ponytail01/ponytail01.mhclo", hair_rgb=(.03, .02, .015), brow="eyebrow005/eyebrow005.mhclo", lash="eyelashes02/eyelashes02.mhclo", coat=True, top=(.02, .17, .20)),
    dict(name="Prof. Henry Whitlock", dept="Clinical Director", g=M, age=.85, h=.54, w=.52, race={"asian": .05, "caucasian": .9, "african": .05}, skin="old_caucasian_male/old_caucasian_male.mhmat",
         hair="short01/short01.mhclo", hair_rgb=(.62, .62, .62), brow="eyebrow001/eyebrow001.mhclo", lash="eyelashes01/eyelashes01.mhclo", suit="male_elegantsuit01/male_elegantsuit01.mhclo"),
    dict(name="Dr. Kofi Mensah", dept="Anaesthetics", g=M, age=.4, h=.58, w=.46, race={"asian": .05, "caucasian": .1, "african": .85}, skin="young_african_male/young_african_male.mhmat",
         hair="short01/short01.mhclo", hair_rgb=(.004, .003, .003), brow="eyebrow003/eyebrow003.mhclo", lash="eyelashes01/eyelashes01.mhclo", coat=True, top=(.10, .30, .28)),
]


def build_doctor(i, spec):
    key = f"doc{i}"
    clothes = [spec["suit"]] if "suit" in spec else [("female_casualsuit01/female_casualsuit01.mhclo" if spec["g"] == 0 else "male_casualsuit06/male_casualsuit06.mhclo"), "female_elegantsuit01/female_elegantsuit01.mhclo"]
    bs.PEOPLE[key] = dict(name=f"Doctor{i}", gender=spec["g"], age=spec["age"], height=spec["h"], weight=spec["w"], muscle=.4, cupsize=.4, race=spec["race"], skin=spec["skin"],
                          eyebrows=spec["brow"], eyelashes=spec["lash"], hair=spec["hair"], hair_rgb=spec["hair_rgb"], clothes=clothes)
    if spec.get("coat"):
        bs.PEOPLE[key].update(scrubs=spec["top"], coat_out=.03)
    rig, human = bs.build(key)
    outfit(bs.PEOPLE[key], rig, human)
    if not spec.get("coat"):
        for o in rig.children:
            if o.type == "MESH" and "suit" in o.name:
                clear_of(o, [human], .007)
    if human.data.shape_keys:
        for n, v in (("mouthSmileLeft", .55), ("mouthSmileRight", .55), ("cheekSquintLeft", .25), ("cheekSquintRight", .25), ("browInnerUp", .08)):
            kb = human.data.shape_keys.key_blocks.get(n)
            if kb:
                kb.value = v
    return rig, human


def render(i, spec, rig, human):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"; sc.cycles.device = "CPU"; sc.cycles.samples = 56; sc.cycles.use_denoising = True
    sc.render.resolution_x, sc.render.resolution_y = 700, 900
    sc.world = bpy.data.worlds.new("w"); sc.world.use_nodes = True
    sc.world.node_tree.nodes["Background"].inputs[0].default_value = (.5, .52, .56, 1); sc.world.node_tree.nodes["Background"].inputs[1].default_value = .6
    head = rig.pose.bones["head"].head if "head" in rig.pose.bones else Vector((0, 0, 1.6))
    eye_z = head.z + .075; face = Vector((0, 0, eye_z - .06))
    # backdrop: a large plane with a soft vertical gradient in blue-grey
    bpy.ops.mesh.primitive_plane_add(size=6, location=(0, 1.2, eye_z - .3), rotation=(math.radians(90), 0, 0)); bg = bpy.context.object
    bm = bg.data
    mat = bpy.data.materials.new("bg"); mat.use_nodes = True; nt = mat.node_tree; b = nt.nodes["Principled BSDF"]; b.inputs["Roughness"].default_value = 1
    grad = nt.nodes.new("ShaderNodeTexGradient"); coord = nt.nodes.new("ShaderNodeTexCoord"); ramp = nt.nodes.new("ShaderNodeValToRGB"); mapn = nt.nodes.new("ShaderNodeMapping")
    mapn.inputs["Location"].default_value = (0, -.5, 0); mapn.inputs["Scale"].default_value = (1, .33, 1)
    ramp.color_ramp.elements[0].color = (.20, .27, .34, 1); ramp.color_ramp.elements[1].color = (.46, .55, .62, 1)
    nt.links.new(coord.outputs["UV"], mapn.inputs["Vector"]); nt.links.new(mapn.outputs["Vector"], grad.inputs["Vector"]); nt.links.new(grad.outputs["Fac"], ramp.inputs["Fac"]); nt.links.new(ramp.outputs["Color"], b.inputs["Base Color"])
    bg.data.materials.append(mat)

    def area(name, loc, energy, size):
        d = bpy.data.lights.new(name, "AREA"); d.energy = energy; d.size = size; o = bpy.data.objects.new(name, d); o.location = loc; bpy.context.collection.objects.link(o)
        tgt = bpy.data.objects.new("t" + name, None); tgt.location = face; bpy.context.collection.objects.link(tgt)
        c = o.constraints.new("TRACK_TO"); c.target = tgt; c.track_axis = "TRACK_NEGATIVE_Z"; c.up_axis = "UP_Y"
    area("key", (1.2, -1.6, eye_z + .35), 260, 1.4); area("fill", (-1.6, -1.4, eye_z), 90, 2.0); area("hair", (-.4, .9, eye_z + .8), 120, .8)
    cd = bpy.data.cameras.new("cam"); cd.lens = 85; cam = bpy.data.objects.new("cam", cd); bpy.context.collection.objects.link(cam); cam.location = (.04, -1.35, eye_z - .06)
    tgt = bpy.data.objects.new("ct", None); tgt.location = (0, 0, eye_z - .17); bpy.context.collection.objects.link(tgt)
    c = cam.constraints.new("TRACK_TO"); c.target = tgt; c.track_axis = "TRACK_NEGATIVE_Z"; c.up_axis = "UP_Y"
    sc.camera = cam; sc.render.filepath = str(OUT / f"doctor_{i}.png"); bpy.ops.render.render(write_still=True)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    which = [int(a) for a in argv] or list(range(len(DOCTORS)))
    for i in which:
        rig, human = build_doctor(i, DOCTORS[i]); render(i, DOCTORS[i], rig, human)
        print("PORTRAIT", i, DOCTORS[i]["name"])
    (OUT / "portraits.json").write_text(json.dumps([{"file": f"doctor_{i}.png", "name": d["name"], "dept": d["dept"]} for i, d in enumerate(DOCTORS)], indent=2))


if __name__ == "__main__":
    main()
