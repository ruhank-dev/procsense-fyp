"""Outfit recipes for the staff builder (imported by build_staff.py inside Blender): hair/brow colouring, scrubs split from a casual suit, lab coat lengthened from a blouse.

All garments are generic CC0 MPFB clothes modified here; nothing depicts a real person or a real uniform.
"""
import bmesh
import math
import bpy
from mathutils import Vector
from mathutils.kdtree import KDTree


def assign_single(obj, mat):
    """One material for the whole garment: every polygon on slot 0 (the garments arrive with several slots), consistent outward normals."""
    obj.data.materials.clear(); obj.data.materials.append(mat)
    for p in obj.data.polygons:
        p.material_index = 0
    bm = bmesh.new(); bm.from_mesh(obj.data); bmesh.ops.recalc_face_normals(bm, faces=bm.faces); bm.to_mesh(obj.data); bm.free()


def solid_material(name, rgb, rough=.85):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes.get("Principled BSDF"); b.inputs["Base Color"].default_value = (rgb[0], rgb[1], rgb[2], 1); b.inputs["Roughness"].default_value = rough
    return m


def tint_texture(obj, rgb):
    """Multiply the colour of an image-textured material (hair, brows, lashes) so the strand texture keeps its detail but gets a real hair colour."""
    for mat in obj.data.materials:
        if not mat or not mat.use_nodes:
            continue
        nt = mat.node_tree
        bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
        if bsdf is None:
            continue
        link = next((l for l in nt.links if l.to_node == bsdf and l.to_socket.name == "Base Color"), None)
        if link is None:
            bsdf.inputs["Base Color"].default_value = (rgb[0], rgb[1], rgb[2], 1)
            continue
        mix = nt.nodes.new("ShaderNodeMixRGB"); mix.blend_type = "MULTIPLY"; mix.inputs[0].default_value = 1.0
        mix.inputs[2].default_value = (rgb[0] * 2.2, rgb[1] * 2.2, rgb[2] * 2.2, 1)
        nt.links.new(link.from_socket, mix.inputs[1]); nt.links.new(mix.outputs[0], bsdf.inputs["Base Color"])


def components(obj):
    bm = bmesh.new(); bm.from_mesh(obj.data); bm.verts.ensure_lookup_table()
    seen, comps = set(), []
    for v in bm.verts:
        if v.index in seen:
            continue
        comp, todo = [], [v]; seen.add(v.index)
        while todo:
            x = todo.pop(); comp.append(x.index)
            for e in x.link_edges:
                o = e.other_vert(x)
                if o.index not in seen:
                    seen.add(o.index); todo.append(o)
        comps.append(comp)
    bm.free()
    return comps


def split_by_verts(obj, groups, names):
    """Split a mesh into one object per vertex-index group (keeps vertex groups / weights)."""
    out = []
    for idx, name in zip(groups, names):
        dup = obj.copy(); dup.data = obj.data.copy(); obj.users_collection[0].objects.link(dup); dup.name = name
        keep = set(idx)
        bm = bmesh.new(); bm.from_mesh(dup.data); bm.verts.ensure_lookup_table()
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.index not in keep], context="VERTS"); bm.to_mesh(dup.data); bm.free()
        out.append(dup)
    bpy.data.objects.remove(obj, do_unlink=True)
    return out


def nudge_out(obj, d=.004):
    for v in obj.data.vertices:
        v.co += v.normal * d


def copy_weights_from_body(obj, body, only_new_from=None):
    """Every vertex of obj (or those with index >= only_new_from) gets the skin weights of the nearest body vertex."""
    kd = KDTree(len(body.data.vertices))
    for v in body.data.vertices:
        kd.insert(body.matrix_world @ v.co, v.index)
    kd.balance()
    names = {g.index: g.name for g in body.vertex_groups}
    for g in body.vertex_groups:
        if g.name not in obj.vertex_groups:
            obj.vertex_groups.new(name=g.name)
    for v in obj.data.vertices:
        if only_new_from is not None and v.index < only_new_from:
            continue
        _, bi, _ = kd.find(obj.matrix_world @ v.co)
        for g in body.data.vertices[bi].groups:
            obj.vertex_groups[names[g.group]].add([v.index], g.weight, "REPLACE")


