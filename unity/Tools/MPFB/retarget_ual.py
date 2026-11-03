"""Retargets Quaternius Universal Animation Library clips (CC0, real motion-capture style sitting / talking / walking / interacting) onto an MPFB GameEngine-rig character, adds hand-held props with grip IK,
and exports one FBX per character with every clip as an animation take.

  blender -b --factory-startup --python-exit-code 1 -P unity/Tools/MPFB/retarget_ual.py -- <key> [--preview <png-prefix>]

Input : build/mpfb/output/staff/<key>.blend (unposed rig, clothes, hair; produced by build_staff.py / build_civilians.py)
        build/ual/x/Universal Animation Library[Standard]/Unity/UAL1_Standard.fbx  (download: see docs/research/asset_sources_and_licenses.md)
Output: build/mpfb/output/staff/<key>/<key>.fbx (+ textures, materials.json via staff_bake helpers), <key>_anim.json (pelvis heights for seat alignment).

Both skeletons use the same bone names (pelvis, spine_01..03, neck_01, head, clavicle, upperarm, lowerarm, hand, finger chains, thigh, calf, foot, ball). Retargeting is done in world space per frame:
the world-space rotation change of every source bone away from its rest orientation is applied to the target bone's rest orientation (so differing rest poses A/T do not matter), and the pelvis motion is scaled by
the leg-length ratio so sitting height and stride suit each body. Hand props are bound to the hand bone; grip targets ride on the chest so they follow breathing and posture changes.
"""
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Quaternion, Vector

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / "build/mpfb/output/staff"
UAL = ROOT / "build/ual/x/Universal Animation Library[Standard]/Unity/UAL1_Standard.fbx"
sys.path.insert(0, str(Path(__file__).resolve().parent))

# ---------------------------------------------------------------- per-character plan
# clip: base UAL clip; look: (yaw_deg toward -X/right positive?, pitch_deg) added on neck/head; hold: prop plan; extra: named behaviours
PLAN = {
    "patient_m_old": dict(clips=["Sitting_Idle_Loop", "Sitting_Talking_Loop"], main="Sitting_Idle_Loop", look=(0, 2)),
    "patient_f_old": dict(clips=["Sitting_Idle_Loop"], main="Sitting_Idle_Loop", look=(0, 3)),
    "adult_f_phone": dict(clips=["Sitting_Idle_Loop"], main="Sitting_Idle_Loop", look=(0, 16), hold=dict(prop="phone", hands="both", center=(0, .215, -.01), tilt=42, grip_r=(-.052, .20, -.075), grip_l=(.052, .20, -.075), thumb_scroll=True)),
    "child": dict(clips=["Sitting_Idle_Loop"], main="Sitting_Idle_Loop", look=(14, 0)),
    "parent_f": dict(clips=["Sitting_Talking_Loop"], main="Sitting_Talking_Loop", look=(-16, 0)),
    "visitor_m_paper": dict(clips=["Idle_Loop"], main="Idle_Loop", look=(0, 22), hold=dict(prop="sheet", hands="both", center=(0, .27, .035), tilt=24, grip_r=(-.105, .245, -.085), grip_l=(.105, .245, -.085))),
    "visitor_f_talk": dict(clips=["Idle_Talking_Loop"], main="Idle_Talking_Loop", look=(-12, 0)),
    "visitor_m_cough": dict(clips=["Idle_Loop"], main="Idle_Loop", look=(10, 4), cough=True),
    "nurse_f": dict(clips=["Idle_Talking_Loop"], main="Idle_Talking_Loop", look=(-10, 0), hold=dict(prop="clipboard", hands="l", center=(.13, .18, -.30), tilt=12, grip_l=(.12, .17, -.40))),
    "nurse_m": dict(clips=["Idle_Talking_Loop"], main="Idle_Talking_Loop", look=(10, 0), hold=dict(prop="clipboard", hands="r", center=(-.13, .18, -.30), tilt=12, grip_r=(-.12, .17, -.40))),
    "walker_nurse": dict(clips=["Walk_Formal_Loop", "Idle_Loop"], main="Walk_Formal_Loop", look=(0, 0)),
    "walker_visitor": dict(clips=["Walk_Loop", "Idle_Loop"], main="Walk_Loop", look=(0, 0)),
}
SEAT_TRIM = .012            # library seat is a little lower than the hospital bench (0.465 m)
FINGERS = ("index", "middle", "ring", "pinky")


