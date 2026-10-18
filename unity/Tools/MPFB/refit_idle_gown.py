"""Loosen the previously covered skirt without changing garment topology/skin weights."""
import json
import math
from pathlib import Path

from mathutils import Vector
from mathutils.bvhtree import BVHTree

review = Path(__file__).resolve().parents[2] / "Logs" / "PatientArmsReview"
data = json.loads((review / "gown-fit-input.json").read_text())
body = [Vector((p["x"], p["y"], p["z"])) for p in data["body"]["vertices"]]
indices = data["body"]["triangles"]
tree = BVHTree.FromPolygons(body, [indices[i:i + 3] for i in range(0, len(indices), 3)])
shorts_tree = None
if data.get("shorts") and data["shorts"].get("vertices"):
    shorts = [Vector((p["x"], p["y"], p["z"])) for p in data["shorts"]["vertices"]]
    si = data["shorts"]["triangles"]
    shorts_tree = BVHTree.FromPolygons(shorts, [si[i:i + 3] for i in range(0, len(si), 3)])
points = []
changed = 0
clearances = []

for raw in data["gown"]["vertices"]:
    p = Vector((raw["x"], raw["y"], raw["z"]))
    # Preserve the accepted chest/sleeves. Below the waist, one loose skirt spans
    # both thighs instead of hugging/intersecting each leg under the old blanket.
    blend = max(0, min(1, (1.09 - p.z) / .17))
    blend = blend * blend * (3 - 2 * blend)
    if blend:
        angle = math.atan2(p.y - .81, p.x)
        length = max(0, min(1, (1.04 - p.z) / .46))
        radius = .275 + .015 * length
        x = radius * math.cos(angle)
        y = .81 + (.185 - .045 * length) * max(0, math.sin(angle))
        if math.sin(angle) < 0:
            y = .785 + .015 * math.sin(angle)
        if math.sin(angle) > .10:
            roofs = []
            for dx in (-.012, 0, .012):
                for dz in (-.012, 0, .012):
                    hit, _, _, _ = tree.ray_cast(Vector((x + dx, 2, p.z + dz)), Vector((0, -1, 0)), 3)
                    if hit is not None:
                        roofs.append(hit.y)
            if roofs:
                y = max(y, max(roofs) + .022)
            y += .002 * math.sin(x * 35 + p.z * 13) * math.sin(angle)
        p.x += (x - p.x) * blend
        p.y += (y - p.y) * blend
        changed += 1
        if p.y > .87 and p.z < .92:
            hit, _, _, _ = tree.ray_cast(Vector((p.x, 2, p.z)), Vector((0, -1, 0)), 3)
            if hit is not None:
                clearances.append(p.y - hit.y)
    points.append({"x": p.x, "y": p.y, "z": p.z})

# Apply a smooth outer-cloth envelope rather than independent hard vertex
# clamps, which create ridges along the coarse gown's waistband rows.
if shorts_tree is not None:
    verts = [Vector((p["x"], p["y"], p["z"])) for p in points]
    lifts = []
    for p in verts:
        lift = 0
        if p.y > .80 and .65 < p.z < 1.32:
            roofs = []
            for dx in (-.02, 0, .02):
                for dz in (-.02, 0, .02):
                    hit, _, _, _ = shorts_tree.ray_cast(Vector((p.x + dx, 2, p.z + dz)), Vector((0, -1, 0)), 3)
                    if hit is not None:
                        roofs.append(hit.y)
            if roofs:
                gate = max(0, min(1, (p.y - .80) / .09))
                gate = gate * gate * (3 - 2 * gate)
                lift = max(0, max(roofs) + .015 - p.y) * gate
        lifts.append(lift)
    # Nearby duplicated seam vertices receive the same displacement too.
    neighbors = [[j for j, q in enumerate(verts) if (p - q).length_squared < .065 ** 2]
                 for p in verts]
    for _ in range(8):
        lifts = [.35 * lifts[i] + .65 * sum(lifts[j] for j in near) / len(near)
                 for i, near in enumerate(neighbors)]
    for i, p in enumerate(points):
        p["y"] += lifts[i]

if not clearances or min(clearances) < .012:
    raise RuntimeError("The uncovered skirt still intersects the thigh envelope")
(review / "gown-fit-output.json").write_text(json.dumps({"vertices": points}))
(review / "gown-fit-validation.json").write_text(json.dumps({
    "vertices": len(points), "changedSkirtVertices": changed,
    "minimumFrontSkirtClearance": min(clearances), "sampledSkirtVertices": len(clearances),
    "topologyAndWeightsPreserved": True,
}, indent=2))
print("GOWN_FIT_COMPLETE", len(points), changed, min(clearances))