def make_coat(blouse, body, hem_z=.52, flare=1.02, steps=4):
    """Lab coat from the blouse: drop the skirt shell, keep the long-sleeved collared upper, extrude its hem down to mid-thigh with a loose flare, weights from the body."""
    comps = components(blouse)
    upper = max(comps, key=lambda c: max(blouse.data.vertices[i].co.z for i in c))
    keep = set(upper)
    bm = bmesh.new(); bm.from_mesh(blouse.data); bm.verts.ensure_lookup_table()
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.index not in keep], context="VERTS")
    bm.verts.ensure_lookup_table(); bm.edges.ensure_lookup_table(); n0 = len(bm.verts)
    torso = [e for e in bm.edges if e.is_boundary and max(abs(v.co.x) for v in e.verts) < .34]      # hem ring of the torso shell; cuffs and neck are further out or higher
    lowest = min(v.co.z for e in torso for v in e.verts)
    hem = [e for e in torso if max(v.co.z for v in e.verts) < lowest + .035]
    cur = hem; total = lowest - hem_z
    for _ in range(steps):
        ret = bmesh.ops.extrude_edge_only(bm, edges=cur)
        for v in [g for g in ret["geom"] if isinstance(g, bmesh.types.BMVert)]:
            v.co.z -= total / steps; v.co.x *= flare; v.co.y *= flare
        cur = [g for g in ret["geom"] if isinstance(g, bmesh.types.BMEdge) and g.is_boundary]
    bm.to_mesh(blouse.data); bm.free()
    copy_weights_from_body(blouse, body, only_new_from=n0)
    for p in blouse.data.polygons:
        p.use_smooth = True
    return blouse


def lift_torso(coat, human, clearance):
    """Pushes only the torso part of the coat (|x| < 0.30 at rest, i.e. not the sleeves) out to `clearance` from the body."""
    tree = surface_tree(human); me = coat.data; inv = coat.matrix_world.inverted(); n = 0
    for v in me.vertices:
        if abs(v.co.x) >= .30: continue
        p = coat.matrix_world @ v.co
        loc, nrm, _, d = tree.find_nearest(p)
        if loc is not None and (p - loc).dot(nrm) < clearance:
            v.co = inv @ (loc + nrm * clearance); n += 1
    me.update(); print("LIFT_TORSO", coat.name, n)


def tighten(coat, human, limit, rounds=2, everywhere=False):
    """3 Nov 2026 (ChatGPT QA VQ-STAFF-002): the blouse-derived sleeves and shoulders stood far off the body (balloon sleeves, padded shoulders). Pull sleeve and shoulder vertices back until they are
    at most `limit` from the body surface (the hem keeps its flare), smooth the pulled region, then the caller pushes everything back out to the clearance so nothing sinks into the skin."""
    tree = surface_tree(human); me = coat.data; inv = coat.matrix_world.inverted()
    adj = {v.index: set() for v in me.vertices}
    for e in me.edges:
        a, b = e.vertices; adj[a].add(b); adj[b].add(a)
    pulled = set()
    for v in me.vertices:
        sleeve = abs(v.co.x) >= .30; shoulder = v.co.z > 1.30
        if not (everywhere or sleeve or shoulder): continue
        p = coat.matrix_world @ v.co
        loc, nrm, _, d = tree.find_nearest(p)
        if loc is None: continue
        off = p - loc
        if off.length > limit:
            v.co = inv @ (loc + off.normalized() * limit); pulled.add(v.index)
    for _ in range(rounds):
        new = {i: me.vertices[i].co * .6 + (sum((me.vertices[j].co for j in adj[i]), Vector()) / len(adj[i])) * .4 for i in pulled if adj[i]}
        for i, c in new.items(): me.vertices[i].co = c
    me.update(); print("TIGHTEN", coat.name, len(pulled))


