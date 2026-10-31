"""Bedside patient monitor on a five-castor pole stand, hero-quality rebuild.

Design reference: compact Philips IntelliVue MX400/MP5 class bedside monitors (photographs kept privately in unity/Logs/MonitorRefs):
pale moulded two-part housing with a parting groove, thick glossy dark front bezel with a recessed glass-covered display, soft-touch
key row on the chin, rotary knob, status lamps, integrated bar handle in a top pocket, side connector bay with colour-coded
sockets, rear vent slots, IEC power inlet, equipotential stud, compliance label, VESA-style pole-mount plate, 5-star base with
swivel castors, instrument basket and a routed power cord.

Node names, anchors and the Body size (0.327 x 0.182 x 0.288 m) match the earlier build so the Unity prefab, ports, sensor
attachment and screen view keep working. Everything except Body / Display / Glass is joined per material at the end.
"""
import math
import sys
from pathlib import Path

HERE = Path(__file__).resolve()
sys.path.insert(0, str(HERE.parents[4] / "unity" / "Tools" / "Environment"))
import bmesh  # noqa: E402
import bpy  # noqa: E402
import prop_kit as pk  # noqa: E402
from mathutils import Vector  # noqa: E402
from prop_kit import cyl, lathe, rbox, tube  # noqa: E402
from procsense_bpy import point  # noqa: E402

pk.geo.PALETTE.update({
    "MonShell": ((.70, .715, .71, 1), 0, .46),            # pale moulded ABS/PC
    "MonBezel": ((.011, .012, .014, 1), 0, .20),           # glossy black bezel
    "MonGlass": ((.01, .012, .014, 1), 0, .03),
    "MonLCD": ((.009, .023, .029, 1), .05, .24),
    "MonShell2": ((.70, .715, .71, 1), 0, .46),            # same plastic as MonShell; its own material so Body and the loose shell parts can carry separate AO maps
    "MonChrome": ((.85, .86, .88, 1), 1.0, .06),            # polished pole: full metal, F0 from the base colour
    "MonSteel": ((.55, .56, .58, 1), 1.0, .32),             # brushed basket / hardware
    "MonPlastic": ((.26, .27, .285, 1), 0, .5),             # satin mid-grey ABS of the stand, clamps and handle (the old near-black .06 albedo read as black in the room)
    "MonKey": ((.050, .055, .060, 1), 0, .55),             # soft-touch key tops
    "MonLabels": ((.80, .80, .80, 1), 0, .5),              # decal atlas
    "MonSocket": ((.004, .004, .005, 1), 0, .6),
    "LedGreen": ((.04, .55, .16, 1), 0, .15),
    "LedAmber": ((.65, .38, .03, 1), 0, .15),
    "LedRed": ((.62, .04, .03, 1), 0, .15),
})

KEEP_SEPARATE = ("Body", "Display", "Glass")
FACING = {  # normal, right (u), up (v) as seen by a viewer looking at that face
    "-Y": ((0, -1, 0), (1, 0, 0), (0, 0, 1)),
    "+Y": ((0, 1, 0), (-1, 0, 0), (0, 0, 1)),
    "-X": ((-1, 0, 0), (0, -1, 0), (0, 0, 1)),
    "+X": ((1, 0, 0), (0, 1, 0), (0, 0, 1)),
}
RECTS = {}


def _activate(o):
    bpy.ops.object.select_all(action="DESELECT")
    o.select_set(True)
    bpy.context.view_layer.objects.active = o


def cutbox(size, loc):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o = bpy.context.object
    o.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return o


def carve(target, cutters):
    if len(cutters) > 1:
        bpy.ops.object.select_all(action="DESELECT")
        for c in cutters:
            c.select_set(True)
        bpy.context.view_layer.objects.active = cutters[0]
        bpy.ops.object.join()
        cutter = bpy.context.object
    else:
        cutter = cutters[0]
    m = target.modifiers.new("carve", "BOOLEAN")
    m.operation = "DIFFERENCE"
    m.solver = "EXACT"
    m.object = cutter
    _activate(target)
    bpy.ops.object.modifier_apply(modifier=m.name)
    bpy.data.objects.remove(cutter, do_unlink=True)


