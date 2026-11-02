"""Pose the hospital staff (receptionist, two nurses) in Blender, bake the pose into the meshes and the rig, and export an FBX for Unity.

  blender -b --factory-startup -P unity/Tools/MPFB/staff_pose.py -- <key> [--bake]
Without --bake it only saves build/mpfb/output/staff/<key>_posed.blend (pose applied through the armature) for review renders. With --bake it also applies the pose to every mesh (body, clothes,
hair, eyes, teeth) and every shape key with linear-blend skinning, makes the posed skeleton the new rest pose, and exports <key>.fbx plus materials.json under build/mpfb/output/staff/<key>/.
Poses are authored with world-space joint rotations and a two-bone IK for arms/legs; no mocap, no real person.
"""
import json
import math
import shutil
import sys
from pathlib import Path

import bpy
from mathutils import Matrix, Vector

root = Path(__file__).resolve().parents[3]
OUT = root / "build/mpfb/output/staff"
X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))      # Blender world; the character faces -Y, so its left side is +X


def upd():
    bpy.context.view_layer.update()


def rot(rig, name, axis, deg, pivot=None):
    """Rotate a pose bone (and everything below it) about a world-space axis through `pivot` (default: the bone's head)."""
    pb = rig.pose.bones[name]; p = pb.head.copy() if pivot is None else pivot
    pb.matrix = Matrix.Translation(p) @ Matrix.Rotation(math.radians(deg), 4, axis) @ Matrix.Translation(-p) @ pb.matrix
    upd()


def aim(rig, name, direction):
    """Turn a pose bone so its length axis points along `direction` (minimal twist), keeping its head."""
    pb = rig.pose.bones[name]; axis = pb.matrix.to_3x3() @ Vector((0, 1, 0))
    q = axis.rotation_difference(Vector(direction).normalized()) @ pb.matrix.to_quaternion()
    m = q.to_matrix().to_4x4(); m.translation = pb.head
    pb.matrix = m; upd()


def two_bone(rig, upper, lower, target, pole):
    """Place the end of the `lower` bone at `target` by bending between upper and lower, the elbow/knee toward `pole`."""
    pu, pl = rig.pose.bones[upper], rig.pose.bones[lower]
    s = pu.head.copy(); l1, l2 = pu.length, pl.length
    d = (Vector(target) - s); dist = min(max(d.length, abs(l1 - l2) + 1e-4), l1 + l2 - 1e-4); u = d.normalized()
    a = (l1 * l1 - l2 * l2 + dist * dist) / (2 * dist); h = math.sqrt(max(l1 * l1 - a * a, 0))
    p = (Vector(pole) - u * Vector(pole).dot(u)).normalized()
    elbow = s + u * a + p * h
    aim(rig, upper, elbow - s); aim(rig, lower, (s + u * dist) - elbow)


def curl(rig, side, finger, deg, axis_sign=1.0, joints=(1, 2, 3), weights=(1, 1.1, .8)):
    """Curl a finger chain about the hand's lateral axis (index-to-pinky), the palm side being the curl direction."""
    hand = rig.pose.bones[f"hand_{side}"]
    lateral = (rig.pose.bones[f"pinky_01_{side}"].head - rig.pose.bones[f"index_01_{side}"].head).normalized()
    for j, w in zip(joints, weights):
        name = f"{finger}_{j:02d}_{side}"
        if name in rig.pose.bones:
            rot(rig, name, lateral, axis_sign * deg * w)


def load(key):
    bpy.ops.wm.open_mainfile(filepath=str(OUT / f"{key}.blend"))
    rig = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    for pb in rig.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    upd()
    return rig


def bone(rig, n):
    return rig.pose.bones[n]


def grip(rig, side, deg=42, thumb=24):
    """Wrap the fingers (and a little thumb) around what the hand holds: a clipboard edge, a sheet, a phone."""
    for f in ("index", "middle", "ring", "pinky"):
        curl(rig, side, f, deg * (1.0 if f != "pinky" else 1.1))
    curl(rig, side, "thumb", thumb, joints=(1, 2, 3), weights=(.6, 1, .8))