def refine_coat(coat, seed=3):
    """Makes the lab coat read as a garment: the front closes along a buttoned centre line (it used to leave a slit that showed the scrubs at the hip), the shell is subdivided once, and soft folds are pushed
    outward only (vertical drape folds that deepen toward the hem, a gentle waist gather, elbow creases in the sleeves) so the coat never sinks into the body it was cleared against.
    Vertex-group weights are interpolated by the subdivision, so the skinning is unchanged."""
    import mathutils.noise as mn
    me = coat.data; bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
    front = [v for v in bm.verts if v.is_boundary and abs(v.co.x) < .09 and v.co.y < -.02 and .62 < v.co.z < 1.40]
    for v in front: v.co.x = 0.0
    bmesh.ops.remove_doubles(bm, verts=front, dist=.004)
    bmesh.ops.subdivide_edges(bm, edges=list(bm.edges), cuts=1, use_grid_fill=True)
    bm.verts.ensure_lookup_table(); bm.normal_update()
    for _ in range(2):                                                                               # relax the subdivision so the silhouette stays round, boundaries stay put
        pos = {}
        for v in bm.verts:
            if v.is_boundary: continue
            nb = [e.other_vert(v).co for e in v.link_edges]
            if nb: pos[v.index] = v.co * .5 + (sum(nb, Vector()) / len(nb)) * .5
        for i, c in pos.items(): bm.verts[i].co = c
    bm.normal_update()
    for v in bm.verts:
        ang = math.atan2(v.co.y, v.co.x); z = v.co.z
        if abs(v.co.x) > .30:                                                                          # sleeves: creases bunch at the elbow (z about 1.08 in the standing rest pose), fine drape elsewhere
            crease = max(0.0, 1.0 - abs(z - 1.08) / .09)
            d = .0035 * abs(mn.noise(Vector((ang * 3.0, z * 6.0, seed)))) + .006 * crease * abs(math.sin(z * 120.0))
        else:
            hem = max(0.0, min(1.0, (1.18 - z) / .55))                                                  # folds deepen toward the hem
            gather = max(0.0, 1.0 - abs(z - 1.02) / .07) * .004                                          # waist gather
            d = (.004 + .011 * hem) * abs(mn.noise(Vector((ang * 5.0, z * 1.3, seed + 7)))) + gather * abs(mn.noise(Vector((ang * 9.0, 0, seed))))
        v.co += v.normal * d
    bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = True
    me.update()
    print("REFINE_COAT", coat.name, "verts", len(me.vertices))