def depth(b):
    d = 0
    while b.parent:
        b = b.parent; d += 1
    return d


def load_target(key):
    bpy.ops.wm.open_mainfile(filepath=str(OUT / f"{key}.blend"))
    return next(o for o in bpy.data.objects if o.type == "ARMATURE")


def import_source():
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=str(UAL))
    new = [o for o in bpy.data.objects if o not in before]
    arm = next(o for o in new if o.type == "ARMATURE")
    for o in new:
        if o.type == "MESH":
            bpy.data.objects.remove(o, do_unlink=True)
    return arm


def assign(arm, act):
    arm.animation_data_create(); arm.animation_data.action = act
    if hasattr(arm.animation_data, "action_slot") and len(act.slots):
        arm.animation_data.action_slot = act.slots[0]


def retarget(tgt, src, clip_name, look):
    """Writes action `clip_name` on tgt from the UAL action of the same name; returns pelvis world heights (min, max)."""
    sc = bpy.context.scene
    act = next(a for a in bpy.data.actions if a.name.endswith("|" + clip_name))
    assign(src, act)
    f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
    new = bpy.data.actions.new(clip_name); tgt.animation_data_create(); tgt.animation_data.action = new
    if hasattr(tgt.animation_data, "action_slot") and hasattr(new, "slots") and not len(new.slots):
        pass
    smap = {b.name.lower(): b.name for b in src.data.bones}
    order = sorted(tgt.data.bones, key=depth)
    Wt = tgt.matrix_world; Ws = src.matrix_world
    def toe_dir(arm, W):                                   # which way the feet point in world space (the character's facing)
        return (W @ arm.data.bones["ball_l"].head_local - W @ arm.data.bones["foot_l"].head_local).normalized()
    if toe_dir(tgt, Wt).dot(toe_dir(src, Ws)) < 0:         # the library skeleton faces the opposite way: turn it about Z so both face the same direction
        Ws = Matrix.Rotation(math.pi, 4, "Z") @ Ws
    tgt_rest = {b.name: Wt @ b.matrix_local for b in tgt.data.bones}
    src_rest = {n: Ws @ src.data.bones[n].matrix_local for n in smap.values()}
    scale = tgt_rest["pelvis"].translation.z / src_rest[smap["pelvis"]].translation.z
    for pb in tgt.pose.bones:
        pb.rotation_mode = "QUATERNION"
    # look offsets (yaw about world Z toward the character's left when positive, pitch forward when positive)
    yaw, pitch = look
    heights = []
    for f in range(f0, f1 + 1):
        sc.frame_set(f)
        P = {}
        for b in order:
            n = b.name; pb = tgt.pose.bones[n]
            if b.parent is None:
                Q = tgt_rest[n].copy()
            else:
                Q = P[b.parent.name] @ tgt_rest[b.parent.name].inverted() @ tgt_rest[n]
            if n.lower() in smap:
                sn = smap[n.lower()]; spose = Ws @ src.pose.bones[sn].matrix
                D = spose.to_3x3() @ src_rest[sn].to_3x3().inverted()
                dS = (src_rest[sn].to_3x3() @ Vector((0, 1, 0))).normalized(); dA = (tgt_rest[n].to_3x3() @ Vector((0, 1, 0))).normalized()
                C = dA.rotation_difference(dS).to_matrix()                       # rest-pose alignment: the target limb starts in a different rest pose (A) than the library (T)
                R = D @ C @ tgt_rest[n].to_3x3()
                pos = Q.translation.copy()
                if n == "pelvis":
                    pos = tgt_rest[n].translation + (spose.translation - src_rest[sn].translation) * scale          # standing / walking: the body's own proportions scale the motion
                    if "Sitting" in clip_name:
                        pos.z = spose.translation.z + SEAT_TRIM                                                        # seated: the SEAT sets the pelvis height, not the legs (short legs, like a child's, simply dangle)
                if n in ("neck_01", "head") and (yaw or pitch):
                    share = .5
                    R = Matrix.Rotation(math.radians(yaw * share), 3, "Z") @ R                                  # turn toward the other person
                    right = Vector((1, 0, 0)); R = Matrix.Rotation(math.radians(pitch * share), 3, right) @ R    # +pitch = nod forward (matches the earlier pose tooling)
                Pd = Matrix.Translation(pos) @ R.to_4x4()
            else:
                Pd = Q.copy()
            basis = Q.inverted() @ Pd
            pb.rotation_quaternion = basis.to_quaternion(); pb.location = basis.translation if n in ("pelvis", "root") else (0, 0, 0)
            P[n] = Pd
            pb.keyframe_insert("rotation_quaternion", frame=f)
            if n in ("pelvis", "root"):
                pb.keyframe_insert("location", frame=f)
        heights.append(P["pelvis"].translation.z)
    return f0, f1, min(heights), max(heights), scale


