"""Bake a posed staff rig into its meshes (linear-blend skinning on the basis AND every shape key), make the posed skeleton the new rest pose, add the clipboard and the name badge,
and export an FBX + materials.json for the Unity importer. Imported by staff_pose.py inside Blender.
"""
import json
import math
import shutil
from pathlib import Path

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree

root = Path(__file__).resolve().parents[3]
OUT = root / "build/mpfb/output/staff"
PROPS = OUT / "props"


def skin_matrices(rig):
    """Armature-space skinning matrix per bone: current pose x inverse rest."""
    return {pb.name: np.array(pb.matrix @ pb.bone.matrix_local.inverted()) for pb in rig.pose.bones}


def bake_mesh(obj, rig, mats):
    me = obj.data; n = len(me.vertices)
    names = {g.index: g.name for g in obj.vertex_groups}
    bone_names = [b for b in mats]; idx = {b: i for i, b in enumerate(bone_names)}
    W = np.zeros((n, len(bone_names)), dtype=np.float64)
    for v in me.vertices:
        for g in v.groups:
            nm = names.get(g.group)
            if nm in idx:
                W[v.index, idx[nm]] += g.weight
    tot = W.sum(1, keepdims=True); W = np.where(tot > 1e-8, W / np.maximum(tot, 1e-8), 0.0)            # unweighted vertices stay where they are
    M = np.stack([mats[b] for b in bone_names])                                                         # (B, 4, 4)
    to_arm = np.array(rig.matrix_world.inverted() @ obj.matrix_world); from_arm = np.linalg.inv(to_arm)

    def skin(co):
        h = np.concatenate([co, np.ones((n, 1))], 1) @ to_arm.T
        sk = np.einsum("nb,bij,nj->ni", W, M, h)
        sk = np.where(tot > 1e-8, sk, h)
        return (sk @ from_arm.T)[:, :3]

    buf = np.empty(n * 3, dtype=np.float32)
    if me.shape_keys:
        for kb in me.shape_keys.key_blocks:
            kb.data.foreach_get("co", buf); new = skin(buf.reshape(n, 3).astype(np.float64)).astype(np.float32).ravel(); kb.data.foreach_set("co", new)
    me.vertices.foreach_get("co", buf); new = skin(buf.reshape(n, 3).astype(np.float64)).astype(np.float32).ravel(); me.vertices.foreach_set("co", new)
    me.update()


def rest_from_pose(rig):
    mats = {pb.name: pb.matrix.copy() for pb in rig.pose.bones}
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode="EDIT")
    for eb in rig.data.edit_bones:
        eb.matrix = mats[eb.name]
    bpy.ops.object.mode_set(mode="OBJECT")
    for pb in rig.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()


def textured_material(name, image_path, rough=.8, alpha=False):
    m = bpy.data.materials.new(name); m.use_nodes = True
    nt = m.node_tree; b = nt.nodes.get("Principled BSDF"); b.inputs["Roughness"].default_value = rough
    t = nt.nodes.new("ShaderNodeTexImage"); t.name = "DiffuseTexture"; t.image = bpy.data.images.load(str(image_path))
    nt.links.new(t.outputs["Color"], b.inputs["Base Color"])
    return m