# ---------------------------------------------------------------- poses
def pose_nurse_f(rig):
    """Slightly tilted, weight on the left leg, right hand on the hip, left hand holding a clipboard at the waist, head turned toward her colleague (to her right, -X)."""
    rot(rig, "pelvis", Y, 4.5); rot(rig, "spine_01", Y, -2.2); rot(rig, "spine_02", Y, -2.4); rot(rig, "spine_03", Y, -1.0)   # hip kicked out, shoulders level out again
    rot(rig, "thigh_r", X, -5); rot(rig, "calf_r", X, 9); rot(rig, "foot_r", X, -4)                                              # relaxed right knee
    rot(rig, "neck_01", Z, 10); rot(rig, "head", Z, 12); rot(rig, "head", Y, 5); rot(rig, "head", X, -4)                     # looks toward the colleague, a small nod and tilt
    sr, sl = bone(rig, "upperarm_r").head, bone(rig, "upperarm_l").head
    hip_r = Vector((-.205, -.045, bone(rig, "pelvis").head.z + .02))
    two_bone(rig, "upperarm_r", "lowerarm_r", hip_r, Vector((-1, .55, .1)))                                                       # elbow out and back
    two_bone(rig, "upperarm_l", "lowerarm_l", Vector((.10, -.19, 1.07)), Vector((.6, .4, -.8)))                                   # forearm across the waist, wrist at the lower edge of the board
    grip(rig, "l")
    wrist = bone(rig, "hand_l").head.copy()
    return {"clipboard_hand": "l", "clip_pos": wrist + Vector((-.01, -.05, .105)), "clip_tilt": 10}               # the board stands on the hand and leans against the forearm, computed from the real wrist


def pose_nurse_m(rig):
    """Standing straight, weight even, a clipboard held in the right hand at chest height, left arm relaxed with a bent elbow, head turned toward her (to his left, +X)."""
    rot(rig, "neck_01", Z, -9); rot(rig, "head", Z, -11); rot(rig, "head", X, -2)
    two_bone(rig, "upperarm_r", "lowerarm_r", Vector((-.09, -.21, 1.13)), Vector((-.6, .4, -.8)))                                 # forearm across the chest, wrist at the lower edge of the board
    two_bone(rig, "upperarm_l", "lowerarm_l", Vector((.19, -.07, bone(rig, "pelvis").head.z - .05)), Vector((.7, .4, -.5)))        # other arm relaxed, a little bent
    grip(rig, "r")
    wrist = bone(rig, "hand_r").head.copy()
    return {"clipboard_hand": "r", "clip_pos": wrist + Vector((.01, -.05, .105)), "clip_tilt": 12}


def pose_receptionist(rig):
    """Seated, upright, forearms forward and slightly down toward a keyboard, looking forward at the screen."""
    pelvis = bone(rig, "pelvis"); seat = .47; drop = pelvis.head.z - (seat + .12)
    pelvis.matrix = Matrix.Translation((0, .06, -drop)) @ pelvis.matrix; upd()
    for s in ("l", "r"):
        rot(rig, f"thigh_{s}", X, 88); rot(rig, f"calf_{s}", X, -92); rot(rig, f"foot_{s}", X, 6)
    rot(rig, "spine_02", X, 5); rot(rig, "neck_01", X, 4); rot(rig, "head", X, 3)
    two_bone(rig, "upperarm_r", "lowerarm_r", Vector((-.12, -.44, .80)), Vector((-.4, .5, -.6)))
    two_bone(rig, "upperarm_l", "lowerarm_l", Vector((.10, -.44, .80)), Vector((.4, .5, -.6)))
    return {}


POSES = {"nurse_f": pose_nurse_f, "nurse_m": pose_nurse_m, "receptionist": pose_receptionist}
sys.path.insert(0, str(Path(__file__).resolve().parent))
import civilian_poses  # noqa: E402  (waiting-room civilians, registered here so the same bake/export applies)
civilian_poses.register(sys.modules[__name__], POSES)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:]
    key = argv[0]; bake = "--bake" in argv
    rig = load(key)
    info = POSES[key](rig)
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT / f"{key}_posed.blend"))
    print("POSED", key, json.dumps({k: (list(v) if hasattr(v, "__len__") else v) for k, v in info.items()}))
    if bake:
        import staff_bake
        staff_bake.bake_and_export(rig, key, info)


sys.path.insert(0, str(Path(__file__).resolve().parent))
main()