def soften(o, width, seg=2, angle=35):
    _activate(o)
    m = o.modifiers.new("soften", "BEVEL")
    m.width, m.segments, m.limit_method, m.angle_limit = width, seg, "ANGLE", math.radians(angle)
    bpy.ops.object.modifier_apply(modifier=m.name)


def refinish(o, uv_size=.08, smooth=40):
    _activate(o)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(smooth))
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.cube_project(cube_size=uv_size, correct_aspect=True, clip_to_bounds=False, scale_to_bounds=False)
    bpy.ops.object.mode_set(mode="OBJECT")


def slab(part, size, loc, mat, facing="-Y", rect=None):
    """Thin closed box; with rect its facing side gets that atlas rectangle (u0, v0, u1, v1), full face = 0..1 texture."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o = bpy.context.object
    o.dimensions = size
    pk._finish(o, part, mat, 0.0)
    if rect is not None:
        n, u, v = (Vector(a) for a in FACING[facing])
        dims = [abs(size[i]) for i in range(3)]
        w = abs(u.x) * dims[0] + abs(u.y) * dims[1] + abs(u.z) * dims[2]
        h = abs(v.x) * dims[0] + abs(v.y) * dims[1] + abs(v.z) * dims[2]
        me = o.data
        uv = me.uv_layers.active.data
        for poly in me.polygons:
            front = poly.normal.dot(n) > .9
            for li in poly.loop_indices:
                p = me.vertices[me.loops[li].vertex_index].co
                if front:
                    uu, vv = p.dot(u) / w + .5, p.dot(v) / h + .5
                    uv[li].uv = (rect[0] + uu * (rect[2] - rect[0]), rect[1] + vv * (rect[3] - rect[1]))
                else:
                    uv[li].uv = (rect[0] + .002, rect[1] + .002)
    return o


def decal(part, name, centre, wh, facing="-Y"):
    n = Vector(FACING[facing][0])
    t = .0010
    size = {"-Y": (wh[0], t, wh[1]), "+Y": (wh[0], t, wh[1]), "-X": (t, wh[0], wh[1]), "+X": (t, wh[0], wh[1])}[facing]
    loc = Vector(centre) + n * (t / 2)
    return slab(part, size, loc, "MonLabels", facing, RECTS[name])


def screw(part, pos, facing, radius=.0033):
    n = Vector({"+Y": (0, 1, 0), "-Z": (0, 0, -1), "-Y": (0, -1, 0)}[facing])
    a = Vector(pos)
    cyl(part, a, a + n * .0017, radius, "SteelBrushed", 20, r=.0004, seg=2)
    perp = Vector((1, 0, 0)) if facing != "-Z" else Vector((1, 0, 0))
    up = n.cross(perp).normalized()
    for k, d in enumerate((perp, up)):
        c = a + n * .0018
        size = [.0001] * 3
        bpy.ops.mesh.primitive_cube_add(size=1, location=c)
        o = bpy.context.object
        o.dimensions = (abs(d.x) * .0054 + abs(n.x) * .0006 + .0006 * (1 - abs(d.x) - abs(n.x)),
                        abs(d.y) * .0054 + abs(n.y) * .0006 + .0006 * (1 - abs(d.y) - abs(n.y)),
                        abs(d.z) * .0054 + abs(n.z) * .0006 + .0006 * (1 - abs(d.z) - abs(n.z)))
        pk._finish(o, f"{part}_x{k}", "PlasticGrey", 0.0)


def join_by_material():
    remap = {"M_Chrome": "MonChrome", "M_SteelBrushed": "MonSteel", "M_PlasticGrey": "MonPlastic"}       # the monitor's metals get their own authored materials
    for o in [o for o in bpy.context.scene.objects if o.type == "MESH" and o.data.materials[0].name == "M_MonShell" and not o.name.endswith("_Body")]:
        o.data.materials[0] = pk.geo.material("MonShell2")
    for o in [o for o in bpy.context.scene.objects if o.type == "MESH"]:
        n = o.data.materials[0].name
        if n in remap:
            o.data.materials[0] = pk.geo.material(remap[n])
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    names = sorted({o.data.materials[0].name for o in meshes})
    for mat in names:
        group = [o for o in bpy.context.scene.objects if o.type == "MESH" and o.data.materials[0].name == mat and not any(o.name.endswith("_" + k) for k in KEEP_SEPARATE)]
        if len(group) < 1:
            continue
        bpy.ops.object.select_all(action="DESELECT")
        for o in group:
            o.select_set(True)
        bpy.context.view_layer.objects.active = group[0]
        if len(group) > 1:
            bpy.ops.object.join()
        o = bpy.context.object
        o.name = f"EQ_{pk.geo.ASSET}_{mat[2:]}"
        bpy.context.scene.cursor.location = (0, 0, 0)
        bpy.ops.object.origin_set(type="ORIGIN_CURSOR")


AO_OBJECTS = {"Body": 2048, "MonShell2": 2048, "MonBezel": 1024, "MonKey": 1024}


def bake_ao():
    """Ambient occlusion of the whole assembly baked into one map per surface (UV0 becomes a unique layout for these meshes;
    the plastic grain is applied as a tiled detail map in the engine, so the grain scale no longer depends on UV0)."""
    out = HERE.parents[4] / "unity" / "Logs" / "MonitorBuild"
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"; sc.cycles.samples = 96; sc.cycles.device = "CPU"
    sc.world = bpy.data.worlds.new("aoworld"); sc.world.use_nodes = True
    sc.world.light_settings.distance = .05
    sc.cycles.bake_type = "AO"
    # printed legends and the front glass are not part of the moulded parts: they must not throw occlusion onto the surface they sit on
    hidden = [o for o in sc.objects if o.type == "MESH" and o.data.materials and o.data.materials[0].name in ("M_MonLabels", "M_MonGlass")]
    for o in hidden:
        o.hide_render = True
    for o in [o for o in sc.objects if o.type == "MESH"]:
        key = o.name.replace(f"EQ_{pk.geo.ASSET}_", "")
        if key not in AO_OBJECTS:
            continue
        size = AO_OBJECTS[key]
        _activate(o)
        bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=.004)
        bpy.ops.object.mode_set(mode="OBJECT")
        img = bpy.data.images.new("ao_" + key, size, size, alpha=False); img.colorspace_settings.name = "Non-Color"
        orig = o.data.materials[0]
        tmp = bpy.data.materials.new("aobake"); tmp.use_nodes = True
        node = tmp.node_tree.nodes.new("ShaderNodeTexImage"); node.image = img; tmp.node_tree.nodes.active = node
        o.data.materials[0] = tmp
        bpy.ops.object.bake(type="AO", margin=8, use_clear=True)
        o.data.materials[0] = orig
        img.filepath_raw = str(out / f"ao_{key}.png"); img.file_format = "PNG"; img.save()
        print("AO BAKED", key, size)
    for o in hidden:
        o.hide_render = False


def build():
    global RECTS
    import json
    atlas = HERE.parents[4] / "unity" / "Logs" / "MonitorBuild" / "monitor_labels.json"
    RECTS = json.loads(atlas.read_text())
    height = 1.12
    FRONT = -.091            # body front face
    KEY_Z = 1.176

    # ---------------------------------------------------------------- base, pole, basket
    cyl("Upright", (0, .045, .12), (0, .045, height), .019, "Chrome", 48, r=.003)
    lathe("Hub", [(.080, 0), (.080, .016), (.074, .020), (.074, .030), (.060, .052), (.056, .060), (.050, .062), (.042, .090)], (0, .045, .085), "PlasticGrey", verts=64)
    lathe("HubRing", [(.0775, .0), (.0775, .004)], (0, .045, .1015), "SteelBrushed", verts=64)
    for i in range(5):
        a = i * math.tau / 5 + math.radians(18)
        x, y = .27 * math.cos(a), .045 + .27 * math.sin(a)
        tube(f"Leg{i}", [(0, .045, .13), (x * .5, .045 + (y - .045) * .5, .122), (x, y, .098)], .0215, "PlasticGrey", corner=.06, res=14)
        lathe(f"LegCap{i}", [(.027, 0), (.027, .008), (.022, .016)], (x, y, .088), "PlasticGrey", verts=40)
        before = {o.name for o in bpy.data.objects}
        pk.castor(f"Castor{i}", x, y, 0.0, radius=.038, width=.026)
        for o in [o for o in bpy.data.objects if o.name not in before and o.type == "MESH"]:
            pk.tilt(o, math.degrees(a) - 90 + (-18, 22, -9, 15, -24)[i], (x, y, 0), "Z")
    point("COL_Base", (0, .045, .07), (.57, .57, .14))
    point("COL_Pole", (0, .045, height / 2), (.05, .05, height))
    lathe("PoleJoint", [(.0225, 0), (.0255, .003), (.0255, .042), (.0225, .045)], (0, .045, .60), "PlasticGrey", verts=48)
    lathe("PoleLockKnob", [(.006, 0), (.006, .012), (.015, .012), (.015, .024), (.012, .027)], (.0255, .045, .62), "RubberBlack", axis="X", verts=28)
    rbox("BasketClamp", (.056, .042, .052), (0, .045, .775), "PlasticGrey", .006)

    # basket
    rbox("BasketFloor", (.23, .16, .006), (0, .03, .79), "SteelBrushed", .0015)
    for x in (-.115, .115):
        tube("BasketSide" + str(x), [(x, -.05, .79), (x, -.05, .89), (x, .11, .89), (x, .11, .79)], .0035, "SteelBrushed", corner=.02)
    for i in range(8):
        x = -.1075 + i * .0307
        cyl(f"BasketWire{i}", (x, -.05, .79), (x, -.05, .89), .0018, "SteelBrushed", 10)
        cyl(f"BasketWireB{i}", (x, .11, .79), (x, .11, .89), .0018, "SteelBrushed", 10)
    cyl("BasketRimF", (-.115, -.05, .89), (.115, -.05, .89), .0035, "SteelBrushed", 14)
    cyl("BasketRimB", (-.115, .11, .89), (.115, .11, .89), .0035, "SteelBrushed", 14)
    cyl("BasketRimMid", (-.115, -.05, .84), (.115, -.05, .84), .002, "SteelBrushed", 10)

    # ---------------------------------------------------------------- mount head
    lathe("MountNeck", [(.034, 0), (.034, .006), (.027, .012), (.027, .022)], (0, .045, 1.110), "PlasticGrey", verts=48)
    rbox("MountPlate", (.135, .105, .012), (0, .045, 1.140), "PlasticGrey", .003)
    lathe("MountKnob", [(.006, 0), (.006, .010), (.014, .010), (.014, .020), (.011, .023)], (.034, .045, 1.122), "RubberBlack", axis="X", verts=28)
    for sx in (-1, 1):
        for sy in (-1, 1):
            screw(f"MountScrew{sx}{sy}", (sx * .052, .045 + sy * .037, 1.134), "-Z", .0036)
            cyl(f"Foot{sx}{sy}", (sx * .125, sy * .062, 1.1465), (sx * .125, sy * .062, 1.1405), .0095, "RubberBlack", 28, r=.0015)

    # ---------------------------------------------------------------- housing
    body = rbox("Body", (.327, .182, .288), (0, 0, 1.29), "MonShell", .030, 6)
    ring = pk.extrude("GrooveRingOuter", pk.rounded_rect(.350, .310, .040), .0016, (0, -.0408, 1.29), "MonShell", r=.0002)
    inner = pk.extrude("GrooveRingInner", pk.rounded_rect(.322, .283, .0285), .0100, (0, -.0450, 1.29), "MonShell", r=.0002)
    carve(ring, [inner])
    pocket = cutbox((.22, .036, .040), (0, .010, 1.434))
    slots = [cutbox((.012, .0030, .052), (.1635, -.036 + i * .0095, 1.205)) for i in range(8)]
    carve(body, [ring, pocket] + slots)
    refinish(body, .08)

    rear = rbox("RearCover", (.270, .045, .240), (0, .1085, 1.285), "MonShell", .020, 6)
    vents = [cutbox((.072, .015, .0036), (-.048, .1295, 1.318 + i * .0115)) for i in range(6)]
    iec = cutbox((.040, .012, .027), (.085, .1285, 1.205))
    carve(rear, vents + [iec])
    refinish(rear, .08)
    for k, px in enumerate((.0765, .0935)):
        cyl(f"IecPin{k}", (px, .1225, 1.1995), (px, .1295, 1.1995), .0017, "Chrome", 12)
    cyl("IecPinE", (.085, .1225, 1.2145), (.085, .1295, 1.2145), .0017, "Chrome", 12)
    rbox("PowerPlug", (.038, .036, .026), (.085, .1425, 1.205), "RubberBlack", .004)
    tube("PowerCord", [(.085, .160, 1.205), (.085, .190, 1.205), (.085, .205, .95), (.03, .090, .62), (0, .071, .30), (0, .071, .13)], .0048, "RubberBlack", corner=.05, res=14)
    # equipotential stud, rear screws, label
    cyl("EqStud", (.105, .131, 1.330), (.105, .1405, 1.330), .0040, "Chrome", 24, r=.0005)
    lathe("EqNut", [(.0075, 0), (.0075, .005)], (.105, .1405, 1.330), "Chrome", axis="-Y", verts=6)
    decal("RearLabel", "rear_label", (-.040, .131, 1.222), (.090, .0355), "+Y")
    for sx in (-1, 1):
        for z in (1.185, 1.385):
            screw(f"RearScrew{sx}{int(z * 100)}", (sx * .110, .131, z), "+Y")

    # ---------------------------------------------------------------- front bezel, display, glass
    bezel = pk.extrude("Bezel", pk.rounded_rect(.318, .222, .026, 10), .020, (0, -.1055, 1.315), "MonBezel", r=.0062, plane="XZ")
    window = pk.extrude("DisplayCut", pk.rounded_rect(.270, .172, .010), .0125, (-.008, -.1100, 1.316), "MonBezel", r=.0045)
    carve(bezel, [window])
    soften(bezel, .0008, 2)
    refinish(bezel, .08)
    slab("Display", (.2611, .0030, .1632), (-.008, -.0990, 1.316), "MonLCD", "-Y", (0, 0, 1, 1))
    slab("Glass", (.262, .0012, .165), (-.008, -.1030, 1.316), "MonGlass", "-Y", (0, 0, 1, 1))

    # status lamps in the bezel's right panel
    for z, mat in ((1.392, "LedGreen"), (1.370, "LedAmber"), (1.348, "LedRed")):
        lathe(f"Led_{mat}", [(.0038, 0), (.0038, .0007), (.0030, .0018), (.0016, .0024)], (.1425, -.1052, z), mat, axis="Y", verts=24)
    decal("WordmarkDecal", "wordmark", (-.095, -.1055, 1.4115), (.060, .0122), "-Y")

    # ---------------------------------------------------------------- chin: keys and knob
    keys = [(-.125, "power", .0102, "Key0"), (-.086, "silence", .0, "Key1"), (-.047, "menu", .0, "Key2"), (-.008, "home", .0, "Key3")]
    for x, icon, circ, nm in keys:
        w = .021 if circ else .030
        pk.extrude(nm, pk.rounded_rect(w, .021, .0102 if circ else .006), .0075, (x, FRONT - .0075 + .0005, KEY_Z), "MonKey", r=.0014, plane="XZ")
        decal(nm + "_icon", icon, (x, FRONT - .0075 + .0005, KEY_Z), (.0186, .0186) if circ else (.0272, .0186), "-Y")
    pk.extrude("Key4", pk.rounded_rect(.072, .026, .008), .0075, (.0555, FRONT - .0075 + .0005, KEY_Z), "MonKey", r=.0014, plane="XZ")
    decal("Key4_icon", "key_start", (.0555, FRONT - .0075 + .0005, KEY_Z), (.066, .022), "-Y")
    gear = []
    for k in range(64):
        a = math.tau * k / 64
        r = .0222 if (k // 2) % 2 == 0 else .0206
        gear.append((r * math.cos(a), r * math.sin(a)))
    pk.extrude("Dial", gear, .0155, (.1285, FRONT - .0155 + .0004, KEY_Z), "RubberBlack", r=.0016, plane="XZ")
    lathe("DialCap", [(.0155, 0), (.0155, .0012), (.0125, .0030), (.0100, .0034)], (.1285, FRONT - .0155, KEY_Z), "Chrome", axis="Y", verts=48)
    slab("DialMark", (.0012, .0004, .0085), (.1285, FRONT - .0155 - .0036, KEY_Z + .0090), "LedAmber")

    # ---------------------------------------------------------------- left connector bay
    rbox("BayPlate", (.006, .066, .210), (-.1665, -.020, 1.2755), "MonBezel", .002)
    ports = [("ECG", "GasGreen", "cap_ecg"), ("SpO2", "GasBlue", "cap_spo2"), ("NIBP", "GasWhite", "cap_nibp"), ("Temp", "GasYellow", "cap_temp")]
    for i, (port, colour, cap) in enumerate(ports):
        z = 1.21 + i * .044
        lathe(f"Port{port}", [(.0132, 0), (.0132, -.0050), (.0116, -.0062), (.0100, -.0062)], (-.1695, -.020, z), colour, axis="X", verts=40)
        lathe(f"Socket{port}", [(.0088, -.0050), (.0088, -.0066)], (-.1695, -.020, z), "MonSocket", axis="X", verts=32)
        for j in range(3):
            ang = math.tau * j / 3 + math.radians(90)
            py, pz = -.020 + .0042 * math.cos(ang), z + .0042 * math.sin(ang)
            cyl(f"Pin{port}{j}", (-.1731, py, pz), (-.1685, py, pz), .0009, "Chrome", 10)
        decal(f"Caption{port}", cap, (-.1695, -.020, z - .0205), (.026, .0087), "-X")
        point("ATTACH_" + port, (-.173, -.02, z))

    # ---------------------------------------------------------------- handle (in its top pocket)
    tube("Handle", [(-.09, .010, 1.412), (-.09, .010, 1.485), (.09, .010, 1.485), (.09, .010, 1.412)], .0105, "PlasticGrey", corner=.03, res=14)
    cyl("HandleGrip", (-.062, .010, 1.485), (.062, .010, 1.485), .0165, "RubberBlack", 40, r=.004)
    for x in (-.062, .062):
        cyl(f"HandleCollar{x}", (x - .003 * (1 if x > 0 else -1), .010, 1.485), (x + .003 * (1 if x > 0 else -1), .010, 1.485), .0185, "MonShell", 40, r=.001)

    point("SCREEN_Main", (-.008, -.1012, 1.316))
    point("GRAB_Handle", (0, .01, 1.485))
    point("COL_Body", (0, 0, 1.29), (.327, .182, .288))
    join_by_material()
    if not __import__("os").environ.get("NOAO"):
        bake_ao()