def bone_world(tgt, name):
    return tgt.matrix_world @ tgt.pose.bones[name].matrix


def make_empty(name, world_pos, tgt, bone):
    e = bpy.data.objects.new(name, None); bpy.context.collection.objects.link(e); e.empty_display_size = .03
    e.parent = tgt; e.parent_type = "BONE"; e.parent_bone = bone
    e.matrix_parent_inverse = bone_world(tgt, bone).inverted()
    e.location = world_pos
    return e


def hold_ik(tgt, f0, f1, hold, plan_main, cough=False):
    """Grip IK: empties ride on spine_03, IK constraints on both arms (or one), bake to the action, then curl the fingers and add a thumb scroll."""
    sc = bpy.context.scene; sc.frame_set(f0); bpy.context.view_layer.update()
    chest = bone_world(tgt, "spine_03").translation.copy()

    def at(offset):                                     # (left+, forward+, up+) in character terms; the character faces -Y, its left side is +X
        return chest + Vector((offset[0], -offset[1], offset[2]))

    targets = {}
    for side, key in (("r", "grip_r"), ("l", "grip_l")):
        if key in hold:
            e = make_empty(f"grip_{side}", at(hold[key]), tgt, "spine_03")
            sgn = -1 if side == "r" else 1
            p = make_empty(f"pole_{side}", at((sgn * .55, -.05, -.45)), tgt, "spine_03")
            c = tgt.pose.bones[f"lowerarm_{side}"].constraints.new("IK"); c.target = e; c.pole_target = p; c.pole_angle = math.radians(-90 if side == "r" else -90); c.chain_count = 2
            targets[side] = (e, p)
    bpy.ops.object.mode_set(mode="OBJECT") if bpy.context.object and bpy.context.object.mode != "OBJECT" else None
    bpy.ops.object.select_all(action="DESELECT"); tgt.select_set(True); bpy.context.view_layer.objects.active = tgt
    bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.nla.bake(frame_start=f0, frame_end=f1, step=1, only_selected=False, visual_keying=True, clear_constraints=True, use_current_action=True, bake_types={"POSE"})
    bpy.ops.object.mode_set(mode="OBJECT")
    for e, p in targets.values():
        bpy.data.objects.remove(e, do_unlink=True); bpy.data.objects.remove(p, do_unlink=True)


