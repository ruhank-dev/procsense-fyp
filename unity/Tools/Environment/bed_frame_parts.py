"""Geometry helpers for the hospital bed frame (Blender 4.5.14): rounded prisms, quad rings, rods, swatch materials.

Parts are built per material name, then joined into one mesh whose UVs point at flat swatches of a small atlas, so the
whole frame is one draw call with plastic, painted metal, stainless, rubber and accent colours each keeping their own
smoothness and metallic value.
"""
import math

import bmesh
import bpy
from mathutils import Vector

# name -> (atlas cell, sRGB hex, metallic, smoothness) - plan values: plastic rough .42, paint .50, stainless .32, rubber .82
SWATCHES = {
    "Plastic": (0, "#E7ECEB", 0.0, .55), "PaintedMetal": (1, "#D8DFE0", 0.0, .45), "Stainless": (2, "#BFC4C7", 1.0, .84),
    "Rubber": (3, "#282C30", 0.0, .12), "BlueInlay": (4, "#3A5C8E", 0.0, .46), "Lime": (5, "#9CC63C", 0.0, .45),
    "DarkTrim": (6, "#22292C", 0.0, .40), "Keypad": (7, "#10343F", 0.0, .62), "Screen": (8, "#4A93B5", 0.0, .75),
}
ASSET = "hospital_bed"


def preview_material(name):
    key = "M_" + name
    if key in bpy.data.materials:
        return bpy.data.materials[key]
    _, hx, metal, smooth = SWATCHES[name]
    rgb = [int(hx[i:i + 2], 16) / 255 for i in (1, 3, 5)]
    lin = [c / 12.92 if c <= .04045 else ((c + .055) / 1.055) ** 2.4 for c in rgb]
    mat = bpy.data.materials.new(key)
    mat.use_nodes = True
    b = mat.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*lin, 1)
    b.inputs["Metallic"].default_value = metal
    b.inputs["Roughness"].default_value = 1 - smooth
    return mat


def rr_profile(w, h, radii, seg=3):
    """Rounded rectangle (u, v); radii order top-right, top-left, bottom-left, bottom-right."""
    hw, hh = w / 2, h / 2
    centres = [(hw - radii[0], hh - radii[0], 0), (-hw + radii[1], hh - radii[1], 90),
               (-hw + radii[2], -hh + radii[2], 180), (hw - radii[3], -hh + radii[3], 270)]
    pts = []
    for (cx, cy, a0), r in zip(centres, radii):
        if r <= 1e-6:
            pts.append((cx, cy))
            continue
        pts += [(cx + r * math.cos(math.radians(a0 + 90 * i / seg)), cy + r * math.sin(math.radians(a0 + 90 * i / seg)))
                for i in range(seg + 1)]
    return pts


def superellipse(a, b, p, n, dv=0.0):
    out = []
    for i in range(n):
        t = math.tau * i / n
        c, s = math.cos(t), math.sin(t)
        out.append((a * math.copysign(abs(c) ** (2 / p), c), dv + b * math.copysign(abs(s) ** (2 / p), s)))
    return out


def _pt(axis, u, v, d):
    return {"Z": (u, v, d), "Y": (u, d, v), "X": (d, u, v)}[axis]


def _link(bm, part, loc, rot):
    mesh = bpy.data.meshes.new(part)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(part, mesh)
    bpy.context.collection.objects.link(obj)
    obj.location = loc
    obj.rotation_euler = rot
    return obj


def finish(obj, part, mat, bevel=.003, segs=1):
    obj.name = f"EQ_{ASSET}_{part}"
    obj.data.materials.append(preview_material(mat))
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    if bevel:
        m = obj.modifiers.new("Edge", "BEVEL")
        m.width, m.segments, m.limit_method, m.angle_limit = bevel, segs, "ANGLE", math.radians(40)
        bpy.ops.object.modifier_apply(modifier=m.name)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(38))
    return obj


def prism(part, profile, depth, loc, axis="Z", mat="Plastic", bevel=.003, segs=1, rot=(0, 0, 0)):
    bm = bmesh.new()
    lo = [bm.verts.new(_pt(axis, u, v, -depth / 2)) for u, v in profile]
    hi = [bm.verts.new(_pt(axis, u, v, depth / 2)) for u, v in profile]
    bm.faces.new(lo)
    bm.faces.new(hi)
    n = len(profile)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((lo[i], lo[j], hi[j], hi[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return finish(_link(bm, part, loc, rot), part, mat, bevel, segs)


def ring_prism(part, outer, inner, depth, loc, axis="X", mat="Plastic", bevel=.003, segs=2, rot=(0, 0, 0)):
    """Quad-only ring: outer outline, inner opening (same vertex count), extruded along the axis."""
    assert len(outer) == len(inner)
    n = len(outer)
    bm = bmesh.new()
    r = {k: [bm.verts.new(_pt(axis, u, v, d)) for u, v in prof]
         for k, prof, d in (("oh", outer, depth / 2), ("ol", outer, -depth / 2), ("ih", inner, depth / 2), ("il", inner, -depth / 2))}
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((r["oh"][i], r["oh"][j], r["ih"][j], r["ih"][i]))
        bm.faces.new((r["ol"][j], r["ol"][i], r["il"][i], r["il"][j]))
        bm.faces.new((r["ol"][i], r["ol"][j], r["oh"][j], r["oh"][i]))
        bm.faces.new((r["il"][j], r["il"][i], r["ih"][i], r["ih"][j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return finish(_link(bm, part, loc, rot), part, mat, bevel, segs)


def slab(part, w, h, d, r, loc, axis, mat="Plastic", bevel=.003, radii=None, segs=1, seg=3, rot=(0, 0, 0)):
    return prism(part, rr_profile(w, h, radii or (r, r, r, r), seg), d, loc, axis, mat, bevel, segs, rot)


def box(part, size, loc, mat="Plastic", bevel=.003, segs=1, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.object
    o.dimensions = size
    return finish(o, part, mat, min(bevel, min(size) / 4), segs)


def rod(part, a, b, radius, mat="Stainless", vertices=10, bevel=0):
    delta = Vector(b) - Vector(a)
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=delta.length, location=(Vector(a) + Vector(b)) / 2)
    o = bpy.context.object
    o.rotation_euler = delta.to_track_quat("Z", "Y").to_euler()
    return finish(o, part, mat, bevel)


def cable(part, pts, radius=.0035, mat="DarkTrim", sides=6):
    """Low-poly cable: a swept tube along a polyline, closed at both ends."""
    bm = bmesh.new()
    rings = []
    for k, p in enumerate(pts):
        t = Vector(pts[min(k + 1, len(pts) - 1)]) - Vector(pts[max(k - 1, 0)])
        t.normalize()
        up = Vector((0, 0, 1)) if abs(t.z) < .9 else Vector((1, 0, 0))
        u = t.cross(up); u.normalize(); v = t.cross(u)
        rings.append([bm.verts.new(Vector(p) + (u * math.cos(a) + v * math.sin(a)) * radius)
                      for a in (math.tau * i / sides for i in range(sides))])
    for k in range(len(rings) - 1):
        for i in range(sides):
            j = (i + 1) % sides
            bm.faces.new((rings[k][i], rings[k][j], rings[k + 1][j], rings[k + 1][i]))
    bm.faces.new(rings[0][::-1])
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return finish(_link(bm, part, (0, 0, 0), (0, 0, 0)), part, mat, 0)