def solid_material(name, rgb, rough=.5, metal=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF"); b.inputs["Base Color"].default_value = (rgb[0], rgb[1], rgb[2], 1); b.inputs["Roughness"].default_value = rough; b.inputs["Metallic"].default_value = metal
    return m


def add_box(name, size, loc, mat, bevel=.0015):
    me = bpy.data.meshes.new(name); bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = (v.co.x * size[0], v.co.y * size[1], v.co.z * size[2])
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(o); o.location = loc; o.data.materials.append(mat)
    return o


def bind_to_bone(rig, obj, bone):
    """Weight every vertex 100% to `bone` and give the object an armature modifier on `rig`."""
    obj.parent = rig; obj.parent_type = "OBJECT"
    vg = obj.vertex_groups.new(name=bone); vg.add(list(range(len(obj.data.vertices))), 1.0, "REPLACE")
    mod = obj.modifiers.new("Armature", "ARMATURE"); mod.object = rig


def make_clipboard(rig, side, center, tilt_deg, seed):
    """A clipboard with a steel clip and a sheet of chart paper, its paper face toward the holder (+Y), tilted so the top leans toward them."""
    parts = []
    board_m = solid_material("Clipboard board", (.06, .13, .24), .45)
    paper_m = textured_material("Clipboard chart", PROPS / f"chart_{seed}.png", .85)
    clip_m = solid_material("Clipboard clip", (.62, .64, .66), .35, 1.0)
    board = add_box("clip_board", (.232, .012, .325), (0, 0, 0), board_m)
    paper = add_box("clip_paper", (.206, .0035, .290), (0, .0075, -.012), paper_m)
    clip = add_box("clip_clip", (.075, .020, .034), (0, .011, .150), clip_m)
    clip2 = add_box("clip_hinge", (.040, .008, .014), (0, .013, .172), clip_m)
    objs = [board, paper, clip, clip2]
    # UVs for the paper: front (+Y) face carries the full chart
    me = paper.data; bm = bmesh.new(); bm.from_mesh(me); uv = bm.loops.layers.uv.verify()
    for f in bm.faces:
        for l in f.loops:
            c = l.vert.co
            l[uv].uv = ((c.x / .206) + .5, (c.z / .290) + .5) if f.normal.y > .5 else (0.0, 0.0)
    bm.to_mesh(me); bm.free()
    for o in objs:
        for p in o.data.polygons:
            p.use_smooth = False
    # place: parent empty carries the transform; apply it into the mesh so the bake is trivial
    R = Matrix.Rotation(math.radians(-tilt_deg), 4, "X")
    for o in objs:
        o.data.transform(Matrix.Translation(center) @ R)
        o.location = (0, 0, 0)
        bind_to_bone(rig, o, f"hand_{side}")
    return objs


def _place(rig, objs, center, tilt_deg, bone):
    R = Matrix.Rotation(math.radians(-tilt_deg), 4, "X")
    for o in objs:
        o.data.transform(Matrix.Translation(center) @ R); o.location = (0, 0, 0)
        bind_to_bone(rig, o, bone)


def make_sheet(rig, pos, tilt_deg, hand="r"):
    """Paperwork held in the hand: card stock with a teal header band, tilted toward the reader; skinned to the holding hand like the clipboards."""
    paper_m = solid_material("Paperwork", (.93, .93, .90), .55); band_m = solid_material("Paperwork header", (.12, .38, .42), .5)
    objs = [add_box("sheet_paper", (.215, .004, .300), (0, 0, 0), paper_m), add_box("sheet_band", (.215, .0045, .035), (0, .0003, .120), band_m)]
    _place(rig, objs, Vector(pos), tilt_deg, f"hand_{hand}")
    return objs


def make_phone(rig, pos, tilt_deg, hand="r"):
    """A smartphone: black glass slab in a dark body, held at reading distance."""
    glass = solid_material("Phone glass", (.02, .025, .03), .12); body = solid_material("Phone body", (.08, .09, .10), .35)
    objs = [add_box("phone_body", (.072, .009, .150), (0, 0, 0), body), add_box("phone_screen", (.066, .0015, .140), (0, .0045, 0), glass)]
    _place(rig, objs, Vector(pos), tilt_deg, f"hand_{hand}")
    return objs


def make_badge(rig, host_mesh, texture, bone="spine_03", center_x=.085, z=1.285):
    """A name badge flat on the chest, placed on the host garment's front surface by ray cast."""
    me = host_mesh.data
    tree = BVHTree.FromPolygons([host_mesh.matrix_world @ v.co for v in me.vertices], [p.vertices[:] for p in me.polygons])
    hit, _, _, _ = tree.ray_cast(Vector((center_x, -.6, z)), Vector((0, 1, 0)), 1.0)
    y = (hit.y if hit else -.14) - .004
    mat = textured_material("Name badge", PROPS / texture, .45)
    o = add_box("name_badge", (.086, .004, .054), (center_x, y, z), mat)
    me2 = o.data; bm = bmesh.new(); bm.from_mesh(me2); uv = bm.loops.layers.uv.verify()
    for f in bm.faces:
        for l in f.loops:
            c = l.vert.co
            l[uv].uv = ((c.x / .086) + .5, (c.z / .054) + .5) if f.normal.y < -.5 else (0.0, 0.0)
    bm.to_mesh(me2); bm.free()
    clip = add_box("badge_clip", (.014, .0035, .060), (center_x - .03, y + .0035, z + .004), solid_material("Badge clip", (.6, .62, .64), .35, 1.0))
    for ob in (o, clip):
        ob.data.transform(Matrix.Translation(ob.location)); ob.location = (0, 0, 0)
        bind_to_bone(rig, ob, bone)
    return o


def bake_and_export(rig, key, info):
    for o in bpy.data.objects:
        o.select_set(False)
    mats = skin_matrices(rig)
    meshes = [o for o in rig.children if o.type == "MESH"]
    for o in meshes:
        bake_mesh(o, rig, mats)
    rest_from_pose(rig)
    # props go on after the pose is baked, in the posed rest frame
    host = next((o for o in rig.children if o.type == "MESH" and ("lab_coat" in o.name or "elegantsuit" in o.name)), None)
    if "clipboard_hand" in info:
        make_clipboard(rig, info["clipboard_hand"], info["clip_pos"], info["clip_tilt"], 1 if key == "nurse_f" else 2)
    badge_tex = {"nurse_f": "badge_nurse_f.png", "nurse_m": "badge_nurse_m.png", "receptionist": "badge_receptionist.png"}.get(key)       # civilians wear no badge
    if "sheet_pos" in info:
        make_sheet(rig, info["sheet_pos"], info.get("sheet_tilt", 10), info.get("sheet_hand", "r"))
    if "phone_pos" in info:
        make_phone(rig, info["phone_pos"], info.get("phone_tilt", 35), info.get("phone_hand", "r"))
    if host is not None and badge_tex:
        spine = rig.data.bones["spine_03"].head_local.z                                  # baked rest: follows a seated pose as well as a standing one
        make_badge(rig, host, badge_tex, center_x=.085, z=spine + .305)
        print("BADGE_Z spine_03", round(spine, 3))
    # export
    folder = OUT / key; (folder / "textures").mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT"); rig.select_set(True)
    for o in rig.children:
        o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(folder / f"{key}.fbx"), use_selection=True, object_types={"MESH", "ARMATURE"}, use_mesh_modifiers=False, axis_forward="-Z", axis_up="Y",
                             add_leaf_bones=False, use_armature_deform_only=True, bake_anim=False, path_mode="STRIP")
    materials, report = [], {"key": key, "bones": len(rig.data.bones), "meshes": []}
    for obj in rig.children:
        if obj.type != "MESH":
            continue
        obj.data.calc_loop_triangles()
        report["meshes"].append({"name": obj.name, "triangles": len(obj.data.loop_triangles), "shapes": len(obj.data.shape_keys.key_blocks) if obj.data.shape_keys else 0})
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
    (folder / "materials.json").write_text(json.dumps(materials, indent=2)); (folder / "validation.json").write_text(json.dumps(report, indent=2))
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT / f"{key}_baked.blend"))
    print("EXPORT_COMPLETE", key, str(folder / f"{key}.fbx"), json.dumps({m["name"]: m["triangles"] for m in report["meshes"]}))