def curl_fingers(tgt, sides, f0, f1, deg=40, thumb=22, scroll=False):
    """Finger flexion about the hand's lateral axis expressed in each finger bone's own frame (constant), keyed on every frame; optional slow thumb scrolling for phones."""
    for side in sides:
        idx = tgt.data.bones[f"index_01_{side}"].head_local; pin = tgt.data.bones[f"pinky_01_{side}"].head_local
        lateral = ((pin - idx)).normalized()
        for finger in FINGERS + ("thumb",):
            for j in (1, 2, 3):
                n = f"{finger}_{j:02d}_{side}"
                if n not in tgt.pose.bones:
                    continue
                b = tgt.data.bones[n]; axis = (b.matrix_local.to_3x3().inverted() @ lateral).normalized()
                amt = (thumb if finger == "thumb" else deg * (1.1 if finger == "pinky" else 1.0)) * (.6, 1.0, .8)[j - 1]
                for f in range(f0, f1 + 1):
                    extra = amt + (5 * math.sin((f - f0) / (f1 - f0 + 1) * math.tau * 2) if (scroll and finger == "thumb") else 0)
                    pb = tgt.pose.bones[n]
                    pb.rotation_quaternion = Quaternion(axis, math.radians(extra))
                    pb.keyframe_insert("rotation_quaternion", frame=f)


def make_prop(tgt, hold, f0):
    """Builds the prop at its posed place (relative to the chest at frame f0), then converts the vertices to rest space of the holding hand and skins them 100 percent to that hand bone."""
    import bmesh
    sc = bpy.context.scene; sc.frame_set(f0); bpy.context.view_layer.update()
    chest = bone_world(tgt, "spine_03").translation.copy(); cx = chest + Vector((hold["center"][0], -hold["center"][2] * 0 - hold["center"][1], hold["center"][2]))
    kind = hold["prop"]
    def box(name, size, loc, rgb, rough=.5):
        me = bpy.data.meshes.new(name); bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
        for v in bm.verts: v.co = (v.co.x * size[0], v.co.y * size[1], v.co.z * size[2])
        bm.to_mesh(me); bm.free(); o = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(o); o.location = loc
        m = bpy.data.materials.new(name); m.use_nodes = True; b = m.node_tree.nodes.get("Principled BSDF"); b.inputs["Base Color"].default_value = (*rgb, 1); b.inputs["Roughness"].default_value = rough
        o.data.materials.append(m); return o
    parts = []
    if kind == "phone":
        parts = [box("phone_body", (.072, .009, .150), (0, 0, 0), (.08, .09, .10), .35), box("phone_screen", (.066, .0015, .140), (0, .0045, 0), (.02, .025, .03), .12)]
    elif kind == "sheet":
        parts = [box("sheet_paper", (.215, .004, .300), (0, 0, 0), (.93, .93, .90), .55), box("sheet_band", (.215, .0045, .035), (0, .0003, .120), (.12, .38, .42), .5)]
    elif kind == "clipboard":
        parts = [box("clip_board", (.232, .012, .325), (0, 0, 0), (.06, .13, .24), .45), box("clip_paper", (.206, .0035, .290), (0, .0075, -.012), (.93, .93, .90), .85), box("clip_clip", (.075, .020, .034), (0, .011, .150), (.62, .64, .66), .35),
                 box("clip_hinge", (.040, .008, .014), (0, .013, .172), (.62, .64, .66), .35)]
    side = "r" if hold["hands"] in ("r", "both") else "l"
    hand = tgt.pose.bones[f"hand_{side}"]
    skin = (tgt.matrix_world @ hand.matrix @ tgt.data.bones[hand.name].matrix_local.inverted() @ tgt.matrix_world.inverted())
    R = Matrix.Rotation(math.radians(hold["tilt"]), 4, "X")          # sheet faces the reader (+Y of the model), top leaning toward them
    for o in parts:
        place = Matrix.Translation(cx) @ R                           # the character faces -Y, so the reader is at +Y of the prop: the prop's +Y face looks back at the holder
        o.data.transform(skin.inverted() @ place); o.location = (0, 0, 0)
        o.parent = tgt; o.parent_type = "OBJECT"
        vg = o.vertex_groups.new(name=hand.name); vg.add(list(range(len(o.data.vertices))), 1.0, "REPLACE")
        mod = o.modifiers.new("Armature", "ARMATURE"); mod.object = tgt
    return parts