def outfit(spec, rig, human):
    meshes = {o.name: o for o in rig.children if o.type == "MESH"}
    for o in meshes.values():                                   # garments and hair: edits must reach what is drawn, so drop the (inactive) shape keys first, as the patient build does
        if o is not human and o.data.shape_keys and "body" not in o.name:
            for kb in o.data.shape_keys.key_blocks[1:]:
                assert abs(kb.value) < 1e-6, (o.name, kb.name, kb.value)
            o.shape_key_clear()
    for o in list(meshes.values()):
        if any(w in o.name for w in ("bob", "ponytail", "short0")):
            tint_texture(o, spec["hair_rgb"])
        if "eyebrow" in o.name or "eyelashes" in o.name:
            tint_texture(o, tuple(c * .8 for c in spec["hair_rgb"]))
    if "scrubs" in spec:
        suit = next(o for n, o in meshes.items() if "casualsuit" in n)
        comps = components(suit)
        comps.sort(key=lambda c: -max(suit.data.vertices[i].co.z for i in c))
        top, trousers = split_by_verts(suit, comps[:2], [spec["name"] + ".scrub_top", spec["name"] + ".scrub_trousers"])
        for o, rgb in ((top, spec["scrubs"]), (trousers, spec.get("trousers", tuple(c * .92 for c in spec["scrubs"])))):
            assign_single(o, solid_material(o.name + "_mat", rgb))
        tighten(trousers, human, .020, everywhere=True)             # same for the waistband and hips (a blue sliver still showed at the male hip)
        tighten(top, human, .020, everywhere=True)                  # the loose scrub shirt bulged past the coat at the belly (blue patch showing through); keep it within 20 mm of the body
        n1 = clear_of(top, [human], .009); n2 = clear_of(trousers, [human], .013)
        blouse = next(o for n, o in meshes.items() if "elegantsuit" in n)
        coat = make_coat(blouse, human); coat.name = spec["name"] + ".lab_coat"
        assign_single(coat, solid_material(coat.name + "_mat", (.86, .87, .88), .8))
        n3 = clear_of(coat, [human], spec["coat_out"])
        lift_torso(coat, human, spec["coat_out"] + .010)             # the torso sits clear of the scrubs' own 9 mm (the top used to show through at the flank)
        tighten(coat, human, spec["coat_out"] + .020)
        n3 += clear_of(coat, [human], spec["coat_out"])
        refine_coat(coat)
        from coat_details import add_details                              # owner reference photos: notched collar, snap placket, patch pockets, stethoscope
        add_details(rig, human, coat, spec["name"])
        print("CLEARANCE pushed", n1, n2, n3)
    else:
        for o in rig.children:
            if o.type == "MESH" and "elegantsuit" in o.name:
                clear_of(o, [human], .007)


def surface_tree(obj):
    """BVH of the object's REST geometry (raw mesh data, no modifiers): the garments are pushed in rest space, and the armature deforms body and cloth the same way afterwards."""
    from mathutils.bvhtree import BVHTree
    me = obj.data
    verts = [obj.matrix_world @ v.co for v in me.vertices]
    return BVHTree.FromPolygons(verts, [p.vertices[:] for p in me.polygons])


def clear_of(obj, others, clearance, rounds=3):
    """Push every vertex of obj out of (and at least `clearance` from) the surfaces of `others`; between pushes the pushed vertices are smoothed into their neighbours, and the last step is always a push."""
    trees = [surface_tree(o) for o in others]
    me = obj.data; adj = {v.index: set() for v in me.vertices}
    for e in me.edges:
        a, b = e.vertices; adj[a].add(b); adj[b].add(a)
    inv = obj.matrix_world.inverted(); pushed_all = set()

    def push():
        n = 0
        for v in me.vertices:
            p = obj.matrix_world @ v.co; moved = False
            for _k in range(2):
                for tr in trees:
                    loc, nrm, _, d = tr.find_nearest(p)
                    if loc is not None and (p - loc).dot(nrm) < clearance:
                        p = loc + nrm * clearance; moved = True
            if moved:
                v.co = inv @ p; pushed_all.add(v.index); n += 1
        return n

    for _ in range(rounds):
        push()
        new = {}
        for i in pushed_all:
            nb = adj[i]
            if nb:
                new[i] = me.vertices[i].co * .65 + (sum((me.vertices[j].co for j in nb), Vector()) / len(nb)) * .35
        for i, c in new.items():
            me.vertices[i].co = c
    push()
    me.update()
    chk = [ (obj.matrix_world @ v.co) for v in me.vertices ]
    inside = sum(1 for p in chk if (p - trees[0].find_nearest(p)[0]).dot(trees[0].find_nearest(p)[1]) < clearance - .0005)
    print('CLEAR_OF', obj.name, 'pushed', len(pushed_all), 'still under clearance vs first surface', inside, 'of', len(chk), 'mods', [m.type for m in obj.modifiers], 'parent', obj.parent.name if obj.parent else None)
    return len(pushed_all)
