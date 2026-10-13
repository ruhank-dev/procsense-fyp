"""Rounded mattress shell using the preserved review bed's footprint."""
import bpy
import json
from pathlib import Path

root = Path(__file__).resolve().parents[3]
output = root / 'build/mpfb/output'
bed = json.loads((output / 'fabric-fit.json').read_text())['bed']
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.mesh.primitive_cube_add(size=1)
obj = bpy.context.object
obj.name = 'Soft mattress'
obj.dimensions = (bed['max'][0]-bed['min'][0], bed['max'][2]-bed['min'][2], .14)
obj.location = (0, 1, bed['max'][1]-.07)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
bevel = obj.modifiers.new('Rounded mattress', 'BEVEL')
bevel.width = .035
bevel.segments = 4
bpy.ops.object.modifier_apply(modifier=bevel.name)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
obj.data.calc_loop_triangles()
(output/'soft-mattress.json').write_text(json.dumps({
    'vertices': [[v.co.x, v.co.z, v.co.y] for v in obj.data.vertices],
    'triangles': [i for t in obj.data.loop_triangles for i in (t.vertices[0], t.vertices[2], t.vertices[1])]
}))