def cough_keys(tgt, f0, f1):
    """A cough every loop: the right forearm rises to the mouth, the torso flexes, then relaxes (IK empties keyed over time, baked)."""
    sc = bpy.context.scene; sc.frame_set(f0); bpy.context.view_layer.update()
    head = bone_world(tgt, "head").translation.copy(); n = f1 - f0 + 1
    def hw(off): return head + Vector((off[0], -off[1], off[2]))
    e = make_empty("cough_target", hw((0, .0, -.02)), tgt, "spine_03"); p = make_empty("cough_pole", hw((-.4, .5, -.45)), tgt, "spine_03")
    rest = bone_world(tgt, "hand_r").translation.copy()
    c = tgt.pose.bones["lowerarm_r"].constraints.new("IK"); c.target = e; c.pole_target = p; c.pole_angle = math.radians(-90); c.chain_count = 2
    c.use_stretch = False
    e.location = rest                                                                   # starts where the hand already is (parent inverse makes local = world at the first frame)
    mouth = hw((-.03, .115, -.08))
    rest_l = e.location.copy()
    def key(frame, loc, inf):
        sc.frame_set(frame); e.location = loc; e.keyframe_insert("location", frame=frame); c.influence = inf; c.keyframe_insert("influence", frame=frame)
    key(f0, rest_l, 0.0); key(f0 + int(n * .38), rest_l, 0.0); key(f0 + int(n * .46), mouth, 1.0); key(f0 + int(n * .62), mouth, 1.0); key(f0 + int(n * .72), rest_l, 0.0); key(f1, rest_l, 0.0)
    bpy.ops.object.select_all(action="DESELECT"); tgt.select_set(True); bpy.context.view_layer.objects.active = tgt; bpy.ops.object.mode_set(mode="POSE")
    bpy.ops.nla.bake(frame_start=f0, frame_end=f1, step=1, only_selected=False, visual_keying=True, clear_constraints=True, use_current_action=True, bake_types={"POSE"}); bpy.ops.object.mode_set(mode="OBJECT")
    bpy.data.objects.remove(e, do_unlink=True); bpy.data.objects.remove(p, do_unlink=True)


def write_materials(folder, tgt):
    """materials.json + textures for StaffSetup.Import (same schema as staff_bake.bake_and_export)."""
    import shutil
    materials = []
    for obj in tgt.children:
        if obj.type != "MESH":
            continue
        for mat in obj.data.materials:
            if not mat or not mat.use_nodes:
                continue
            nodes = mat.node_tree.nodes
            diffuse = next((n for n in nodes if n.type == "TEX_IMAGE" and n.name == "DiffuseTexture"), None); texture = None
            if diffuse and diffuse.image:
                src = Path(bpy.path.abspath(diffuse.image.filepath))
                if src.exists():
                    texture = src.name; shutil.copyfile(src, folder / "textures" / texture)
            pr = next((n for n in nodes if n.type == "BSDF_PRINCIPLED"), None)
            color = list(pr.inputs["Base Color"].default_value) if pr else [1, 1, 1, 1]
            rough = float(pr.inputs["Roughness"].default_value) if pr else .6; metal = float(pr.inputs["Metallic"].default_value) if pr else 0.0
            materials.append({"mesh": obj.name, "name": mat.name, "texture": texture, "color": color, "roughness": rough, "metallic": metal,
                              "cutout": any(p in obj.name for p in ("hair", "ponytail", "short0", "bob", "long0", "afro", "braid", "eyebrow", "eyelashes"))})
    (folder / "materials.json").write_text(json.dumps(materials, indent=2))


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]; key = argv[0]; plan = PLAN[key]
    preview = argv[argv.index("--preview") + 1] if "--preview" in argv else None
    tgt = load_target(key); src = import_source()
    meta = {"key": key, "clips": {}}
    props = []
    for clip in plan["clips"]:
        look = plan["look"] if clip == plan["main"] else (0, 0)
        f0, f1, hmin, hmax, scale = retarget(tgt, src, clip, look)
        bpy.context.view_layer.objects.active = tgt
        if clip == plan["main"]:
            if "hold" in plan:
                hold = plan["hold"]
                hold_ik(tgt, f0, f1, hold, clip)
                curl_fingers(tgt, ("r", "l") if hold["hands"] == "both" else (hold["hands"],), f0, f1, deg=34 if hold["prop"] == "phone" else 40, scroll=hold.get("thumb_scroll", False))
                if not props:
                    props = make_prop(tgt, hold, f0)                                   # after the grip pose is baked: the prop is skinned to the hand as it really is at frame f0
            if plan.get("cough"):
                cough_keys(tgt, f0, f1)
        meta["clips"][clip] = {"frames": [f0, f1], "pelvis_z_min": hmin, "pelvis_z_max": hmax, "pelvis_scale": scale}
        tgt.animation_data.action.use_fake_user = True
    meta["pelvis_rest_z"] = (tgt.matrix_world @ tgt.data.bones["pelvis"].matrix_local).translation.z
    # drop the source rig, keep one pass of nothing else
    bpy.data.objects.remove(src, do_unlink=True)
    for a in list(bpy.data.actions):
        if a.name.startswith("Armature|"):
            bpy.data.actions.remove(a)
    # preview renders of the main clip at a few frames
    if preview:
        sc = bpy.context.scene; sc.render.engine = "BLENDER_WORKBENCH"; sc.render.resolution_x, sc.render.resolution_y = 420, 520
        sc.display.shading.light = "STUDIO"; sc.display.shading.color_type = "TEXTURE"; sc.world = bpy.data.worlds.new("w"); sc.world.color = (.7, .72, .75)
        cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); sc.collection.objects.link(cam); sc.camera = cam; cam.data.lens = 40
        main = bpy.data.actions[plan["main"]]; tgt.animation_data.action = main
        f0, f1 = meta["clips"][plan["main"]]["frames"]
        for name, loc in (("front", (0, -3.0, 1.0)), ("side", (-3.0, 0, 1.0)), ("three", (-2.2, -2.2, 1.2)), ("rear", (0, 3.0, 1.0))):
            cam.location = loc; d = Vector((0, 0, .8)) - Vector(loc); cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
            for fi, f in enumerate((f0, (f0 + f1) // 2)):
                sc.frame_set(f); sc.render.filepath = f"{preview}_{name}_{fi}.png"; bpy.ops.render.render(write_still=True)
    # export
    folder = OUT / key; (folder / "textures").mkdir(parents=True, exist_ok=True)
    write_materials(folder, tgt)
    bpy.ops.object.select_all(action="DESELECT"); tgt.select_set(True)
    for o in bpy.data.objects:
        if o.parent == tgt and o.type == "MESH":
            o.select_set(True)
    bpy.context.view_layer.objects.active = tgt
    tgt.animation_data.action = bpy.data.actions[plan["main"]]
    bpy.ops.export_scene.fbx(filepath=str(folder / f"{key}.fbx"), use_selection=True, object_types={"MESH", "ARMATURE"}, use_mesh_modifiers=False, axis_forward="-Z", axis_up="Y", add_leaf_bones=False,
                             use_armature_deform_only=True, bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False, bake_anim_step=1.0, bake_anim_simplify_factor=0.0, path_mode="STRIP")
    (OUT / f"{key}_anim.json").write_text(json.dumps(meta, indent=2))
    print("RETARGETED", key, json.dumps(meta["clips"]))


main()
